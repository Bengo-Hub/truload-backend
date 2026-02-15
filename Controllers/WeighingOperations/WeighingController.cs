using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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

    public WeighingController(
        IWeighingService weighingService,
        IVehicleRepository vehicleRepository,
        IKeNHAService kenhaService,
        IVehicleTagService vehicleTagService,
        ITenantContext tenantContext,
        ILogger<WeighingController> logger)
    {
        _weighingService = weighingService;
        _vehicleRepository = vehicleRepository;
        _kenhaService = kenhaService;
        _vehicleTagService = vehicleTagService;
        _tenantContext = tenantContext;
        _logger = logger;
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
            var stationId = request.StationId ?? _tenantContext.StationId;

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

            // Run weighing initiation, KeNHA tag check, and local tag check concurrently
            var weighingTask = _weighingService.InitiateWeighingAsync(
                request.TicketNumber,
                request.StationId,
                userGuid,
                vehicleId,
                vehicleRegNo,
                request.Bound,
                request.ScaleTestId,
                request.DriverId,
                request.TransporterId,
                request.WeighingType ?? "static");

            var kenhaTagTask = CheckKeNHATagAsync(vehicleRegNo);
            var localTagsTask = _vehicleTagService.CheckVehicleTagsAsync(vehicleRegNo);

            await Task.WhenAll(weighingTask, kenhaTagTask, localTagsTask);

            var transaction = weighingTask.Result;
            var dto = MapToDto(transaction);
            dto.KeNHATagAlert = kenhaTagTask.Result;
            dto.OpenTags = localTagsTask.Result;

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

            // Update only provided fields
            if (!string.IsNullOrEmpty(request.VehicleRegNumber))
                transaction.VehicleRegNumber = request.VehicleRegNumber;
            
            if (request.DriverId.HasValue)
                transaction.DriverId = request.DriverId;
            
            if (request.TransporterId.HasValue)
                transaction.TransporterId = request.TransporterId;

            await _weighingService.UpdateTransactionAsync(transaction);

            var dto = MapToDto(transaction);
            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Weighing transaction {id} not found");
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

            if (transaction.ControlStatus != "Pending")
            {
                return BadRequest($"Cannot delete weighing in status '{transaction.ControlStatus}'. Only Pending transactions can be deleted.");
            }

            await _weighingService.DeleteTransactionAsync(id);
            return NoContent();
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
            var effectiveStationId = stationId ?? _tenantContext.StationId;
            var from = dateFrom.HasValue ? DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
            var to = dateTo.HasValue ? DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc) : DateTime.UtcNow;

            var (items, _) = await _weighingService.SearchTransactionsLightAsync(
                stationId: effectiveStationId,
                fromDate: from,
                toDate: to,
                take: 10000);

            var totalWeighings = items.Count;
            var legalCount = items.Count(t => string.Equals(t.ControlStatus, "LEGAL", StringComparison.OrdinalIgnoreCase));
            var overloadedCount = items.Count(t => string.Equals(t.ControlStatus, "OVERLOAD", StringComparison.OrdinalIgnoreCase));
            var warningCount = items.Count(t => string.Equals(t.ControlStatus, "WARNING", StringComparison.OrdinalIgnoreCase));
            var complianceRate = totalWeighings > 0 ? Math.Round((decimal)legalCount / totalWeighings * 100, 1) : 0;
            var totalFeesKes = items.Sum(t => t.TotalFeeUsd);
            var overloadedItems = items.Where(t => t.OverloadKg > 0).ToList();
            var avgOverloadKg = overloadedItems.Count > 0 ? Math.Round((decimal)overloadedItems.Average(t => t.OverloadKg), 0) : 0;

            return Ok(new WeighingStatisticsDto
            {
                TotalWeighings = totalWeighings,
                LegalCount = legalCount,
                OverloadedCount = overloadedCount,
                WarningCount = warningCount,
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
            var effectiveStationId = stationId ?? _tenantContext.StationId;
            var from = dateFrom.HasValue ? DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
            var to = dateTo.HasValue ? DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc) : DateTime.UtcNow;

            var (items, _) = await _weighingService.SearchTransactionsAsync(
                stationId: effectiveStationId,
                fromDate: from,
                toDate: to,
                take: 10000);

            var trend = items
                .GroupBy(t => t.WeighedAt.Date)
                .OrderBy(g => g.Key)
                .Select(g => new ComplianceTrendDto
                {
                    Name = g.Key.ToString("MMM dd"),
                    Compliant = g.Count(t => t.ControlStatus == "LEGAL"),
                    Overloaded = g.Count(t => t.ControlStatus == "OVERLOAD"),
                    Warning = g.Count(t => t.ControlStatus == "WARNING")
                })
                .ToList();

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
            var effectiveStationId = stationId ?? _tenantContext.StationId;
            var from = dateFrom.HasValue ? DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
            var to = dateTo.HasValue ? DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc) : DateTime.UtcNow;

            var (items, _) = await _weighingService.SearchTransactionsAsync(
                stationId: effectiveStationId,
                fromDate: from,
                toDate: to,
                controlStatus: "OVERLOAD",
                take: 10000);

            var total = items.Count;
            var bands = new List<(string Name, int Min, int Max)>
            {
                ("0-5%", 0, 5),
                ("5-10%", 5, 10),
                ("10-20%", 10, 20),
                ("20-50%", 20, 50),
                (">50%", 50, int.MaxValue)
            };

            var distribution = bands.Select(b =>
            {
                var count = items.Count(t =>
                {
                    if (t.GvwPermissibleKg <= 0) return false;
                    var pct = (decimal)t.OverloadKg / t.GvwPermissibleKg * 100;
                    return pct >= b.Min && (b.Max == int.MaxValue || pct < b.Max);
                });

                return new OverloadDistributionDto
                {
                    Name = b.Name,
                    Count = count,
                    Percentage = total > 0 ? Math.Round((decimal)count / total * 100, 1) : 0
                };
            }).ToList();

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
            var effectiveStationId = stationId ?? _tenantContext.StationId;
            var from = dateFrom.HasValue ? DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
            var to = dateTo.HasValue ? DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc) : DateTime.UtcNow;

            var (items, _) = await _weighingService.SearchTransactionsAsync(
                stationId: effectiveStationId,
                fromDate: from,
                toDate: to,
                take: 10000);

            // Group by axle count as proxy for vehicle type
            var distribution = items
                .GroupBy(t => t.WeighingAxles?.Count ?? 2)
                .Select(g => new
                {
                    Name = $"{g.Key}-Axle",
                    Value = g.Count()
                })
                .OrderBy(d => d.Name)
                .ToList();

            return Ok(distribution);
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
            var effectiveStationId = stationId ?? _tenantContext.StationId;
            var from = dateFrom.HasValue ? DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
            var to = dateTo.HasValue ? DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc) : DateTime.UtcNow;

            var (items, _) = await _weighingService.SearchTransactionsAsync(
                stationId: effectiveStationId,
                fromDate: from,
                toDate: to,
                take: 10000);

            var volume = items
                .GroupBy(t => t.WeighedAt.Date)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Name = g.Key.ToString("MMM dd"),
                    Value = g.Count()
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

    /// <summary>
    /// Gets axle violation distribution.
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
            var effectiveStationId = stationId ?? _tenantContext.StationId;
            var from = dateFrom.HasValue ? DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
            var to = dateTo.HasValue ? DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc) : DateTime.UtcNow;

            var (items, _) = await _weighingService.SearchTransactionsAsync(
                stationId: effectiveStationId,
                fromDate: from,
                toDate: to,
                controlStatus: "OVERLOAD",
                take: 10000);

            // Flatten axles and count violations per axle position
            var axleViolations = items
                .SelectMany(t => t.WeighingAxles ?? new List<WeighingAxle>())
                .Where(a => a.OverloadKg > 0)
                .GroupBy(a => a.AxleNumber)
                .Select(g => new OverloadDistributionDto
                {
                    Name = $"Axle {g.Key}",
                    Count = g.Count(),
                    Percentage = 0 // Will calculate below
                })
                .OrderBy(d => d.Name)
                .ToList();

            var totalViolations = axleViolations.Sum(v => v.Count);
            foreach (var v in axleViolations)
            {
                v.Percentage = totalViolations > 0 ? Math.Round((decimal)v.Count / totalViolations * 100, 1) : 0;
            }

            return Ok(axleViolations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting axle violations");
            return StatusCode(500, "An error occurred while getting axle violations.");
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

        return new WeighingTransactionDto
        {
            Id = transaction.Id,
            TicketNumber = transaction.TicketNumber,
            VehicleId = transaction.VehicleId,
            VehicleRegNumber = transaction.VehicleRegNumber,
            DriverId = transaction.DriverId,
            TransporterId = transaction.TransporterId,
            StationId = transaction.StationId,
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

            // Vehicle details
            VehicleMake = transaction.Vehicle?.Make,
            VehicleModel = transaction.Vehicle?.Model,
            VehicleType = transaction.Vehicle?.VehicleType,
            AxleConfiguration = transaction.Vehicle?.AxleConfiguration?.AxleCode,
            IsMultiDeck = isMultiDeck,

            // People
            DriverName = transaction.Driver?.FullNames,
            TransporterName = transaction.Transporter?.Name,
            WeighedByUserName = transaction.WeighedByUser?.FullName,

            // Station
            StationName = transaction.Station?.Name,
            StationCode = transaction.Station?.Code,

            // Timing
            TimeTakenSeconds = (int)(transaction.WeighedAt - transaction.CreatedAt).TotalSeconds,

            // Deck weights from axle groupings (A/B/C/D) — only for static/multideck
            DeckAWeightKg = isMultiDeck ? NullIfZero(axles.Where(a => a.AxleGrouping == "A").Sum(a => a.MeasuredWeightKg)) : null,
            DeckBWeightKg = isMultiDeck ? NullIfZero(axles.Where(a => a.AxleGrouping == "B").Sum(a => a.MeasuredWeightKg)) : null,
            DeckCWeightKg = isMultiDeck ? NullIfZero(axles.Where(a => a.AxleGrouping == "C").Sum(a => a.MeasuredWeightKg)) : null,
            DeckDWeightKg = isMultiDeck ? NullIfZero(axles.Where(a => a.AxleGrouping == "D").Sum(a => a.MeasuredWeightKg)) : null,

            // Route & Cargo
            SourceLocation = transaction.Origin?.Name,
            DestinationLocation = transaction.Destination?.Name,
            CargoType = transaction.Cargo?.Name,
            CargoDescription = transaction.Cargo?.Category,

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
