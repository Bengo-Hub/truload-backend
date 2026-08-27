using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.DTOs.Weighing;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Services.Interfaces.Authorization;
using TruLoad.Backend.Services.Interfaces.Infrastructure;
using TruLoad.Backend.Services.Interfaces.Subscription;
using TruLoad.Backend.Services.Interfaces.Weighing;

namespace TruLoad.Backend.Controllers.WeighingOperations;

[ApiController]
[Route("api/v1/commercial-weighing")]
[Authorize]
[EnableRateLimiting("weighing")]
public class CommercialWeighingController : ControllerBase
{
    private readonly ICommercialWeighingService _commercialWeighingService;
    private readonly IPdfService _pdfService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ITenantContext _tenantContext;
    private readonly TruLoadDbContext _db;
    private readonly IPermissionVerificationService _permissionVerificationService;
    private readonly ILogger<CommercialWeighingController> _logger;

    public CommercialWeighingController(
        ICommercialWeighingService commercialWeighingService,
        IPdfService pdfService,
        ISubscriptionService subscriptionService,
        ITenantContext tenantContext,
        TruLoadDbContext db,
        IPermissionVerificationService permissionVerificationService,
        ILogger<CommercialWeighingController> logger)
    {
        _commercialWeighingService = commercialWeighingService;
        _pdfService = pdfService;
        _subscriptionService = subscriptionService;
        _tenantContext = tenantContext;
        _db = db;
        _permissionVerificationService = permissionVerificationService;
        _logger = logger;
    }

    /// <summary>
    /// Initiates a new commercial weighing transaction.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Permission:weighing.create")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CommercialWeighingResultDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Initiate([FromBody] InitiateCommercialWeighingRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            return Unauthorized("User ID not found in claims");

