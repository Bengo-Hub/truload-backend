using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Controllers.CaseManagement;

/// <summary>
/// Provides read-only access to case management taxonomy/lookup data.
/// Used by frontend dropdowns, filters, and workflow logic.
/// </summary>
[ApiController]
[Route("api/v1/case/taxonomy")]
[Authorize]
public class TaxonomyController : ControllerBase
{
    private readonly TruLoadDbContext _context;

    public TaxonomyController(TruLoadDbContext context)
    {
        _context = context;
    }

    [HttpGet("disposition-types")]
    public async Task<IActionResult> GetDispositionTypes(CancellationToken ct)
    {
        var items = await _context.DispositionTypes
            .Where(x => x.DeletedAt == null && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Code, x.Name, x.Description })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("release-types")]
    public async Task<IActionResult> GetReleaseTypes(CancellationToken ct)
    {
        var items = await _context.ReleaseTypes
            .Where(x => x.DeletedAt == null && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Code, x.Name, x.Description })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("violation-types")]
    public async Task<IActionResult> GetViolationTypes(CancellationToken ct)
    {
        var items = await _context.ViolationTypes
            .Where(x => x.DeletedAt == null && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Code, x.Name, x.Description })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("case-statuses")]
    public async Task<IActionResult> GetCaseStatuses(CancellationToken ct)
    {
        var items = await _context.CaseStatuses
            .Where(x => x.DeletedAt == null && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Code, x.Name, x.Description })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("hearing-types")]
    public async Task<IActionResult> GetHearingTypes(CancellationToken ct)
    {
        var items = await _context.HearingTypes
            .Where(x => x.DeletedAt == null && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Code, x.Name, x.Description })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("hearing-statuses")]
    public async Task<IActionResult> GetHearingStatuses(CancellationToken ct)
    {
        var items = await _context.HearingStatuses
            .Where(x => x.DeletedAt == null && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Code, x.Name, x.Description })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("hearing-outcomes")]
    public async Task<IActionResult> GetHearingOutcomes(CancellationToken ct)
    {
        var items = await _context.HearingOutcomes
            .Where(x => x.DeletedAt == null && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Code, x.Name, x.Description })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("subfile-types")]
    public async Task<IActionResult> GetSubfileTypes(CancellationToken ct)
    {
        var items = await _context.SubfileTypes
            .Where(x => x.DeletedAt == null && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Code, x.Name, x.Description })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("closure-types")]
    public async Task<IActionResult> GetClosureTypes(CancellationToken ct)
    {
        var items = await _context.ClosureTypes
            .Where(x => x.DeletedAt == null && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Code, x.Name, x.Description })
            .ToListAsync(ct);
        return Ok(items);
    }
}
