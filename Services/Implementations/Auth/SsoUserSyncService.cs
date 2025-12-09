using Microsoft.EntityFrameworkCore;
using truload_backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Services.Interfaces.Auth;

namespace TruLoad.Backend.Services.Implementations.Auth;

/// <summary>
/// Implementation of SSO user sync service.
/// Handles user creation/update, tenant (organization) creation, and role assignment from SSO claims.
/// </summary>
public class SsoUserSyncService : ISsoUserSyncService
{
    private readonly TruLoadDbContext _dbContext;
    private readonly ILogger<SsoUserSyncService> _logger;

    // Default role names
    private const string SuperUserRoleName = "SUPERUSER";
    private const string AdminRoleName = "ADMIN";
    private const string UserRoleName = "USER";

    public SsoUserSyncService(TruLoadDbContext dbContext, ILogger<SsoUserSyncService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<User> SyncUserFromSsoAsync(
        string ssoUserId,
        string email,
        string tenantSlug,
        string role,
        bool isSuperUser = false,
        string? fullName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ssoUserId))
            throw new ArgumentException("SSO user ID cannot be empty", nameof(ssoUserId));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty", nameof(email));

        if (string.IsNullOrWhiteSpace(tenantSlug))
            throw new ArgumentException("Tenant slug cannot be empty", nameof(tenantSlug));

        try
        {
            _logger.LogInformation("Syncing user from SSO: Email={Email}, TenantSlug={TenantSlug}, Role={Role}, IsSuperUser={IsSuperUser}",
                email, tenantSlug, role, isSuperUser);

            // Parse SSO user ID
            if (!Guid.TryParse(ssoUserId, out var ssoUserGuid))
                throw new InvalidOperationException($"Invalid SSO user ID format: {ssoUserId}");

            // Get or create organization (maps to SSO tenant)
            var organization = await GetOrCreateOrganizationAsync(tenantSlug, cancellationToken);

            // Get or create role
            var roleEntity = await GetOrCreateRoleAsync(role, isSuperUser, cancellationToken);

            // Check if user already exists
            var existingUser = await _dbContext.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.AuthServiceUserId == ssoUserGuid, cancellationToken);

            if (existingUser != null)
            {
                // Update existing user
                existingUser.Email = email;
                existingUser.FullName = fullName ?? existingUser.FullName;
                existingUser.OrganizationId = organization.Id;
                existingUser.SyncStatus = "synced";
                existingUser.SyncAt = DateTime.UtcNow;

                // Assign role if not already assigned
                var hasRole = existingUser.UserRoles.Any(ur => ur.RoleId == roleEntity.Id);
                if (!hasRole)
                {
                    var userRole = new UserRole
                    {
                        UserId = existingUser.Id,
                        RoleId = roleEntity.Id,
                        AssignedAt = DateTime.UtcNow
                    };
                    existingUser.UserRoles.Add(userRole);
                }

                _dbContext.Users.Update(existingUser);
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Updated existing user: UserId={UserId}, Email={Email}", existingUser.Id, email);
                return existingUser;
            }

            // Create new user
            var newUser = new User
            {
                Id = Guid.NewGuid(),
                AuthServiceUserId = ssoUserGuid,
                Email = email,
                FullName = fullName ?? email.Split('@')[0], // Use email prefix as fallback
                Status = "active",
                OrganizationId = organization.Id,
                SyncStatus = "synced",
                SyncAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.Users.AddAsync(newUser, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Assign role to user
            var userRoleEntry = new UserRole
            {
                UserId = newUser.Id,
                RoleId = roleEntity.Id,
                AssignedAt = DateTime.UtcNow
            };

            await _dbContext.UserRoles.AddAsync(userRoleEntry, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created new user: UserId={UserId}, Email={Email}, RoleId={RoleId}",
                newUser.Id, email, roleEntity.Id);

            return newUser;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing user from SSO: Email={Email}, TenantSlug={TenantSlug}",
                email, tenantSlug);
            throw;
        }
    }

    public async Task<Organization> GetOrCreateOrganizationAsync(string tenantSlug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantSlug))
            throw new ArgumentException("Tenant slug cannot be empty", nameof(tenantSlug));

        try
        {
            // Try to find existing organization by slug-based code
            var existingOrg = await _dbContext.Organizations
                .FirstOrDefaultAsync(o => o.Code == tenantSlug, cancellationToken);

            if (existingOrg != null)
            {
                _logger.LogDebug("Found existing organization: OrgId={OrgId}, Code={Code}", existingOrg.Id, tenantSlug);
                return existingOrg;
            }

            // Create new organization
            var newOrg = new Organization
            {
                Id = Guid.NewGuid(),
                Code = tenantSlug,
                Name = tenantSlug, // Use slug as name (can be updated later)
                OrgType = "tenant",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _dbContext.Organizations.AddAsync(newOrg, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created new organization: OrgId={OrgId}, Code={Code}", newOrg.Id, tenantSlug);
            return newOrg;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting or creating organization for tenant: {TenantSlug}", tenantSlug);
            throw;
        }
    }

    public async Task<Role> GetOrCreateRoleAsync(string roleName, bool isSuperUser = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleName))
            throw new ArgumentException("Role name cannot be empty", nameof(roleName));

        try
        {
            // Determine role code based on superuser status
            var roleCode = isSuperUser ? SuperUserRoleName : MapRoleNameToCode(roleName);

            // Check if role exists
            var existingRole = await _dbContext.Roles
                .FirstOrDefaultAsync(r => r.Code == roleCode, cancellationToken);

            if (existingRole != null)
            {
                _logger.LogDebug("Found existing role: RoleId={RoleId}, Code={Code}", existingRole.Id, roleCode);
                return existingRole;
            }

            // Create new role
            var newRole = new Role
            {
                Id = Guid.NewGuid(),
                Code = roleCode,
                Name = isSuperUser ? "Super User" : MapRoleNameToDisplayName(roleName),
                Description = $"SSO role: {roleName}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _dbContext.Roles.AddAsync(newRole, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created new role: RoleId={RoleId}, Code={Code}", newRole.Id, roleCode);
            return newRole;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting or creating role: {RoleName}", roleName);
            throw;
        }
    }

    private string MapRoleNameToCode(string roleName)
    {
        return roleName?.ToUpper() switch
        {
            "ADMIN" or "ADMINISTRATOR" => AdminRoleName,
            "SUPERUSER" or "SUPER_USER" => SuperUserRoleName,
            _ => UserRoleName
        };
    }

    private string MapRoleNameToDisplayName(string roleName)
    {
        return roleName?.ToLower() switch
        {
            "admin" or "administrator" => "Administrator",
            "superuser" or "super_user" => "Super User",
            _ => "User"
        };
    }
}
