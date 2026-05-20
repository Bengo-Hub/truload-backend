using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Text;
using TruLoad.Backend.DTOs.Portal;
using TruLoad.Backend.Services.Interfaces.Portal;

namespace TruLoad.Backend.Controllers.Portal;

[ApiController]
[Route("api/v1/portal")]
[Authorize]
[EnableRateLimiting("weighing")]
public class TransporterPortalController : ControllerBase
{
    private readonly ITransporterPortalService _portalService;
    private readonly ILogger<TransporterPortalController> _logger;

    public TransporterPortalController(
        ITransporterPortalService portalService,
        ILogger<TransporterPortalController> logger)
    {
        _portalService = portalService;
        _logger = logger;
    }

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
        if (userId == null) return Unauthorized("User ID not found in claims");

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

    [HttpGet("weighings/{id}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(PortalWeighingDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetWeighingDetail(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized("User ID not found in claims");

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
    /// Downloads the weight ticket PDF for a specific weighing.
    /// </summary>
    [HttpGet("weighings/{id}/pdf")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DownloadTicketPdf(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized("User ID not found in claims");

        try
        {
            var (pdfBytes, fileName) = await _portalService.DownloadWeighingPdfAsync(userId.Value, id);
            return File(pdfBytes, "application/pdf", fileName);
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
            _logger.LogError(ex, "Error generating PDF for portal weighing {WeighingId}", id);
            return StatusCode(500, "An error occurred while generating the ticket PDF.");
        }
    }

    [HttpGet("vehicles")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<PortalVehicleDto>), 200)]
    public async Task<IActionResult> GetVehicles()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized("User ID not found in claims");

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

    [HttpGet("vehicles/{vehicleId}/weight-trends")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<PortalVehicleWeightTrendDto>), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetVehicleWeightTrends(Guid vehicleId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized("User ID not found in claims");

        try
        {
            var result = await _portalService.GetVehicleWeightTrendsAsync(userId.Value, vehicleId);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { code = "feature_locked", message = ex.Message });
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

    [HttpGet("drivers")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<PortalDriverDto>), 200)]
    public async Task<IActionResult> GetDrivers()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized("User ID not found in claims");

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

    [HttpGet("drivers/{driverId}/performance")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(PortalDriverPerformanceDto), 200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetDriverPerformance(Guid driverId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized("User ID not found in claims");

        try
        {
            var result = await _portalService.GetDriverPerformanceAsync(userId.Value, driverId);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { code = "feature_locked", message = ex.Message });
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

    [HttpGet("consignments")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(PortalPagedResult<PortalConsignmentDto>), 200)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> GetConsignments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized("User ID not found in claims");

        try
        {
            var result = await _portalService.GetConsignmentsAsync(
                userId.Value, page, pageSize, fromDate, toDate);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { code = "feature_locked", message = ex.Message });
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
        if (userId == null) return Unauthorized("User ID not found in claims");

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

    /// <summary>
    /// Downloads a portal data report (CSV or PDF) based on report type.
    /// Gated by subscription tier.
    /// </summary>
    [HttpGet("reports/{reportId}")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> DownloadReport(
        string reportId,
        [FromQuery] string format = "csv")
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized("User ID not found in claims");

        try
        {
            var limits = await _portalService.GetFeatureAccessAsync(userId.Value);
            var tier = limits.Tier;
            var tierOrder = new Dictionary<string, int> { ["basic"] = 0, ["standard"] = 1, ["premium"] = 2 };
            var currentOrder = tierOrder.GetValueOrDefault(tier, 0);

            // Check tier requirements per report
            var reportTierRequired = reportId switch
            {
                "weighing-summary" or "vehicle-weight-history" => 0,
                "driver-trips" or "cargo-analysis" => 1,
                "fleet-utilization" or "weight-discrepancy" => 2,
                _ => 99
            };

            if (reportTierRequired == 99)
                return NotFound($"Report '{reportId}' not found.");

            if (currentOrder < reportTierRequired)
            {
                var required = reportTierRequired == 1 ? "Standard" : "Premium";
                return StatusCode(403, new
                {
                    code = "feature_locked",
                    message = $"This report requires a {required} subscription."
                });
            }

            // Generate CSV data
            var csvBytes = await BuildReportCsvAsync(userId.Value, reportId, limits.HistoryMonths);
            var fileName = $"{reportId}-{DateTime.UtcNow:yyyyMMdd}.csv";
            return File(csvBytes, "text/csv", fileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating portal report {ReportId}", reportId);
            return StatusCode(500, "An error occurred while generating the report.");
        }
    }

    // ── Private helpers ──

    private async Task<byte[]> BuildReportCsvAsync(Guid userId, string reportId, int historyMonths)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-historyMonths);

        var sb = new StringBuilder();

        switch (reportId)
        {
            case "weighing-summary":
            {
                var result = await _portalService.GetWeighingsAsync(userId, 1, 1000, cutoff, null, null, null);
                sb.AppendLine("Ticket,Date,Vehicle,Organization,Station,Tare(kg),Gross(kg),Net(kg),Cargo,Status");
                foreach (var w in result.Items)
                    sb.AppendLine($"\"{w.TicketNumber}\",\"{w.WeighedAt:yyyy-MM-dd HH:mm}\",\"{w.VehicleRegNumber}\",\"{w.OrganizationName}\",\"{w.StationName}\",{w.TareWeightKg},{w.GrossWeightKg},{w.NetWeightKg},\"{w.CargoType}\",\"{w.ControlStatus}\"");
                break;
            }
            case "vehicle-weight-history":
            {
                var result = await _portalService.GetWeighingsAsync(userId, 1, 2000, cutoff, null, null, null);
                sb.AppendLine("Vehicle,Date,Tare(kg),Gross(kg),Net(kg),Station,Ticket");
                foreach (var w in result.Items)
                    sb.AppendLine($"\"{w.VehicleRegNumber}\",\"{w.WeighedAt:yyyy-MM-dd HH:mm}\",{w.TareWeightKg},{w.GrossWeightKg},{w.NetWeightKg},\"{w.StationName}\",\"{w.TicketNumber}\"");
                break;
            }
            case "driver-trips":
            {
                var drivers = await _portalService.GetDriversAsync(userId);
                sb.AppendLine("Driver,LicenseNo,TotalTrips");
                foreach (var d in drivers)
                    sb.AppendLine($"\"{d.FullName}\",\"{d.DrivingLicenseNo}\",{d.TotalTrips}");
                break;
            }
            case "cargo-analysis":
            {
                var result = await _portalService.GetWeighingsAsync(userId, 1, 2000, cutoff, null, null, null);
                var grouped = result.Items
                    .GroupBy(w => w.CargoType ?? "Unknown")
                    .Select(g => new { Cargo = g.Key, Count = g.Count(), TotalNet = g.Sum(w => w.NetWeightKg ?? 0) });
                sb.AppendLine("CargoType,WeighingCount,TotalNetKg");
                foreach (var g in grouped)
                    sb.AppendLine($"\"{g.Cargo}\",{g.Count},{g.TotalNet}");
                break;
            }
            default:
                sb.AppendLine("Report,GeneratedAt");
                sb.AppendLine($"\"{reportId}\",\"{DateTime.UtcNow:yyyy-MM-dd HH:mm}\"");
                break;
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private Guid? GetUserId()
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return null;
        return userId;
    }
}
