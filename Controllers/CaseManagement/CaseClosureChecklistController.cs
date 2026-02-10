using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.Services.Interfaces.CaseManagement;

namespace TruLoad.Backend.Controllers.CaseManagement;

/// <summary>
/// API controller for case closure checklist management.
/// Supports checklist updates, review requests, approvals, and rejections.
/// </summary>
[ApiController]
[Authorize]
public class CaseClosureChecklistController : ControllerBase
{
    private readonly ICaseClosureChecklistService _caseClosureChecklistService;

    public CaseClosureChecklistController(ICaseClosureChecklistService caseClosureChecklistService)
    {
        _caseClosureChecklistService = caseClosureChecklistService;
    }

    /// <summary>
    /// Get closure checklist for a case
    /// </summary>
    [HttpGet("api/v1/cases/{caseId}/closure-checklist")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetByCaseId(Guid caseId, CancellationToken ct)
    {
        var checklist = await _caseClosureChecklistService.GetByCaseIdAsync(caseId, ct);
        if (checklist == null) return NotFound();
        return Ok(checklist);
    }

    /// <summary>
    /// Create or update closure checklist for a case
    /// </summary>
    [HttpPut("api/v1/cases/{caseId}/closure-checklist")]
    [HasPermission("case.close")]
    public async Task<IActionResult> CreateOrUpdate(
        Guid caseId,
        [FromBody] UpdateChecklistRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetCurrentUserId();

        try
        {
            var checklist = await _caseClosureChecklistService.CreateOrUpdateAsync(caseId, request, userId, ct);
            return Ok(checklist);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Request review of closure checklist
    /// </summary>
    [HttpPost("api/v1/cases/{caseId}/closure-checklist/request-review")]
    [HasPermission("case.close")]
    public async Task<IActionResult> RequestReview(
        Guid caseId,
        [FromBody] RequestReviewRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetCurrentUserId();

        try
        {
            var checklist = await _caseClosureChecklistService.RequestReviewAsync(caseId, request, userId, ct);
            return Ok(checklist);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Approve closure checklist review
    /// </summary>
    [HttpPost("api/v1/cases/{caseId}/closure-checklist/approve-review")]
    [HasPermission("case.close")]
    public async Task<IActionResult> ApproveReview(
        Guid caseId,
        [FromBody] ReviewDecisionRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetCurrentUserId();

        try
        {
            var checklist = await _caseClosureChecklistService.ApproveReviewAsync(caseId, request, userId, ct);
            return Ok(checklist);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Reject closure checklist review
    /// </summary>
    [HttpPost("api/v1/cases/{caseId}/closure-checklist/reject-review")]
    [HasPermission("case.close")]
    public async Task<IActionResult> RejectReview(
        Guid caseId,
        [FromBody] ReviewDecisionRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetCurrentUserId();

        try
        {
            var checklist = await _caseClosureChecklistService.RejectReviewAsync(caseId, request, userId, ct);
            return Ok(checklist);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
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
