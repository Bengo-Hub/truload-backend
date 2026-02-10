using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.Integration;
using TruLoad.Backend.Services.Interfaces.Integration;

namespace TruLoad.Backend.Controllers.Integration;

/// <summary>
/// Endpoints for external API integrations (NTSA vehicle search, KeNHA tags).
/// Used by case register workflows to pull vehicle owner/details from NTSA,
/// and by weighing workflows to verify KeNHA tags.
/// </summary>
[ApiController]
[Route("api/v1/integration")]
[Authorize]
public class ExternalIntegrationController : ControllerBase
{
    private readonly INTSAService _ntsaService;
    private readonly IKeNHAService _kenhaService;
    private readonly ILogger<ExternalIntegrationController> _logger;

    public ExternalIntegrationController(
        INTSAService ntsaService,
        IKeNHAService kenhaService,
        ILogger<ExternalIntegrationController> logger)
    {
        _ntsaService = ntsaService;
        _kenhaService = kenhaService;
        _logger = logger;
    }

    /// <summary>
    /// Search for vehicle details from NTSA by registration number.
    /// Returns owner info, vehicle details, inspection status, and caveat info.
    /// Results cached in Redis (24 hours) to reduce API calls.
    /// Integrated into case register workflows.
    /// </summary>
    [HttpGet("ntsa/vehicle/{regNo}")]
    [HasPermission("case.read")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(NTSAVehicleSearchResult), 200)]
    [ProducesResponseType(204)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> SearchNTSAVehicle(string regNo, CancellationToken ct)
    {
        if (!await _ntsaService.IsAvailableAsync(ct))
        {
            return StatusCode(503, new { message = "NTSA integration is not configured or inactive" });
        }

        var result = await _ntsaService.SearchVehicleAsync(regNo, ct);
        if (result == null || !result.Found)
        {
            return NoContent();
        }

        return Ok(result);
    }

    /// <summary>
    /// Verify if a vehicle has an existing KeNHA tag by registration number.
    /// Used by weighing capture screen to check for existing prohibitions.
    /// Only available when KeNHA integration is configured and active.
    /// </summary>
    [HttpGet("kenha/tag/{regNo}")]
    [HasPermission("weighing.read")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(KeNHATagVerificationResult), 200)]
    [ProducesResponseType(204)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> VerifyKeNHATag(string regNo, CancellationToken ct)
    {
        if (!await _kenhaService.IsAvailableAsync(ct))
        {
            return StatusCode(503, new { message = "KeNHA integration is not configured or inactive" });
        }

        var result = await _kenhaService.VerifyVehicleTagAsync(regNo, ct);
        if (result == null || !result.HasTag)
        {
            return NoContent();
        }

        return Ok(result);
    }

    /// <summary>
    /// Health check for all external integrations.
    /// Returns connectivity status for NTSA and KeNHA.
    /// </summary>
    [HttpGet("health")]
    [HasPermission("config.read")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(IntegrationHealthSummary), 200)]
    public async Task<IActionResult> HealthCheck(CancellationToken ct)
    {
        var ntsaHealth = await _ntsaService.TestConnectivityAsync(ct);
        var kenhaHealth = await _kenhaService.TestConnectivityAsync(ct);

        return Ok(new IntegrationHealthSummary
        {
            Integrations = new List<IntegrationHealthResult> { ntsaHealth, kenhaHealth },
            CheckedAt = DateTime.UtcNow
        });
    }
}

/// <summary>
/// Summary of all integration health checks.
/// </summary>
public class IntegrationHealthSummary
{
    public List<IntegrationHealthResult> Integrations { get; set; } = new();
    public DateTime CheckedAt { get; set; }
}
