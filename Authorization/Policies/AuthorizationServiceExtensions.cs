using Microsoft.AspNetCore.Authorization;
using TruLoad.Backend.Authorization.Requirements;

namespace TruLoad.Backend.Authorization.Policies;

/// <summary>
/// Extension methods for registering permission-based authorization policies.
/// Keeps Program.cs clean by centralizing all policy definitions here.
/// </summary>
public static class AuthorizationServiceExtensions
{
    /// <summary>
    /// All permission codes that need authorization policies registered.
    /// Organized by module for maintainability.
    /// When adding new permissions, add them to both PermissionSeeder and this list.
    /// </summary>
    private static readonly string[] AllPermissionCodes =
    [
        // System
        "system.admin", "system.manage_roles", "system.manage_organizations",
        "system.manage_stations", "system.manage_departments", "system.audit_logs",
        "system.cache_management", "system.integration_management",
        "system.backup_restore", "system.security_policy",

        // User
        "user.create", "user.read", "user.read_own", "user.update", "user.update_own",
        "user.delete", "user.assign_roles", "user.manage_permissions",
        "user.manage_shifts", "user.audit",

        // Station
        "station.read", "station.read_own", "station.create", "station.update",
        "station.update_own", "station.delete", "station.manage_staff",
        "station.manage_devices", "station.manage_io", "station.configure_defaults",
        "station.export", "station.audit",

        // Configuration
        "config.read", "config.create", "config.update",
        "config.manage_axle", "config.manage_permits", "config.manage_fees",
        "config.manage_acts", "config.manage_taxonomy", "config.manage_references", "config.audit",

        // Vehicle
        "vehicle.create", "vehicle.read", "vehicle.update",

        // Transporter
        "transporter.create", "transporter.read", "transporter.update", "transporter.delete",

        // Driver
        "driver.create", "driver.read", "driver.update",

        // Weighing
        "weighing.create", "weighing.read", "weighing.read_own", "weighing.update",
        "weighing.approve", "weighing.override", "weighing.send_to_yard",
        "weighing.scale_test", "weighing.export", "weighing.delete",
        "weighing.webhook", "weighing.audit",

        // Case
        "case.create", "case.read", "case.read_own", "case.update", "case.assign",
        "case.close", "case.escalate", "case.special_release", "case.subfile_manage",
        "case.closure_review", "case.arrest_warrant", "case.court_hearing",
        "case.reweigh_schedule", "case.export", "case.audit",

        // Prosecution
        "prosecution.create", "prosecution.read", "prosecution.read_own", "prosecution.update",
        "prosecution.compute_charges", "prosecution.generate_certificate",
        "prosecution.export", "prosecution.audit",

        // Financial - Invoice
        "invoice.create", "invoice.read", "invoice.read_own", "invoice.update", "invoice.void",

        // Financial - Receipt
        "receipt.create", "receipt.read", "receipt.read_own", "receipt.void",
        "financial.audit",

        // Analytics
        "analytics.read", "analytics.read_own", "analytics.export", "analytics.schedule",
        "analytics.custom_query", "analytics.manage_dashboards", "analytics.superset",
        "analytics.audit",

        // Yard
        "yard.create", "yard.read", "yard.read_own", "yard.update", "yard.release",
        "yard.export", "yard.delete", "yard.audit",

        // Tag
        "tag.create", "tag.read", "tag.read_own", "tag.update", "tag.resolve",
        "tag.export", "tag.delete", "tag.audit"
    ];

    /// <summary>
    /// Registers all permission-based authorization policies.
    /// Each permission code "xxx.yyy" gets a policy named "Permission:xxx.yyy".
    /// </summary>
    public static AuthorizationBuilder AddPermissionPolicies(this AuthorizationBuilder builder)
    {
        foreach (var code in AllPermissionCodes)
        {
            builder.AddPolicy($"Permission:{code}", policy =>
                policy.RequireAuthenticatedUser()
                      .AddRequirements(new PermissionRequirement(code)));
        }

        return builder;
    }
}
