using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;
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

    public WeighingController(
        IWeighingService weighingService,
        IVehicleRepository vehicleRepository,
        IKeNHAService kenhaService,
        IVehicleTagService vehicleTagService,
        ITenantContext tenantContext,
        ILogger<WeighingController> logger,
        TruLoadDbContext context,
        ICacheService cacheService)
    {
        _weighingService = weighingService;
        _vehicleRepository = vehicleRepository;
        _kenhaService = kenhaService;
        _vehicleTagService = vehicleTagService;
        _tenantContext = tenantContext;
        _logger = logger;
        _context = context;
        _cacheService = cacheService;
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
            var hasGlobalRead = User.HasClaim(c => c.Type == "Permission" && c.Value == "weighing.read");
            var stationId = (request.StationId == null && hasGlobalRead) ? null : (request.StationId ?? _tenantContext.StationId);

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
                request.WeighingType);

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
            var query = from wa in _context.WeighingAxles.AsNoTracking()
                        join wt in _context.WeighingTransactions.AsNoTracking() on wa.WeighingId equals wt.Id
                        where (wa.MeasuredWeightKg - wa.PermissibleWeightKg) > 0
                        select new { wa, wt };

            if (dateFrom.HasValue)
                query = query.Where(x => x.wt.WeighedAt >= dateFrom.Value);
            if (dateTo.HasValue)
                query = query.Where(x => x.wt.WeighedAt <= dateTo.Value.AddDays(1));
            if (stationId.HasValue)
                query = query.Where(x => x.wt.StationId == stationId.Value);

            var grouped = await query
                .GroupBy(x => string.IsNullOrEmpty(x.wa.AxleType) ? "Other" : x.wa.AxleType)
                .Select(g => new OverloadDistributionDto
                {
                    Name = g.Key,
                    Count = g.Count(),
                    Percentage = 0
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync(ct);

            var total = grouped.Sum(x => x.Count);
            foreach (var item in grouped)
                item.Percentage = total > 0 ? Math.Round((decimal)item.Count * 100 / total, 2) : 0;

            return Ok(grouped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting axle type violations");
            return StatusCode(500, "An error occurred while getting axle type violations.");
        }
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
            var query = from wa in _context.WeighingAxles.AsNoTracking()
                        join wt in _context.WeighingTransactions.AsNoTracking() on wa.WeighingId equals wt.Id
                        where (wa.MeasuredWeightKg - wa.PermissibleWeightKg) > 0
                        select new { wa, wt };

            if (dateFrom.HasValue)
                query = query.Where(x => x.wt.WeighedAt >= dateFrom.Value);
            if (dateTo.HasValue)
                query = query.Where(x => x.wt.WeighedAt <= dateTo.Value.AddDays(1));
            if (stationId.HasValue)
                query = query.Where(x => x.wt.StationId == stationId.Value);

            var grouped = await query
                .GroupBy(x => string.IsNullOrEmpty(x.wa.AxleType) ? "Other" : x.wa.AxleType)
                .Select(g => new OverloadDistributionDto
                {
                    Name = g.Key,
                    Count = g.Count(),
                    Percentage = 0
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync(ct);

            var total = grouped.Sum(x => x.Count);
            foreach (var item in grouped)
                item.Percentage = total > 0 ? Math.Round((decimal)item.Count * 100 / total, 2) : 0;

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
                request.CargoId);

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

            var resultDto = MapToResultDto(transaction);
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
    [Authorize(Policy = "Permission:weighing.read")]
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
        CancellationToken ct)
    {
        try
        {
            bool hasGlobalRead = User.HasClaim(c => c.Type == "Permission" && c.Value == "weighing.read");
            var effectiveStationId = (stationId == null && hasGlobalRead) ? null : (stationId ?? _tenantContext.StationId);
            var from = (dateFrom.HasValue ? DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30)).Date;
            var to = (dateTo.HasValue ? DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc) : DateTime.UtcNow).Date;

            var todayUtc = DateTime.UtcNow.Date;
            var rows = await _context.MvDailyWeighingStats
                .AsNoTracking()
                .Where(m => m.WeighingDate >= from && m.WeighingDate <= to)
                .Where(m => !effectiveStationId.HasValue || m.StationId == effectiveStationId)
                .Where(m => m.WeighingDate < todayUtc) // Exclude today - use live data below
                .ToListAsync(ct);

            // Live fallback for today: MV may not be refreshed yet
            int todayWeighings = 0, todayLegal = 0, todayOverloaded = 0, todayWarning = 0;
            decimal todayFees = 0, todayAvgOverload = 0;
            if (to >= todayUtc)
            {
                var todayQuery = _context.WeighingTransactions
                    .AsNoTracking()
                    .Where(wt => wt.WeighedAt >= todayUtc && wt.WeighedAt <= to && wt.DeletedAt == null)
                    .Where(wt => !effectiveStationId.HasValue || wt.StationId == effectiveStationId);
                todayWeighings = await todayQuery.CountAsync(ct);
                todayLegal = await todayQuery.CountAsync(wt => wt.ControlStatus == "Compliant" || wt.ControlStatus == "LEGAL", ct);
                todayOverloaded = await todayQuery.CountAsync(wt => wt.ControlStatus == "Overloaded" || wt.ControlStatus == "OVERLOAD", ct);
                todayWarning = todayWeighings - todayLegal - todayOverloaded;
                var todayRows = await todayQuery
                    .Select(wt => new { wt.TotalFeeUsd, wt.OverloadKg })
                    .ToListAsync(ct);
                todayFees = todayRows.Sum(r => r.TotalFeeUsd);
                var overloadedToday = todayRows.Where(r => r.OverloadKg > 0).ToList();
                todayAvgOverload = overloadedToday.Any()
                    ? Math.Round((decimal)overloadedToday.Average(r => (double)r.OverloadKg), 0)
                    : 0;
            }

            var totalWeighings = rows.Sum(m => m.TotalWeighings) + todayWeighings;
            var legalCount = rows.Sum(m => m.CompliantCount) + todayLegal;
            var overloadedCount = rows.Sum(m => m.NonCompliantCount) + todayOverloaded;
            var warningCount = totalWeighings - legalCount - overloadedCount;
            var complianceRate = totalWeighings > 0 ? Math.Round((decimal)legalCount / totalWeighings * 100, 1) : 0;
            var totalFeesKes = rows.Sum(m => m.TotalFeesCollected ?? 0) + todayFees;
            var mvOverloadedCount = (int)rows.Sum(m => m.NonCompliantCount);
            var mvWeightedOverload = rows
                .Where(m => m.NonCompliantCount > 0 && m.AvgOverload.HasValue)
                .Sum(m => (decimal)(m.AvgOverload!.Value * m.NonCompliantCount));
            var totalOverloadedForAvg = mvOverloadedCount + todayOverloaded;
            var avgOverloadKg = totalOverloadedForAvg > 0
                ? Math.Round((mvWeightedOverload + todayAvgOverload * todayOverloaded) / totalOverloadedForAvg, 0)
                : 0m;

            return Ok(new WeighingStatisticsDto
            {
                TotalWeighings = (int)totalWeighings,
                LegalCount = (int)legalCount,
                OverloadedCount = (int)overloadedCount,
                WarningCount = (int)warningCount,
                ComplianceRate = complianceRate,
                TotalFeesKes = totalFeesKes,
                AvgOverloadKg = avgOverloadKg
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
            bool hasGlobalRead = User.HasClaim(c => c.Type == "Permission" && c.Value == "weighing.read");
            var effectiveStationId = (stationId == null && hasGlobalRead) ? null : (stationId ?? _tenantContext.StationId);
            var from = dateFrom.HasValue ? DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
            var to = dateTo.HasValue ? DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc) : DateTime.UtcNow;

            // Server-side GROUP BY using composite index IX_weighing_transactions_station_status_date
            var trendData = await _context.WeighingTransactions
                .AsNoTracking()
                .Where(wt => wt.WeighedAt >= from && wt.WeighedAt <= to && wt.DeletedAt == null)
                .Where(wt => !effectiveStationId.HasValue || wt.StationId == effectiveStationId)
                .GroupBy(wt => wt.WeighedAt.Date)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Date = g.Key,
                    Compliant = g.Count(t => t.ControlStatus == "Compliant" || t.ControlStatus == "LEGAL"),
                    Overloaded = g.Count(t => t.ControlStatus == "Overloaded" || t.ControlStatus == "OVERLOAD"),
                    Warning = g.Count(t => t.ControlStatus == "Warning" || t.ControlStatus == "WARNING")
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
            bool hasGlobalRead = User.HasClaim(c => c.Type == "Permission" && c.Value == "weighing.read");
            var effectiveStationId = (stationId == null && hasGlobalRead) ? null : (stationId ?? _tenantContext.StationId);
            var from = dateFrom.HasValue ? DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
            var to = dateTo.HasValue ? DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc) : DateTime.UtcNow;

            var cacheKey = $"dashboard:overload-dist:{from:yyyyMMdd}:{to:yyyyMMdd}:{effectiveStationId}";
            var cached = await _cacheService.GetStringAsync(cacheKey, ct);
            if (cached != null)
                return Ok(JsonSerializer.Deserialize<List<OverloadDistributionDto>>(cached));

            // Project only the overload percentage from DB (avoids loading full records)
            var pcts = await _context.WeighingTransactions
                .AsNoTracking()
                .Where(wt => wt.WeighedAt >= from && wt.WeighedAt <= to && wt.DeletedAt == null)
                .Where(wt => !effectiveStationId.HasValue || wt.StationId == effectiveStationId)
                .Where(wt => wt.ControlStatus == "OVERLOAD" && wt.GvwPermissibleKg > 0)
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
            bool hasGlobalRead = User.HasClaim(c => c.Type == "Permission" && c.Value == "weighing.read");
            var effectiveStationId = (stationId == null && hasGlobalRead) ? null : (stationId ?? _tenantContext.StationId);
            var from = dateFrom.HasValue ? DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
            var to = dateTo.HasValue ? DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc) : DateTime.UtcNow;

            var cacheKey = $"dashboard:vehicle-dist:{from:yyyyMMdd}:{to:yyyyMMdd}:{effectiveStationId}";
            var cached = await _cacheService.GetStringAsync(cacheKey, ct);
            if (cached != null)
                return Ok(JsonSerializer.Deserialize<List<object>>(cached));

            // Server-side GroupBy by WeighingType — avoids loading full records + axle joins
            var data = await _context.WeighingTransactions
                .AsNoTracking()
                .Where(wt => wt.WeighedAt >= from && wt.WeighedAt <= to && wt.DeletedAt == null)
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
            bool hasGlobalRead = User.HasClaim(c => c.Type == "Permission" && c.Value == "weighing.read");
            var effectiveStationId = (stationId == null && hasGlobalRead) ? null : (stationId ?? _tenantContext.StationId);
            var from = (dateFrom.HasValue ? DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30)).Date;
            var to = (dateTo.HasValue ? DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc) : DateTime.UtcNow).Date;

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

            // Timing
            TimeTakenSeconds = (int)(transaction.WeighedAt - transaction.CreatedAt).TotalSeconds,

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
    /// </summary>
    private WeighingResultDto MapToResultDto(WeighingTransaction transaction)
    {
        return new WeighingResultDto
        {
            WeighingId = transaction.Id,
            TicketNumber = transaction.TicketNumber,
            VehicleRegNumber = transaction.VehicleRegNumber,
            GvwMeasuredKg = transaction.GvwMeasuredKg,
            GvwPermissibleKg = transaction.GvwPermissibleKg,
            GvwOverloadKg = transaction.OverloadKg,
            OverloadKg = transaction.OverloadKg,
            IsCompliant = transaction.IsCompliant,
            ControlStatus = transaction.ControlStatus,
            ViolationReason = transaction.ViolationReason,
            IsSentToYard = transaction.IsSentToYard,
            CaptureStatus = transaction.CaptureStatus ?? string.Empty,
            VehicleId = transaction.VehicleId,
            TotalFeeUsd = transaction.TotalFeeUsd,
            HasPermit = transaction.HasPermit,
            ReweighCycleNo = transaction.ReweighCycleNo,
            WeighedAt = transaction.WeighedAt,
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
}
