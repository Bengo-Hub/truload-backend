using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TruLoad.Backend.DTOs.Analytics;
using TruLoad.Backend.Hubs;
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
    private readonly IHubContext<AnalyticsHub> _analyticsHub;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SupersetController> _logger;

    public SupersetController(
        ISupersetService supersetService,
        IHubContext<AnalyticsHub> analyticsHub,
        IServiceScopeFactory scopeFactory,
        ILogger<SupersetController> logger)
    {
        _supersetService = supersetService;
        _analyticsHub = analyticsHub;
        _scopeFactory = scopeFactory;
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
    /// Execute a natural language query using AI-powered text-to-SQL (synchronous).
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

    /// <summary>
    /// Submit a natural language query for async processing via SignalR.
    /// Returns a job ID immediately; results are pushed to the client's SignalR connection.
    /// </summary>
    [HttpPost("query/async")]
    [HasPermission("analytics.custom_query")]
    [ProducesResponseType(typeof(AsyncQueryAcceptedResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<AsyncQueryAcceptedResponse> SubmitAsyncQuery(
        [FromBody] AsyncNaturalLanguageQueryRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Validate that the SignalR connection is active before dispatching work
        if (!AnalyticsHub.IsConnected(request.ConnectionId))
        {
            return BadRequest(new { message = "Invalid or disconnected SignalR connection. Please reconnect and try again." });
        }

        var jobId = Guid.NewGuid().ToString("N");

        _logger.LogInformation("Async query submitted: JobId={JobId}, Question={Question}, ConnectionId={ConnectionId}",
            jobId, request.Question, request.ConnectionId);

        // Capture values needed by the background task (avoid capturing scoped controller services)
        var scopeFactory = _scopeFactory;
        var hubContext = _analyticsHub;
        var connectionId = request.ConnectionId;
        var question = request.Question;
        var schemaContext = request.SchemaContext;

        _ = Task.Run(async () =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            // Create a new DI scope — the controller's scope is tied to the HTTP request
            // and will be disposed after the 202 response is sent.
            await using var scope = scopeFactory.CreateAsyncScope();
            var supersetService = scope.ServiceProvider.GetRequiredService<ISupersetService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SupersetController>>();

            try
            {
                var nlqRequest = new NaturalLanguageQueryRequest
                {
                    Question = question,
                    SchemaContext = schemaContext,
                };

                var result = await supersetService.ExecuteNaturalLanguageQueryAsync(nlqRequest, cts.Token);

                await hubContext.Clients.Client(connectionId)
                    .SendAsync("QueryResult", new { jobId, result }, cts.Token);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Async query timed out: JobId={JobId}", jobId);

                try
                {
                    var timeoutResult = new NaturalLanguageQueryResponse(
                        question, "", null, "Query timed out after 2 minutes. Try a simpler question.", false);

                    await hubContext.Clients.Client(connectionId)
                        .SendAsync("QueryResult", new { jobId, result = timeoutResult });
                }
                catch { /* Client may have disconnected */ }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Async query failed: JobId={JobId}", jobId);

                try
                {
                    var errorResult = new NaturalLanguageQueryResponse(
                        question, "", null, ex.Message, false);

                    await hubContext.Clients.Client(connectionId)
                        .SendAsync("QueryResult", new { jobId, result = errorResult });
                }
                catch { /* Client may have disconnected */ }
            }
        });

        return Accepted(new AsyncQueryAcceptedResponse(jobId));
    }
}
