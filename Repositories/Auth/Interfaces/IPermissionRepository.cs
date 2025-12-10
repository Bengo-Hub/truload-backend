using TruLoad.Backend.Models;

namespace TruLoad.Backend.Repositories.Auth.Interfaces;

/// <summary>
/// Repository interface for Permission entity operations.
/// Provides methods for querying, creating, updating, and deleting permissions.
/// </summary>
public interface IPermissionRepository
{
    /// <summary>
    /// Get a permission by its unique identifier.
    /// </summary>
    Task<Permission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a permission by its unique code (e.g., "weighing.create").
    /// </summary>
    Task<Permission?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all permissions in a specific category.
    /// </summary>
    Task<IEnumerable<Permission>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all permissions (active and inactive).
    /// </summary>
    Task<IEnumerable<Permission>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all active permissions only.
    /// </summary>
    Task<IEnumerable<Permission>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all permissions assigned to a specific role.
    /// </summary>
    Task<IEnumerable<Permission>> GetForRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new permission.
    /// </summary>
    Task<Permission> CreateAsync(Permission permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing permission.
    /// </summary>
    Task<Permission> UpdateAsync(Permission permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a permission by its identifier.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a permission code exists.
    /// </summary>
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Count all permissions.
    /// </summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
