using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.Services.Interfaces.CaseManagement;

namespace TruLoad.Backend.Controllers.CaseManagement;

[ApiController]
[Authorize]
public class CaseAssignmentLogController : ControllerBase
{
    private readonly ICaseAssignmentLogService _assignmentService;

    public CaseAssignmentLogController(ICaseAssignmentLogService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    [HttpGet("api/v1/cases/{caseId}/assignments")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetByCaseId(Guid caseId, CancellationToken ct)
    {
        var logs = await _assignmentService.GetByCaseIdAsync(caseId, ct);
        return Ok(logs);
    }

    [HttpGet("api/v1/cases/{caseId}/assignments/current")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetCurrentAssignment(Guid caseId, CancellationToken ct)
    {
        var assignment = await _assignmentService.GetCurrentAssignmentAsync(caseId, ct);
        if (assignment == null) return NotFound("No current IO assignment");
        return Ok(assignment);
    }

    [HttpPost("api/v1/cases/{caseId}/assignments")]
    [HasPermission("case.update")]
    public async Task<IActionResult> LogAssignment(Guid caseId, [FromBody] LogAssignmentRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetCurrentUserId();

        try
        {
            var log = await _assignmentService.LogAssignmentAsync(caseId, request, userId, ct);
            return Created($"api/v1/cases/{caseId}/assignments", log);
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
