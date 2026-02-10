using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.Services.Interfaces.CaseManagement;

namespace TruLoad.Backend.Controllers.CaseManagement;

/// <summary>
/// API controller for managing case parties (officers, defendants, witnesses).
/// </summary>
[ApiController]
[Authorize]
public class CasePartyController : ControllerBase
{
    private readonly ICasePartyService _casePartyService;

    public CasePartyController(ICasePartyService casePartyService)
    {
        _casePartyService = casePartyService;
    }

    /// <summary>
    /// Get all parties for a case
    /// </summary>
    [HttpGet("api/v1/cases/{caseId}/parties")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetByCaseId(Guid caseId, CancellationToken ct)
    {
        var parties = await _casePartyService.GetByCaseIdAsync(caseId, ct);
        return Ok(parties);
    }

    /// <summary>
    /// Add a party to a case
    /// </summary>
    [HttpPost("api/v1/cases/{caseId}/parties")]
    [HasPermission("case.update")]
    public async Task<IActionResult> AddParty(Guid caseId, [FromBody] AddCasePartyRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var party = await _casePartyService.AddPartyAsync(caseId, request, ct);
            return CreatedAtAction(nameof(GetByCaseId), new { caseId }, party);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Update a case party
    /// </summary>
    [HttpPut("api/v1/cases/{caseId}/parties/{partyId}")]
    [HasPermission("case.update")]
    public async Task<IActionResult> UpdateParty(Guid partyId, [FromBody] UpdateCasePartyRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var party = await _casePartyService.UpdatePartyAsync(partyId, request, ct);
            return Ok(party);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Remove a party from a case
    /// </summary>
    [HttpDelete("api/v1/cases/{caseId}/parties/{partyId}")]
    [HasPermission("case.update")]
    public async Task<IActionResult> RemoveParty(Guid partyId, CancellationToken ct)
    {
        var removed = await _casePartyService.RemovePartyAsync(partyId, ct);
        if (!removed) return NotFound();
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            throw new UnauthorizedAccessException("User ID not found in claims");
        return Guid.Parse(userIdClaim);
    }
}
