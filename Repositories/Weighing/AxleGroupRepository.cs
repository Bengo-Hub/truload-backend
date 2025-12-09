using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using truload_backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Weighing.Interfaces;

namespace TruLoad.Backend.Repositories.Weighing;

/// <summary>
/// Axle group repository with distributed caching
/// </summary>
public class AxleGroupRepository : IAxleGroupRepository
{
    private readonly TruLoadDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly ILogger<AxleGroupRepository> _logger;
    private static readonly JsonSerializerOptions CacheOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly DistributedCacheEntryOptions CacheEntryOptions = new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
    };

    private const string CacheKeyAllActive = "axlegroups:active";

    public AxleGroupRepository(TruLoadDbContext context, IDistributedCache cache, ILogger<AxleGroupRepository> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<AxleGroup>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetStringAsync(CacheKeyAllActive, cancellationToken);
        if (!string.IsNullOrEmpty(cached))
        {
            var cachedResult = JsonSerializer.Deserialize<List<AxleGroup>>(cached, CacheOptions);
            if (cachedResult != null)
            {
                return cachedResult;
            }
        }

        var result = await _context.AxleGroups
            .Where(g => g.IsActive)
            .OrderBy(g => g.Code)
            .ToListAsync(cancellationToken);

        await _cache.SetStringAsync(CacheKeyAllActive, JsonSerializer.Serialize(result, CacheOptions), CacheEntryOptions, cancellationToken);
        return result;
    }

    public async Task<AxleGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.AxleGroups.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<AxleGroup?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.AxleGroups.FirstOrDefaultAsync(g => g.Code == code, cancellationToken);
    }

    public async Task<AxleGroup> CreateAsync(AxleGroup axleGroup, CancellationToken cancellationToken = default)
    {
        _context.AxleGroups.Add(axleGroup);
        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(cancellationToken);
        _logger.LogInformation("Created axle group {Code}", axleGroup.Code);
        return axleGroup;
    }

    public async Task<AxleGroup> UpdateAsync(AxleGroup axleGroup, CancellationToken cancellationToken = default)
    {
        _context.AxleGroups.Update(axleGroup);
        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(cancellationToken);
        _logger.LogInformation("Updated axle group {Code}", axleGroup.Code);
        return axleGroup;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var axleGroup = await _context.AxleGroups.FindAsync(new object[] { id }, cancellationToken);
        if (axleGroup == null)
        {
            return false;
        }

        axleGroup.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(cancellationToken);
        _logger.LogInformation("Soft-deleted axle group {Code}", axleGroup.Code);
        return true;
    }

    private Task InvalidateCacheAsync(CancellationToken cancellationToken)
    {
        return _cache.RemoveAsync(CacheKeyAllActive, cancellationToken);
    }
}
