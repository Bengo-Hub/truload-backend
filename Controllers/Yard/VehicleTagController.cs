using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.DTOs.Yard;
using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Services.Interfaces.Yard;

namespace TruLoad.Backend.Controllers.Yard;

[ApiController]
[Route("api/v1/vehicle-tags")]
[Authorize]
public class VehicleTagController : ControllerBase
{
    private readonly IVehicleTagService _vehicleTagService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<VehicleTagController> _logger;

    public VehicleTagController(
        IVehicleTagService vehicleTagService,
        ITenantContext tenantContext,
        ILogger<VehicleTagController> logger)
    {
        _vehicleTagService = vehicleTagService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Search vehicle tags with filters and pagination.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Permission:tag.read")]
    [ProducesResponseType(typeof(PagedResponse<VehicleTagDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] SearchVehicleTagsRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _vehicleTagService.SearchAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a vehicle tag by ID.
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:tag.read")]
    [ProducesResponseType(typeof(VehicleTagDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var tag = await _vehicleTagService.GetByIdAsync(id, ct);

        if (tag == null)
            return NotFound($"Vehicle tag {id} not found");

        return Ok(tag);
    }

    /// <summary>
    /// Check if a vehicle has any open tags.
    /// </summary>
    [HttpGet("check/{regNo}")]
    [Authorize(Policy = "Permission:tag.read")]
    [ProducesResponseType(typeof(List<VehicleTagDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckVehicle(string regNo, CancellationToken ct)
    {
        var tags = await _vehicleTagService.CheckVehicleTagsAsync(regNo, ct);
        return Ok(tags);
    }

    /// <summary>
    /// Get vehicle tag statistics.
    /// </summary>
    [HttpGet("statistics")]
    [Authorize(Policy = "Permission:tag.read")]
    [ProducesResponseType(typeof(VehicleTagStatisticsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? stationId,
        CancellationToken ct)
    {
        var hasGlobalRead = User.HasClaim(c => c.Type == "Permission" && c.Value == "tag.read");
        var effectiveStationId = (stationId == null && hasGlobalRead) ? null : (stationId ?? _tenantContext.StationId);
        var stats = await _vehicleTagService.GetStatisticsAsync(dateFrom, dateTo, effectiveStationId, ct);
        return Ok(stats);
    }

    /// <summary>
    /// Get vehicle tag trend over time for charts
    /// </summary>
    [HttpGet("trend")]
    [Authorize(Policy = "Permission:tag.read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTrend(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken ct)
    {
        var from = dateFrom.HasValue ? DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
        var to = dateTo.HasValue ? DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc) : DateTime.UtcNow;

        var request = new SearchVehicleTagsRequest
        {
            FromDate = from,
            ToDate = to,
            PageSize = 10000
        };
        var result = await _vehicleTagService.SearchAsync(request, ct);

        var trend = result.Items
            .GroupBy(t => t.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Name = g.Key.ToString("MMM dd"),
                Created = g.Count(),
                Closed = g.Count(t => t.Status == "Closed")
            })
            .ToList();

        return Ok(trend);
    }

    /// <summary>
    /// Get vehicle tags grouped by category for charts
    /// </summary>
    [HttpGet("by-category")]
    [Authorize(Policy = "Permission:tag.read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByCategory(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken ct)
    {
        var from = dateFrom.HasValue ? DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
        var to = dateTo.HasValue ? DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc) : DateTime.UtcNow;

        var request = new SearchVehicleTagsRequest
        {
            FromDate = from,
            ToDate = to,
            PageSize = 10000
        };
        var result = await _vehicleTagService.SearchAsync(request, ct);

        var byCategory = result.Items
            .GroupBy(t => t.TagCategoryName ?? "Uncategorized")
            .Select(g => new { Name = g.Key, Value = g.Count() })
            .ToList();

        return Ok(byCategory);
    }

    /// <summary>
    /// Create a new vehicle tag.
    /// Manual tags are automatically linked to case register for violation tracking.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Permission:tag.create")]
    [ProducesResponseType(typeof(VehicleTagDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateVehicleTagRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized("User ID not found in claims");

        var tag = await _vehicleTagService.CreateAsync(request, userId, ct);

        return CreatedAtAction(nameof(GetById), new { id = tag.Id }, tag);
    }

    /// <summary>
    /// Close a vehicle tag.
    /// </summary>
    [HttpPut("{id}/close")]
    [Authorize(Policy = "Permission:tag.update")]
    [ProducesResponseType(typeof(VehicleTagDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseVehicleTagRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized("User ID not found in claims");

        var tag = await _vehicleTagService.CloseAsync(id, request, userId, ct);
        return Ok(tag);
    }

    /// <summary>
    /// Get all tag categories.
    /// </summary>
    [HttpGet("categories")]
    [Authorize(Policy = "Permission:tag.read")]
    [ProducesResponseType(typeof(List<TagCategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var categories = await _vehicleTagService.GetCategoriesAsync(ct);
        return Ok(categories);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}
