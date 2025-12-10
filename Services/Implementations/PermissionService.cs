using Microsoft.Extensions.Caching.Distributed;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Auth.Interfaces;
using TruLoad.Backend.Services.Interfaces;
using System.Text.Json;

namespace TruLoad.Backend.Services.Implementations;

/// <summary>
/// Permission service implementation with Redis caching.
/// Cache TTL: 1 hour (3600 seconds)
/// Cache keys follow pattern: perm:{type}:{identifier}
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly IPermissionRepository _repository;
    private readonly IDistributedCache _cache;
    private const int CacheTtlSeconds = 3600; // 1 hour

    public PermissionService(IPermissionRepository repository, IDistributedCache cache)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<Permission?> GetPermissionByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return null;

        return await _repository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<Permission?> GetPermissionByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var cacheKey = $"perm:code:{code}";

        // Try to get from cache
        var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedData))
        {
            return JsonSerializer.Deserialize<Permission>(cachedData);
        }

        // Get from repository
        var permission = await _repository.GetByCodeAsync(code, cancellationToken);

        // Cache if found
        if (permission != null)
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CacheTtlSeconds)
            };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(permission), options, cancellationToken);
        }

        return permission;
    }

    public async Task<IEnumerable<Permission>> GetPermissionsByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(category))
            return Enumerable.Empty<Permission>();

        var cacheKey = $"perm:category:{category}";

        // Try to get from cache
        var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedData))
        {
            return JsonSerializer.Deserialize<List<Permission>>(cachedData) ?? Enumerable.Empty<Permission>();
        }

        // Get from repository
        var permissions = await _repository.GetByCategoryAsync(category, cancellationToken);
        var permissionList = permissions.ToList();

        // Cache results
        if (permissionList.Any())
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CacheTtlSeconds)
            };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(permissionList), options, cancellationToken);
        }

        return permissionList;
    }

    public async Task<IEnumerable<Permission>> GetAllActivePermissionsAsync(CancellationToken cancellationToken = default)
    {
        const string cacheKey = "perm:active:all";

        // Try to get from cache
        var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedData))
        {
            return JsonSerializer.Deserialize<List<Permission>>(cachedData) ?? Enumerable.Empty<Permission>();
        }

        // Get from repository
        var permissions = await _repository.GetActiveAsync(cancellationToken);
        var permissionList = permissions.ToList();

        // Cache results
        if (permissionList.Any())
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CacheTtlSeconds)
            };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(permissionList), options, cancellationToken);
        }

        return permissionList;
    }

    public async Task<IEnumerable<Permission>> GetAllPermissionsAsync(CancellationToken cancellationToken = default)
    {
        const string cacheKey = "perm:all";

        var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedData))
        {
            return JsonSerializer.Deserialize<List<Permission>>(cachedData) ?? Enumerable.Empty<Permission>();
        }

        var permissions = await _repository.GetAllAsync(cancellationToken);
        var permissionList = permissions.ToList();

        if (permissionList.Any())
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CacheTtlSeconds)
            };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(permissionList), options, cancellationToken);
        }

        return permissionList;
    }

    public async Task<IEnumerable<Permission>> GetPermissionsForRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"perm:role:{roleId}";

        // Try to get from cache
        var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedData))
        {
            return JsonSerializer.Deserialize<List<Permission>>(cachedData) ?? Enumerable.Empty<Permission>();
        }

        // Get from repository
        var permissions = await _repository.GetForRoleAsync(roleId, cancellationToken);
        var permissionList = permissions.ToList();

        // Cache results
        if (permissionList.Any())
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CacheTtlSeconds)
            };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(permissionList), options, cancellationToken);
        }

        return permissionList;
    }

    public async Task<bool> UserHasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
            return false;

        // This is a placeholder - actual implementation would:
        // 1. Get user's roles
        // 2. Get permissions for each role
        // 3. Check if permission exists
        // For now, we just check if permission is active
        var permission = await GetPermissionByCodeAsync(permissionCode, cancellationToken);
        return permission?.IsActive ?? false;
    }

    public async Task<bool> RoleHasPermissionAsync(Guid roleId, string permissionCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
            return false;

        var rolePermissions = await GetPermissionsForRoleAsync(roleId, cancellationToken);
        return rolePermissions.Any(p => p.Code == permissionCode && p.IsActive);
    }

    public async Task InvalidatePermissionCacheAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return;

        await _cache.RemoveAsync($"perm:code:{code}", cancellationToken);
        await _cache.RemoveAsync("perm:active:all", cancellationToken);
        await _cache.RemoveAsync("perm:all", cancellationToken);
    }

    public async Task InvalidateAllPermissionCacheAsync(CancellationToken cancellationToken = default)
    {
        // Note: IDistributedCache doesn't have a pattern-based clear
        // In production, consider using Redis directly or maintaining a cache key registry
        // For now, clear common patterns
        await _cache.RemoveAsync("perm:active:all", cancellationToken);
        await _cache.RemoveAsync("perm:all", cancellationToken);
    }
}
