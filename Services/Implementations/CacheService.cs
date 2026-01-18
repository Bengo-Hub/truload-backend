using Microsoft.Extensions.Caching.Distributed;
using TruLoad.Backend.Services.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace TruLoad.Backend.Services.Implementations;

/// <summary>
/// Distributed cache service implementation.
/// Wraps IDistributedCache to provide testable interface.
/// </summary>
public class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;

    public CacheService(IDistributedCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _cache.GetStringAsync(key, cancellationToken);
    }

    public async Task SetStringAsync(string key, string value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var options = expiration.HasValue
            ? new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiration }
            : null;

        await _cache.SetStringAsync(key, value, options ?? new DistributedCacheEntryOptions(), cancellationToken);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(key, cancellationToken);
    }
}