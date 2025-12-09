using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using truload_backend.Data;

namespace TruLoad.Backend.Data.Seeders;

/// <summary>
/// Seeds 77 default permissions across 8 categories into the database.
/// Run once during application initialization.
/// </summary>
public static class PermissionSeeder
{
    /// <summary>
    /// Define all 77 permissions across 8 categories.
    /// Each permission has Code, Name, Category, and Description.
    /// </summary>
    private static readonly List<(string Code, string Name, string Category, string Description)> DefaultPermissions = new()
    {
        // Weighing Category (12 permissions)
        ("weighing.create", "Create Weighing", "Weighing", "Create new weighing records"),
        ("weighing.read", "Read All Weighing", "Weighing", "Read all weighing records"),
        ("weighing.read_own", "Read Own Weighing", "Weighing", "Read own weighing records only"),
        ("weighing.update", "Update Weighing", "Weighing", "Update existing weighing records"),
        ("weighing.approve", "Approve Weighing", "Weighing", "Approve weighing records"),
        ("weighing.override", "Override Weighing", "Weighing", "Override weighing validations and rules"),
        ("weighing.send_to_yard", "Send to Yard", "Weighing", "Send weighing to yard"),
        ("weighing.scale_test", "Scale Test", "Weighing", "Perform scale calibration and testing"),
        ("weighing.export", "Export Weighing", "Weighing", "Export weighing data"),
        ("weighing.delete", "Delete Weighing", "Weighing", "Delete weighing records"),
        ("weighing.webhook", "Weighing Webhook", "Weighing", "Manage weighing webhooks"),
        ("weighing.audit", "Audit Weighing", "Weighing", "View weighing audit logs"),

        // Case Category (15 permissions)
        ("case.create", "Create Case", "Case", "Create new cases"),
        ("case.read", "Read All Cases", "Case", "Read all cases"),
        ("case.read_own", "Read Own Cases", "Case", "Read own cases only"),
        ("case.update", "Update Case", "Case", "Update case details"),
        ("case.assign", "Assign Case", "Case", "Assign cases to users"),
        ("case.close", "Close Case", "Case", "Close cases"),
        ("case.escalate", "Escalate Case", "Case", "Escalate cases to higher level"),
        ("case.special_release", "Special Release", "Case", "Process special vehicle releases"),
        ("case.subfile_manage", "Manage Subfiles", "Case", "Manage case subfiles"),
        ("case.closure_review", "Review Closure", "Case", "Review case closures"),
        ("case.arrest_warrant", "Arrest Warrant", "Case", "Manage arrest warrants"),
        ("case.court_hearing", "Court Hearing", "Case", "Schedule and manage court hearings"),
        ("case.reweigh_schedule", "Schedule Reweigh", "Case", "Schedule reweighing"),
        ("case.export", "Export Cases", "Case", "Export case data"),
        ("case.audit", "Audit Cases", "Case", "View case audit logs"),

        // Prosecution Category (8 permissions)
        ("prosecution.create", "Create Prosecution", "Prosecution", "Create prosecution records"),
        ("prosecution.read", "Read All Prosecutions", "Prosecution", "Read all prosecutions"),
        ("prosecution.read_own", "Read Own Prosecutions", "Prosecution", "Read own prosecutions only"),
        ("prosecution.update", "Update Prosecution", "Prosecution", "Update prosecution records"),
        ("prosecution.compute_charges", "Compute Charges", "Prosecution", "Calculate prosecution charges"),
        ("prosecution.generate_certificate", "Generate Certificate", "Prosecution", "Generate prosecution certificates"),
        ("prosecution.export", "Export Prosecutions", "Prosecution", "Export prosecution data"),
        ("prosecution.audit", "Audit Prosecutions", "Prosecution", "View prosecution audit logs"),

        // User Category (10 permissions)
        ("user.create", "Create User", "User", "Create new users"),
        ("user.read", "Read All Users", "User", "Read all user records"),
        ("user.read_own", "Read Own User", "User", "Read own user record only"),
        ("user.update", "Update User", "User", "Update user details"),
        ("user.update_own", "Update Own User", "User", "Update own user record only"),
        ("user.delete", "Delete User", "User", "Delete user accounts"),
        ("user.assign_roles", "Assign Roles", "User", "Assign roles to users"),
        ("user.manage_permissions", "Manage Permissions", "User", "Manage user permissions"),
        ("user.manage_shifts", "Manage Shifts", "User", "Assign and manage user shifts"),
        ("user.audit", "Audit Users", "User", "View user audit logs"),

        // Station Category (12 permissions)
        ("station.read", "Read All Stations", "Station", "Read all station records"),
        ("station.read_own", "Read Own Station", "Station", "Read own station record only"),
        ("station.create", "Create Station", "Station", "Create new stations"),
        ("station.update", "Update Station", "Station", "Update station details"),
        ("station.update_own", "Update Own Station", "Station", "Update own station details only"),
        ("station.delete", "Delete Station", "Station", "Delete stations"),
        ("station.manage_staff", "Manage Staff", "Station", "Assign and manage station staff"),
        ("station.manage_devices", "Manage Devices", "Station", "Manage station devices (scales, cameras)"),
        ("station.manage_io", "Manage IO", "Station", "Manage station input/output configurations"),
        ("station.configure_defaults", "Configure Defaults", "Station", "Configure station default settings"),
        ("station.export", "Export Stations", "Station", "Export station data"),
        ("station.audit", "Audit Stations", "Station", "View station audit logs"),

        // Configuration Category (8 permissions)
        ("config.read", "Read Configuration", "Configuration", "Read system configurations"),
        ("config.manage_axle", "Manage Axle", "Configuration", "Configure axle types and references"),
        ("config.manage_permits", "Manage Permits", "Configuration", "Configure permit types and rules"),
        ("config.manage_fees", "Manage Fees", "Configuration", "Configure fee schedules"),
        ("config.manage_acts", "Manage Acts", "Configuration", "Configure legal acts and regulations"),
        ("config.manage_taxonomy", "Manage Taxonomy", "Configuration", "Configure system taxonomy and references"),
        ("config.manage_references", "Manage References", "Configuration", "Manage reference data"),
        ("config.audit", "Audit Configuration", "Configuration", "View configuration change audit logs"),

        // Analytics Category (8 permissions)
        ("analytics.read", "Read Analytics", "Analytics", "Read analytics and reports"),
        ("analytics.read_own", "Read Own Analytics", "Analytics", "Read own analytics only"),
        ("analytics.export", "Export Analytics", "Analytics", "Export analytics data"),
        ("analytics.schedule", "Schedule Reports", "Analytics", "Schedule report generation"),
        ("analytics.custom_query", "Custom Query", "Analytics", "Run custom analytics queries"),
        ("analytics.manage_dashboards", "Manage Dashboards", "Analytics", "Create and manage dashboards"),
        ("analytics.superset", "Superset Access", "Analytics", "Access Superset analytics platform"),
        ("analytics.audit", "Audit Analytics", "Analytics", "View analytics access audit logs"),

        // System Category (6 permissions)
        ("system.admin", "System Admin", "System", "Full system administration access"),
        ("system.audit_logs", "Audit Logs", "System", "View and manage audit logs"),
        ("system.cache_management", "Cache Management", "System", "Manage system cache"),
        ("system.integration_management", "Integration Management", "System", "Manage third-party integrations"),
        ("system.backup_restore", "Backup & Restore", "System", "Manage system backups and restoration"),
        ("system.security_policy", "Security Policy", "System", "Manage security policies and configurations")
    };

    /// <summary>
    /// Seed permissions into database if they don't exist.
    /// Idempotent - safe to run multiple times.
    /// </summary>
    public static async Task SeedAsync(TruLoadDbContext context)
    {
        // Check if permissions already exist
        if (await context.Permissions.AnyAsync())
            return; // Already seeded

        var permissions = new List<Permission>();

        foreach (var (code, name, category, description) in DefaultPermissions)
        {
            permissions.Add(new Permission
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = name,
                Category = category,
                Description = description,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        context.Permissions.AddRange(permissions);
        await context.SaveChangesAsync();
    }
}
