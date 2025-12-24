namespace TruLoad.Backend.Services.Interfaces.Authorization;

/// <summary>
/// Service interface for verifying user permissions at runtime.
/// Uses JWT claims and PermissionService to check authorization.
/// </summary>
public interface IPermissionVerificationService
{
    /// <summary>
    /// Check if the user from JWT claims has a specific permission.
    /// </summary>
    /// <param name="httpContext">The HTTP context containing JWT claims and headers.</param>
    /// <param name="permissionCode">The permission code to check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if user has the permission; false otherwise.</returns>
    Task<bool> UserHasPermissionAsync(HttpContext httpContext, string permissionCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the user has at least one of the specified permissions (OR logic).
    /// </summary>
    /// <param name="httpContext">The HTTP context containing JWT claims and headers.</param>
    /// <param name="permissionCodes">The permission codes to check (at least one required).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if user has at least one permission; false otherwise.</returns>
    Task<bool> UserHasAnyPermissionAsync(HttpContext httpContext, IEnumerable<string> permissionCodes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the user has all of the specified permissions (AND logic).
    /// </summary>
    /// <param name="httpContext">The HTTP context containing JWT claims and headers.</param>
    /// <param name="permissionCodes">The permission codes to check (all required).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if user has all permissions; false otherwise.</returns>
    Task<bool> UserHasAllPermissionsAsync(HttpContext httpContext, IEnumerable<string> permissionCodes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all permissions assigned to the user via their role.
    /// Results are cached per HTTP request to avoid multiple lookups.
    /// </summary>
    /// <param name="httpContext">The HTTP context containing JWT claims and headers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Collection of permission codes the user has; empty if user has no role or invalid JWT.</returns>
    Task<IEnumerable<string>> GetUserPermissionsAsync(HttpContext httpContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the user ID from JWT claims (auth_service_user_id).
    /// </summary>
    /// <param name="httpContext">The HTTP context containing JWT claims and headers.</param>
    /// <returns>The user ID from claims, or null if not found or invalid.</returns>
    string? GetUserIdFromClaims(HttpContext httpContext);

    /// <summary>
    /// Get the user's role ID from JWT claims.
    /// </summary>
    /// <param name="httpContext">The HTTP context containing JWT claims and headers.</param>
    /// <returns>The role ID from claims, or null if not found.</returns>

}
