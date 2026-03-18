using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.Infrastructure;

namespace TruLoad.Backend.Controllers.System;

/// <summary>
/// Location hierarchy: Counties and Subcounties.
/// </summary>
/// GET responses are cached for 5 minutes; POST invalidates cache.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class GeographicController : ControllerBase
{
    private const string CacheKeyCounties = "Geographic_Counties";
    private const string CacheKeySubcountiesPrefix = "Geographic_Subcounties_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly TruLoadDbContext _context;
    private readonly IMemoryCache _cache;

    public GeographicController(TruLoadDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    /// <summary>
    /// Create a new county.
    /// </summary>
    [HttpPost("counties")]
    [HasPermission("config.create")]
    [ProducesResponseType(typeof(CountyDto), 201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<CountyDto>> CreateCounty([FromBody] CreateCountyRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Name is required.");
        var code = string.IsNullOrWhiteSpace(request.Code) ? request.Name[..Math.Min(10, request.Name.Length)].ToUpperInvariant().Replace(" ", "") : request.Code.Trim();
        if (await _context.Counties.AnyAsync(c => c.Code == code && c.DeletedAt == null, ct))
            return BadRequest($"A county with code '{code}' already exists.");
        var county = new Counties
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = request.Name.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Counties.Add(county);
        await _context.SaveChangesAsync(ct);
        _cache.Remove(CacheKeyCounties);
        return Created($"/api/v1/geographic/counties", new CountyDto { Id = county.Id, Code = county.Code, Name = county.Name });
    }

    /// <summary>
    /// Create a new subcounty (district) under a county.
    /// </summary>
    [HttpPost("subcounties")]
    [HasPermission("config.create")]
    [ProducesResponseType(typeof(SubcountyDto), 201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<SubcountyDto>> CreateSubcounty([FromBody] CreateSubcountyRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || !request.CountyId.HasValue)
            return BadRequest("Name and CountyId are required.");
        var county = await _context.Counties.FindAsync(new object[] { request.CountyId.Value }, ct);
        if (county == null || county.DeletedAt != null)
            return BadRequest("County not found.");
        var code = string.IsNullOrWhiteSpace(request.Code) ? $"{county.Code}-{request.Name[..Math.Min(5, request.Name.Length)].ToUpperInvariant()}" : request.Code.Trim();
        if (await _context.Subcounties.AnyAsync(s => s.Code == code && s.DeletedAt == null, ct))
            return BadRequest($"A subcounty with code '{code}' already exists.");
        var subcounty = new Subcounty
        {
            Id = Guid.NewGuid(),
            CountyId = request.CountyId.Value,
            Code = code,
            Name = request.Name.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Subcounties.Add(subcounty);
        await _context.SaveChangesAsync(ct);
        _cache.Remove(CacheKeySubcountiesPrefix + request.CountyId.Value);
        _cache.Remove(CacheKeySubcountiesPrefix + "all");
        return Created($"/api/v1/geographic/subcounties", new SubcountyDto { Id = subcounty.Id, CountyId = subcounty.CountyId, Code = subcounty.Code, Name = subcounty.Name });
    }

    /// <summary>
    /// Get all counties (for location hierarchy and prosecution defaults). Cached 5 min.
    /// </summary>
    [HttpGet("counties")]
    [HasPermission("config.read")]
    [ProducesResponseType(typeof(List<CountyDto>), 200)]
    public async Task<ActionResult<List<CountyDto>>> GetCounties(CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKeyCounties, out List<CountyDto>? cached) && cached != null)
            return Ok(cached);

        var list = await _context.Counties
            .AsNoTracking()
            .Where(c => c.IsActive && c.DeletedAt == null)
            .OrderBy(c => c.Name)
            .Select(c => new CountyDto { Id = c.Id, Code = c.Code, Name = c.Name })
            .ToListAsync(ct);
        _cache.Set(CacheKeyCounties, list, CacheDuration);
        return Ok(list);
    }

    /// <summary>
    /// Get subcounties optionally filtered by county. Cached 5 min per countyId.
    /// </summary>
    [HttpGet("subcounties")]
    [HasPermission("config.read")]
    [ProducesResponseType(typeof(List<SubcountyDto>), 200)]
    public async Task<ActionResult<List<SubcountyDto>>> GetSubcounties(
        [FromQuery] Guid? countyId,
        CancellationToken ct)
    {
        var cacheKey = CacheKeySubcountiesPrefix + (countyId?.ToString() ?? "all");
        if (_cache.TryGetValue(cacheKey, out List<SubcountyDto>? cached) && cached != null)
            return Ok(cached);

        var query = _context.Subcounties
            .AsNoTracking()
            .Where(s => s.IsActive && s.DeletedAt == null);

        if (countyId.HasValue)
            query = query.Where(s => s.CountyId == countyId.Value);

        var list = await query
            .OrderBy(s => s.Name)
            .Select(s => new SubcountyDto
            {
                Id = s.Id,
                CountyId = s.CountyId,
                Code = s.Code,
                Name = s.Name
            })
            .ToListAsync(ct);
        _cache.Set(cacheKey, list, CacheDuration);
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

public class CreateCountyRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
}

public class CreateSubcountyRequest
{
    public Guid? CountyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
}
