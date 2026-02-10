using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.Services.Interfaces.CaseManagement;

namespace TruLoad.Backend.Controllers.CaseManagement;

/// <summary>
/// API controller for case subfile (document) management.
/// Handles CRUD for case documents across subfile types.
/// </summary>
[ApiController]
[Authorize]
public class CaseSubfileController : ControllerBase
{
    private readonly ICaseSubfileService _caseSubfileService;

    public CaseSubfileController(ICaseSubfileService caseSubfileService)
    {
        _caseSubfileService = caseSubfileService;
    }

    /// <summary>
    /// Get subfile by ID
    /// </summary>
    [HttpGet("api/v1/case/subfiles/{id}")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var subfile = await _caseSubfileService.GetByIdAsync(id, ct);
        if (subfile == null) return NotFound();
        return Ok(subfile);
    }

    /// <summary>
    /// Get all subfiles for a case
    /// </summary>
    [HttpGet("api/v1/case/subfiles/by-case/{caseId}")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetByCaseId(Guid caseId, CancellationToken ct)
    {
        var subfiles = await _caseSubfileService.GetByCaseIdAsync(caseId, ct);
        return Ok(subfiles);
    }

    /// <summary>
    /// Get subfiles for a case filtered by subfile type
    /// </summary>
    [HttpGet("api/v1/case/subfiles/by-case/{caseId}/type/{subfileTypeId}")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetByCaseAndType(Guid caseId, Guid subfileTypeId, CancellationToken ct)
    {
        var subfiles = await _caseSubfileService.GetByCaseAndTypeAsync(caseId, subfileTypeId, ct);
        return Ok(subfiles);
    }

    /// <summary>
    /// Get subfile completion status for a case
    /// </summary>
    [HttpGet("api/v1/case/subfiles/by-case/{caseId}/completion")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetSubfileCompletion(Guid caseId, CancellationToken ct)
    {
        var completion = await _caseSubfileService.GetSubfileCompletionAsync(caseId, ct);
        return Ok(completion);
    }

    /// <summary>
    /// Search subfiles with filters
    /// </summary>
    [HttpPost("api/v1/case/subfiles/search")]
    [HasPermission("case.read")]
    public async Task<IActionResult> Search([FromBody] CaseSubfileSearchCriteria criteria, CancellationToken ct)
    {
        var subfiles = await _caseSubfileService.SearchAsync(criteria, ct);
        return Ok(subfiles);
    }

    /// <summary>
    /// Create a new case subfile
    /// </summary>
    [HttpPost("api/v1/case/subfiles")]
    [HasPermission("case.create")]
    public async Task<IActionResult> Create([FromBody] CreateCaseSubfileRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetCurrentUserId();

        try
        {
            var subfile = await _caseSubfileService.CreateAsync(request, userId, ct);
            return CreatedAtAction(nameof(GetById), new { id = subfile.Id }, subfile);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Update an existing case subfile
    /// </summary>
    [HttpPut("api/v1/case/subfiles/{id}")]
    [HasPermission("case.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCaseSubfileRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var subfile = await _caseSubfileService.UpdateAsync(id, request, ct);
            return Ok(subfile);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Delete a case subfile (soft delete)
    /// </summary>
    [HttpDelete("api/v1/case/subfiles/{id}")]
    [HasPermission("case.update")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await _caseSubfileService.DeleteAsync(id, ct);
        if (!deleted) return NotFound();
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
