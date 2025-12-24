using Microsoft.AspNetCore.Identity;
using TruLoad.Backend.Models.Identity;

namespace TruLoad.Data.Seeders;

/// <summary>
/// Seeds role definitions for TruLoad backend.
/// Defines 7 roles: SUPERUSER, SYSTEM_ADMIN, STATION_MANAGER, WEIGHING_OPERATOR, ENFORCEMENT_OFFICER, INSPECTOR, AUDITOR.
/// Permissions are managed via RolePermissions junction table (see RolePermissionSeeder).
/// Idempotent - safe to run multiple times.
/// </summary>
public class RoleSeeder
{
    private readonly RoleManager<ApplicationRole> _roleManager;

    public RoleSeeder(RoleManager<ApplicationRole> roleManager)
    {
        _roleManager = roleManager;
    }

    public async Task SeedAsync()
    {
        await SeedCoreRolesAsync();
    }

    private async Task SeedCoreRolesAsync()
    {
        // Define core roles required for TruLoad
        var roles = new[]
        {
            new { Name = "Superuser", Code = "SUPERUSER", Description = "Superuser with unrestricted access to all system features and administrative functions" },
            new { Name = "System Admin", Code = "SYSTEM_ADMIN", Description = "System administrator with access to all features except system-level administration" },
            new { Name = "Station Manager", Code = "STATION_MANAGER", Description = "Station manager with authority over station operations, staff allocation, and weighing approvals" },
            new { Name = "Weighing Operator", Code = "WEIGHING_OPERATOR", Description = "Weighing operator performing weighing operations and recording weighing data" },
            new { Name = "Enforcement Officer", Code = "ENFORCEMENT_OFFICER", Description = "Enforcement officer with authority to manage cases and enforcement actions" },
            new { Name = "Inspector", Code = "INSPECTOR", Description = "Inspector with authority to view and analyze weighing and case data" },
            new { Name = "Auditor", Code = "AUDITOR", Description = "Auditor with authority to review and audit system operations and data integrity" }
        };

        foreach (var roleData in roles)
        {
            var exists = await _roleManager.RoleExistsAsync(roleData.Name);
            if (!exists)
            {
                var role = new ApplicationRole
                {
                    Name = roleData.Name,
                    NormalizedName = roleData.Name.ToUpper(),
                    Code = roleData.Code,
                    Description = roleData.Description,
                    IsActive = true
                };

                var result = await _roleManager.CreateAsync(role);
                if (!result.Succeeded)
                {
                    throw new Exception($"Failed to create role {roleData.Name}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
        }
    }
}
