using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using TruLoad.Backend.DTOs.Portal;
using TruLoad.Backend.Services.Interfaces.Portal;
using TruLoad.Backend.Services.Interfaces.Infrastructure;

namespace TruLoad.Backend.Controllers.Portal;

[ApiController]
[Route("api/v1/portal")]
[Authorize]
[EnableRateLimiting("weighing")]
public class TransporterPortalController : ControllerBase
{
    private readonly ITransporterPortalService _portalService;
    private readonly IPdfService _pdfService;
    private readonly ILogger<TransporterPortalController> _logger;

    public TransporterPortalController(
        ITransporterPortalService portalService,
        IPdfService pdfService,
        ILogger<TransporterPortalController> logger)
    {
        _portalService = portalService;
        _pdfService = pdfService;
        _logger = logger;
    }

    /// <summary>
    /// Registers a portal account by linking the authenticated user to an existing transporter.
    /// </summary>
    [HttpPost("register")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(PortalRegistrationResult), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Register([FromBody] PortalRegistrationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();
        if (userId == null)
            return Unauthorized("User ID not found in claims");

        var userEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? request.Email;

        try
        {
            var result = await _portalService.RegisterAsync(userId.Value, userEmail, request);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering portal account");
            return StatusCode(500, "An error occurred while registering the portal account.");
        }
    }

    /// <summary>
    /// Gets paginated weighing history for the transporter across organizations.
    /// </summary>
    [HttpGet("weighings")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(PortalPagedResult<PortalWeighingDto>), 200)]
    public async Task<IActionResult> GetWeighings(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] Guid? vehicleId = null,
        [FromQuery] Guid? organizationId = null)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized("User ID not found in claims");

        try
        {
            var result = await _portalService.GetWeighingsAsync(
                userId.Value, page, pageSize, fromDate, toDate, vehicleId, organizationId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting portal weighings");
            return StatusCode(500, "An error occurred while retrieving weighing data.");
        }
    }

    /// <summary>
    /// Gets a single weighing detail.
    /// </summary>
    [HttpGet("weighings/{id}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(PortalWeighingDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetWeighingDetail(Guid id)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized("User ID not found in claims");

        try
        {
            var result = await _portalService.GetWeighingDetailAsync(userId.Value, id);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Weighing transaction {id} not found");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting portal weighing detail {WeighingId}", id);
            return StatusCode(500, "An error occurred while retrieving the weighing detail.");
        }
    }

    /// <summary>
    /// Gets the list of vehicles for the transporter.
    /// </summary>
    [HttpGet("vehicles")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<PortalVehicleDto>), 200)]
    public async Task<IActionResult> GetVehicles()
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized("User ID not found in claims");

        try
        {
            var result = await _portalService.GetVehiclesAsync(userId.Value);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting portal vehicles");
            return StatusCode(500, "An error occurred while retrieving vehicles.");
        }
    }

    /// <summary>
    /// Gets weight trend data for a specific vehicle.
    /// </summary>
    [HttpGet("vehicles/{vehicleId}/weight-trends")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<PortalVehicleWeightTrendDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetVehicleWeightTrends(Guid vehicleId)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized("User ID not found in claims");

        try
        {
            var result = await _portalService.GetVehicleWeightTrendsAsync(userId.Value, vehicleId);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Vehicle {vehicleId} not found");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vehicle weight trends {VehicleId}", vehicleId);
            return StatusCode(500, "An error occurred while retrieving vehicle weight trends.");
        }
    }

    /// <summary>
    /// Gets the list of drivers for the transporter.
    /// </summary>
    [HttpGet("drivers")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<PortalDriverDto>), 200)]
    public async Task<IActionResult> GetDrivers()
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized("User ID not found in claims");

        try
        {
            var result = await _portalService.GetDriversAsync(userId.Value);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting portal drivers");
            return StatusCode(500, "An error occurred while retrieving drivers.");
        }
    }

    /// <summary>
    /// Gets performance metrics for a specific driver.
    /// </summary>
    [HttpGet("drivers/{driverId}/performance")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(PortalDriverPerformanceDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetDriverPerformance(Guid driverId)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized("User ID not found in claims");

        try
        {
            var result = await _portalService.GetDriverPerformanceAsync(userId.Value, driverId);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Driver {driverId} not found");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting driver performance {DriverId}", driverId);
            return StatusCode(500, "An error occurred while retrieving driver performance.");
        }
    }

    /// <summary>
    /// Gets consignment tracking data for the transporter.
    /// </summary>
    [HttpGet("consignments")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(PortalPagedResult<PortalConsignmentDto>), 200)]
    public async Task<IActionResult> GetConsignments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized("User ID not found in claims");

        try
        {
            var result = await _portalService.GetConsignmentsAsync(
                userId.Value, page, pageSize, fromDate, toDate);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting portal consignments");
            return StatusCode(500, "An error occurred while retrieving consignment data.");
        }
    }

    /// <summary>
    /// Gets current subscription status and feature access flags.
    /// </summary>
    [HttpGet("subscription")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(PortalSubscriptionDto), 200)]
    public async Task<IActionResult> GetSubscription()
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized("User ID not found in claims");

        try
        {
            var result = await _portalService.GetFeatureAccessAsync(userId.Value);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting portal subscription");
            return StatusCode(500, "An error occurred while retrieving subscription data.");
        }
    }

    // ── Private helpers ──

    private Guid? GetUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return null;
        return userId;
    }
}
