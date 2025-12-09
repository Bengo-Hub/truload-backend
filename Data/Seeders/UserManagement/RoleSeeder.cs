using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using truload_backend.Data;

namespace TruLoad.Backend.Data.Seeders.UserManagement;

/// <summary>
/// Seeds role definitions for TruLoad backend.
/// Defines permission scopes for Admin, Station Manager, and Scale Operator roles.
/// Idempotent - safe to run multiple times.
/// </summary>
public class RoleSeeder
{
    private readonly TruLoadDbContext _context;

    public RoleSeeder(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync()
    {
        await SeedCoreRolesAsync();
    }

    private async Task SeedCoreRolesAsync()
    {
        // Define core roles required for Sprint 1
        var roles = new[]
        {
            new Role
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Name = "Admin",
                Code = "ADMIN",
                Description = "Administrator with full system access including user management, configuration, and analytics",
                Permissions = """
                {
                    "users": ["create", "read", "update", "delete", "manage_roles"],
                    "organizations": ["create", "read", "update", "delete"],
                    "stations": ["create", "read", "update", "delete", "manage_staff"],
                    "departments": ["create", "read", "update", "delete"],
                    "roles": ["create", "read", "update", "delete"],
                    "axle_configurations": ["create", "read", "update", "delete"],
                    "weighing": ["create", "read", "update", "delete", "override"],
                    "permits": ["create", "read", "update", "delete", "approve"],
                    "violations": ["create", "read", "update", "delete"],
                    "analytics": ["read", "export", "custom_reports"],
                    "system": ["configure", "audit_logs"]
                }
                """,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Role
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Name = "Station Manager",
                Code = "STATION_MANAGER",
                Description = "Station manager with authority over station operations, staff allocation, and weighing approvals",
                Permissions = """
                {
                    "users": ["read", "update_own"],
                    "organizations": ["read_own"],
                    "stations": ["read", "update_own"],
                    "departments": ["read_own"],
                    "roles": ["read"],
                    "axle_configurations": ["read", "create"],
                    "weighing": ["create", "read", "approve", "reject"],
                    "permits": ["read", "verify"],
                    "violations": ["read", "approve"],
                    "analytics": ["read", "station_reports"],
                    "system": ["view_audit_logs"]
                }
                """,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Role
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                Name = "Scale Operator",
                Code = "SCALE_OPERATOR",
                Description = "Scale operator performing weighing operations and recording weighing data",
                Permissions = """
                {
                    "users": ["read_own"],
                    "organizations": ["read_own"],
                    "stations": ["read_own"],
                    "departments": ["read"],
                    "roles": ["read"],
                    "axle_configurations": ["read"],
                    "weighing": ["create", "read"],
                    "permits": ["read"],
                    "violations": ["create", "read"],
                    "analytics": ["read_own"],
                    "system": []
                }
                """,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        // Check which roles already exist
        var existingRoleIds = await _context.Roles
            .Where(r => roles.Select(x => x.Id).Contains(r.Id))
            .Select(r => r.Id)
            .ToListAsync();

        // Add only new roles
        var rolesToAdd = roles.Where(r => !existingRoleIds.Contains(r.Id)).ToList();
        if (rolesToAdd.Count > 0)
        {
            _context.Roles.AddRange(rolesToAdd);
            await _context.SaveChangesAsync();
        }

        // Update permissions for existing roles if needed (optional)
        foreach (var existingRole in roles.Where(r => existingRoleIds.Contains(r.Id)))
        {
            var dbRole = await _context.Roles.FindAsync(existingRole.Id);
            if (dbRole != null && dbRole.Permissions != existingRole.Permissions)
            {
                dbRole.Permissions = existingRole.Permissions;
                dbRole.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (rolesToAdd.Count > 0 || roles.Where(r => existingRoleIds.Contains(r.Id)).Any(r => 
            _context.Roles.AsNoTracking().First(x => x.Id == r.Id).Permissions != r.Permissions))
        {
            await _context.SaveChangesAsync();
        }
    }
}
