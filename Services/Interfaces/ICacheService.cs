using System;
using System.Threading;
using System.Threading.Tasks;

namespace TruLoad.Backend.Services.Interfaces;

/// <summary>
/// Cache service interface for testable caching operations.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Gets a string value from cache.
    /// </summary>
    Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a string value in cache with optional expiration.
    /// </summary>
    Task SetStringAsync(string key, string value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a value from cache.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}