        // Subscription guard — fail-open if subscriptions-api is unreachable
        try
        {
            var org = await _db.Organizations
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(o => o.Id == _tenantContext.OrganizationId)
                .Select(o => new { o.SsoTenantSlug, o.TenantType })
                .FirstOrDefaultAsync();

            if (org != null && org.TenantType == "CommercialWeighing" && !string.IsNullOrWhiteSpace(org.SsoTenantSlug))
            {
                var sub = await _subscriptionService.GetTenantSubscriptionAsync(org.SsoTenantSlug);
                if (sub.Status is not ("ACTIVE" or "TRIAL"))
                    return StatusCode(402, new { code = "subscription_inactive", message = "Your subscription is inactive. Please renew to continue weighing.", upgrade = true });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[CommercialWeighing] Subscription check failed for org {OrgId} — proceeding (fail-open)", _tenantContext.OrganizationId);
        }

        try
        {
            var transaction = await _commercialWeighingService.InitiateCommercialWeighingAsync(request, userGuid);
            var result = await _commercialWeighingService.GetCommercialResultAsync(transaction.Id);
            return CreatedAtAction(nameof(GetResult), new { id = transaction.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating commercial weighing");
            return StatusCode(500, "An error occurred while initiating the commercial weighing.");
        }
    }

    /// <summary>
    /// Gets the commercial weighing result for a transaction.
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:weighing.read")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CommercialWeighingResultDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetResult(Guid id)
    {
        try
        {
            var result = await _commercialWeighingService.GetCommercialResultAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Weighing transaction {id} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving commercial weighing result {TransactionId}", id);
            return StatusCode(500, "An error occurred while retrieving the commercial weighing result.");
        }
    }

    /// <summary>
    /// Captures the first weight (first pass on the scale).
    /// </summary>
    [HttpPost("{id}/first-weight")]
    [Authorize(Policy = "Permission:weighing.create")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CommercialWeighingResultDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> CaptureFirstWeight(Guid id, [FromBody] CaptureFirstWeightRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (request.IsManualEntry && !await HasManualWeightOverridePermissionAsync())
            return Forbid();

        try
        {
            await _commercialWeighingService.CaptureFirstWeightAsync(id, request);
            var result = await _commercialWeighingService.GetCommercialResultAsync(id);
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
            _logger.LogError(ex, "Error capturing first weight for transaction {TransactionId}", id);
            return StatusCode(500, "An error occurred while capturing the first weight.");
        }
    }

    /// <summary>
    /// Captures the second weight (second pass on the scale).
    /// Auto-determines tare/gross and calculates net weight.
    /// </summary>
    [HttpPost("{id}/second-weight")]
    [Authorize(Policy = "Permission:weighing.create")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CommercialWeighingResultDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> CaptureSecondWeight(Guid id, [FromBody] CaptureSecondWeightRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (request.IsManualEntry && !await HasManualWeightOverridePermissionAsync())
            return Forbid();

        try
        {
            await _commercialWeighingService.CaptureSecondWeightAsync(id, request);
            var result = await _commercialWeighingService.GetCommercialResultAsync(id);
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
            _logger.LogError(ex, "Error capturing second weight for transaction {TransactionId}", id);
            return StatusCode(500, "An error occurred while capturing the second weight.");
        }
    }

    /// <summary>
    /// Uses stored/preset tare weight instead of measuring on the scale.
    /// </summary>
    [HttpPost("{id}/use-stored-tare")]
    [Authorize(Policy = "Permission:weighing.create")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CommercialWeighingResultDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UseStoredTare(Guid id, [FromBody] UseStoredTareRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            await _commercialWeighingService.UseStoredTareAsync(id, request);
            var result = await _commercialWeighingService.GetCommercialResultAsync(id);
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
            _logger.LogError(ex, "Error using stored tare for transaction {TransactionId}", id);
            return StatusCode(500, "An error occurred while using stored tare.");
        }
    }

    /// <summary>
    /// Updates quality deduction and recalculates adjusted net weight.
    /// </summary>
    [HttpPut("{id}/quality-deduction")]
    [Authorize(Policy = "Permission:weighing.update")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CommercialWeighingResultDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateQualityDeduction(Guid id, [FromBody] UpdateQualityDeductionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            await _commercialWeighingService.UpdateQualityDeductionAsync(id, request);
            var result = await _commercialWeighingService.GetCommercialResultAsync(id);
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
            _logger.LogError(ex, "Error updating quality deduction for transaction {TransactionId}", id);
            return StatusCode(500, "An error occurred while updating the quality deduction.");
        }
    }

    /// <summary>
    /// Gets tare weight history for a vehicle.
    /// </summary>
    [HttpGet("vehicles/{vehicleId}/tare-history")]
    [Authorize(Policy = "Permission:weighing.read")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<VehicleTareHistoryDto>), 200)]
    public async Task<IActionResult> GetVehicleTareHistory(Guid vehicleId)
    {
        try
        {
            var history = await _commercialWeighingService.GetVehicleTareHistoryAsync(vehicleId);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tare history for vehicle {VehicleId}", vehicleId);
            return StatusCode(500, "An error occurred while getting vehicle tare history.");
        }
    }

    /// <summary>
    /// Records a new tare weight history entry for a vehicle (Tare Register "Record Tare" dialog).
    /// Optionally updates the vehicle's stored tare (SetAsDefault) so it is used by single-pass
    /// commercial weighing going forward.
    /// </summary>
    [HttpPost("tare-history")]
    [Authorize(Policy = "Permission:weighing.create")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(VehicleTareHistoryDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RecordTareHistory([FromBody] RecordTareHistoryRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid? recordedByUserId = Guid.TryParse(userIdClaim, out var userGuid) ? userGuid : null;

        try
        {
            var result = await _commercialWeighingService.RecordTareHistoryEntryAsync(request, recordedByUserId);
            return CreatedAtAction(nameof(GetVehicleTareHistory), new { vehicleId = request.VehicleId }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording tare history entry for vehicle {VehicleId}", request.VehicleId);
            return StatusCode(500, "An error occurred while recording the tare weight.");
        }
    }

    /// <summary>
    /// Generates and returns a PDF weight ticket for a commercial weighing transaction.
    /// </summary>
    [HttpGet("{id}/ticket/pdf")]
    [Authorize(Policy = "Permission:weighing.read")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> GetWeightTicketPdf(Guid id)
    {
        try
        {
            var result = await _commercialWeighingService.GetCommercialResultAsync(id);

            // A transaction that breached tolerance must be approved (or rejected) by a supervisor
            // before the "final" ticket is issued — printing it unapproved would let an unresolved
            // discrepancy leave the station as if it were a clean weighing. The interim ticket
            // (first-pass-only) remains available regardless, since it never carries a final net
            // weight or tolerance verdict.
            if (result.ToleranceExceeded && !result.ToleranceExceptionApproved)
            {
                return StatusCode(409, new
                {
                    code = "tolerance_exception_pending_approval",
                    message = "This transaction exceeded the configured weight tolerance and has not yet been approved by a supervisor. " +
                        "Approve or reject the tolerance exception before printing the final ticket, or use the interim ticket in the meantime."
                });
            }

            var pdfBytes = await _pdfService.GenerateCommercialWeightTicketAsync(result, result.StationId);
            return File(pdfBytes, "application/pdf", $"weight-ticket-{result.TicketNumber}.pdf");
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
    /// Generates an interim weight ticket PDF after the first weight is captured.
    /// Issued between first and second weighing — e.g. while vehicle unloads/loads.
    /// </summary>
    [HttpGet("{id}/interim-ticket/pdf")]
    [Authorize(Policy = "Permission:weighing.read")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetInterimTicketPdf(Guid id)
    {
        try
        {
            var result = await _commercialWeighingService.GetCommercialResultAsync(id);
            if (result.FirstWeightKg == null)
                return BadRequest("First weight has not been captured yet.");
            // Reuse the commercial ticket PDF — partial results render with available weights only
            var pdfBytes = await _pdfService.GenerateCommercialWeightTicketAsync(result, result.StationId);
            return File(pdfBytes, "application/pdf", $"interim-ticket-{result.TicketNumber}.pdf");
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Weighing transaction {id} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating interim ticket PDF for transaction {TransactionId}", id);
            return StatusCode(500, "An error occurred while generating the interim ticket PDF.");
        }
    }

    /// <summary>
    /// Approves a tolerance exception for a transaction where the weight discrepancy
    /// exceeded configured tolerance bands. Requires weighing.override permission.
    /// </summary>
    [HttpPost("{id}/approve-tolerance-exception")]
    [Authorize(Policy = "Permission:weighing.override")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CommercialWeighingResultDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ApproveToleranceException(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userGuid))
            return Unauthorized("User ID not found in claims");

        try
        {
            var result = await _commercialWeighingService.ApproveToleranceExceptionAsync(id, userGuid);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Weighing transaction {id} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving tolerance exception for transaction {TransactionId}", id);
            return StatusCode(500, "An error occurred while approving the tolerance exception.");
        }
    }

    /// <summary>
    /// Rejects a tolerance exception for a transaction where the weight discrepancy
    /// exceeded configured tolerance bands. Reuses the Void state transition, recording
    /// the reason via the existing VoidReason field. Requires weighing.override permission
    /// (same policy as approve-tolerance-exception).
    /// </summary>
    [HttpPost("{id}/reject-tolerance-exception")]
    [Authorize(Policy = "Permission:weighing.override")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CommercialWeighingResultDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RejectToleranceException(Guid id, [FromBody] VoidCommercialWeighingRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userGuid))
            return Unauthorized("User ID not found in claims");

        try
        {
            var result = await _commercialWeighingService.RejectToleranceExceptionAsync(id, request.Reason, userGuid);
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
            _logger.LogError(ex, "Error rejecting tolerance exception for transaction {TransactionId}", id);
            return StatusCode(500, "An error occurred while rejecting the tolerance exception.");
        }
    }

    // ============================================================================
    // Tare Anomaly Detection - Supervisor Resolution (Phase 7 MVP)
    // ============================================================================

    /// <summary>
    /// Approves a flagged tare anomaly as-is. Requires weighing.override permission
    /// (same policy as approve/reject-tolerance-exception).
    /// </summary>
    [HttpPost("{id}/approve-tare-anomaly")]
    [Authorize(Policy = "Permission:weighing.override")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CommercialWeighingResultDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ApproveTareAnomaly(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userGuid))
            return Unauthorized("User ID not found in claims");

        try
        {
            var result = await _commercialWeighingService.ApproveTareAnomalyAsync(id, userGuid);
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
            _logger.LogError(ex, "Error approving tare anomaly for transaction {TransactionId}", id);
            return StatusCode(500, "An error occurred while approving the tare anomaly.");
        }
    }

    /// <summary>
    /// Rejects a flagged tare anomaly. Does not change the already-recorded tare value - see
    /// ICommercialWeighingService.RejectTareAnomalyAsync. Requires weighing.override permission.
    /// </summary>
    [HttpPost("{id}/reject-tare-anomaly")]
    [Authorize(Policy = "Permission:weighing.override")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CommercialWeighingResultDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RejectTareAnomaly(Guid id, [FromBody] ResolveTareAnomalyRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userGuid))
            return Unauthorized("User ID not found in claims");

        try
        {
            var result = await _commercialWeighingService.RejectTareAnomalyAsync(id, request.Reason, userGuid);
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
            _logger.LogError(ex, "Error rejecting tare anomaly for transaction {TransactionId}", id);
            return StatusCode(500, "An error occurred while rejecting the tare anomaly.");
        }
    }

    /// <summary>
    /// Overrides a flagged tare anomaly with a supervisor-corrected tare value (required
    /// justification). Updates the vehicle's stored tare going forward. Requires weighing.override
    /// permission.
    /// </summary>
    [HttpPost("{id}/override-tare-anomaly")]
    [Authorize(Policy = "Permission:weighing.override")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CommercialWeighingResultDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> OverrideTareAnomaly(Guid id, [FromBody] OverrideTareAnomalyRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userGuid))
            return Unauthorized("User ID not found in claims");

        try
        {
            var result = await _commercialWeighingService.OverrideTareAnomalyAsync(id, request, userGuid);
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
            _logger.LogError(ex, "Error overriding tare anomaly for transaction {TransactionId}", id);
            return StatusCode(500, "An error occurred while overriding the tare anomaly.");
        }
    }

    /// <summary>
    /// Lists flagged, unresolved tare anomalies for the current organization (Tare Register
    /// "Pending Review" queue). Combines both WeighingTransaction- and VehicleTareHistory-anchored
    /// anomalies - see TareAnomalyDto.
    /// </summary>
    [HttpGet("tare-anomalies")]
    [Authorize(Policy = "Permission:weighing.read")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(PagedResponse<TareAnomalyDto>), 200)]
    public async Task<IActionResult> GetFlaggedTareAnomalies(
        [FromQuery] Guid? stationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 20;

        try
        {
            var result = await _commercialWeighingService.GetFlaggedTareAnomaliesAsync(stationId, page, pageSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching flagged tare anomalies");
            return StatusCode(500, "An error occurred while fetching flagged tare anomalies.");
        }
    }

    // ============================================================================
    // Commercial Tolerance Settings
    // ============================================================================

    /// <summary>
    /// Gets all commercial tolerance settings for the current organization.
    /// </summary>
    [HttpGet("tolerance-settings")]
    [Authorize(Policy = "Permission:weighing.read")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(List<CommercialToleranceSettingDto>), 200)]
    public async Task<IActionResult> GetToleranceSettings()
    {
        try
        {
            var settings = await _commercialWeighingService.GetCommercialToleranceSettingsAsync();
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting commercial tolerance settings");
            return StatusCode(500, "An error occurred while getting tolerance settings.");
        }
    }

    /// <summary>
    /// Creates a new commercial tolerance setting.
    /// </summary>
    [HttpPost("tolerance-settings")]
    [Authorize(Policy = "Permission:weighing.create")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CommercialToleranceSettingDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateToleranceSetting([FromBody] CommercialToleranceSettingDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _commercialWeighingService.CreateCommercialToleranceSettingAsync(dto);
            return CreatedAtAction(nameof(GetToleranceSettings), result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating commercial tolerance setting");
            return StatusCode(500, "An error occurred while creating the tolerance setting.");
        }
    }

    /// <summary>
    /// Updates an existing commercial tolerance setting.
    /// </summary>
    [HttpPut("tolerance-settings/{id}")]
    [Authorize(Policy = "Permission:weighing.update")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CommercialToleranceSettingDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UpdateToleranceSetting(Guid id, [FromBody] CommercialToleranceSettingDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _commercialWeighingService.UpdateCommercialToleranceSettingAsync(id, dto);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Tolerance setting {id} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating commercial tolerance setting {SettingId}", id);
            return StatusCode(500, "An error occurred while updating the tolerance setting.");
        }
    }

    /// <summary>
    /// Deletes a commercial tolerance setting.
    /// </summary>
    [HttpDelete("tolerance-settings/{id}")]
    [Authorize(Policy = "Permission:weighing.update")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteToleranceSetting(Guid id)
    {
        try
        {
            await _commercialWeighingService.DeleteCommercialToleranceSettingAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Tolerance setting {id} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting commercial tolerance setting {SettingId}", id);
            return StatusCode(500, "An error occurred while deleting the tolerance setting.");
        }
    }

    /// <summary>
    /// Voids a pending commercial weighing transaction.
    /// </summary>
    [HttpPost("{id}/void")]
    [Authorize(Policy = "Permission:weighing.create")]
    [ProducesResponseType(typeof(CommercialWeighingResultDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Void(Guid id, [FromBody] VoidCommercialWeighingRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            return Unauthorized("User ID not found in claims");

        try
        {
            var result = await _commercialWeighingService.VoidCommercialWeighingAsync(id, request, userGuid);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error voiding commercial weighing {TransactionId}", id);
            return StatusCode(500, "An error occurred while voiding the transaction.");
        }
    }

    /// <summary>
    /// Gets pending commercial weighing transactions (first weight captured, awaiting second pass).
    /// </summary>
    [HttpGet("pending")]
    [Authorize(Policy = "Permission:weighing.read")]
    [ProducesResponseType(typeof(List<CommercialWeighingResultDto>), 200)]
    public async Task<IActionResult> GetPending([FromQuery] Guid stationId)
    {
        try
        {
            var results = await _commercialWeighingService.GetPendingCommercialTransactionsAsync(stationId);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching pending commercial transactions for station {StationId}", stationId);
            return StatusCode(500, "An error occurred while fetching pending transactions.");
        }
    }

    /// <summary>
    /// Finds open first-weight-only transactions for a specific vehicle plate within the configured time threshold.
    /// Called by the capture screen when an operator enters a plate number, to detect vehicles that need a second
    /// pass rather than starting a new transaction.
    /// </summary>
    [HttpGet("pending-by-plate/{regNo}")]
    [Authorize(Policy = "Permission:weighing.read")]
    [ProducesResponseType(typeof(List<CommercialWeighingResultDto>), 200)]
    public async Task<IActionResult> GetPendingByPlate(string regNo, [FromQuery] int? thresholdHours = null)
    {
        if (string.IsNullOrWhiteSpace(regNo))
            return BadRequest(new { message = "regNo is required" });

        try
        {
            var results = await _commercialWeighingService.GetPendingByPlateAsync(regNo, thresholdHours);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching pending commercial transactions for plate {RegNo}", regNo);
            return StatusCode(500, "An error occurred while looking up pending transactions.");
        }
    }

    /// <summary>
    /// Checks the caller's manual_weight_override permission (docs: "Scale fault during capture").
    /// A plain [Authorize(Policy = ...)] attribute can't be used here because the requirement is
    /// conditional on request.IsManualEntry, not on the endpoint as a whole.
    /// </summary>
    private Task<bool> HasManualWeightOverridePermissionAsync()
        => _permissionVerificationService.UserHasPermissionAsync(HttpContext, "manual_weight_override");
}
