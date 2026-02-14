using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.Identity;

namespace TruLoad.Backend.Tests.Integration.Helpers;

/// <summary>
/// Factory for creating TruLoadDbContext instances backed by InMemory databases.
/// Each call to Create() produces a fresh, isolated database suitable for parallel test execution.
/// </summary>
public static class TestDbContextFactory
{
    /// <summary>
    /// Creates a new TruLoadDbContext with a unique InMemory database.
    /// Each invocation produces a completely isolated database instance.
    /// </summary>
    public static TruLoadDbContext Create()
    {
        return Create(Guid.NewGuid().ToString());
    }

    /// <summary>
    /// Creates a new TruLoadDbContext with a named InMemory database.
    /// Use the same dbName to share state across multiple context instances
    /// (e.g., when simulating concurrent access or testing detached entities).
    /// </summary>
    public static TruLoadDbContext Create(string dbName)
    {
        var options = new DbContextOptionsBuilder<TruLoadDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new TruLoadDbContext(options);
    }

    /// <summary>
    /// Seeds base reference data required by most integration tests:
    /// - 3 roles: Superuser, System Admin, Enforcement Officer
    /// - 7 core permissions: user.read, user.create, user.update, user.delete, config.read, system.security_policy, user.manage_shifts
    /// - Role-permission mappings (all permissions assigned to Superuser and System Admin; user.read to Enforcement Officer)
    /// </summary>
    public static async Task SeedBaseData(TruLoadDbContext context)
    {
        // --- Roles ---
        var superUserId = Guid.NewGuid();
        var systemAdminId = Guid.NewGuid();
        var officerId = Guid.NewGuid();

        var roles = new[]
        {
            new ApplicationRole
            {
                Id = superUserId,
                Name = "Superuser",
                NormalizedName = "SUPERUSER",
                Code = "SUPERUSER",
                Description = "Superuser with unrestricted access to all system features and administrative functions",
                IsActive = true
            },
            new ApplicationRole
            {
                Id = systemAdminId,
                Name = "System Admin",
                NormalizedName = "SYSTEM ADMIN",
                Code = "SYSTEM_ADMIN",
                Description = "System administrator with access to all features except system-level administration",
                IsActive = true
            },
            new ApplicationRole
            {
                Id = officerId,
                Name = "Enforcement Officer",
                NormalizedName = "ENFORCEMENT OFFICER",
                Code = "ENFORCEMENT_OFFICER",
                Description = "Enforcement officer with authority to manage cases and enforcement actions",
                IsActive = true
            }
        };

        context.Roles.AddRange(roles);

        // --- Permissions ---
        var permissions = new[]
        {
            new Permission { Id = Guid.NewGuid(), Code = "user.read",              Name = "Read All Users",       Category = "User",          Description = "Read all user records",                          IsActive = true },
            new Permission { Id = Guid.NewGuid(), Code = "user.create",            Name = "Create User",          Category = "User",          Description = "Create new users",                               IsActive = true },
            new Permission { Id = Guid.NewGuid(), Code = "user.update",            Name = "Update User",          Category = "User",          Description = "Update user details",                            IsActive = true },
            new Permission { Id = Guid.NewGuid(), Code = "user.delete",            Name = "Delete User",          Category = "User",          Description = "Delete user accounts",                           IsActive = true },
            new Permission { Id = Guid.NewGuid(), Code = "config.read",            Name = "Read Configuration",   Category = "Configuration", Description = "Read system configurations",                      IsActive = true },
            new Permission { Id = Guid.NewGuid(), Code = "system.security_policy", Name = "Security Policy",      Category = "System",        Description = "Manage security policies and configurations",     IsActive = true },
            new Permission { Id = Guid.NewGuid(), Code = "user.manage_shifts",     Name = "Manage Shifts",        Category = "User",          Description = "Assign and manage user shifts",                   IsActive = true }
        };

        context.Permissions.AddRange(permissions);

        // --- Role-Permission Mappings ---
        var rolePermissions = new List<RolePermission>();

        // Superuser and System Admin get all permissions
        foreach (var permission in permissions)
        {
            rolePermissions.Add(new RolePermission
            {
                RoleId = superUserId,
                PermissionId = permission.Id,
                AssignedAt = DateTime.UtcNow
            });

            rolePermissions.Add(new RolePermission
            {
                RoleId = systemAdminId,
                PermissionId = permission.Id,
                AssignedAt = DateTime.UtcNow
            });
        }

        // Enforcement Officer gets user.read only
        var userReadPermission = permissions.First(p => p.Code == "user.read");
        rolePermissions.Add(new RolePermission
        {
            RoleId = officerId,
            PermissionId = userReadPermission.Id,
            AssignedAt = DateTime.UtcNow
        });

        context.RolePermissions.AddRange(rolePermissions);

        await context.SaveChangesAsync();
    }
}