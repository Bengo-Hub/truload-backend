using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TruLoad.Backend.Data;
using TruLoad.Backend.Services.Interfaces.Shared;

namespace TruLoad.Backend.Services.Implementations.Shared;

/// <summary>
/// Centralized service for looking up status/type entities by code.
/// Uses in-memory caching to avoid repeated database queries.
/// </summary>
public class StatusLookupService : IStatusLookupService
{
    private readonly TruLoadDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<StatusLookupService> _logger;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    public StatusLookupService(
        TruLoadDbContext context,
        IMemoryCache cache,
        ILogger<StatusLookupService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    // ============================================================================
    // Case Statuses
    // ============================================================================

    public async Task<Guid> GetCaseStatusIdAsync(string code, CancellationToken ct = default)
    {
        var id = await TryGetCaseStatusIdAsync(code, ct);
        return id ?? throw new InvalidOperationException($"Case status '{code}' not found");
    }

    public async Task<Guid?> TryGetCaseStatusIdAsync(string code, CancellationToken ct = default)
    {
        var cacheKey = $"CaseStatus:{code}";

        if (_cache.TryGetValue(cacheKey, out Guid cachedId))
        {
            return cachedId;
        }

        var status = await _context.CaseStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Code == code, ct);

        if (status != null)
        {
            _cache.Set(cacheKey, status.Id, CacheDuration);
            return status.Id;
        }

        _logger.LogWarning("Case status '{Code}' not found in database", code);
        return null;
    }

    // ============================================================================
    // Disposition Types
    // ============================================================================

    public async Task<Guid> GetDispositionTypeIdAsync(string code, CancellationToken ct = default)
    {
        var id = await TryGetDispositionTypeIdAsync(code, ct);
        return id ?? throw new InvalidOperationException($"Disposition type '{code}' not found");
    }

    public async Task<Guid?> TryGetDispositionTypeIdAsync(string code, CancellationToken ct = default)
    {
        var cacheKey = $"DispositionType:{code}";

        if (_cache.TryGetValue(cacheKey, out Guid cachedId))
        {
            return cachedId;
        }

        var type = await _context.DispositionTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Code == code, ct);

        if (type != null)
        {
            _cache.Set(cacheKey, type.Id, CacheDuration);
            return type.Id;
        }

        _logger.LogWarning("Disposition type '{Code}' not found in database", code);
        return null;
    }

    // ============================================================================
    // Violation Types
    // ============================================================================

    public async Task<Guid> GetViolationTypeIdAsync(string code, CancellationToken ct = default)
    {
        var id = await TryGetViolationTypeIdAsync(code, ct);
        return id ?? throw new InvalidOperationException($"Violation type '{code}' not found");
    }

    public async Task<Guid?> TryGetViolationTypeIdAsync(string code, CancellationToken ct = default)
    {
        var cacheKey = $"ViolationType:{code}";

        if (_cache.TryGetValue(cacheKey, out Guid cachedId))
        {
            return cachedId;
        }

        var type = await _context.ViolationTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Code == code, ct);

        if (type != null)
        {
            _cache.Set(cacheKey, type.Id, CacheDuration);
            return type.Id;
        }

        _logger.LogWarning("Violation type '{Code}' not found in database", code);
        return null;
    }

    // ============================================================================
    // Hearing Statuses
    // ============================================================================

    public async Task<Guid> GetHearingStatusIdAsync(string code, CancellationToken ct = default)
    {
        var id = await TryGetHearingStatusIdAsync(code, ct);
        return id ?? throw new InvalidOperationException($"Hearing status '{code}' not found");
    }

    public async Task<Guid?> TryGetHearingStatusIdAsync(string code, CancellationToken ct = default)
    {
        var cacheKey = $"HearingStatus:{code}";

        if (_cache.TryGetValue(cacheKey, out Guid cachedId))
        {
            return cachedId;
        }

        var status = await _context.HearingStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Code == code, ct);

        if (status != null)
        {
            _cache.Set(cacheKey, status.Id, CacheDuration);
            return status.Id;
        }

        _logger.LogWarning("Hearing status '{Code}' not found in database", code);
        return null;
    }

    // ============================================================================
    // Hearing Outcomes
    // ============================================================================

    public async Task<Guid> GetHearingOutcomeIdAsync(string code, CancellationToken ct = default)
    {
        var id = await TryGetHearingOutcomeIdAsync(code, ct);
        return id ?? throw new InvalidOperationException($"Hearing outcome '{code}' not found");
    }

    public async Task<Guid?> TryGetHearingOutcomeIdAsync(string code, CancellationToken ct = default)
    {
        var cacheKey = $"HearingOutcome:{code}";

        if (_cache.TryGetValue(cacheKey, out Guid cachedId))
        {
            return cachedId;
        }

        var outcome = await _context.HearingOutcomes
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Code == code, ct);

        if (outcome != null)
        {
            _cache.Set(cacheKey, outcome.Id, CacheDuration);
            return outcome.Id;
        }

        _logger.LogWarning("Hearing outcome '{Code}' not found in database", code);
        return null;
    }

    // ============================================================================
    // Cache Management
    // ============================================================================

    public void ClearCache()
    {
        // Note: IMemoryCache doesn't have a direct way to clear all entries
        // In production, you might use a distributed cache or implement cache keys tracking
        _logger.LogInformation("Cache clear requested - individual entries will expire based on TTL");
    }
}
