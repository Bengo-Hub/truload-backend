using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using TruLoad.Backend.Services.Interfaces;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace TruLoad.Backend.Services.Implementations;

/// <summary>
/// Distributed cache service implementation.
/// Wraps IDistributedCache to provide testable interface.
/// Includes fail-open resilience: if Redis is unreachable, caching degrades gracefully.
/// </summary>
public class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheService> _logger;

    public CacheService(IDistributedCache cache, ILogger<CacheService> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _cache.GetStringAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read from cache for key: {Key}", key);
            return null; // Fail-open: treat as cache miss
        }
    }

    public async Task SetStringAsync(string key, string value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = expiration.HasValue
                ? new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiration }
                : null;

            await _cache.SetStringAsync(key, value, options ?? new DistributedCacheEntryOptions(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write to cache for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove from cache for key: {Key}", key);
        }
    }
}
