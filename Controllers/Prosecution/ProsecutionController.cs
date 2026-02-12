using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.Prosecution;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Services.Interfaces.Prosecution;
using TruLoad.Backend.Services.Interfaces.Infrastructure;

namespace TruLoad.Backend.Controllers.Prosecution;

/// <summary>
/// API controller for prosecution case management.
/// Handles charge calculation, case creation, and status tracking.
/// </summary>
[ApiController]
[Authorize]
public class ProsecutionController : ControllerBase
{
    private readonly IProsecutionService _prosecutionService;
    private readonly IPdfService _pdfService;
    private readonly ITenantContext _tenantContext;

    public ProsecutionController(
        IProsecutionService prosecutionService,
        IPdfService pdfService,
        ITenantContext tenantContext)
    {
        _prosecutionService = prosecutionService;
        _pdfService = pdfService;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Get prosecution case by ID
    /// </summary>
    [HttpGet("api/v1/prosecutions/{id}")]
    [HasPermission("prosecution.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var prosecution = await _prosecutionService.GetByIdAsync(id, ct);
        if (prosecution == null) return NotFound();
        return Ok(prosecution);
    }

    /// <summary>
    /// Get prosecution case by case register ID
    /// </summary>
    [HttpGet("api/v1/cases/{caseId}/prosecution")]
    [HasPermission("prosecution.read")]
    public async Task<IActionResult> GetByCaseId(Guid caseId, CancellationToken ct)
    {
        var prosecution = await _prosecutionService.GetByCaseIdAsync(caseId, ct);
        if (prosecution == null) return NotFound("No prosecution case found for this case");
        return Ok(prosecution);
    }

    /// <summary>
    /// Search prosecution cases with filters
    /// </summary>
    [HttpPost("api/v1/prosecutions/search")]
    [HasPermission("prosecution.read")]
    public async Task<IActionResult> Search([FromBody] ProsecutionSearchCriteria criteria, CancellationToken ct)
    {
        var prosecutions = await _prosecutionService.SearchAsync(criteria, ct);
        return Ok(prosecutions);
    }

    /// <summary>
    /// Calculate charges for a weighing transaction
    /// </summary>
    [HttpPost("api/v1/weighings/{weighingId}/calculate-charges")]
    [HasPermission("prosecution.create")]
    public async Task<IActionResult> CalculateCharges(
        Guid weighingId,
        [FromQuery] string legalFramework = "TRAFFIC_ACT",
        CancellationToken ct = default)
    {
        try
        {
            var result = await _prosecutionService.CalculateChargesAsync(weighingId, legalFramework, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Create prosecution case from a case register
    /// </summary>
    [HttpPost("api/v1/cases/{caseId}/prosecution")]
    [HasPermission("prosecution.create")]
    public async Task<IActionResult> CreateFromCase(
        Guid caseId,
        [FromBody] CreateProsecutionRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetCurrentUserId();

        try
        {
            var prosecution = await _prosecutionService.CreateFromCaseAsync(caseId, request, userId, ct);
            return CreatedAtAction(nameof(GetById), new { id = prosecution.Id }, prosecution);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Update an existing prosecution case
    /// </summary>
    [HttpPut("api/v1/prosecutions/{id}")]
    [HasPermission("prosecution.update")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProsecutionRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetCurrentUserId();

        try
        {
            var prosecution = await _prosecutionService.UpdateAsync(id, request, userId, ct);
            return Ok(prosecution);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Delete a prosecution case (soft delete)
    /// </summary>
    [HttpDelete("api/v1/prosecutions/{id}")]
    [HasPermission("prosecution.update")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await _prosecutionService.DeleteAsync(id, ct);
        if (!deleted) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Get prosecution statistics for dashboard
    /// </summary>
    [HttpGet("api/v1/prosecutions/statistics")]
    [HasPermission("prosecution.read")]
    public async Task<IActionResult> GetStatistics(CancellationToken ct)
    {
        var stats = await _prosecutionService.GetStatisticsAsync(ct);
        return Ok(stats);
    }

    /// <summary>
    /// Download charge sheet PDF for a prosecution case
    /// </summary>
    [HttpGet("api/v1/prosecutions/{id}/charge-sheet")]
    [HasPermission("prosecution.read")]
    public async Task<IActionResult> DownloadChargeSheet(Guid id, CancellationToken ct)
    {
        try
        {
            var pdfBytes = await _pdfService.GenerateChargeSheetAsync(id, ct);
            var prosecution = await _prosecutionService.GetByIdAsync(id, ct);
            var fileName = $"ChargeSheet_{prosecution?.CertificateNo ?? id.ToString()}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to generate charge sheet PDF: {ex.Message}");
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            throw new UnauthorizedAccessException("User ID not found in claims");
        return Guid.Parse(userIdClaim);
    }
}
