using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using truload_backend.Data;

namespace TruLoad.Backend.Data.Seeders;

/// <summary>
/// Seeds role-permission assignments for 6 built-in roles.
/// Assigns permissions based on role type and authorization level.
/// </summary>
public static class RolePermissionSeeder
{
    /// <summary>
    /// Define permission codes for each role.
    /// SuperAdmin: 77 permissions (all)
    /// Admin: 65 permissions (exclude system.*)
    /// StationManager: 45 permissions
    /// Prosecutor: 30 permissions
    /// ScaleOperator: 12 permissions
    /// Inspector: 18 permissions
    /// </summary>
    private static readonly Dictionary<string, List<string>> RolePermissions = new()
    {
        {
            "SYSTEM_ADMIN", new List<string>
            {
                // All 77 permissions
                "weighing.create", "weighing.read", "weighing.read_own", "weighing.update", "weighing.approve",
                "weighing.override", "weighing.send_to_yard", "weighing.scale_test", "weighing.export",
                "weighing.delete", "weighing.webhook", "weighing.audit",
                "case.create", "case.read", "case.read_own", "case.update", "case.assign", "case.close",
                "case.escalate", "case.special_release", "case.subfile_manage", "case.closure_review",
                "case.arrest_warrant", "case.court_hearing", "case.reweigh_schedule", "case.export", "case.audit",
                "prosecution.create", "prosecution.read", "prosecution.read_own", "prosecution.update",
                "prosecution.compute_charges", "prosecution.generate_certificate", "prosecution.export",
                "prosecution.audit",
                "user.create", "user.read", "user.read_own", "user.update", "user.update_own", "user.delete",
                "user.assign_roles", "user.manage_permissions", "user.manage_shifts", "user.audit",
                "station.read", "station.read_own", "station.create", "station.update", "station.update_own",
                "station.delete", "station.manage_staff", "station.manage_devices", "station.manage_io",
                "station.configure_defaults", "station.export", "station.audit",
                "config.read", "config.manage_axle", "config.manage_permits", "config.manage_fees",
                "config.manage_acts", "config.manage_taxonomy", "config.manage_references", "config.audit",
                "analytics.read", "analytics.read_own", "analytics.export", "analytics.schedule",
                "analytics.custom_query", "analytics.manage_dashboards", "analytics.superset", "analytics.audit",
                "system.admin", "system.audit_logs", "system.cache_management", "system.integration_management",
                "system.backup_restore", "system.security_policy"
            }
        },
        {
            "ADMIN", new List<string>
            {
                // All except system.*
                "weighing.create", "weighing.read", "weighing.read_own", "weighing.update", "weighing.approve",
                "weighing.override", "weighing.send_to_yard", "weighing.scale_test", "weighing.export",
                "weighing.delete", "weighing.webhook", "weighing.audit",
                "case.create", "case.read", "case.read_own", "case.update", "case.assign", "case.close",
                "case.escalate", "case.special_release", "case.subfile_manage", "case.closure_review",
                "case.arrest_warrant", "case.court_hearing", "case.reweigh_schedule", "case.export", "case.audit",
                "prosecution.create", "prosecution.read", "prosecution.read_own", "prosecution.update",
                "prosecution.compute_charges", "prosecution.generate_certificate", "prosecution.export",
                "prosecution.audit",
                "user.create", "user.read", "user.read_own", "user.update", "user.update_own", "user.delete",
                "user.assign_roles", "user.manage_permissions", "user.manage_shifts", "user.audit",
                "station.read", "station.read_own", "station.create", "station.update", "station.update_own",
                "station.delete", "station.manage_staff", "station.manage_devices", "station.manage_io",
                "station.configure_defaults", "station.export", "station.audit",
                "config.read", "config.manage_axle", "config.manage_permits", "config.manage_fees",
                "config.manage_acts", "config.manage_taxonomy", "config.manage_references", "config.audit",
                "analytics.read", "analytics.read_own", "analytics.export", "analytics.schedule",
                "analytics.custom_query", "analytics.manage_dashboards", "analytics.superset", "analytics.audit"
            }
        },
        {
            "STATION_MANAGER", new List<string>
            {
                // Station, weighing, case, limited prosecution/user, analytics
                "weighing.create", "weighing.read", "weighing.read_own", "weighing.update", "weighing.scale_test",
                "weighing.export", "weighing.audit",
                "case.create", "case.read", "case.read_own", "case.update", "case.assign", "case.export", "case.audit",
                "prosecution.read", "prosecution.read_own", "prosecution.export", "prosecution.audit",
                "user.read", "user.read_own", "user.audit",
                "station.read", "station.read_own", "station.update", "station.update_own", "station.manage_staff",
                "station.manage_devices", "station.manage_io", "station.configure_defaults", "station.export",
                "station.audit",
                "config.read",
                "analytics.read", "analytics.read_own", "analytics.export", "analytics.audit"
            }
        },
        {
            "PROSECUTOR", new List<string>
            {
                // Prosecution, case, limited weighing/user, analytics
                "weighing.read", "weighing.read_own", "weighing.export", "weighing.audit",
                "case.read", "case.read_own", "case.update", "case.assign", "case.escalate", "case.closure_review",
                "case.court_hearing", "case.export", "case.audit",
                "prosecution.create", "prosecution.read", "prosecution.read_own", "prosecution.update",
                "prosecution.compute_charges", "prosecution.generate_certificate", "prosecution.export",
                "prosecution.audit",
                "user.read", "user.read_own", "user.audit",
                "analytics.read", "analytics.read_own", "analytics.export", "analytics.audit"
            }
        },
        {
            "SCALE_OPERATOR", new List<string>
            {
                // Only weighing operations
                "weighing.create", "weighing.read_own", "weighing.scale_test", "weighing.audit"
            }
        },
        {
            "INSPECTOR", new List<string>
            {
                // Weighing, case, limited prosecution, user, analytics
                "weighing.read", "weighing.read_own", "weighing.export", "weighing.audit",
                "case.read", "case.read_own", "case.assign", "case.export", "case.audit",
                "prosecution.read", "prosecution.read_own", "prosecution.export", "prosecution.audit",
                "user.read", "user.read_own", "user.audit",
                "analytics.read", "analytics.read_own", "analytics.export", "analytics.audit"
            }
        }
    };

    /// <summary>
    /// Seed role-permission assignments.
    /// Links 6 built-in roles with their permissions.
    /// Idempotent - safe to run multiple times.
    /// </summary>
    public static async Task SeedAsync(TruLoadDbContext context)
    {
        // Check if role permissions already exist
        if (await context.RolePermissions.AnyAsync())
            return; // Already seeded

        var rolePermissions = new List<RolePermission>();

        // Get all roles and permissions
        var roles = await context.Roles.ToListAsync();
        var permissions = await context.Permissions.ToListAsync();

        foreach (var (roleCode, permissionCodes) in RolePermissions)
        {
            var role = roles.FirstOrDefault(r => r.Code == roleCode);
            if (role == null)
                continue; // Skip if role doesn't exist

            foreach (var permissionCode in permissionCodes)
            {
                var permission = permissions.FirstOrDefault(p => p.Code == permissionCode);
                if (permission == null)
                    continue; // Skip if permission doesn't exist

                rolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id,
                    AssignedAt = DateTime.UtcNow
                });
            }
        }

        context.RolePermissions.AddRange(rolePermissions);
        await context.SaveChangesAsync();
    }
}
