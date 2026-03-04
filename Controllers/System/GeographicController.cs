using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Controllers.System;

/// <summary>
/// Location hierarchy: Counties and Subcounties (Districts).
/// Subcounty and District mean the same thing; API uses Subcounty for consistency.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class GeographicController : ControllerBase
{
    private readonly TruLoadDbContext _context;

    public GeographicController(TruLoadDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all counties (for location hierarchy and prosecution defaults).
    /// </summary>
    [HttpGet("counties")]
    [HasPermission("config.read")]
    [ProducesResponseType(typeof(List<CountyDto>), 200)]
    public async Task<ActionResult<List<CountyDto>>> GetCounties(CancellationToken ct)
    {
        var list = await _context.Counties
            .AsNoTracking()
            .Where(c => c.IsActive && c.DeletedAt == null)
            .OrderBy(c => c.Name)
            .Select(c => new CountyDto { Id = c.Id, Code = c.Code, Name = c.Name })
            .ToListAsync(ct);
        return Ok(list);
    }

    /// <summary>
    /// Get subcounties (districts) optionally filtered by county.
    /// District and Subcounty are the same; we use Subcounty in the API.
    /// </summary>
    [HttpGet("subcounties")]
    [HasPermission("config.read")]
    [ProducesResponseType(typeof(List<SubcountyDto>), 200)]
    public async Task<ActionResult<List<SubcountyDto>>> GetSubcounties(
        [FromQuery] Guid? countyId,
        CancellationToken ct)
    {
        var query = _context.Districts
            .AsNoTracking()
            .Where(d => d.IsActive && d.DeletedAt == null);

        if (countyId.HasValue)
            query = query.Where(d => d.CountyId == countyId.Value);

        var list = await query
            .OrderBy(d => d.Name)
            .Select(d => new SubcountyDto
            {
                Id = d.Id,
                CountyId = d.CountyId,
                Code = d.Code,
                Name = d.Name
            })
            .ToListAsync(ct);
        return Ok(list);
    }
}

public class CountyDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class SubcountyDto
{
    public Guid Id { get; set; }
    public Guid CountyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
