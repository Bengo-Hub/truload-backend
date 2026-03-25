using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.DTOs.Yard;
using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Services.Interfaces.Yard;

namespace TruLoad.Backend.Controllers.Yard;

[ApiController]
[Route("api/v1/yard-entries")]
[Authorize]
public class YardController : ControllerBase
{
    private readonly IYardService _yardService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<YardController> _logger;

    public YardController(
        IYardService yardService,
        ITenantContext tenantContext,
        ILogger<YardController> logger)
    {
        _yardService = yardService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Search yard entries with filters and pagination.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Permission:yard.read")]
    [ProducesResponseType(typeof(PagedResponse<YardEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] SearchYardEntriesRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var isHqOrAdmin = User.FindFirst("is_hq_user")?.Value == "true" || User.IsInRole("Superuser") || User.IsInRole("System Admin");
            var effectiveStationId = isHqOrAdmin ? null : _tenantContext.StationId;

            var result = await _yardService.SearchAsync(request, effectiveStationId, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching yard entries");
            return StatusCode(500, "An error occurred while searching yard entries.");
        }
    }
    /// Get a yard entry by ID.
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:yard.read")]
    [ProducesResponseType(typeof(YardEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var entry = await _yardService.GetByIdAsync(id, ct);

        if (entry == null)
            return NotFound($"Yard entry {id} not found");

        return Ok(entry);
    }

    /// <summary>
    /// Get yard entry by weighing transaction ID.
    /// </summary>
    [HttpGet("by-weighing/{weighingId}")]
    [Authorize(Policy = "Permission:yard.read")]
    [ProducesResponseType(typeof(YardEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByWeighingId(Guid weighingId, CancellationToken ct)
    {
        var entry = await _yardService.GetByWeighingIdAsync(weighingId, ct);

        if (entry == null)
            return NotFound($"Yard entry for weighing {weighingId} not found");

        return Ok(entry);
    }

    /// <summary>
    /// Get yard statistics.
    /// </summary>
    [HttpGet("statistics")]
    [Authorize(Policy = "Permission:yard.read")]
    [ProducesResponseType(typeof(YardStatisticsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? stationId,
        CancellationToken ct)
    {
        try
        {
            var isHqOrAdmin = User.FindFirst("is_hq_user")?.Value == "true" || User.IsInRole("Superuser") || User.IsInRole("System Admin");
            var effectiveStationId = (stationId == null && isHqOrAdmin) ? null : (stationId ?? _tenantContext.StationId);
            var stats = await _yardService.GetStatisticsAsync(effectiveStationId, dateFrom, dateTo, ct);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting yard statistics");
            return StatusCode(500, "An error occurred while getting yard statistics.");
        }
    }
    /// Get yard processing time trend for charts
    /// </summary>
    [HttpGet("processing-trend")]
    [Authorize(Policy = "Permission:yard.read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProcessingTrend(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken ct)
    {
        var from = dateFrom.HasValue ? DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
        var to = dateTo.HasValue ? DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc) : DateTime.UtcNow;

        var request = new SearchYardEntriesRequest
        {
            FromDate = from,
            ToDate = to,
            PageSize = 10000
        };
        
        var isHqOrAdmin = User.FindFirst("is_hq_user")?.Value == "true" || User.IsInRole("Superuser") || User.IsInRole("System Admin");
        var effectiveStationId = isHqOrAdmin ? null : _tenantContext.StationId;

        var result = await _yardService.SearchAsync(request, effectiveStationId, ct);

        var trend = result.Items
            .Where(e => e.EnteredAt != default)
            .GroupBy(e => e.EnteredAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Name = g.Key.ToString("MMM dd"),
                AvgHours = g.Where(e => e.ReleasedAt.HasValue)
                    .Select(e => (e.ReleasedAt!.Value - e.EnteredAt).TotalHours)
                    .DefaultIfEmpty(0)
                    .Average()
            })
            .ToList();

        return Ok(trend);
    }

    /// <summary>
    /// Create a new yard entry.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Permission:yard.create")]
    [ProducesResponseType(typeof(YardEntryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateYardEntryRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        try
        {
            var entry = await _yardService.CreateAsync(request, userId, ct);
            return CreatedAtAction(nameof(GetById), new { id = entry.Id }, entry);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { message = "A yard entry already exists for this weighing transaction. Vehicle has already been sent to yard." });
        }
    }

    /// <summary>
    /// Release a vehicle from the yard.
    /// </summary>
    [HttpPut("{id}/release")]
    [Authorize(Policy = "Permission:yard.release")]
    [ProducesResponseType(typeof(YardEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Release(Guid id, [FromBody] ReleaseYardEntryRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var entry = await _yardService.ReleaseAsync(id, request, userId, ct);
        return Ok(entry);
    }

    /// <summary>
    /// Update yard entry status.
    /// </summary>
    [HttpPut("{id}/status")]
    [Authorize(Policy = "Permission:yard.update")]
    [ProducesResponseType(typeof(YardEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] string status, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var entry = await _yardService.UpdateStatusAsync(id, status, userId, ct);
        return Ok(entry);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(global::System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
