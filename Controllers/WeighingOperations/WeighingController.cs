using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.Common;
using TruLoad.Backend.Data;
using TruLoad.Backend.Services.Interfaces;
using TruLoad.Backend.Services.Interfaces.Weighing;
using TruLoad.Backend.Services.Interfaces.Integration;
using TruLoad.Backend.Services.Interfaces.Yard;
using TruLoad.Backend.DTOs.Integration;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Models;
using TruLoad.Backend.DTOs.Weighing;
using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Data.Repositories.Weighing;
using TruLoad.Backend.Repositories.Weighing.Interfaces;
using TruLoad.Backend.Services.Implementations.Reporting;
using System.Security.Claims;

namespace TruLoad.Backend.Controllers.WeighingOperations;

[ApiController]
[Route("api/v1/weighing-transactions")]
[Authorize]
[EnableRateLimiting("weighing")]
public class WeighingController : ControllerBase
{
    private readonly IWeighingService _weighingService;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IKeNHAService _kenhaService;
    private readonly IVehicleTagService _vehicleTagService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<WeighingController> _logger;
    private readonly TruLoadDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly IToleranceRepository _toleranceRepository;
    private readonly IAxleGroupAggregationService _axleGroupAggregationService;

    public WeighingController(
        IWeighingService weighingService,
        IVehicleRepository vehicleRepository,
        IKeNHAService kenhaService,
        IVehicleTagService vehicleTagService,
        ITenantContext tenantContext,
        ILogger<WeighingController> logger,
        TruLoadDbContext context,
        ICacheService cacheService,
        IToleranceRepository toleranceRepository,
        IAxleGroupAggregationService axleGroupAggregationService)
    {
        _weighingService = weighingService;
        _vehicleRepository = vehicleRepository;
        _kenhaService = kenhaService;
        _vehicleTagService = vehicleTagService;
        _tenantContext = tenantContext;
        _logger = logger;
        _context = context;
        _cacheService = cacheService;
        _toleranceRepository = toleranceRepository;
        _axleGroupAggregationService = axleGroupAggregationService;
    }

    /// <summary>
    /// Searches weighing transactions with filters, pagination, and sorting.
    /// Results are automatically scoped to the user's station if assigned.
    /// </summary>
    /// <param name="request">Search filters and pagination parameters</param>
    /// <returns>Paginated list of weighing transactions</returns>
    [HttpGet]
    [Authorize(Policy = "Permission:weighing.read")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(PagedResponse<WeighingTransactionDto>), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Search([FromQuery] SearchWeighingRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            // Use tenant context station if not explicitly provided in request
            var isHqOrAdmin = User.FindFirst("is_hq_user")?.Value == "true" || User.IsInRole("Superuser") || User.IsInRole("System Admin");
            var stationId = (request.StationId == null && isHqOrAdmin) ? null : (request.StationId ?? _tenantContext.StationId);

            _logger.LogDebug(
                "Searching weighing transactions: StationId={StationId}, OrgId={OrgId}",
                stationId, _tenantContext.OrganizationId);

            var (items, totalCount) = await _weighingService.SearchTransactionsAsync(
                stationId,
                request.VehicleRegNo,
                request.FromDate,
                request.ToDate,
                request.ControlStatus,
                request.IsCompliant,
                request.OperatorId,
                request.Skip,
                request.PageSize,
                request.SortBy,
                request.SortOrder,
                request.WeighingType,
                request.State,
                request.AxleConfiguration,
                request.SearchTicketNo,
                request.FromTime,
                request.ToTime);

            var dtos = items.Select(t => MapToDto(t)).ToList();

            var result = PagedResponse<WeighingTransactionDto>.Create(
                dtos, totalCount, request.PageNumber, request.PageSize);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching weighing transactions");
            return StatusCode(500, "An error occurred while searching weighing transactions.");
        }
    }

    /// <summary>
    /// Exports the commercial ticket list matching the given filters as CSV. Takes the same
    /// filters as <see cref="Search"/> (including FromTime/ToTime) and reuses the Reports module's
    /// shared CSV writer (<c>BaseReportGenerator.GenerateCsv</c>) rather than a new serializer.
    /// Columns are commercial-ticket fields only - enforcement-only fields (overload/compliance/
    /// tags/case) belong to the separate Reports module, not this export.
    /// </summary>
    [HttpGet("export")]
    [Authorize(Policy = "Permission:weighing.export")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Export([FromQuery] SearchWeighingRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var isHqOrAdmin = User.FindFirst("is_hq_user")?.Value == "true" || User.IsInRole("Superuser") || User.IsInRole("System Admin");
            var stationId = (request.StationId == null && isHqOrAdmin) ? null : (request.StationId ?? _tenantContext.StationId);

            // Not paginated - streams up to a sane cap so a single export request can't attempt
            // to dump the entire table (mirrors SearchTransactionsLightAsync's existing cap).
            var (items, _) = await _weighingService.SearchTransactionsAsync(
                stationId,
                request.VehicleRegNo,
                request.FromDate,
                request.ToDate,
                request.ControlStatus,
                request.IsCompliant,
                request.OperatorId,
                skip: 0,
                take: 20000,
                request.SortBy,
                request.SortOrder,
                request.WeighingType,
                request.State,
                request.AxleConfiguration,
                request.SearchTicketNo,
                request.FromTime,
                request.ToTime);

            var commercialItems = items.Where(t => t.WeighingMode == "commercial").ToList();

            // Batch-fetch the weighing fee per ticket (commercial fee lives on the Invoice record,
            // not on WeighingTransaction) - same lookup CommercialWeighingService.GetCommercialResultAsync
            // uses, just batched instead of per-row.
            var transactionIds = commercialItems.Select(t => t.Id).ToList();
            var feesByWeighingId = await _context.Invoices
                .AsNoTracking()
                .Where(i => i.WeighingId.HasValue && transactionIds.Contains(i.WeighingId.Value)
                    && i.InvoiceType == "commercial_weighing_fee")
                .ToDictionaryAsync(i => i.WeighingId!.Value, i => i.AmountDue);

            var headers = new[]
            {
                "Ticket Number", "Date/Time", "Station", "Vehicle Reg", "Transporter", "Driver",
                "Cargo Type", "Tare (kg)", "Gross (kg)", "Net (kg)", "Adjusted Net (kg)",
                "Tare Source", "Status", "Fee (KES)"
            };

            var rows = commercialItems.Select(t => new[]
            {
                t.TicketNumber ?? string.Empty,
                t.WeighedAt.ToString("yyyy-MM-dd HH:mm"),
                t.Station?.Name ?? string.Empty,
                t.VehicleRegNumber ?? string.Empty,
                t.SnapshotTransporterName ?? t.Transporter?.Name ?? string.Empty,
                t.SnapshotDriverName ?? (t.Driver != null ? $"{t.Driver.FullNames} {t.Driver.Surname}".Trim() : string.Empty),
                t.SnapshotCargoTypeName ?? t.Cargo?.Name ?? string.Empty,
                t.TareWeightKg?.ToString() ?? string.Empty,
                t.GrossWeightKg?.ToString() ?? string.Empty,
                t.NetWeightKg?.ToString() ?? string.Empty,
                t.AdjustedNetWeightKg?.ToString() ?? string.Empty,
                t.TareSource ?? string.Empty,
                t.ControlStatus ?? string.Empty,
                feesByWeighingId.TryGetValue(t.Id, out var fee) ? fee.ToString("F2") : string.Empty,
            });

            var csvBytes = BaseReportGenerator.GenerateCsv(headers, rows);
            return File(csvBytes, "text/csv", $"weighing-tickets_{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting weighing tickets");
            return StatusCode(500, "An error occurred while exporting weighing tickets.");
        }
    }

