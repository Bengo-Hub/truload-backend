using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.Services.Interfaces.CaseManagement;

namespace TruLoad.Backend.Controllers.CaseManagement;

/// <summary>
/// API controller for court registry management.
/// Supports CRUD operations for court records.
/// </summary>
[ApiController]
[Authorize]
public class CourtController : ControllerBase
{
    private readonly ICourtService _courtService;

    public CourtController(ICourtService courtService)
    {
        _courtService = courtService;
    }

    /// <summary>
    /// Search courts with filters
    /// </summary>
    [HttpGet("api/v1/courts")]
    [HasPermission("config.read")]
    public async Task<IActionResult> Search([FromQuery] CourtSearchCriteria criteria, CancellationToken ct)
    {
        var courts = await _courtService.SearchAsync(criteria, ct);
        return Ok(courts);
    }

    /// <summary>
    /// Get court by ID
    /// </summary>
    [HttpGet("api/v1/courts/{id}")]
    [HasPermission("config.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var court = await _courtService.GetByIdAsync(id, ct);
        if (court == null) return NotFound();
        return Ok(court);
    }

    /// <summary>
    /// Get court by code
    /// </summary>
    [HttpGet("api/v1/courts/by-code/{code}")]
    [HasPermission("config.read")]
    public async Task<IActionResult> GetByCode(string code, CancellationToken ct)
    {
        var court = await _courtService.GetByCodeAsync(code, ct);
        if (court == null) return NotFound();
        return Ok(court);
    }

    /// <summary>
    /// Create a new court
    /// </summary>
    [HttpPost("api/v1/courts")]
    [HasPermission("config.create")]
    public async Task<IActionResult> Create([FromBody] CreateCourtRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var court = await _courtService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = court.Id }, court);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Update an existing court
    /// </summary>
    [HttpPut("api/v1/courts/{id}")]
    [HasPermission("config.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCourtRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var court = await _courtService.UpdateAsync(id, request, ct);
            return Ok(court);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Delete a court (soft delete)
    /// </summary>
    [HttpDelete("api/v1/courts/{id}")]
    [HasPermission("config.update")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await _courtService.DeleteAsync(id, ct);
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
