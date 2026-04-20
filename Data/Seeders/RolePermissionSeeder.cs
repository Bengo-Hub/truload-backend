using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Data;

namespace TruLoad.Data.Seeders;

/// <summary>
/// Seeds role-permission assignments for built-in roles.
/// Assigns permissions based on role type and authorization level.
///
/// Enforcement roles:
///   SUPERUSER: all permissions (maps to auth-service superuser flag)
///   SYSTEM_ADMIN: all except system.admin
///   STATION_MANAGER: station ops + financial create/read + case/prosecution read
///   WEIGHING_OPERATOR: weighing create + vehicle/driver management
///   ENFORCEMENT_OFFICER: case/prosecution + financial + limited weighing
///   INSPECTOR: read-only across weighing/case/prosecution + analytics
///   AUDITOR: read-only + audit logs across all domains + financial read
///   MIDDLEWARE_SERVICE: minimal autoweigh permissions only
///
/// Commercial weighing roles:
///   COMMERCIAL_MANAGER: full commercial stack — no yard/tag/case/prosecution
///   COMMERCIAL_OPERATOR: daily ops — weighing create, vehicle/tare management
///   COMMERCIAL_AUDITOR: read-only across commercial weighing, financial, analytics
/// </summary>
public static class RolePermissionSeeder
{
    /// <summary>
    /// Permission codes for each built-in role. Idempotent — only adds missing assignments.
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
                "system.integration_management", "system.backup_restore", "system.security_policy",
                "technical.read", "technical.calibration", "technical.scale_test", "technical.audit"
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
                "system.integration_management", "system.backup_restore", "system.security_policy",
                "technical.read", "technical.calibration", "technical.scale_test", "technical.audit"
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
                "config.read", "config.manage_axle",
                "technical.read", "technical.calibration", "technical.scale_test", "technical.audit",
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
                "technical.read", "technical.scale_test",
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
        },

        // ── Commercial weighing roles (weighbridge operator staff) ────────────────
        {
            "COMMERCIAL_MANAGER", new List<string>
            {
                // Full commercial weighing stack — no enforcement-only perms (yard, tag, case, prosecution)
                "weighing.create", "weighing.read", "weighing.read_own", "weighing.update",
                "weighing.approve", "weighing.override", "weighing.export", "weighing.audit", "weighing.scale_test",
                "vehicle.create", "vehicle.read", "vehicle.update",
                "transporter.create", "transporter.read", "transporter.update",
                "driver.create", "driver.read", "driver.update",
                // Config: tolerance settings, cargo types, weighing metadata
                "config.read", "config.create", "config.update",
                "config.manage_taxonomy", "config.manage_references", "config.audit",
                // User & shift management
                "user.create", "user.read", "user.read_own", "user.update", "user.update_own",
                "user.assign_roles", "user.manage_shifts", "user.audit",
                // Station
                "station.read", "station.read_own", "station.update_own",
                "station.manage_staff", "station.configure_defaults", "station.export", "station.audit",
                // Technical (scale calibration)
                "technical.read", "technical.calibration", "technical.scale_test", "technical.audit",
                // Analytics & reporting
                "analytics.read", "analytics.read_own", "analytics.export",
                "analytics.schedule", "analytics.manage_dashboards", "analytics.audit",
                // Financial
                "invoice.create", "invoice.read", "invoice.read_own", "invoice.update", "invoice.void",
                "receipt.create", "receipt.read", "receipt.read_own", "receipt.void",
                "financial.audit"
            }
        },
        {
            "COMMERCIAL_SUPERVISOR", new List<string>
            {
                // Weighing review, tare approval, reports — no user management or config changes
                "weighing.create", "weighing.read", "weighing.read_own", "weighing.update",
                "weighing.approve", "weighing.export", "weighing.audit",
                "vehicle.create", "vehicle.read", "vehicle.update",
                "transporter.read", "transporter.create", "transporter.update",
                "driver.read", "driver.create", "driver.update",
                // Config read-only (to understand tolerance/cargo rules, not change them)
                "config.read",
                // Technical
                "technical.read", "technical.scale_test",
                // Analytics
                "analytics.read", "analytics.read_own", "analytics.export", "analytics.audit",
                // Financial read (for reconciliation reference)
                "invoice.read", "invoice.read_own", "invoice.create", "invoice.update",
                "receipt.read", "receipt.read_own", "receipt.create",
                "financial.audit"
            }
        },
        {
            "COMMERCIAL_OPERATOR", new List<string>
            {
                // Day-to-day commercial weighing: create weighings, record tare, manage vehicles/drivers
                "weighing.create", "weighing.read_own", "weighing.export", "weighing.audit",
                "vehicle.create", "vehicle.read", "vehicle.update",
                "transporter.read",
                "driver.create", "driver.read", "driver.update",
                // Technical: view calibration status and run scale tests
                "technical.read", "technical.scale_test",
                // Financial: view own invoices/receipts
                "invoice.read_own",
                "receipt.read_own"
            }
        },
        {
            "COMMERCIAL_FINANCE", new List<string>
            {
                // Invoice management, payment reconciliation, financial reporting
                "invoice.create", "invoice.read", "invoice.read_own", "invoice.update", "invoice.void",
                "receipt.create", "receipt.read", "receipt.read_own", "receipt.void",
                "financial.audit",
                // Weighing read (for reconciling billed weights against records)
                "weighing.read", "weighing.read_own", "weighing.export", "weighing.audit",
                // Vehicle/transporter read (for billing reference)
                "vehicle.read",
                "transporter.read",
                "driver.read",
                // Analytics (for financial reports)
                "analytics.read", "analytics.read_own", "analytics.export", "analytics.audit"
            }
        },
        {
            "COMMERCIAL_AUDITOR", new List<string>
            {
                // Read-only access across commercial weighing, financial, and analytics
                "weighing.read", "weighing.read_own", "weighing.export", "weighing.audit",
                "vehicle.read",
                "transporter.read",
                "driver.read",
                "config.read", "config.audit",
                "technical.read", "technical.audit",
                "analytics.read", "analytics.read_own", "analytics.export", "analytics.audit",
                "invoice.read", "invoice.read_own",
                "receipt.read", "receipt.read_own",
                "financial.audit"
            }
        },

        // ── Transporter portal roles (self-service portal for fleet operators) ──────
        {
            "TRANSPORTER_ADMIN", new List<string>
            {
                // Full portal: fleet management + team management + billing + export
                "portal.access", "portal.manage_fleet", "portal.manage_team",
                "portal.manage_billing", "portal.export"
            }
        },
        {
            "TRANSPORTER_MANAGER", new List<string>
            {
                // Fleet management + history + export — no team/billing management
                "portal.access", "portal.manage_fleet", "portal.export"
            }
        },
        {
            "TRANSPORTER_VIEWER", new List<string>
            {
                // Read-only portal: weighing history view only
                "portal.access"
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
