using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using truload_backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Weighing.Interfaces;

namespace TruLoad.Backend.Repositories.Weighing;

/// <summary>
/// Axle fee schedule repository with fee lookup and distributed caching
/// </summary>
public class AxleFeeScheduleRepository : IAxleFeeScheduleRepository
{
    private readonly TruLoadDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly ILogger<AxleFeeScheduleRepository> _logger;
    private static readonly JsonSerializerOptions CacheOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly DistributedCacheEntryOptions CacheEntryOptions = new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
    };

    private const string CacheKeyPrefix = "axlefeeschedules:"; // axlefeeschedules:{framework}

    public AxleFeeScheduleRepository(TruLoadDbContext context, IDistributedCache cache, ILogger<AxleFeeScheduleRepository> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<AxleFeeSchedule>> GetAllByFrameworkAsync(string legalFramework, CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildFrameworkCacheKey(legalFramework);
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cached))
        {
            var cachedResult = JsonSerializer.Deserialize<List<AxleFeeSchedule>>(cached, CacheOptions);
            if (cachedResult != null)
            {
                return cachedResult;
            }
        }

        var result = await _context.AxleFeeSchedules
            .Where(f => f.LegalFramework == legalFramework && f.IsActive && f.DeletedAt == null)
            .OrderBy(f => f.FeeType)
            .ThenBy(f => f.OverloadMinKg)
            .ToListAsync(cancellationToken);

        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result, CacheOptions), CacheEntryOptions, cancellationToken);
        return result;
    }

    public async Task<AxleFeeSchedule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.AxleFeeSchedules.FirstOrDefaultAsync(f => f.Id == id && f.DeletedAt == null, cancellationToken);
    }

    public async Task<AxleFeeSchedule?> GetFeeByOverloadAsync(
        string legalFramework,
        string feeType,
        int overloadKg,
        CancellationToken cancellationToken = default)
    {
        var schedules = await GetAllByFrameworkAsync(legalFramework, cancellationToken);

        return schedules.FirstOrDefault(s =>
            s.FeeType == feeType &&
            overloadKg >= s.OverloadMinKg &&
            (s.OverloadMaxKg == null || overloadKg <= s.OverloadMaxKg.Value));
    }

    public async Task<(decimal FeeAmountUsd, int DemeritPoints)?> CalculateFeeAsync(
        string legalFramework,
        string feeType,
        int overloadKg,
        CancellationToken cancellationToken = default)
    {
        var schedule = await GetFeeByOverloadAsync(legalFramework, feeType, overloadKg, cancellationToken);
        if (schedule == null)
        {
            return null;
        }

        var fee = schedule.FlatFeeUsd + (overloadKg * schedule.FeePerKgUsd);
        return (fee, schedule.DemeritPoints);
    }

    public async Task<AxleFeeSchedule> CreateAsync(
        AxleFeeSchedule feeSchedule,
        CancellationToken cancellationToken = default)
    {
        feeSchedule.CreatedAt = DateTime.UtcNow;
        _context.AxleFeeSchedules.Add(feeSchedule);
        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateFrameworkCacheAsync(feeSchedule.LegalFramework, cancellationToken);
        _logger.LogInformation("Created axle fee schedule {Id} for framework {Framework}", feeSchedule.Id, feeSchedule.LegalFramework);
        return feeSchedule;
    }

    public async Task<AxleFeeSchedule> UpdateAsync(
        AxleFeeSchedule feeSchedule,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.AxleFeeSchedules.FirstOrDefaultAsync(f => f.Id == feeSchedule.Id && f.DeletedAt == null, cancellationToken);
        if (existing == null)
        {
            throw new KeyNotFoundException($"Axle fee schedule {feeSchedule.Id} not found");
        }

        existing.LegalFramework = feeSchedule.LegalFramework;
        existing.FeeType = feeSchedule.FeeType;
        existing.OverloadMinKg = feeSchedule.OverloadMinKg;
        existing.OverloadMaxKg = feeSchedule.OverloadMaxKg;
        existing.FeePerKgUsd = feeSchedule.FeePerKgUsd;
        existing.FlatFeeUsd = feeSchedule.FlatFeeUsd;
        existing.DemeritPoints = feeSchedule.DemeritPoints;
        existing.PenaltyDescription = feeSchedule.PenaltyDescription;
        existing.EffectiveFrom = feeSchedule.EffectiveFrom;
        existing.EffectiveTo = feeSchedule.EffectiveTo;
        existing.IsActive = feeSchedule.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateFrameworkCacheAsync(existing.LegalFramework, cancellationToken);
        _logger.LogInformation("Updated axle fee schedule {Id}", existing.Id);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.AxleFeeSchedules.FirstOrDefaultAsync(f => f.Id == id && f.DeletedAt == null, cancellationToken);
        if (existing == null)
        {
            return false;
        }

        existing.DeletedAt = DateTime.UtcNow;
        existing.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateFrameworkCacheAsync(existing.LegalFramework, cancellationToken);
        _logger.LogInformation("Soft-deleted axle fee schedule {Id}", existing.Id);
        return true;
    }

    private string BuildFrameworkCacheKey(string legalFramework) => $"{CacheKeyPrefix}{legalFramework}";

    private Task InvalidateFrameworkCacheAsync(string legalFramework, CancellationToken cancellationToken)
    {
        var cacheKey = BuildFrameworkCacheKey(legalFramework);
        return _cache.RemoveAsync(cacheKey, cancellationToken);
    }
}
