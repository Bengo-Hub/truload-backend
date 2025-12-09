using TruLoad.Backend.Models;

namespace TruLoad.Backend.Services.Interfaces.Auth;

/// <summary>
/// Service interface for syncing users from SSO into local database.
/// Handles user creation/update, tenant assignment, and role assignment.
/// </summary>
public interface ISsoUserSyncService
{
    /// <summary>
    /// Sync user from SSO JWT claims into local database.
    /// Creates new user if doesn't exist, updates existing user.
    /// Assigns tenant and role based on SSO claims.
    /// </summary>
    /// <param name="ssoUserId">User ID from SSO (from 'sub' claim)</param>
    /// <param name="email">User email from SSO</param>
    /// <param name="tenantSlug">Tenant slug from SSO (e.g., "codevertex")</param>
    /// <param name="role">Role from SSO (e.g., "superuser", "admin", "user")</param>
    /// <param name="isSuperUser">Whether user is superuser in SSO</param>
    /// <param name="fullName">User's full name from SSO (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Synced local User with assigned Tenant and Role</returns>
    Task<User> SyncUserFromSsoAsync(
        string ssoUserId,
        string email,
        string tenantSlug,
        string role,
        bool isSuperUser = false,
        string? fullName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get or create organization for SSO tenant.
    /// Organizations map to SSO tenants (1:1 relationship via tenant_slug).
    /// </summary>
    /// <param name="tenantSlug">Tenant slug from SSO</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Organization entity for the tenant</returns>
    Task<Organization> GetOrCreateOrganizationAsync(string tenantSlug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get or create role for SSO role name.
    /// Assigns superuser or regular user role based on SSO claims.
    /// </summary>
    /// <param name="roleName">Role name from SSO (e.g., "superuser")</param>
    /// <param name="isSuperUser">Whether user is superuser</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Role entity</returns>
    Task<Role> GetOrCreateRoleAsync(string roleName, bool isSuperUser = false, CancellationToken cancellationToken = default);
}
