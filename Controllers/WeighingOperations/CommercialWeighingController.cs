using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using TruLoad.Backend.DTOs.Weighing;
using TruLoad.Backend.Services.Interfaces.Infrastructure;
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
    private readonly ILogger<CommercialWeighingController> _logger;

    public CommercialWeighingController(
        ICommercialWeighingService commercialWeighingService,
        IPdfService pdfService,
        ILogger<CommercialWeighingController> logger)
    {
        _commercialWeighingService = commercialWeighingService;
        _pdfService = pdfService;
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
    [ProducesResponseType(404)]
    public async Task<IActionResult> CaptureFirstWeight(Guid id, [FromBody] CaptureFirstWeightRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

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
    [ProducesResponseType(404)]
    public async Task<IActionResult> CaptureSecondWeight(Guid id, [FromBody] CaptureSecondWeightRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

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
    /// Generates and returns a PDF weight ticket for a commercial weighing transaction.
    /// </summary>
    [HttpGet("{id}/ticket/pdf")]
    [Authorize(Policy = "Permission:weighing.read")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetWeightTicketPdf(Guid id)
    {
        try
        {
            var result = await _commercialWeighingService.GetCommercialResultAsync(id);
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
}
