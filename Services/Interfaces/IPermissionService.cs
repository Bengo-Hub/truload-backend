using TruLoad.Backend.Models;

namespace TruLoad.Backend.Services.Interfaces;

/// <summary>
/// Service interface for permission management and caching.
/// Provides methods for retrieving permissions with Redis caching strategy.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Get a permission by its unique identifier.
    /// </summary>
    Task<Permission?> GetPermissionByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a permission by its unique code with caching.
    /// Cache key: perm:code:{code}
    /// Cache TTL: 1 hour
    /// </summary>
    Task<Permission?> GetPermissionByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all permissions in a category with caching.
    /// Cache key: perm:category:{category}
    /// Cache TTL: 1 hour
    /// </summary>
    Task<IEnumerable<Permission>> GetPermissionsByCategoryAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all active permissions with caching.
    /// Cache key: perm:active:all
    /// Cache TTL: 1 hour
    /// </summary>
    Task<IEnumerable<Permission>> GetAllActivePermissionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all permissions (active and inactive) with caching.
    /// Cache key: perm:all
    /// Cache TTL: 1 hour
    /// </summary>
    Task<IEnumerable<Permission>> GetAllPermissionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all permissions assigned to a role with caching.
    /// Cache key: perm:role:{roleId}
    /// Cache TTL: 1 hour
    /// </summary>
    Task<IEnumerable<Permission>> GetPermissionsForRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if user has a specific permission (by code).
    /// Uses cached permission data.
    /// </summary>
    Task<bool> UserHasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a role has a specific permission (by code).
    /// Uses cached role permissions.
    /// </summary>
    Task<bool> RoleHasPermissionAsync(Guid roleId, string permissionCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate permission cache for a specific code.
    /// Called when a permission is updated.
    /// </summary>
    Task InvalidatePermissionCacheAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate all permission caches.
    /// Called when permissions are bulk updated or during admin operations.
    /// </summary>
    Task InvalidateAllPermissionCacheAsync(CancellationToken cancellationToken = default);
}
