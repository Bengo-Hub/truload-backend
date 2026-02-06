using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.DTOs.Analytics;
using TruLoad.Backend.Services.Interfaces.Analytics;
using TruLoad.Backend.Authorization.Attributes;

namespace TruLoad.Backend.Controllers.Analytics;

/// <summary>
/// Controller for Apache Superset analytics integration.
/// </summary>
[ApiController]
[Route("api/v1/analytics")]
[Authorize]
public class SupersetController : ControllerBase
{
    private readonly ISupersetService _supersetService;
    private readonly ILogger<SupersetController> _logger;

    public SupersetController(
        ISupersetService supersetService,
        ILogger<SupersetController> logger)
    {
        _supersetService = supersetService;
        _logger = logger;
    }

    /// <summary>
    /// Get a guest token for embedding Superset dashboards.
    /// </summary>
    [HttpPost("superset/guest-token")]
    [HasPermission("analytics.superset")]
    [ProducesResponseType(typeof(SupersetGuestTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SupersetGuestTokenResponse>> GetGuestToken(
        [FromBody] SupersetGuestTokenRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _supersetService.GetGuestTokenAsync(request, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Superset guest token");
            return BadRequest(new { message = "Failed to get guest token from Superset" });
        }
    }

    /// <summary>
    /// List available Superset dashboards.
    /// </summary>
    [HttpGet("superset/dashboards")]
    [HasPermission("analytics.superset")]
    [ProducesResponseType(typeof(List<SupersetDashboardDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SupersetDashboardDto>>> GetDashboards(CancellationToken ct)
    {
        try
        {
            var dashboards = await _supersetService.GetDashboardsAsync(ct);
            return Ok(dashboards);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Superset dashboards");
            return Ok(new List<SupersetDashboardDto>());
        }
    }

    /// <summary>
    /// Get a specific Superset dashboard.
    /// </summary>
    [HttpGet("superset/dashboards/{id}")]
    [HasPermission("analytics.superset")]
    [ProducesResponseType(typeof(SupersetDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupersetDashboardDto>> GetDashboard(int id, CancellationToken ct)
    {
        try
        {
            var dashboard = await _supersetService.GetDashboardAsync(id, ct);
            if (dashboard == null)
            {
                return NotFound(new { message = "Dashboard not found" });
            }
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Superset dashboard {DashboardId}", id);
            return NotFound(new { message = "Dashboard not found" });
        }
    }

    /// <summary>
    /// Execute a natural language query using AI-powered text-to-SQL.
    /// </summary>
    [HttpPost("query")]
    [HasPermission("analytics.custom_query")]
    [ProducesResponseType(typeof(NaturalLanguageQueryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NaturalLanguageQueryResponse>> ExecuteNaturalLanguageQuery(
        [FromBody] NaturalLanguageQueryRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _supersetService.ExecuteNaturalLanguageQueryAsync(request, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing natural language query");
            return BadRequest(new { message = "Failed to execute query" });
        }
    }
}