    /// <summary>
    /// Gets axle violation distribution by axle type (Steering, SingleDrive, Tandem, Tridem, etc.).
    /// Must be declared before [HttpGet("{id}")] so the path is not matched as an id.
    /// </summary>
    [HttpGet("axle-type-violations")]
    [Authorize(Policy = "Permission:weighing.read")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<OverloadDistributionDto>), 200)]
    public async Task<IActionResult> GetAxleTypeViolations(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? stationId,
        CancellationToken ct)
    {
        try
        {
            var grouped = await BuildAxleViolationDistributionAsync(dateFrom, dateTo, stationId, ct);
            return Ok(grouped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting axle type violations");
            return StatusCode(500, "An error occurred while getting axle type violations.");
        }
    }

    /// <summary>
    /// Builds the axle-violation-by-type distribution using the SAME group-level, DB-driven
    /// tolerance logic as <see cref="IAxleGroupAggregationService"/> (Kenya law tolerates axle
    /// GROUPS, e.g. Tandem/Tridem, not individual axles) — NOT a raw per-axle
    /// measured-minus-permissible diff, which flags axles the authoritative compliance engine
    /// considers fully compliant (any group within its tolerance-adjusted limit). Grouping/tolerance
    /// resolution mirrors <see cref="TruLoad.Backend.Services.Implementations.Weighing.AxleGroupAggregationService.AggregateAxleGroupsAsync"/>
    /// exactly, but done set-based over the date range instead of per-transaction, and with the
    /// ToleranceSetting lookups cached per (legalFramework, isSingleAxle) combination (at most a
    /// handful system-wide) rather than re-queried per axle group — this endpoint can cover a wide
    /// date range, so a per-transaction async loop would be an N+1 query risk.
    /// </summary>
    private async Task<List<OverloadDistributionDto>> BuildAxleViolationDistributionAsync(
        DateTime? dateFrom, DateTime? dateTo, Guid? stationId, CancellationToken ct)
    {
        var flatQuery = from wa in _context.WeighingAxles.AsNoTracking()
                        join wt in _context.WeighingTransactions.AsNoTracking() on wa.WeighingId equals wt.Id
                        select new
                        {
                            wa.WeighingId,
                            wa.AxleGrouping,
                            wa.AxleType,
                            wa.MeasuredWeightKg,
                            wa.PermissibleWeightKg,
                            wt.WeighedAt,
                            wt.StationId,
                            LegalFramework = wt.Act != null && wt.Act.ActType == "EAC" ? "EAC" : "TRAFFIC_ACT"
                        };

        if (dateFrom.HasValue || dateTo.HasValue)
        {
            var (rangeFromUtc, rangeToUtcExclusive) = WeighingQueryHelpers.ResolveEatDayRange(dateFrom, dateTo);
            flatQuery = flatQuery.Where(x => x.WeighedAt >= rangeFromUtc && x.WeighedAt < rangeToUtcExclusive);
        }
        if (stationId.HasValue)
            flatQuery = flatQuery.Where(x => x.StationId == stationId.Value);

        var flatRows = await flatQuery.ToListAsync(ct);

        // Group by (transaction, axle-grouping) — the actual unit Kenya law tolerates, not per-axle.
        var groups = flatRows
            .GroupBy(r => new { r.WeighingId, r.AxleGrouping, r.LegalFramework })
            .Select(g => new
            {
                g.Key.LegalFramework,
                AxleType = string.IsNullOrEmpty(g.First().AxleType) ? "Other" : g.First().AxleType,
                IsSingleAxle = g.Count() <= 1,
                MeasuredKg = g.Sum(x => x.MeasuredWeightKg),
                PermissibleKg = g.Sum(x => x.PermissibleWeightKg)
            })
            .ToList();

        var toleranceCache = new Dictionary<(string LegalFramework, bool IsSingleAxle), (int? FixedKg, decimal Pct)>();
        var violatingByType = new Dictionary<string, int>();

        foreach (var g in groups)
        {
            var cacheKey = (g.LegalFramework, g.IsSingleAxle);
            if (!toleranceCache.TryGetValue(cacheKey, out var rule))
            {
                rule = await _axleGroupAggregationService.ResolveGroupToleranceRuleAsync(g.IsSingleAxle, g.LegalFramework);
                toleranceCache[cacheKey] = rule;
            }

            var toleranceKg = rule.FixedKg ?? (rule.Pct > 0 ? (int)Math.Round(g.PermissibleKg * (rule.Pct / 100m)) : 0);
            var overloadKg = Math.Max(0, g.MeasuredKg - (g.PermissibleKg + toleranceKg));
            if (overloadKg > 0)
                violatingByType[g.AxleType] = violatingByType.GetValueOrDefault(g.AxleType) + 1;
        }

        var grouped = violatingByType
            .Select(kv => new OverloadDistributionDto { Name = kv.Key, Count = kv.Value, Percentage = 0 })
            .OrderByDescending(x => x.Count)
            .ToList();

        var total = grouped.Sum(x => x.Count);
        foreach (var item in grouped)
            item.Percentage = total > 0 ? Math.Round((decimal)item.Count * 100 / total, 2) : 0;

        return grouped;
    }

    /// <summary>
    /// Gets axle violation distribution by axle type. Alias for analytics; respects dateFrom, dateTo, stationId.
    /// Must be declared before [HttpGet("{id}")] so the path is not matched as an id.
    /// </summary>
    [HttpGet("axle-violations")]
    [Authorize(Policy = "Permission:weighing.read")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<OverloadDistributionDto>), 200)]
    public async Task<IActionResult> GetAxleViolations(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? stationId,
        CancellationToken ct)
    {
        try
        {
            var grouped = await BuildAxleViolationDistributionAsync(dateFrom, dateTo, stationId, ct);
            return Ok(grouped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting axle violations");
            return StatusCode(500, "An error occurred while getting axle violations.");
        }
    }

    /// <summary>
    /// Gets a weighing transaction by ID.
    /// </summary>
    /// <param name="id">Transaction ID</param>
    /// <returns>Weighing transaction details with axle weights and compliance info</returns>
    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:weighing.read")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(WeighingTransactionDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var transaction = await _weighingService.GetTransactionAsync(id);
            if (transaction == null)
            {
                return NotFound($"Weighing transaction {id} not found");
            }

            var dto = MapToDto(transaction);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving weighing transaction {TransactionId}", id);
            return StatusCode(500, "An error occurred while retrieving the weighing transaction.");
        }
    }

    /// <summary>
    /// Initiates a new weighing transaction.
    /// Provide either VehicleId or VehicleRegNo. When VehicleRegNo is provided,
    /// the backend will look up the vehicle by reg number and auto-create it if not found.
    /// </summary>
    /// <param name="request">Weighing transaction details</param>
    /// <returns>Created transaction with ID and initial status</returns>
    [HttpPost]
    [Authorize(Policy = "Permission:weighing.create")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(WeighingTransactionDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateWeighingRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            return Unauthorized("User ID not found in claims");
        }

        try
        {
            // Resolve vehicle: either by VehicleId or VehicleRegNo (lookup/create)
            Guid vehicleId;
            string vehicleRegNo;

            if (request.VehicleId.HasValue && request.VehicleId.Value != Guid.Empty)
            {
                vehicleId = request.VehicleId.Value;
                var vehicle = await _vehicleRepository.GetByIdAsync(vehicleId);
                vehicleRegNo = vehicle?.RegNo ?? request.VehicleRegNo?.Trim().ToUpper() ?? string.Empty;
            }
            else if (!string.IsNullOrWhiteSpace(request.VehicleRegNo))
            {
                var normalizedRegNo = request.VehicleRegNo.Trim().ToUpper();
                var existingVehicle = await _vehicleRepository.GetByRegNoAsync(normalizedRegNo);

                if (existingVehicle != null)
                {
                    vehicleId = existingVehicle.Id;
                    _logger.LogInformation("Found existing vehicle {RegNo} with ID {VehicleId}", normalizedRegNo, vehicleId);
                }
                else
                {
                    // Auto-create vehicle with just the registration number
                    var newVehicle = new Vehicle { RegNo = normalizedRegNo };
                    var created = await _vehicleRepository.CreateAsync(newVehicle);
                    vehicleId = created.Id;
                    _logger.LogInformation("Auto-created vehicle {RegNo} with ID {VehicleId}", normalizedRegNo, vehicleId);
                }
                vehicleRegNo = normalizedRegNo;
            }
            else
            {
                return BadRequest("Either VehicleId or VehicleRegNo must be provided.");
            }

            // Run sequentially to avoid DbContext concurrency issues
            // (all services share the same scoped DbContext)
            var transaction = await _weighingService.InitiateWeighingAsync(
                request.StationId,
                userGuid,
                vehicleId,
                vehicleRegNo,
                request.Bound,
                request.ScaleTestId,
                request.DriverId,
                request.TransporterId,
                request.WeighingType ?? "static",
                request.ActId,
                request.RoadId,
                request.SubcountyId,
                request.LocationTown,
                request.LocationSubcounty,
                request.LocationCounty,
                request.LocationLat,
                request.LocationLng,
                request.OriginId,
                request.DestinationId,
                request.CargoId,
                request.ClientLocalId);

            // Reload with includes so MapToDto gets Vehicle, Driver, Transporter, Origin, Destination, Cargo, Road, Subcounty, etc.
            var loaded = await _weighingService.GetTransactionAsync(transaction.Id);
            var transactionForDto = loaded ?? transaction;

            // Tag checks are informational — run after transaction is safely created
            var kenhaTag = await CheckKeNHATagAsync(vehicleRegNo);
            var localTags = await _vehicleTagService.CheckVehicleTagsAsync(vehicleRegNo);

            var dto = MapToDto(transactionForDto);
            dto.KeNHATagAlert = kenhaTag;
            dto.OpenTags = localTags;

            return CreatedAtAction(nameof(GetById), new { id = transaction.Id }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating weighing transaction");
            return StatusCode(500, "An error occurred while creating the weighing transaction.");
        }
    }

    /// <summary>
    /// Updates weighing transaction details (vehicle info, driver, etc.).
    /// </summary>
    /// <param name="id">Transaction ID</param>
    /// <param name="request">Updated transaction details</param>
    /// <returns>Updated transaction</returns>
    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:weighing.update")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(WeighingTransactionDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWeighingRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var transaction = await _weighingService.GetTransactionAsync(id);
            if (transaction == null)
            {
                return NotFound($"Weighing transaction {id} not found");
            }

            // Update only provided fields (include all metadata for weight ticket / compliance)
            if (!string.IsNullOrEmpty(request.VehicleRegNumber))
                transaction.VehicleRegNumber = request.VehicleRegNumber;

            if (request.DriverId.HasValue)
                transaction.DriverId = request.DriverId;

            if (request.TransporterId.HasValue)
                transaction.TransporterId = request.TransporterId;

            if (request.ActId.HasValue)
                transaction.ActId = request.ActId;

            if (request.OriginId.HasValue)
                transaction.OriginId = request.OriginId;

            if (request.DestinationId.HasValue)
                transaction.DestinationId = request.DestinationId;

            if (request.CargoId.HasValue)
                transaction.CargoId = request.CargoId;

            if (request.RoadId.HasValue)
                transaction.RoadId = request.RoadId;
            if (request.LocationTown != null)
                transaction.LocationTown = request.LocationTown;
            if (request.LocationSubcounty != null)
                transaction.LocationSubcounty = request.LocationSubcounty;
            if (request.LocationCounty != null)
                transaction.LocationCounty = request.LocationCounty;
            if (request.LocationLat.HasValue)
                transaction.LocationLat = request.LocationLat;
            if (request.SubcountyId.HasValue)
            {
                var subcountyExists = await _context.Subcounties.AsNoTracking().AnyAsync(s => s.Id == request.SubcountyId.Value);
                if (!subcountyExists)
                {
                    return BadRequest($"SubcountyId '{request.SubcountyId.Value}' is not valid. The subcounty may have been removed or does not exist.");
                }
                transaction.SubcountyId = request.SubcountyId;
            }
            if (request.LocationLng.HasValue)
                transaction.LocationLng = request.LocationLng;

            await _weighingService.UpdateTransactionAsync(transaction);

            // Reload with includes so MapToDto has Origin, Destination, Cargo, Vehicle, etc. populated
            var reloaded = await _weighingService.GetTransactionAsync(id);
            var dto = MapToDto(reloaded ?? transaction);
            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Weighing transaction {id} not found");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23503")
        {
            _logger.LogWarning(ex, "Foreign key violation updating weighing transaction {TransactionId}", id);
            return BadRequest("One or more reference values (e.g. Subcounty, Road, Driver, Transporter) are invalid or do not exist. Check the selected location and entity IDs.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating weighing transaction {TransactionId}", id);
            return StatusCode(500, "An error occurred while updating the weighing transaction.");
        }
    }

    /// <summary>
    /// Deletes a weighing transaction (only allowed in Pending status).
    /// </summary>
    /// <param name="id">Transaction ID</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [Authorize(Policy = "Permission:weighing.delete")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var transaction = await _weighingService.GetTransactionAsync(id);
            if (transaction == null)
            {
                return NotFound($"Weighing transaction {id} not found");
            }

            if (transaction.CaptureStatus.ToUpper() != "PENDING")
            {
                return BadRequest($"Cannot delete weighing in status '{transaction.ControlStatus}'. Only Pending transactions can be deleted.");
            }

            await _weighingService.DeleteTransactionAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            // Business-rule validation (e.g. non-Pending status) should return 400, not 500
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Weighing transaction {id} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting weighing transaction {TransactionId}", id);
            return StatusCode(500, "An error occurred while deleting the weighing transaction.");
        }
    }

    /// <summary>
    /// Captures axle weights for a weighing transaction.
    /// Unified endpoint for all weighing modes (Static, WIM, Mobile):
    /// Frontend sends array of WeighingAxle objects regardless of capture mode.
    /// Backend calculates GVW and validates compliance.
    /// </summary>
    /// <param name="id">Transaction ID</param>
    /// <param name="request">Captured axle weights (multiple axles in single call)</param>
    /// <returns>Updated transaction with compliance status</returns>
    [HttpPost("{id}/capture-weights")]
    [Authorize(Policy = "Permission:weighing.create")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(WeighingResultDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CaptureWeights(Guid id, [FromBody] CaptureWeightsRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (request.Axles == null || !request.Axles.Any())
        {
            return BadRequest("At least one axle weight must be provided");
        }

        try
        {
            // Map DTOs to entities (configId resolved by service layer if not provided)
            var axles = request.Axles.Select(dto => new WeighingAxle
            {
                AxleNumber = dto.AxleNumber,
                MeasuredWeightKg = dto.MeasuredWeightKg,
                AxleConfigurationId = dto.AxleConfigurationId.HasValue && dto.AxleConfigurationId.Value != Guid.Empty
                    ? dto.AxleConfigurationId.Value
                    : Guid.Empty,
                WeighingId = id,
                CapturedAt = DateTime.UtcNow
            }).ToList();

            // CaptureWeightsAsync saves axles, resolves config, and runs compliance
            var transaction = await _weighingService.CaptureWeightsAsync(id, axles);

            var resultDto = await MapToResultDtoAsync(transaction);
            return Ok(resultDto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Weighing transaction {id} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing weights for transaction {TransactionId}", id);
            return StatusCode(500, "An error occurred while capturing weights.");
        }
    }

    /// <summary>
    /// Processes autoweigh capture from TruConnect middleware.
    /// Creates weighing transaction, captures weights, and calculates compliance in a single operation.
    /// Supports idempotency via optional ClientLocalId field.
    /// </summary>
    /// <param name="request">Autoweigh capture data from middleware</param>
    /// <returns>Compliance result with weighing details</returns>
    [HttpPost("autoweigh")]
    [Authorize(Policy = "Permission:weighing.webhook")]
    [EnableRateLimiting("autoweigh")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(AutoweighResultDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Autoweigh([FromBody] AutoweighCaptureRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (request.Axles == null || !request.Axles.Any())
        {
            return BadRequest("At least one axle weight must be provided");
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            return Unauthorized("User ID not found in claims");
        }

        try
        {
            var result = await _weighingService.ProcessAutoweighAsync(request, userGuid);

            _logger.LogInformation(
                "Autoweigh processed: Transaction={TransactionId}, Vehicle={VehicleReg}, GVW={GvwKg}kg, Status={Status}",
                result.WeighingId, result.VehicleRegNumber, result.GvwMeasuredKg, result.ControlStatus);

            return CreatedAtAction(nameof(GetById), new { id = result.WeighingId }, result);
        }
        catch (InvalidOperationException ex)
        {
            // Scale test not found or other validation errors
            _logger.LogWarning(ex, "Autoweigh validation failed");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing autoweigh");
            return StatusCode(500, "An error occurred while processing the autoweigh request.");
        }
    }

    /// <summary>
    /// Downloads the weight ticket PDF for a completed weighing transaction.
    /// </summary>
    /// <param name="id">Transaction ID</param>
    /// <returns>PDF document</returns>
    [HttpGet("{id}/ticket/pdf")]
    // Surfaced/linked from the case register too, so case officers (case.read) can view the
    // weight ticket without also needing weighing.read.
    [HasAnyPermission("weighing.read", "case.read")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetWeightTicketPdf(Guid id)
    {
        try
        {
            var transaction = await _weighingService.GetTransactionAsync(id);
            if (transaction == null)
            {
                return NotFound($"Weighing transaction {id} not found");
            }

            var pdfBytes = await _weighingService.GenerateWeightTicketPdfAsync(id);
            return File(pdfBytes, "application/pdf", $"WeightTicket_{transaction.TicketNumber}.pdf");
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Weighing transaction {id} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating weight ticket PDF for transaction {TransactionId}", id);
            return StatusCode(500, "An error occurred while generating the weight ticket PDF.");
        }
    }

    /// <summary>
    /// Initiates a reweigh cycle for a non-compliant vehicle.
    /// </summary>
    /// <param name="request">Original weighing ID and new ticket number</param>
    /// <returns>New reweigh transaction</returns>
    [HttpPost("reweigh")]
    [Authorize(Policy = "Permission:weighing.create")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(WeighingTransactionDto), 201)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> StartReweigh([FromBody] InitiateReweighRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            return Unauthorized("User ID not found in claims");
        }

        try
        {
            var transaction = await _weighingService.InitiateReweighAsync(
                request.OriginalWeighingId,
                request.ReweighTicketNumber,
                userGuid,
                request.ReliefTruckRegNumber,
                request.ReliefTruckEmptyWeightKg);
            
            var dto = MapToDto(transaction);
            return CreatedAtAction(nameof(GetById), new { id = transaction.Id }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Original weighing transaction not found.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating reweigh transaction");
            return StatusCode(500, "An error occurred while initiating the reweigh transaction.");
        }
    }

    // ============================================================================
    // Dashboard Statistics Endpoints
    // ============================================================================

    /// <summary>
    /// Gets weighing statistics for the dashboard.
    /// Supports date range filtering.
    /// </summary>
    [HttpGet("statistics")]
    [Authorize(Policy = "Permission:weighing.read")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(WeighingStatisticsDto), 200)]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? stationId,
        [FromQuery] string? weighingType,
        [FromQuery] string? controlStatus,
        CancellationToken ct)
    {
        try
        {
            var isHqOrAdmin = User.FindFirst("is_hq_user")?.Value == "true" || User.IsInRole("Superuser") || User.IsInRole("System Admin");
            var effectiveStationId = (stationId == null && isHqOrAdmin) ? null : (stationId ?? _tenantContext.StationId);
            // Centralized EAT-aware day-boundary resolution (Common/WeighingQueryHelpers.cs) - fixes
            // the confirmed bug where SpecifyKind(...,Utc) relabelled the client's calendar date as
            // UTC instead of converting from Nairobi local time, shifting every "selected day" by 3h.
            var (from, toExclusive) = WeighingQueryHelpers.ResolveEatDayRange(dateFrom, dateTo);
            var to = toExclusive.AddTicks(-1);

            int totalWeighings, legalCount, overloadedCount, warningCount;
            decimal totalFeesKes, totalFeesUsd, avgOverloadKg, complianceRate;
            long totalNetWeightKg;
            int uniqueTransporters;

            // MV has no WeighingType/ControlStatus columns to filter BY (it only has pre-aggregated
            // counts per status) — query live table directly whenever either filter is active.
            bool useDirectQuery = !string.IsNullOrWhiteSpace(weighingType) || !string.IsNullOrWhiteSpace(controlStatus);
            if (useDirectQuery)
            {
                var q = _context.WeighingTransactions
                    .AsNoTracking()
                    .Where(wt => wt.WeighedAt >= from && wt.WeighedAt < toExclusive && wt.DeletedAt == null)
                    .Where(wt => !effectiveStationId.HasValue || wt.StationId == effectiveStationId);
                if (!string.IsNullOrWhiteSpace(weighingType))
                    q = q.Where(wt => wt.WeighingType == weighingType);
                if (!string.IsNullOrWhiteSpace(controlStatus))
                    q = WeighingQueryHelpers.ApplyControlStatusFilter(q, controlStatus);

                totalWeighings = await q.CountAsync(ct);
                legalCount = await q.CountAsync(wt => wt.ControlStatus == "Compliant", ct);
                overloadedCount = await q.CountAsync(wt => wt.ControlStatus == "Overloaded", ct);
                warningCount = await q.CountAsync(wt => wt.ControlStatus == "Warning", ct);
                complianceRate = totalWeighings > 0 ? Math.Round((decimal)legalCount / totalWeighings * 100, 1) : 0;
                var directRows = await q.Select(wt => new { wt.TotalFeeKes, wt.TotalFeeUsd, wt.OverloadKg, wt.NetWeightKg }).ToListAsync(ct);
                totalFeesKes = directRows.Sum(r => r.TotalFeeKes);
                totalFeesUsd = directRows.Sum(r => r.TotalFeeUsd);
                totalNetWeightKg = directRows.Sum(r => (long)(r.NetWeightKg ?? 0));
                uniqueTransporters = await q.Where(wt => wt.TransporterId != null).Select(wt => wt.TransporterId).Distinct().CountAsync(ct);
                var overloadedRows = directRows.Where(r => r.OverloadKg > 0).ToList();
                avgOverloadKg = overloadedRows.Any()
                    ? Math.Round((decimal)overloadedRows.Average(r => (double)r.OverloadKg), 0)
                    : 0;
            }
            else
            {
                var todayUtc = DateTime.UtcNow.Date;
                var rows = await _context.MvDailyWeighingStats
                    .AsNoTracking()
                    .Where(m => m.WeighingDate >= from.Date && m.WeighingDate <= to.Date)
                    .Where(m => !effectiveStationId.HasValue || m.StationId == effectiveStationId)
                    .Where(m => m.WeighingDate < todayUtc) // Exclude today - use live data below
                    .ToListAsync(ct);

                // MV only tracks KES fees; sum USD fees from live table for the full range
                totalFeesUsd = await _context.WeighingTransactions
                    .AsNoTracking()
                    .Where(wt => wt.WeighedAt >= from && wt.WeighedAt < toExclusive && wt.DeletedAt == null)
                    .Where(wt => !effectiveStationId.HasValue || wt.StationId == effectiveStationId)
                    .SumAsync(wt => (decimal?)wt.TotalFeeUsd ?? 0, ct);

                // MV has no net-weight or distinct-transporter columns; compute live for the full
                // range (same "MV lacks column, sum live instead" pattern as totalFeesUsd above).
                var liveRangeQuery = _context.WeighingTransactions
                    .AsNoTracking()
                    .Where(wt => wt.WeighedAt >= from && wt.WeighedAt < toExclusive && wt.DeletedAt == null)
                    .Where(wt => !effectiveStationId.HasValue || wt.StationId == effectiveStationId);
                totalNetWeightKg = await liveRangeQuery.SumAsync(wt => (long?)(wt.NetWeightKg ?? 0) ?? 0, ct);
                uniqueTransporters = await liveRangeQuery
                    .Where(wt => wt.TransporterId != null)
                    .Select(wt => wt.TransporterId)
                    .Distinct()
                    .CountAsync(ct);

                // Live fallback for today: MV may not be refreshed yet
                int todayWeighings = 0, todayLegal = 0, todayOverloaded = 0, todayWarning = 0;
                decimal todayFees = 0, todayAvgOverload = 0;
                if (toExclusive > todayUtc)
                {
                    var todayQuery = _context.WeighingTransactions
                        .AsNoTracking()
                        .Where(wt => wt.WeighedAt >= todayUtc && wt.WeighedAt < todayUtc.AddDays(1) && wt.DeletedAt == null)
                        .Where(wt => !effectiveStationId.HasValue || wt.StationId == effectiveStationId);
                    todayWeighings = await todayQuery.CountAsync(ct);
                    todayLegal = await todayQuery.CountAsync(wt => wt.ControlStatus == "Compliant", ct);
                    todayOverloaded = await todayQuery.CountAsync(wt => wt.ControlStatus == "Overloaded", ct);
                    todayWarning = await todayQuery.CountAsync(wt => wt.ControlStatus == "Warning", ct);
                    var todayRows = await todayQuery
                        .Select(wt => new { wt.TotalFeeKes, wt.OverloadKg })
                        .ToListAsync(ct);
                    todayFees = todayRows.Sum(r => r.TotalFeeKes);
                    var overloadedToday = todayRows.Where(r => r.OverloadKg > 0).ToList();
                    todayAvgOverload = overloadedToday.Any()
                        ? Math.Round((decimal)overloadedToday.Average(r => (double)r.OverloadKg), 0)
                        : 0;
                }

                totalWeighings = (int)rows.Sum(m => m.TotalWeighings) + todayWeighings;
                legalCount = (int)rows.Sum(m => m.CompliantCount) + todayLegal;
                // Real 3-way split from the MV's warning_count/overloaded_count columns (added
                // 2026-08-12 to fix the confirmed bug where this used to derive overloadedCount
                // from the boolean non_compliant_count, folding axle-only warnings into it and
                // collapsing warningCount to ~0).
                overloadedCount = (int)rows.Sum(m => m.OverloadedCount) + todayOverloaded;
                warningCount = (int)rows.Sum(m => m.WarningCount) + todayWarning;
                complianceRate = totalWeighings > 0 ? Math.Round((decimal)legalCount / totalWeighings * 100, 1) : 0;
                totalFeesKes = rows.Sum(m => m.TotalFeesCollected ?? 0) + todayFees;

                // avgOverloadKg weighted by the TRUE gvw-overloaded count (matches what AvgOverload
                // was itself averaged over: `AVG(overload_kg) FILTER (WHERE overload_kg > 0)`) -
                // previously weighted by non_compliant_count, which double-counted axle warnings
                // that have zero GVW overload, skewing the average.
                var mvOverloadedCount = (int)rows.Sum(m => m.OverloadedCount);
                var mvWeightedOverload = rows
                    .Where(m => m.OverloadedCount > 0 && m.AvgOverload.HasValue)
                    .Sum(m => (decimal)(m.AvgOverload!.Value * m.OverloadedCount));
                var totalOverloadedForAvg = mvOverloadedCount + todayOverloaded;
                avgOverloadKg = totalOverloadedForAvg > 0
                    ? Math.Round((mvWeightedOverload + todayAvgOverload * todayOverloaded) / totalOverloadedForAvg, 0)
                    : 0m;
            }

            return Ok(new WeighingStatisticsDto
            {
                TotalWeighings = totalWeighings,
                LegalCount = legalCount,
                OverloadedCount = overloadedCount,
                WarningCount = warningCount,
                ComplianceRate = complianceRate,
                TotalFeesKes = totalFeesKes,
                TotalFeesUsd = totalFeesUsd,
                AvgOverloadKg = avgOverloadKg,
                TotalNetWeightKg = totalNetWeightKg,
                UniqueTransporters = uniqueTransporters
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting weighing statistics");
            return StatusCode(500, "An error occurred while getting weighing statistics.");
        }
    }

    /// <summary>
    /// Gets compliance trend data for charts.
    /// Returns daily compliance/overload counts over the date range.
    /// </summary>
    [HttpGet("compliance-trend")]
    [Authorize(Policy = "Permission:weighing.read")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<ComplianceTrendDto>), 200)]
    public async Task<IActionResult> GetComplianceTrend(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? stationId,
        CancellationToken ct)
    {
        try
        {
            var isHqOrAdmin = User.FindFirst("is_hq_user")?.Value == "true" || User.IsInRole("Superuser") || User.IsInRole("System Admin");
            var effectiveStationId = (stationId == null && isHqOrAdmin) ? null : (stationId ?? _tenantContext.StationId);
            var (from, to) = WeighingQueryHelpers.ResolveEatDayRange(dateFrom, dateTo);

            // Server-side GROUP BY using composite index IX_weighing_transactions_station_status_date
            var trendData = await _context.WeighingTransactions
                .AsNoTracking()
                .Where(wt => wt.WeighedAt >= from && wt.WeighedAt < to && wt.DeletedAt == null)
                .Where(wt => !effectiveStationId.HasValue || wt.StationId == effectiveStationId)
                // Group by the EAT-local calendar date (not the raw UTC date) so an early-morning
                // EAT weighing (e.g. 01:00 EAT = 22:00 UTC the previous day) lands on the day the
                // officer actually weighed it on, not the day before.
                .GroupBy(wt => wt.WeighedAt.AddHours(3).Date)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Date = g.Key,
                    Compliant = g.Count(t => t.ControlStatus == "Compliant"),
                    Overloaded = g.Count(t => t.ControlStatus == "Overloaded"),
                    Warning = g.Count(t => t.ControlStatus == "Warning")
                })
                .ToListAsync(ct);

            var trend = trendData.Select(d => new ComplianceTrendDto
            {
                Name = d.Date.ToString("MMM dd"),
                Compliant = d.Compliant,
                Overloaded = d.Overloaded,
                Warning = d.Warning
            }).ToList();

            return Ok(trend);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance trend");
            return StatusCode(500, "An error occurred while getting compliance trend.");
        }
    }

    /// <summary>
    /// Gets overload distribution by severity bands.
    /// </summary>
    [HttpGet("overload-distribution")]
    [Authorize(Policy = "Permission:weighing.read")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<OverloadDistributionDto>), 200)]
    public async Task<IActionResult> GetOverloadDistribution(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? stationId,
        CancellationToken ct)
    {
        try
        {
            var isHqOrAdmin = User.FindFirst("is_hq_user")?.Value == "true" || User.IsInRole("Superuser") || User.IsInRole("System Admin");
            var effectiveStationId = (stationId == null && isHqOrAdmin) ? null : (stationId ?? _tenantContext.StationId);
            var (from, to) = WeighingQueryHelpers.ResolveEatDayRange(dateFrom, dateTo);

            var cacheKey = $"dashboard:overload-dist:{from:yyyyMMddHHmm}:{to:yyyyMMddHHmm}:{effectiveStationId}";
            var cached = await _cacheService.GetStringAsync(cacheKey, ct);
            if (cached != null)
                return Ok(JsonSerializer.Deserialize<List<OverloadDistributionDto>>(cached));

            // Project only the overload percentage from DB (avoids loading full records).
            // "ControlStatus == \"OVERLOAD\"" was a confirmed bug - that value is never persisted
            // (the real value is "Overloaded"), so this chart was always empty.
            var pcts = await WeighingQueryHelpers
                .ApplyControlStatusFilter(
                    _context.WeighingTransactions.AsNoTracking()
                        .Where(wt => wt.WeighedAt >= from && wt.WeighedAt < to && wt.DeletedAt == null)
                        .Where(wt => !effectiveStationId.HasValue || wt.StationId == effectiveStationId)
                        .Where(wt => wt.GvwPermissibleKg > 0),
                    "Overloaded")
                .Select(wt => (double)wt.OverloadKg / (double)wt.GvwPermissibleKg * 100)
                .ToListAsync(ct);

            var total = pcts.Count;
            var bands = new List<(string Name, double Min, double Max)>
            {
                ("0-5%",    0,   5),
                ("5-10%",   5,  10),
                ("10-20%", 10,  20),
                ("20-50%", 20,  50),
                (">50%",   50, double.MaxValue)
            };

            var distribution = bands.Select(b =>
            {
                var count = pcts.Count(p => p >= b.Min && (b.Max == double.MaxValue || p < b.Max));
                return new OverloadDistributionDto
                {
                    Name = b.Name,
                    Count = count,
                    Percentage = total > 0 ? Math.Round((decimal)count / total * 100, 1) : 0
                };
            }).ToList();

            await _cacheService.SetStringAsync(cacheKey, JsonSerializer.Serialize(distribution), TimeSpan.FromMinutes(5), ct);
            return Ok(distribution);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting overload distribution");
            return StatusCode(500, "An error occurred while getting overload distribution.");
        }
    }

    /// <summary>
    /// Gets vehicle type distribution.
    /// </summary>
    [HttpGet("vehicle-distribution")]
    [Authorize(Policy = "Permission:weighing.read")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<object>), 200)]
    public async Task<IActionResult> GetVehicleDistribution(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? stationId,
        CancellationToken ct)
    {
        try
        {
            var isHqOrAdmin = User.FindFirst("is_hq_user")?.Value == "true" || User.IsInRole("Superuser") || User.IsInRole("System Admin");
            var effectiveStationId = (stationId == null && isHqOrAdmin) ? null : (stationId ?? _tenantContext.StationId);
            var (from, to) = WeighingQueryHelpers.ResolveEatDayRange(dateFrom, dateTo);

            var cacheKey = $"dashboard:vehicle-dist:{from:yyyyMMddHHmm}:{to:yyyyMMddHHmm}:{effectiveStationId}";
            var cached = await _cacheService.GetStringAsync(cacheKey, ct);
            if (cached != null)
                return Ok(JsonSerializer.Deserialize<List<object>>(cached));

            // Server-side GroupBy by WeighingType — avoids loading full records + axle joins
            var data = await _context.WeighingTransactions
                .AsNoTracking()
                .Where(wt => wt.WeighedAt >= from && wt.WeighedAt < to && wt.DeletedAt == null)
                .Where(wt => !effectiveStationId.HasValue || wt.StationId == effectiveStationId)
                .GroupBy(wt => wt.WeighingType)
                .OrderBy(g => g.Key)
                .Select(g => new { Name = g.Key ?? "Unknown", Value = g.Count() })
                .ToListAsync(ct);

            var json = JsonSerializer.Serialize(data);
            await _cacheService.SetStringAsync(cacheKey, json, TimeSpan.FromMinutes(5), ct);
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vehicle distribution");
            return StatusCode(500, "An error occurred while getting vehicle distribution.");
        }
    }

    /// <summary>
    /// Gets daily weighing volume trend.
    /// </summary>
    [HttpGet("daily-volume")]
    [Authorize(Policy = "Permission:weighing.read")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<object>), 200)]
    public async Task<IActionResult> GetDailyVolume(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? stationId,
        CancellationToken ct)
    {
        try
        {
            var isHqOrAdmin = User.FindFirst("is_hq_user")?.Value == "true" || User.IsInRole("Superuser") || User.IsInRole("System Admin");
            var effectiveStationId = (stationId == null && isHqOrAdmin) ? null : (stationId ?? _tenantContext.StationId);
            var (fromUtc, toUtcExclusive) = WeighingQueryHelpers.ResolveEatDayRange(dateFrom, dateTo);

            // NOTE: mv_daily_weighing_stats.weighing_date is DATE(weighed_at) - a UTC calendar date,
            // not an EAT one. Filtering its bounds with EAT-converted from/to (below) is still an
            // improvement over the previous raw SpecifyKind (it no longer drops/adds a boundary day
            // outright), but a weighing in the first/last 3 EAT hours of the selected range can still
            // land in the adjacent UTC-dated MV row. Fully fixing this needs the MV's own DATE(...)
            // grouping changed to an EAT-shifted expression, a larger change (affects its unique
            // index) deliberately not bundled into this pass - flagged as a follow-up.
            var from = fromUtc.Date;
            var to = toUtcExclusive.AddTicks(-1).Date;

            var rows = await _context.MvDailyWeighingStats
                .AsNoTracking()
                .Where(m => m.WeighingDate >= from && m.WeighingDate <= to)
                .Where(m => !effectiveStationId.HasValue || m.StationId == effectiveStationId)
                .ToListAsync(ct);

            var volume = rows
                .GroupBy(m => m.WeighingDate)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    name = g.Key.ToString("MMM dd"),
                    total = g.Sum(m => m.TotalWeighings)
                })
                .ToList();

            return Ok(volume);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting daily volume");
            return StatusCode(500, "An error occurred while getting daily volume.");
        }
    }

    // ============================================================================
    // KeNHA Tag Verification
    // ============================================================================

    /// <summary>
    /// Checks if a vehicle has an existing KeNHA tag/prohibition.
    /// Only returns data when KeNHA integration is configured and active.
    /// Called by the capture screen after vehicle number plate is entered.
    /// </summary>
    /// <param name="regNo">Vehicle registration number</param>
    /// <returns>Tag alert if found, null if no tag or integration unavailable</returns>
    [HttpGet("kenha-tag-check/{regNo}")]
    [Authorize(Policy = "Permission:weighing.read")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(KeNHATagAlertDto), 200)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> CheckKeNHATag(string regNo)
    {
        var alert = await CheckKeNHATagAsync(regNo);
        if (alert == null)
            return NoContent();

        return Ok(alert);
    }

    /// <summary>
    /// Background KeNHA tag check. Returns null if integration is unavailable or no tag found.
    /// Gracefully handles all errors to never block the weighing workflow.
    /// </summary>
    private async Task<KeNHATagAlertDto?> CheckKeNHATagAsync(string regNo)
    {
        try
        {
            if (!await _kenhaService.IsAvailableAsync())
                return null;

            var result = await _kenhaService.VerifyVehicleTagAsync(regNo);
            if (result == null || !result.HasTag)
                return null;

            var alertLevel = result.TagStatus?.ToLower() switch
            {
                "open" => "critical",
                "closed" => "info",
                _ => "warning"
            };

            return new KeNHATagAlertDto
            {
                HasTag = true,
                TagStatus = result.TagStatus,
                TagCategory = result.TagCategory,
                Reason = result.Reason,
                Station = result.Station,
                TagDate = result.TagDate,
                TagUid = result.TagUid,
                AlertLevel = alertLevel,
                Message = $"Vehicle has an existing KeNHA tag ({result.TagStatus}): {result.Reason ?? result.TagCategory ?? "Unknown reason"}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "KeNHA tag check failed for {RegNo}, continuing without tag data", regNo);
            return null;
        }
    }

    // ============================================================================
    // DTO Mapping Methods
    // ============================================================================

    /// <summary>
    /// Maps WeighingTransaction entity to WeighingTransactionDto.
    /// </summary>
    private WeighingTransactionDto MapToDto(WeighingTransaction transaction)
    {
        var axles = transaction.WeighingAxles?.ToList() ?? new List<WeighingAxle>();
        var isMultiDeck = transaction.WeighingType == "multideck" || transaction.WeighingType == "static";

        // Axle configuration: prefer vehicle's; when vehicle has none (e.g. mobile auto-created vehicle), use config from weighing axles
        var axleConfigCode = transaction.Vehicle?.AxleConfiguration?.AxleCode
            ?? axles.OrderBy(a => a.AxleNumber).Select(a => a.AxleConfiguration?.AxleCode).FirstOrDefault(ac => !string.IsNullOrEmpty(ac));

        return new WeighingTransactionDto
        {
            Id = transaction.Id,
            TicketNumber = transaction.TicketNumber,
            VehicleId = transaction.VehicleId,
            VehicleRegNumber = transaction.VehicleRegNumber,
            DriverId = transaction.DriverId,
            TransporterId = transaction.TransporterId,
            StationId = transaction.StationId ?? Guid.Empty,
            WeighedByUserId = transaction.WeighedByUserId,
            WeighingType = transaction.WeighingType,
            Bound = transaction.Bound,
            GvwMeasuredKg = transaction.GvwMeasuredKg,
            GvwPermissibleKg = transaction.GvwPermissibleKg,
            OverloadKg = transaction.OverloadKg,
            ExcessKg = Math.Max(0, transaction.OverloadKg),
            ControlStatus = transaction.ControlStatus,
            TotalFeeUsd = transaction.TotalFeeUsd,
            TotalFeeKes = transaction.TotalFeeKes,
            ChargingCurrency = transaction.Act?.ChargingCurrency ?? "KES",
            IsCompliant = transaction.IsCompliant,
            IsSentToYard = transaction.IsSentToYard,
            ViolationReason = transaction.ViolationReason,
            CaptureStatus = transaction.CaptureStatus,
            CaptureSource = transaction.CaptureSource,
            WeighedAt = transaction.WeighedAt,
            IsSync = transaction.IsSync,
            ReweighCycleNo = transaction.ReweighCycleNo,
            OriginalWeighingId = transaction.OriginalWeighingId,
            HasPermit = transaction.HasPermit,

            // Vehicle details (vehicle may be auto-created with only reg number on mobile). Use empty string instead of null for display.
            VehicleMake = transaction.Vehicle?.Make ?? string.Empty,
            VehicleModel = transaction.Vehicle?.Model ?? string.Empty,
            VehicleType = transaction.Vehicle?.VehicleType ?? string.Empty,
            AxleConfiguration = axleConfigCode ?? string.Empty,
            IsMultiDeck = isMultiDeck,

            // People
            DriverName = transaction.Driver?.FullNames ?? string.Empty,
            TransporterName = transaction.Transporter?.Name ?? string.Empty,
            WeighedByUserName = transaction.WeighedByUser?.FullName ?? string.Empty,

            // Station
            StationName = transaction.Station?.Name ?? string.Empty,
            StationCode = transaction.Station?.Code ?? string.Empty,

            // Scale test (daily calibration verification for this session)
            ScaleTestId = transaction.ScaleTestId,
            ScaleTestResult = transaction.ScaleTest?.Result,
            ScaleTestCarriedAt = transaction.ScaleTest?.CarriedAt,

            // Timing — ProcessingTimeSeconds is set at CaptureStatus="captured"; null for pre-migration records
            TimeTakenSeconds = transaction.ProcessingTimeSeconds,

            // Deck weights from axle groupings (A/B/C/D) — only for static/multideck
            DeckAWeightKg = isMultiDeck ? NullIfZero(axles.Where(a => a.AxleGrouping == "A").Sum(a => a.MeasuredWeightKg)) : null,
            DeckBWeightKg = isMultiDeck ? NullIfZero(axles.Where(a => a.AxleGrouping == "B").Sum(a => a.MeasuredWeightKg)) : null,
            DeckCWeightKg = isMultiDeck ? NullIfZero(axles.Where(a => a.AxleGrouping == "C").Sum(a => a.MeasuredWeightKg)) : null,
            DeckDWeightKg = isMultiDeck ? NullIfZero(axles.Where(a => a.AxleGrouping == "D").Sum(a => a.MeasuredWeightKg)) : null,

            // Route & Cargo. Use empty string for display fields so response does not return null.
            OriginId = transaction.OriginId,
            DestinationId = transaction.DestinationId,
            CargoId = transaction.CargoId,
            SourceLocation = transaction.Origin?.Name ?? string.Empty,
            DestinationLocation = transaction.Destination?.Name ?? string.Empty,
            CargoType = transaction.Cargo?.Name ?? string.Empty,
            CargoDescription = transaction.Cargo?.Category ?? string.Empty,

            RoadId = transaction.RoadId,
            RoadName = transaction.Road?.Name ?? string.Empty,
            RoadCode = transaction.Road?.Code ?? string.Empty,
            SubcountyId = transaction.SubcountyId,
            LocationSubcounty = transaction.LocationSubcounty ?? string.Empty,
            LocationTown = transaction.LocationTown ?? string.Empty,
            LocationCounty = transaction.LocationCounty ?? string.Empty,
            LocationLat = transaction.LocationLat,
            LocationLng = transaction.LocationLng,

            WeighingAxles = axles.Select(a => new WeighingAxleDto
            {
                Id = a.Id,
                AxleNumber = a.AxleNumber,
                MeasuredWeightKg = a.MeasuredWeightKg,
                PermissibleWeightKg = a.PermissibleWeightKg,
                OverloadKg = a.OverloadKg,
                AxleConfigurationId = a.AxleConfigurationId,
                AxleWeightReferenceId = a.AxleWeightReferenceId,
                CapturedAt = a.CapturedAt
            }).ToList()
        };
    }

    private static int? NullIfZero(int value) => value == 0 ? null : value;

    /// <summary>
    /// Maps WeighingTransaction entity to WeighingResultDto (compliance focused).
    /// Uses stored tolerance values from CalculateComplianceAsync and includes
    /// group-level compliance results for frontend display.
    /// </summary>
    private async Task<WeighingResultDto> MapToResultDtoAsync(WeighingTransaction transaction)
    {
        // Use stored tolerance values (computed by CalculateComplianceAsync).
        // Fallback to DB lookup for legacy transactions without stored tolerance.
        int gvwToleranceKg = transaction.GvwToleranceKg;
        string gvwToleranceDisplay = transaction.GvwToleranceDisplay ?? "0% (strict)";

        if (gvwToleranceKg == 0 && string.IsNullOrEmpty(transaction.GvwToleranceDisplay))
        {
            string fw = transaction.Act?.Code ?? "TRAFFIC_ACT";
            gvwToleranceKg = await _toleranceRepository.CalculateToleranceKgAsync(
                fw, "GVW", transaction.GvwPermissibleKg);
            var gvwSetting = await _toleranceRepository.GetToleranceAsync(fw, "GVW");
            if (gvwSetting != null && gvwToleranceKg > 0)
            {
                gvwToleranceDisplay = gvwSetting.TolerancePercentage > 0
                    ? $"{gvwSetting.TolerancePercentage:0.##}%"
                    : gvwSetting.ToleranceKg is > 0
                        ? $"{gvwSetting.ToleranceKg.Value:N0} kg"
                        : "0% (strict)";
            }
        }

        int gvwEffectiveLimitKg = transaction.GvwPermissibleKg + gvwToleranceKg;

        // Build group results from stored axle data for frontend compliance display
        List<AxleGroupResultDto>? groupResults = null;
        if (transaction.WeighingAxles != null && transaction.WeighingAxles.Any())
        {
            string framework = transaction.Act?.Code ?? "TRAFFIC_ACT";
            string currency = transaction.Act?.ChargingCurrency ?? "KES";
            int opTolerance = transaction.OperationalAllowanceUsed > 0
                ? transaction.OperationalAllowanceUsed : 200;
            groupResults = await _axleGroupAggregationService.AggregateAxleGroupsAsync(
                transaction.WeighingAxles.ToList(), framework, opTolerance, currency);
        }

        return new WeighingResultDto
        {
            WeighingId = transaction.Id,
            TicketNumber = transaction.TicketNumber,
            VehicleRegNumber = transaction.VehicleRegNumber,
            GvwMeasuredKg = transaction.GvwMeasuredKg,
            GvwPermissibleKg = transaction.GvwPermissibleKg,
            GvwOverloadKg = transaction.OverloadKg,
            GvwToleranceKg = gvwToleranceKg,
            GvwEffectiveLimitKg = gvwEffectiveLimitKg,
            GvwToleranceDisplay = gvwToleranceDisplay,
            OverloadKg = transaction.OverloadKg,
            OverallStatus = transaction.ControlStatus == "Compliant" ? "LEGAL"
                : transaction.ControlStatus == "Warning" ? "WARNING"
                : transaction.ControlStatus == "Overloaded" ? "OVERLOAD"
                : transaction.ControlStatus,
            IsCompliant = transaction.IsCompliant,
            ControlStatus = transaction.ControlStatus,
            ViolationReason = transaction.ViolationReason,
            IsSentToYard = transaction.IsSentToYard,
            CaptureStatus = transaction.CaptureStatus ?? string.Empty,
            VehicleId = transaction.VehicleId,
            TotalFeeUsd = transaction.TotalFeeUsd,
            TotalFeeKes = transaction.TotalFeeKes,
            ChargingCurrency = transaction.Act?.ChargingCurrency ?? "KES",
            HasPermit = transaction.HasPermit,
            ReweighCycleNo = transaction.ReweighCycleNo,
            WeighedAt = transaction.WeighedAt,
            OperationalToleranceKg = transaction.OperationalAllowanceUsed,
            AxleToleranceDisplay = transaction.AxleToleranceDisplay,
            GroupResults = groupResults,
            AxleCompliance = transaction.WeighingAxles?.Select(a => new AxleComplianceDto
            {
                AxleNumber = a.AxleNumber,
                MeasuredWeightKg = a.MeasuredWeightKg,
                PermissibleWeightKg = a.PermissibleWeightKg,
                OverloadKg = a.OverloadKg,
                IsCompliant = a.OverloadKg <= 0
            }).ToList() ?? new()
        };
    }

    /// <summary>
    /// Permanently delete a weighing transaction and all its related records from the database.
    /// Superuser-only — this action is irreversible.
    /// </summary>
    [HttpDelete("{id}/hard")]
    [Authorize(Roles = "Superuser")]
    public async Task<IActionResult> HardDeleteTransaction(Guid id, CancellationToken ct)
    {
        var transaction = await _context.WeighingTransactions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (transaction == null)
            return NotFound(new { message = "Weighing transaction not found" });

        _context.WeighingTransactions.Remove(transaction);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Permanently delete a tare record from the database.
    /// Superuser-only — this action is irreversible.
    /// </summary>
    [HttpDelete("tares/{id}/hard")]
    [Authorize(Roles = "Superuser")]
    public async Task<IActionResult> HardDeleteTare(Guid id, CancellationToken ct)
    {
        var tare = await _context.VehicleTareHistory
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (tare == null)
            return NotFound(new { message = "Tare record not found" });

        _context.VehicleTareHistory.Remove(tare);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }
}
