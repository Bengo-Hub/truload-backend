using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.Services.Interfaces.CaseManagement;

namespace TruLoad.Backend.Controllers.CaseManagement;

/// <summary>
/// API controller for arrest warrant management.
/// Handles issuance, execution, and dropping of warrants.
/// </summary>
[ApiController]
[Authorize]
public class ArrestWarrantController : ControllerBase
{
    private readonly IArrestWarrantService _arrestWarrantService;

    public ArrestWarrantController(IArrestWarrantService arrestWarrantService)
    {
        _arrestWarrantService = arrestWarrantService;
    }

    /// <summary>
    /// Get warrant by ID
    /// </summary>
    [HttpGet("api/v1/case/warrants/{id}")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var warrant = await _arrestWarrantService.GetByIdAsync(id, ct);
        if (warrant == null) return NotFound();
        return Ok(warrant);
    }

    /// <summary>
    /// Get all warrants for a case
    /// </summary>
    [HttpGet("api/v1/case/warrants/by-case/{caseId}")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetByCaseId(Guid caseId, CancellationToken ct)
    {
        var warrants = await _arrestWarrantService.GetByCaseIdAsync(caseId, ct);
        return Ok(warrants);
    }

    /// <summary>
    /// Search warrants with filters
    /// </summary>
    [HttpPost("api/v1/case/warrants/search")]
    [HasPermission("case.read")]
    public async Task<IActionResult> Search([FromBody] ArrestWarrantSearchCriteria criteria, CancellationToken ct)
    {
        var warrants = await _arrestWarrantService.SearchAsync(criteria, ct);
        return Ok(warrants);
    }

    /// <summary>
    /// Create a new arrest warrant
    /// </summary>
    [HttpPost("api/v1/case/warrants")]
    [HasPermission("case.arrest_warrant")]
    public async Task<IActionResult> Create([FromBody] CreateArrestWarrantRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetCurrentUserId();

        try
        {
            var warrant = await _arrestWarrantService.CreateAsync(request, userId, ct);
            return CreatedAtAction(nameof(GetById), new { id = warrant.Id }, warrant);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Execute a warrant
    /// </summary>
    [HttpPost("api/v1/case/warrants/{id}/execute")]
    [HasPermission("case.arrest_warrant")]
    public async Task<IActionResult> Execute(Guid id, [FromBody] ExecuteWarrantRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var warrant = await _arrestWarrantService.ExecuteAsync(id, request, ct);
            return Ok(warrant);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Drop a warrant
    /// </summary>
    [HttpPost("api/v1/case/warrants/{id}/drop")]
    [HasPermission("case.arrest_warrant")]
    public async Task<IActionResult> Drop(Guid id, [FromBody] DropWarrantRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var warrant = await _arrestWarrantService.DropAsync(id, request, ct);
            return Ok(warrant);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Lift a warrant (court has lifted the warrant)
    /// </summary>
    [HttpPost("api/v1/case/warrants/{id}/lift")]
    [HasPermission("case.arrest_warrant")]
    public async Task<IActionResult> Lift(Guid id, [FromBody] LiftWarrantRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var warrant = await _arrestWarrantService.LiftAsync(id, request, ct);
            return Ok(warrant);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
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
