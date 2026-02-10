using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Data;

namespace TruLoad.Data.Seeders;

/// <summary>
/// Seeds role-permission assignments for 8 built-in roles.
/// Assigns permissions based on role type and authorization level.
/// SUPERUSER: 109 permissions (all) - maps to auth-service superuser flag
/// SYSTEM_ADMIN: 108 permissions (all except system.admin)
/// STATION_MANAGER: 66 permissions (incl. financial create/read)
/// WEIGHING_OPERATOR: 10 permissions
/// ENFORCEMENT_OFFICER: 44 permissions (incl. financial create/read)
/// INSPECTOR: 26 permissions
/// AUDITOR: 36 permissions (incl. financial read/audit)
/// MIDDLEWARE_SERVICE: 7 permissions (autoweigh operations only)
/// </summary>
public static class RolePermissionSeeder
{
    /// <summary>
    /// Define permission codes for each role.
    /// SUPERUSER: 109 permissions (all - includes system.* and financial.*)
    /// SYSTEM_ADMIN: 108 permissions (exclude system.admin)
    /// STATION_MANAGER: 66 permissions (incl. financial)
    /// WEIGHING_OPERATOR: 10 permissions
    /// ENFORCEMENT_OFFICER: 44 permissions (incl. financial)
    /// INSPECTOR: 26 permissions
    /// AUDITOR: 36 permissions (incl. financial read/audit)
    /// MIDDLEWARE_SERVICE: 7 permissions (autoweigh operations only)
    /// </summary>
    private static readonly Dictionary<string, List<string>> RolePermissions = new()
    {
        {
            "SUPERUSER", new List<string>
            {
                // All permissions - superuser has everything including system admin and financial
                "weighing.create", "weighing.read", "weighing.read_own", "weighing.update", "weighing.approve",
                "weighing.override", "weighing.send_to_yard", "weighing.scale_test", "weighing.export",
                "weighing.delete", "weighing.webhook", "weighing.audit",
                "yard.create", "yard.read", "yard.read_own", "yard.update", "yard.release", "yard.export",
                "yard.delete", "yard.audit",
                "tag.create", "tag.read", "tag.read_own", "tag.update", "tag.resolve", "tag.export",
                "tag.delete", "tag.audit",
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
                "config.read", "config.create", "config.update", "config.manage_axle", "config.manage_permits", "config.manage_fees",
                "config.manage_acts", "config.manage_taxonomy", "config.manage_references", "config.audit",
                "analytics.read", "analytics.read_own", "analytics.export", "analytics.schedule",
                "analytics.custom_query", "analytics.manage_dashboards", "analytics.superset", "analytics.audit",
                "invoice.create", "invoice.read", "invoice.read_own", "invoice.update", "invoice.void",
                "receipt.create", "receipt.read", "receipt.read_own", "receipt.void",
                "financial.audit",
                "vehicle.create", "vehicle.read", "vehicle.update",
                "transporter.create", "transporter.read", "transporter.update", "transporter.delete",
                "driver.create", "driver.read", "driver.update",
                "system.admin", "system.manage_roles", "system.manage_organizations", "system.manage_stations",
                "system.manage_departments", "system.audit_logs", "system.cache_management",
                "system.integration_management", "system.backup_restore", "system.security_policy"
            }
        },
        {
            "SYSTEM_ADMIN", new List<string>
            {
                // All permissions except system.admin (reserved for SUPERUSER)
                "weighing.create", "weighing.read", "weighing.read_own", "weighing.update", "weighing.approve",
                "weighing.override", "weighing.send_to_yard", "weighing.scale_test", "weighing.export",
                "weighing.delete", "weighing.webhook", "weighing.audit",
                "yard.create", "yard.read", "yard.read_own", "yard.update", "yard.release", "yard.export",
                "yard.delete", "yard.audit",
                "tag.create", "tag.read", "tag.read_own", "tag.update", "tag.resolve", "tag.export",
                "tag.delete", "tag.audit",
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
                "config.read", "config.create", "config.update", "config.manage_axle", "config.manage_permits", "config.manage_fees",
                "config.manage_acts", "config.manage_taxonomy", "config.manage_references", "config.audit",
                "analytics.read", "analytics.read_own", "analytics.export", "analytics.schedule",
                "analytics.custom_query", "analytics.manage_dashboards", "analytics.superset", "analytics.audit",
                "invoice.create", "invoice.read", "invoice.read_own", "invoice.update", "invoice.void",
                "receipt.create", "receipt.read", "receipt.read_own", "receipt.void",
                "financial.audit",
                "vehicle.create", "vehicle.read", "vehicle.update",
                "transporter.create", "transporter.read", "transporter.update", "transporter.delete",
                "driver.create", "driver.read", "driver.update",
                "system.manage_roles", "system.manage_organizations", "system.manage_stations",
                "system.manage_departments", "system.audit_logs", "system.cache_management",
                "system.integration_management", "system.backup_restore", "system.security_policy"
            }
        },
        {
            "STATION_MANAGER", new List<string>
            {
                // Station, weighing, yard, tag, case, limited prosecution/user, analytics, financial read
                "weighing.create", "weighing.read", "weighing.read_own", "weighing.update", "weighing.scale_test",
                "weighing.export", "weighing.audit", "weighing.send_to_yard",
                "yard.create", "yard.read", "yard.read_own", "yard.update", "yard.release", "yard.export", "yard.audit",
                "tag.create", "tag.read", "tag.read_own", "tag.update", "tag.resolve", "tag.export", "tag.audit",
                "case.create", "case.read", "case.read_own", "case.update", "case.assign", "case.export", "case.audit",
                "prosecution.read", "prosecution.read_own", "prosecution.export", "prosecution.audit",
                "user.read", "user.read_own", "user.audit",
                "station.read", "station.read_own", "station.update", "station.update_own", "station.manage_staff",
                "station.manage_devices", "station.manage_io", "station.configure_defaults", "station.export",
                "station.audit",
                "config.read",
                "analytics.read", "analytics.read_own", "analytics.export", "analytics.audit",
                "invoice.create", "invoice.read", "invoice.read_own", "invoice.update",
                "receipt.create", "receipt.read", "receipt.read_own",
                "financial.audit",
                "vehicle.create", "vehicle.read", "vehicle.update",
                "transporter.create", "transporter.read", "transporter.update",
                "driver.create", "driver.read", "driver.update"
            }
        },
        {
            "ENFORCEMENT_OFFICER", new List<string>
            {
                // Prosecution, case, yard, tag, limited weighing/user, analytics, financial
                "weighing.read", "weighing.read_own", "weighing.export", "weighing.audit",
                "yard.read", "yard.read_own", "yard.export", "yard.audit",
                "tag.read", "tag.read_own", "tag.export", "tag.audit",
                "case.read", "case.read_own", "case.update", "case.assign", "case.escalate", "case.closure_review",
                "case.arrest_warrant", "case.court_hearing", "case.export", "case.audit",
                "prosecution.create", "prosecution.read", "prosecution.read_own", "prosecution.update",
                "prosecution.compute_charges", "prosecution.generate_certificate", "prosecution.export",
                "prosecution.audit",
                "user.read", "user.read_own", "user.audit",
                "analytics.read", "analytics.read_own", "analytics.export", "analytics.audit",
                "invoice.create", "invoice.read", "invoice.read_own",
                "receipt.create", "receipt.read", "receipt.read_own",
                "vehicle.read", "driver.read", "transporter.read"
            }
        },
        {
            "WEIGHING_OPERATOR", new List<string>
            {
                // Weighing operations plus basic yard/tag for workflow
                "weighing.create", "weighing.read_own", "weighing.scale_test", "weighing.audit",
                "weighing.send_to_yard", "weighing.export",
                "yard.create", "yard.read_own",
                "tag.create", "tag.read_own",
                "vehicle.create", "vehicle.read", "vehicle.update",
                "driver.create", "driver.read", "driver.update",
                "transporter.read"
            }
        },
        {
            "INSPECTOR", new List<string>
            {
                // Weighing, yard, tag, case, limited prosecution, user, analytics
                "weighing.read", "weighing.read_own", "weighing.export", "weighing.audit",
                "yard.read", "yard.read_own", "yard.audit",
                "tag.read", "tag.read_own", "tag.audit",
                "case.read", "case.read_own", "case.assign", "case.export", "case.audit",
                "prosecution.read", "prosecution.read_own", "prosecution.export", "prosecution.audit",
                "user.read", "user.read_own", "user.audit",
                "analytics.read", "analytics.read_own", "analytics.export", "analytics.audit"
            }
        },
        {
            "AUDITOR", new List<string>
            {
                // Read-only access to audit logs across all domains including financial
                "weighing.read", "weighing.read_own", "weighing.audit",
                "yard.read", "yard.read_own", "yard.audit",
                "tag.read", "tag.read_own", "tag.audit",
                "case.read", "case.read_own", "case.audit",
                "prosecution.read", "prosecution.read_own", "prosecution.audit",
                "user.read", "user.read_own", "user.audit",
                "station.read", "station.read_own", "station.audit",
                "config.read", "config.audit",
                "analytics.read", "analytics.read_own", "analytics.audit",
                "invoice.read", "invoice.read_own",
                "receipt.read", "receipt.read_own",
                "financial.audit"
            }
        },
        {
            "MIDDLEWARE_SERVICE", new List<string>
            {
                // Limited permissions for TruConnect middleware autoweigh operations
                "weighing.create", "weighing.read", "weighing.update", "weighing.webhook",
                "vehicle.read", "driver.read", "transporter.read"
            }
        }
    };

    /// <summary>
    /// Seed role-permission assignments.
    /// Links 7 built-in roles with their permissions.
    /// Idempotent - safe to run multiple times. Adds missing role-permission assignments.
    /// </summary>
    public static async Task SeedAsync(TruLoadDbContext context)
    {
        var rolePermissionsToAdd = new List<RolePermission>();

        // Get all roles and permissions
        var roles = await context.Roles.ToListAsync();
        var permissions = await context.Permissions.ToListAsync();

        // Build a hash set of existing role-permission pairs for fast lookup
        var existingRolePermissionPairs = await context.RolePermissions
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync();
        var existingPairSet = existingRolePermissionPairs
            .Select(x => $"{x.RoleId}:{x.PermissionId}")
            .ToHashSet();

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

                // Check if this specific role-permission pair already exists
                var pairKey = $"{role.Id}:{permission.Id}";
                if (existingPairSet.Contains(pairKey))
                    continue; // Skip if already assigned

                rolePermissionsToAdd.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id,
                    AssignedAt = DateTime.UtcNow
                });
            }
        }

        if (rolePermissionsToAdd.Count > 0)
        {
            context.RolePermissions.AddRange(rolePermissionsToAdd);
            await context.SaveChangesAsync();
        }
    }
}
