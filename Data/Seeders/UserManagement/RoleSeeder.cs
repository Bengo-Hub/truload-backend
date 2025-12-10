using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using truload_backend.Data;

namespace TruLoad.Backend.Data.Seeders.UserManagement;

/// <summary>
/// Seeds role definitions for TruLoad backend.
/// Defines roles: Admin, Station Manager, Scale Operator.
/// Permissions are managed via RolePermissions junction table (see RolePermissionSeeder).
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
    }
}
