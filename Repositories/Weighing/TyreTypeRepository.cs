using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using truload_backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Weighing.Interfaces;

namespace TruLoad.Backend.Repositories.Weighing;

/// <summary>
/// Tyre type repository with distributed cache for hot lookups
/// </summary>
public class TyreTypeRepository : ITyreTypeRepository
{
    private readonly TruLoadDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly ILogger<TyreTypeRepository> _logger;
    private static readonly JsonSerializerOptions CacheOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly DistributedCacheEntryOptions CacheEntryOptions = new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
    };

    private const string CacheKeyAllActive = "tyretypes:active";

    public TyreTypeRepository(TruLoadDbContext context, IDistributedCache cache, ILogger<TyreTypeRepository> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<TyreType>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetStringAsync(CacheKeyAllActive, cancellationToken);
        if (!string.IsNullOrEmpty(cached))
        {
            var cachedResult = JsonSerializer.Deserialize<List<TyreType>>(cached, CacheOptions);
            if (cachedResult != null)
            {
                return cachedResult;
            }
        }

        var result = await _context.TyreTypes
            .Where(t => t.IsActive)
            .OrderBy(t => t.Code)
            .ToListAsync(cancellationToken);

        await _cache.SetStringAsync(CacheKeyAllActive, JsonSerializer.Serialize(result, CacheOptions), CacheEntryOptions, cancellationToken);
        return result;
    }

    public async Task<TyreType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.TyreTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<TyreType?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.TyreTypes.FirstOrDefaultAsync(t => t.Code == code, cancellationToken);
    }

    public async Task<TyreType> CreateAsync(TyreType tyreType, CancellationToken cancellationToken = default)
    {
        _context.TyreTypes.Add(tyreType);
        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(cancellationToken);
        _logger.LogInformation("Created tyre type {Code}", tyreType.Code);
        return tyreType;
    }

    public async Task<TyreType> UpdateAsync(TyreType tyreType, CancellationToken cancellationToken = default)
    {
        _context.TyreTypes.Update(tyreType);
        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(cancellationToken);
        _logger.LogInformation("Updated tyre type {Code}", tyreType.Code);
        return tyreType;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tyreType = await _context.TyreTypes.FindAsync(new object[] { id }, cancellationToken);
        if (tyreType == null)
        {
            return false;
        }

        tyreType.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(cancellationToken);
        _logger.LogInformation("Soft-deleted tyre type {Code}", tyreType.Code);
        return true;
    }

    private Task InvalidateCacheAsync(CancellationToken cancellationToken)
    {
        return _cache.RemoveAsync(CacheKeyAllActive, cancellationToken);
    }
}
