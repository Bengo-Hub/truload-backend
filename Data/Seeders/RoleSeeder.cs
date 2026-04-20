using Microsoft.AspNetCore.Identity;
using TruLoad.Backend.Models.Identity;

namespace TruLoad.Data.Seeders;

/// <summary>
/// Seeds role definitions for TruLoad backend.
/// Defines 8 roles: SUPERUSER, SYSTEM_ADMIN, STATION_MANAGER, WEIGHING_OPERATOR, ENFORCEMENT_OFFICER, INSPECTOR, AUDITOR, MIDDLEWARE_SERVICE.
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
            // ── Platform / enforcement roles ──────────────────────────────────────────
            new { Name = "Superuser", Code = "SUPERUSER", Description = "Superuser with unrestricted access to all system features and administrative functions" },
            new { Name = "System Admin", Code = "SYSTEM_ADMIN", Description = "System administrator with access to all features except system-level administration" },
            new { Name = "Station Manager", Code = "STATION_MANAGER", Description = "Station manager with authority over station operations, staff allocation, and weighing approvals" },
            new { Name = "Weighing Operator", Code = "WEIGHING_OPERATOR", Description = "Weighing operator performing weighing operations and recording weighing data" },
            new { Name = "Enforcement Officer", Code = "ENFORCEMENT_OFFICER", Description = "Enforcement officer with authority to manage cases and enforcement actions" },
            new { Name = "Inspector", Code = "INSPECTOR", Description = "Inspector with authority to view and analyze weighing and case data" },
            new { Name = "Auditor", Code = "AUDITOR", Description = "Auditor with authority to review and audit system operations and data integrity" },
            new { Name = "Middleware Service", Code = "MIDDLEWARE_SERVICE", Description = "Service account for TruConnect middleware with limited permissions for autoweigh operations" },
            new { Name = "Middleware Operator", Code = "MIDDLEWARE_OPERATOR", Description = "Operator with limited rights for managing TruConnect middleware settings and logs" },
            // ── Commercial weighing roles (weighbridge operator staff) ────────────────
            new { Name = "Commercial Weighing Manager", Code = "COMMERCIAL_MANAGER", Description = "Manages all commercial weighing operations: tare register, tolerance settings, vehicles, invoicing, staff, and analytics" },
            new { Name = "Commercial Supervisor", Code = "COMMERCIAL_SUPERVISOR", Description = "Reviews and approves commercial weighing transactions, manages tare approvals, and generates reports" },
            new { Name = "Commercial Weighing Operator", Code = "COMMERCIAL_OPERATOR", Description = "Performs daily commercial weighing operations and records tare weights at the weighbridge" },
            new { Name = "Commercial Finance", Code = "COMMERCIAL_FINANCE", Description = "Manages invoicing, payment reconciliation, and financial reporting for commercial weighing operations" },
            new { Name = "Commercial Auditor", Code = "COMMERCIAL_AUDITOR", Description = "Read-only access to commercial weighing records, financial data, and analytics for audit and compliance" },
            // ── Transporter portal roles (transporter self-service access) ────────────
            new { Name = "Transporter Admin", Code = "TRANSPORTER_ADMIN", Description = "Full access to transporter portal including fleet management, team management, and billing" },
            new { Name = "Transporter Manager", Code = "TRANSPORTER_MANAGER", Description = "Manages fleet vehicles and drivers, views full weighing history, and downloads weight tickets" },
            new { Name = "Transporter Viewer", Code = "TRANSPORTER_VIEWER", Description = "Read-only access to weighing history and PDF ticket downloads for assigned vehicles" },
        };

        foreach (var roleData in roles)
        {
            var exists = await _roleManager.RoleExistsAsync(roleData.Name);
            if (!exists)
            {
                var isSystemRole = roleData.Code == "SUPERUSER" || roleData.Code == "MIDDLEWARE_SERVICE";
                var role = new ApplicationRole
                {
                    Name = roleData.Name,
                    NormalizedName = roleData.Name.ToUpper(),
                    Code = roleData.Code,
                    Description = roleData.Description,
                    IsActive = true,
                    IsSystemRole = isSystemRole
                };

                var result = await _roleManager.CreateAsync(role);
                if (!result.Succeeded)
                {
                    throw new Exception($"Failed to create role {roleData.Name}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
            else
            {
                // Update existing roles to set IsSystemRole (for DBs created before this flag existed)
                var role = await _roleManager.FindByNameAsync(roleData.Name);
                if (role != null && (roleData.Code == "SUPERUSER" || roleData.Code == "MIDDLEWARE_SERVICE") && !role.IsSystemRole)
                {
                    role.IsSystemRole = true;
                    await _roleManager.UpdateAsync(role);
                }
            }
        }
    }
}
