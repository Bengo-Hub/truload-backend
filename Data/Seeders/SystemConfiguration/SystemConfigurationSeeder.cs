using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Data.Seeders.SystemConfiguration;

/// <summary>
/// Seeds system configuration data: permit types, tolerance settings,
/// axle type fee schedules, demerit point schedules, and penalty schedules.
/// Implements Kenya Traffic Act Cap 403 and EAC Act 2016 regulatory requirements.
/// Idempotent - safe to run multiple times.
/// </summary>
public class SystemConfigurationSeeder
{
    private readonly TruLoadDbContext _context;

    public SystemConfigurationSeeder(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync()
    {
        await SeedPermitTypesAsync();
        await SeedToleranceSettingsAsync();
        await SeedAxleTypeOverloadFeeSchedulesAsync();
        await SeedDemeritPointSchedulesAsync();
        await SeedPenaltySchedulesAsync();
        await SeedApplicationSettingsAsync();
    }

    /// <summary>
    /// Seeds default application settings for password policy, shifts, backup, and security.
    /// </summary>
    private async Task SeedApplicationSettingsAsync()
    {
        var settings = new[]
        {
            // Password Policy Settings
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.PasswordMinLength,
                SettingValue = "8",
                SettingType = "Integer",
                Category = SettingKeys.CategorySecurity,
                DisplayName = "Minimum Password Length",
                Description = "Minimum number of characters required for passwords",
                DefaultValue = "8",
                IsEditable = true,
                SortOrder = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.PasswordRequireUppercase,
                SettingValue = "true",
                SettingType = "Boolean",
                Category = SettingKeys.CategorySecurity,
                DisplayName = "Require Uppercase",
                Description = "Require at least one uppercase letter in passwords",
                DefaultValue = "true",
                IsEditable = true,
                SortOrder = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.PasswordRequireLowercase,
                SettingValue = "true",
                SettingType = "Boolean",
                Category = SettingKeys.CategorySecurity,
                DisplayName = "Require Lowercase",
                Description = "Require at least one lowercase letter in passwords",
                DefaultValue = "true",
                IsEditable = true,
                SortOrder = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.PasswordRequireDigit,
                SettingValue = "true",
                SettingType = "Boolean",
                Category = SettingKeys.CategorySecurity,
                DisplayName = "Require Digit",
                Description = "Require at least one digit in passwords",
                DefaultValue = "true",
                IsEditable = true,
                SortOrder = 4,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.PasswordRequireSpecial,
                SettingValue = "false",
                SettingType = "Boolean",
                Category = SettingKeys.CategorySecurity,
                DisplayName = "Require Special Character",
                Description = "Require at least one special character (!@#$%^&*) in passwords",
                DefaultValue = "false",
                IsEditable = true,
                SortOrder = 5,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.PasswordLockoutThreshold,
                SettingValue = "5",
                SettingType = "Integer",
                Category = SettingKeys.CategorySecurity,
                DisplayName = "Lockout Threshold",
                Description = "Number of failed login attempts before account lockout",
                DefaultValue = "5",
                IsEditable = true,
                SortOrder = 6,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.PasswordLockoutMinutes,
                SettingValue = "30",
                SettingType = "Integer",
                Category = SettingKeys.CategorySecurity,
                DisplayName = "Lockout Duration (minutes)",
                Description = "Duration of account lockout in minutes",
                DefaultValue = "30",
                IsEditable = true,
                SortOrder = 7,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.PasswordExpiryDays,
                SettingValue = "0",
                SettingType = "Integer",
                Category = SettingKeys.CategorySecurity,
                DisplayName = "Password Expiry (days)",
                Description = "Number of days after which password expires; 0 = no expiry. User must change password before next login.",
                DefaultValue = "0",
                IsEditable = true,
                SortOrder = 8,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },

            // Two-Factor Authentication Settings
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.TwoFactorEnabled,
                SettingValue = "true",
                SettingType = "Boolean",
                Category = SettingKeys.CategorySecurity,
                DisplayName = "Enable Two-Factor Authentication",
                Description = "Allow users to enable 2FA for their accounts",
                DefaultValue = "true",
                IsEditable = true,
                SortOrder = 10,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.TwoFactorEnforceForAdmin,
                SettingValue = "true",
                SettingType = "Boolean",
                Category = SettingKeys.CategorySecurity,
                DisplayName = "Require 2FA for Administrators",
                Description = "Enforce two-factor authentication for users with admin roles",
                DefaultValue = "true",
                IsEditable = true,
                SortOrder = 11,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.TwoFactorBackupCodesCount,
                SettingValue = "10",
                SettingType = "Integer",
                Category = SettingKeys.CategorySecurity,
                DisplayName = "Backup Codes Count",
                Description = "Number of one-time backup codes generated for 2FA recovery",
                DefaultValue = "10",
                IsEditable = true,
                SortOrder = 12,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },

            // Shift Settings
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.ShiftDefaultDuration,
                SettingValue = "8",
                SettingType = "Integer",
                Category = SettingKeys.CategoryShifts,
                DisplayName = "Default Shift Duration (hours)",
                Description = "Default duration of a work shift in hours",
                DefaultValue = "8",
                IsEditable = true,
                SortOrder = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.ShiftGraceMinutes,
                SettingValue = "15",
                SettingType = "Integer",
                Category = SettingKeys.CategoryShifts,
                DisplayName = "Grace Period (minutes)",
                Description = "Grace period in minutes for shift start/end times",
                DefaultValue = "15",
                IsEditable = true,
                SortOrder = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.ShiftEnforceOnLogin,
                SettingValue = "false",
                SettingType = "Boolean",
                Category = SettingKeys.CategoryShifts,
                DisplayName = "Enforce Shift on Login",
                Description = "When enabled, users outside their shift hours are locked out at login",
                DefaultValue = "false",
                IsEditable = true,
                SortOrder = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.ShiftBypassCheck,
                SettingValue = "false",
                SettingType = "Boolean",
                Category = SettingKeys.CategoryShifts,
                DisplayName = "Global Bypass Shift Check",
                Description = "Temporarily bypass all shift checks (e.g. during shift transitions)",
                DefaultValue = "false",
                IsEditable = true,
                SortOrder = 4,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.ShiftExcludedRoles,
                SettingValue = "",
                SettingType = "String",
                Category = SettingKeys.CategoryShifts,
                DisplayName = "Roles Excluded from Shift Check",
                Description = "Comma-separated role codes that bypass shift enforcement",
                DefaultValue = "",
                IsEditable = true,
                SortOrder = 5,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.ShiftRequire2FA,
                SettingValue = "false",
                SettingType = "Boolean",
                Category = SettingKeys.CategoryShifts,
                DisplayName = "Require 2FA for Shift Actions",
                Description = "Require two-factor authentication for shift clock-in/out",
                DefaultValue = "false",
                IsEditable = true,
                SortOrder = 6,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },

            // Compliance Settings
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.DefaultActCode,
                SettingValue = "TRAFFIC_ACT",
                SettingType = "String",
                Category = SettingKeys.CategoryCompliance,
                DisplayName = "Default Act for Charging & Compliance",
                Description = "Default legal act code used for overload violations and compliance checks. Options: TRAFFIC_ACT, EAC_ACT",
                DefaultValue = "TRAFFIC_ACT",
                IsEditable = true,
                SortOrder = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },

            // Backup Settings
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.BackupEnabled,
                SettingValue = "true",
                SettingType = "Boolean",
                Category = SettingKeys.CategoryBackup,
                DisplayName = "Enable Automatic Backups",
                Description = "Enable scheduled automatic database backups",
                DefaultValue = "true",
                IsEditable = true,
                SortOrder = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.BackupScheduleCron,
                SettingValue = "0 0 * * *",
                SettingType = "String",
                Category = SettingKeys.CategoryBackup,
                DisplayName = "Backup Schedule (Cron)",
                Description = "Cron expression for backup schedule (default: daily at midnight)",
                DefaultValue = "0 0 * * *",
                IsEditable = true,
                SortOrder = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.BackupRetentionDays,
                SettingValue = "30",
                SettingType = "Integer",
                Category = SettingKeys.CategoryBackup,
                DisplayName = "Backup Retention (days)",
                Description = "Number of days to retain backup files before deletion",
                DefaultValue = "30",
                IsEditable = true,
                SortOrder = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.BackupStoragePath,
                SettingValue = "/var/backups/truload",
                SettingType = "String",
                Category = SettingKeys.CategoryBackup,
                DisplayName = "Backup Storage Path",
                Description = "Directory path where backup files are stored",
                DefaultValue = "/var/backups/truload",
                IsEditable = true,
                SortOrder = 4,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },

            // Rate Limiting Settings
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.RateLimitGlobalAuthenticatedPermit,
                SettingValue = "600",
                SettingType = "Integer",
                Category = SettingKeys.CategoryRateLimiting,
                DisplayName = "Global Authenticated Permit Limit",
                Description = "Maximum requests per window for authenticated users across all endpoints",
                DefaultValue = "600",
                IsEditable = true,
                SortOrder = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.RateLimitGlobalAuthenticatedWindowMinutes,
                SettingValue = "1",
                SettingType = "Integer",
                Category = SettingKeys.CategoryRateLimiting,
                DisplayName = "Global Authenticated Window (minutes)",
                Description = "Time window in minutes for the global authenticated rate limit",
                DefaultValue = "1",
                IsEditable = true,
                SortOrder = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.RateLimitGlobalAnonymousPermit,
                SettingValue = "30",
                SettingType = "Integer",
                Category = SettingKeys.CategoryRateLimiting,
                DisplayName = "Global Anonymous Permit Limit",
                Description = "Maximum requests per minute for unauthenticated users",
                DefaultValue = "30",
                IsEditable = true,
                SortOrder = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.RateLimitDashboardPermit,
                SettingValue = "800",
                SettingType = "Integer",
                Category = SettingKeys.CategoryRateLimiting,
                DisplayName = "Dashboard Permit Limit",
                Description = "Maximum requests per minute for dashboard endpoints",
                DefaultValue = "800",
                IsEditable = true,
                SortOrder = 4,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.RateLimitApiPermit,
                SettingValue = "200",
                SettingType = "Integer",
                Category = SettingKeys.CategoryRateLimiting,
                DisplayName = "API Permit Limit",
                Description = "Maximum requests per minute for general API endpoints",
                DefaultValue = "200",
                IsEditable = true,
                SortOrder = 5,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.RateLimitWeighingPermit,
                SettingValue = "600",
                SettingType = "Integer",
                Category = SettingKeys.CategoryRateLimiting,
                DisplayName = "Weighing Permit Limit",
                Description = "Maximum requests per minute for weighing endpoints",
                DefaultValue = "600",
                IsEditable = true,
                SortOrder = 6,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.RateLimitAutoweighPermit,
                SettingValue = "1000",
                SettingType = "Integer",
                Category = SettingKeys.CategoryRateLimiting,
                DisplayName = "Auto-Weigh Permit Limit",
                Description = "Maximum requests per minute for automated weighbridge endpoints",
                DefaultValue = "1000",
                IsEditable = true,
                SortOrder = 7,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.RateLimitAuthPermit,
                SettingValue = "10",
                SettingType = "Integer",
                Category = SettingKeys.CategoryRateLimiting,
                DisplayName = "Auth Permit Limit",
                Description = "Maximum authentication requests per window (login/register/password reset)",
                DefaultValue = "10",
                IsEditable = true,
                SortOrder = 8,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.RateLimitAuthWindowMinutes,
                SettingValue = "5",
                SettingType = "Integer",
                Category = SettingKeys.CategoryRateLimiting,
                DisplayName = "Auth Window (minutes)",
                Description = "Time window in minutes for authentication rate limiting",
                DefaultValue = "5",
                IsEditable = true,
                SortOrder = 9,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.RateLimitReportsPermit,
                SettingValue = "30",
                SettingType = "Integer",
                Category = SettingKeys.CategoryRateLimiting,
                DisplayName = "Reports Permit Limit",
                Description = "Maximum report generation requests per minute",
                DefaultValue = "30",
                IsEditable = true,
                SortOrder = 10,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.RateLimitSearchPermit,
                SettingValue = "120",
                SettingType = "Integer",
                Category = SettingKeys.CategoryRateLimiting,
                DisplayName = "Search Permit Limit",
                Description = "Maximum search requests per minute",
                DefaultValue = "120",
                IsEditable = true,
                SortOrder = 11,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },

            // Weighing Settings
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.WeighingMaxReweighCycles,
                SettingValue = "8",
                SettingType = "Integer",
                Category = SettingKeys.CategoryWeighing,
                DisplayName = "Max Re-weigh Cycles",
                Description = "Maximum number of re-weigh attempts allowed per weighing transaction before requiring supervisor override",
                DefaultValue = "8",
                IsEditable = true,
                SortOrder = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.WeighingOperationalToleranceKg,
                SettingValue = "200",
                SettingType = "Integer",
                Category = SettingKeys.CategoryWeighing,
                DisplayName = "Operational Tolerance (kg)",
                Description = "Operational tolerance in kilograms for minor overloads that trigger auto-release with warning instead of yard detention",
                DefaultValue = "200",
                IsEditable = true,
                SortOrder = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },

            // Financial Settings
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.FinancialDefaultForexRate,
                SettingValue = "130.0",
                SettingType = "Decimal",
                Category = SettingKeys.CategoryFinancial,
                DisplayName = "Default Forex Rate (KES/USD)",
                Description = "Default KES to USD exchange rate used for financial calculations when live rate is unavailable",
                DefaultValue = "130.0",
                IsEditable = true,
                SortOrder = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.FinancialInvoiceAgingCurrentDays,
                SettingValue = "30",
                SettingType = "Integer",
                Category = SettingKeys.CategoryFinancial,
                DisplayName = "Invoice Aging - Current (days)",
                Description = "Number of days an invoice is considered 'current' before becoming overdue in aging reports",
                DefaultValue = "30",
                IsEditable = true,
                SortOrder = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.FinancialInvoiceAgingOverdueDays,
                SettingValue = "60",
                SettingType = "Integer",
                Category = SettingKeys.CategoryFinancial,
                DisplayName = "Invoice Aging - Overdue (days)",
                Description = "Number of days after which an invoice is classified as 'overdue' in aging reports",
                DefaultValue = "60",
                IsEditable = true,
                SortOrder = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },

            // Cache Settings
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.CacheSettingsTtlMinutes,
                SettingValue = "5",
                SettingType = "Integer",
                Category = SettingKeys.CategoryCache,
                DisplayName = "Settings Cache TTL (minutes)",
                Description = "Time-to-live in minutes for cached application settings before refresh from database",
                DefaultValue = "5",
                IsEditable = true,
                SortOrder = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.CachePermissionsTtlMinutes,
                SettingValue = "60",
                SettingType = "Integer",
                Category = SettingKeys.CategoryCache,
                DisplayName = "Permissions Cache TTL (minutes)",
                Description = "Time-to-live in minutes for cached user permissions before refresh",
                DefaultValue = "60",
                IsEditable = true,
                SortOrder = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.CacheIntegrationKenhaTtlMinutes,
                SettingValue = "60",
                SettingType = "Integer",
                Category = SettingKeys.CategoryCache,
                DisplayName = "KeNHA Cache TTL (minutes)",
                Description = "Time-to-live in minutes for cached KeNHA road network data",
                DefaultValue = "60",
                IsEditable = true,
                SortOrder = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.CacheIntegrationNtsaTtlHours,
                SettingValue = "24",
                SettingType = "Integer",
                Category = SettingKeys.CategoryCache,
                DisplayName = "NTSA Cache TTL (hours)",
                Description = "Time-to-live in hours for cached NTSA vehicle/driver lookup data",
                DefaultValue = "24",
                IsEditable = true,
                SortOrder = 4,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.CacheSupersetTokenTtlHours,
                SettingValue = "4",
                SettingType = "Integer",
                Category = SettingKeys.CategoryCache,
                DisplayName = "Superset Token Cache TTL (hours)",
                Description = "Time-to-live in hours for cached Superset guest tokens",
                DefaultValue = "4",
                IsEditable = true,
                SortOrder = 5,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },

            // Integration Timeout Settings
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.IntegrationEcitizenTimeoutSeconds,
                SettingValue = "30",
                SettingType = "Integer",
                Category = SettingKeys.CategoryIntegrations,
                DisplayName = "eCitizen Timeout (seconds)",
                Description = "HTTP request timeout in seconds for eCitizen payment gateway integration",
                DefaultValue = "30",
                IsEditable = true,
                SortOrder = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.IntegrationKenhaTimeoutSeconds,
                SettingValue = "15",
                SettingType = "Integer",
                Category = SettingKeys.CategoryIntegrations,
                DisplayName = "KeNHA Timeout (seconds)",
                Description = "HTTP request timeout in seconds for KeNHA road network API",
                DefaultValue = "15",
                IsEditable = true,
                SortOrder = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.IntegrationNtsaTimeoutSeconds,
                SettingValue = "30",
                SettingType = "Integer",
                Category = SettingKeys.CategoryIntegrations,
                DisplayName = "NTSA Timeout (seconds)",
                Description = "HTTP request timeout in seconds for NTSA vehicle/driver lookup API",
                DefaultValue = "30",
                IsEditable = true,
                SortOrder = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.IntegrationOllamaTimeoutSeconds,
                SettingValue = "180",
                SettingType = "Integer",
                Category = SettingKeys.CategoryIntegrations,
                DisplayName = "Ollama Timeout (seconds)",
                Description = "HTTP request timeout in seconds for Ollama AI text-to-SQL generation",
                DefaultValue = "180",
                IsEditable = true,
                SortOrder = 4,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },

            // Notification Settings
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.NotificationEmailEnabled,
                SettingValue = "true",
                SettingType = "Boolean",
                Category = SettingKeys.CategoryNotifications,
                DisplayName = "Enable Email Notifications",
                Description = "Enable sending email notifications via the centralized notifications service",
                DefaultValue = "true",
                IsEditable = true,
                SortOrder = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.NotificationSmsEnabled,
                SettingValue = "false",
                SettingType = "Boolean",
                Category = SettingKeys.CategoryNotifications,
                DisplayName = "Enable SMS Notifications",
                Description = "Enable sending SMS notifications for critical alerts and OTP delivery",
                DefaultValue = "false",
                IsEditable = true,
                SortOrder = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.NotificationPushEnabled,
                SettingValue = "true",
                SettingType = "Boolean",
                Category = SettingKeys.CategoryNotifications,
                DisplayName = "Enable Push Notifications",
                Description = "Enable browser push notifications for real-time alerts to PWA users",
                DefaultValue = "true",
                IsEditable = true,
                SortOrder = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.NotificationServiceUrl,
                SettingValue = "http://notifications-service.notifications.svc.cluster.local:4000",
                SettingType = "String",
                Category = SettingKeys.CategoryNotifications,
                DisplayName = "Notification Service URL",
                Description = "Base URL of the centralized Go notifications-service",
                DefaultValue = "http://notifications-service.notifications.svc.cluster.local:4000",
                IsEditable = true,
                SortOrder = 4,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.NotificationDefaultChannel,
                SettingValue = "email",
                SettingType = "String",
                Category = SettingKeys.CategoryNotifications,
                DisplayName = "Default Notification Channel",
                Description = "Default channel for notifications when not explicitly specified (email, sms, push)",
                DefaultValue = "email",
                IsEditable = true,
                SortOrder = 5,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.ProsecutionDefaultCourtId,
                SettingValue = "",
                SettingType = "String",
                Category = SettingKeys.CategoryProsecution,
                DisplayName = "Default Court",
                Description = "Default court for new prosecution cases (court ID). Used when creating prosecution from case register.",
                DefaultValue = "",
                IsEditable = true,
                SortOrder = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.ProsecutionDefaultComplainantOfficerId,
                SettingValue = "",
                SettingType = "String",
                Category = SettingKeys.CategoryProsecution,
                DisplayName = "Default Complainant Officer",
                Description = "Default complainant officer for prosecution (user ID). Used when creating prosecution from case register.",
                DefaultValue = "",
                IsEditable = true,
                SortOrder = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.ProsecutionDefaultDistrict,
                SettingValue = "",
                SettingType = "String",
                Category = SettingKeys.CategoryProsecution,
                DisplayName = "Default District (legacy text)",
                Description = "Legacy default district name; prefer Default Subcounty.",
                DefaultValue = "",
                IsEditable = true,
                SortOrder = 3,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.ProsecutionDefaultCountyId,
                SettingValue = "",
                SettingType = "String",
                Category = SettingKeys.CategoryProsecution,
                DisplayName = "Default County",
                Description = "Default county for prosecution cases (county ID). Used when creating prosecution from case register.",
                DefaultValue = "",
                IsEditable = true,
                SortOrder = 4,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.ProsecutionDefaultSubCountyId,
                SettingValue = "",
                SettingType = "String",
                Category = SettingKeys.CategoryProsecution,
                DisplayName = "Default Subcounty",
                Description = "Default subcounty/district for prosecution cases (subcounty ID). Used when creating prosecution from case register.",
                DefaultValue = "",
                IsEditable = true,
                SortOrder = 5,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = SettingKeys.ProsecutionDefaultRoadId,
                SettingValue = "",
                SettingType = "String",
                Category = SettingKeys.CategoryProsecution,
                DisplayName = "Default Road",
                Description = "Default road for prosecution cases (road ID). Used when creating prosecution from case register.",
                DefaultValue = "",
                IsEditable = true,
                SortOrder = 6,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        foreach (var setting in settings)
        {
            var existing = await _context.ApplicationSettings
                .FirstOrDefaultAsync(s => s.SettingKey == setting.SettingKey);

            if (existing == null)
            {
                await _context.ApplicationSettings.AddAsync(setting);
                Console.WriteLine($"✓ Seeded application setting: {setting.SettingKey}");
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedPermitTypesAsync()
    {
        var permitTypes = new[]
        {
            new PermitType
            {
                Id = Guid.NewGuid(),
                Code = "2A",
                Name = "Permit 2A - Single Journey",
                Description = "Single journey permit with axle and GVW extensions for overloaded vehicles",
                AxleExtensionKg = 3000,
                GvwExtensionKg = 1000,
                ValidityDays = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new PermitType
            {
                Id = Guid.NewGuid(),
                Code = "3A",
                Name = "Permit 3A - Multiple Journey",
                Description = "Multiple journey permit for vehicles requiring repeated overloading within validity period",
                AxleExtensionKg = 2000,
                GvwExtensionKg = 2000,
                ValidityDays = 30,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new PermitType
            {
                Id = Guid.NewGuid(),
                Code = "3B",
                Name = "Permit 3B - Extended Multiple Journey",
                Description = "Extended multiple journey permit with higher extensions for special cargo",
                AxleExtensionKg = 3000,
                GvwExtensionKg = 3000,
                ValidityDays = 90,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new PermitType
            {
                Id = Guid.NewGuid(),
                Code = "OVERLOAD",
                Name = "Overload Permit - Special Cargo",
                Description = "Special permit for exceptional cargo requiring significant weight extensions",
                AxleExtensionKg = 5000,
                GvwExtensionKg = 5000,
                ValidityDays = 7,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new PermitType
            {
                Id = Guid.NewGuid(),
                Code = "SPECIAL",
                Name = "Special Permit - Custom Configuration",
                Description = "Special permit with custom weight extensions determined case-by-case",
                AxleExtensionKg = 0,
                GvwExtensionKg = 0,
                ValidityDays = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        foreach (var permitType in permitTypes)
        {
            var existing = await _context.PermitTypes
                .FirstOrDefaultAsync(pt => pt.Code == permitType.Code);
            
            if (existing == null)
            {
                await _context.PermitTypes.AddAsync(permitType);
                Console.WriteLine($"✓ Seeded permit type: {permitType.Name} ({permitType.Code})");
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedToleranceSettingsAsync()
    {
        var effectiveDate = DateTime.UtcNow.Date;
        
        var toleranceSettings = new[]
        {
            new ToleranceSetting
            {
                Id = Guid.NewGuid(),
                Code = "EAC_GVW_TOLERANCE",
                Name = "EAC GVW Tolerance",
                LegalFramework = "EAC",
                TolerancePercentage = 5.0m,
                ToleranceKg = null,
                AppliesTo = "GVW",
                Description = "5% tolerance for Gross Vehicle Weight under EAC legal framework",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ToleranceSetting
            {
                Id = Guid.NewGuid(),
                Code = "EAC_AXLE_TOLERANCE",
                Name = "EAC Axle Weight Tolerance",
                LegalFramework = "EAC",
                TolerancePercentage = 5.0m,
                ToleranceKg = null,
                AppliesTo = "AXLE",
                Description = "5% tolerance for individual axle weights under EAC legal framework",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ToleranceSetting
            {
                Id = Guid.NewGuid(),
                Code = "TRAFFIC_ACT_GVW_TOLERANCE",
                Name = "Traffic Act GVW Tolerance",
                LegalFramework = "TRAFFIC_ACT",
                TolerancePercentage = 0.0m,
                ToleranceKg = null,
                AppliesTo = "GVW",
                Description = "Zero tolerance for Gross Vehicle Weight under Traffic Act (strict enforcement)",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ToleranceSetting
            {
                Id = Guid.NewGuid(),
                Code = "TRAFFIC_ACT_AXLE_TOLERANCE",
                Name = "Traffic Act Axle Weight Tolerance",
                LegalFramework = "TRAFFIC_ACT",
                TolerancePercentage = 0.0m,
                ToleranceKg = null,
                AppliesTo = "AXLE",
                Description = "Zero tolerance for individual axle weights under Traffic Act (strict enforcement)",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ToleranceSetting
            {
                Id = Guid.NewGuid(),
                Code = "BOTH_GVW_TOLERANCE",
                Name = "Combined Framework GVW Tolerance",
                LegalFramework = "BOTH",
                TolerancePercentage = 5.0m,
                ToleranceKg = null,
                AppliesTo = "GVW",
                Description = "Default 5% tolerance when both frameworks apply (use most lenient)",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ToleranceSetting
            {
                Id = Guid.NewGuid(),
                Code = "BOTH_AXLE_TOLERANCE",
                Name = "Combined Framework Axle Weight Tolerance",
                LegalFramework = "BOTH",
                TolerancePercentage = 5.0m,
                ToleranceKg = null,
                AppliesTo = "AXLE",
                Description = "Default 5% tolerance for axle weights when both frameworks apply (use most lenient)",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            // Operational tolerance for auto-release warning (200kg default)
            new ToleranceSetting
            {
                Id = Guid.NewGuid(),
                Code = "OPERATIONAL_TOLERANCE",
                Name = "Operational Auto-Release Tolerance",
                LegalFramework = "BOTH",
                TolerancePercentage = 0.0m,
                ToleranceKg = 200,
                AppliesTo = "OPERATIONAL",
                Description = "Operational tolerance (200kg) for minor overloads - auto-release with warning, no yard detention",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        foreach (var setting in toleranceSettings)
        {
            var existing = await _context.ToleranceSettings
                .FirstOrDefaultAsync(ts => ts.Code == setting.Code);
            
            if (existing == null)
            {
                await _context.ToleranceSettings.AddAsync(setting);
                Console.WriteLine($"✓ Seeded tolerance setting: {setting.Name} ({setting.Code})");
            }
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds axle type-specific overload fee schedules per Kenya Traffic Act Cap 403.
    /// Different axle types have different fee rates based on overload amount.
    /// </summary>
    private async Task SeedAxleTypeOverloadFeeSchedulesAsync()
    {
        var effectiveDate = DateTime.UtcNow.Date;

        // Kenya Traffic Act Cap 403 fee structure - per axle type
        // Fee bands based on overload ranges with type-specific rates
        var feeSchedules = new[]
        {
            // Band 1: 0-2000 kg overload
            new AxleTypeOverloadFeeSchedule
            {
                OverloadMinKg = 0,
                OverloadMaxKg = 2000,
                SteeringAxleFeeUsd = 50.00m,
                SingleDriveAxleFeeUsd = 75.00m,
                TandemAxleFeeUsd = 100.00m,
                TridemAxleFeeUsd = 125.00m,
                QuadAxleFeeUsd = 150.00m,
                LegalFramework = "TRAFFIC_ACT",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null
            },
            // Band 2: 2001-5000 kg overload
            new AxleTypeOverloadFeeSchedule
            {
                OverloadMinKg = 2001,
                OverloadMaxKg = 5000,
                SteeringAxleFeeUsd = 100.00m,
                SingleDriveAxleFeeUsd = 150.00m,
                TandemAxleFeeUsd = 200.00m,
                TridemAxleFeeUsd = 250.00m,
                QuadAxleFeeUsd = 300.00m,
                LegalFramework = "TRAFFIC_ACT",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null
            },
            // Band 3: 5001-10000 kg overload
            new AxleTypeOverloadFeeSchedule
            {
                OverloadMinKg = 5001,
                OverloadMaxKg = 10000,
                SteeringAxleFeeUsd = 200.00m,
                SingleDriveAxleFeeUsd = 300.00m,
                TandemAxleFeeUsd = 400.00m,
                TridemAxleFeeUsd = 500.00m,
                QuadAxleFeeUsd = 600.00m,
                LegalFramework = "TRAFFIC_ACT",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null
            },
            // Band 4: 10001-20000 kg overload
            new AxleTypeOverloadFeeSchedule
            {
                OverloadMinKg = 10001,
                OverloadMaxKg = 20000,
                SteeringAxleFeeUsd = 400.00m,
                SingleDriveAxleFeeUsd = 600.00m,
                TandemAxleFeeUsd = 800.00m,
                TridemAxleFeeUsd = 1000.00m,
                QuadAxleFeeUsd = 1200.00m,
                LegalFramework = "TRAFFIC_ACT",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null
            },
            // Band 5: >20000 kg overload (severe)
            new AxleTypeOverloadFeeSchedule
            {
                OverloadMinKg = 20001,
                OverloadMaxKg = null,
                SteeringAxleFeeUsd = 800.00m,
                SingleDriveAxleFeeUsd = 1200.00m,
                TandemAxleFeeUsd = 1600.00m,
                TridemAxleFeeUsd = 2000.00m,
                QuadAxleFeeUsd = 2400.00m,
                LegalFramework = "TRAFFIC_ACT",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null
            },

            // ── EAC Vehicle Load Control Act 2016 ──
            // Rates ~20% higher than Traffic Act (cross-border penalties)
            new AxleTypeOverloadFeeSchedule
            {
                OverloadMinKg = 0, OverloadMaxKg = 2000,
                SteeringAxleFeeUsd = 60.00m, SingleDriveAxleFeeUsd = 90.00m,
                TandemAxleFeeUsd = 120.00m, TridemAxleFeeUsd = 150.00m, QuadAxleFeeUsd = 180.00m,
                LegalFramework = "EAC", EffectiveFrom = effectiveDate, EffectiveTo = null
            },
            new AxleTypeOverloadFeeSchedule
            {
                OverloadMinKg = 2001, OverloadMaxKg = 5000,
                SteeringAxleFeeUsd = 120.00m, SingleDriveAxleFeeUsd = 180.00m,
                TandemAxleFeeUsd = 240.00m, TridemAxleFeeUsd = 300.00m, QuadAxleFeeUsd = 360.00m,
                LegalFramework = "EAC", EffectiveFrom = effectiveDate, EffectiveTo = null
            },
            new AxleTypeOverloadFeeSchedule
            {
                OverloadMinKg = 5001, OverloadMaxKg = 10000,
                SteeringAxleFeeUsd = 240.00m, SingleDriveAxleFeeUsd = 360.00m,
                TandemAxleFeeUsd = 480.00m, TridemAxleFeeUsd = 600.00m, QuadAxleFeeUsd = 720.00m,
                LegalFramework = "EAC", EffectiveFrom = effectiveDate, EffectiveTo = null
            },
            new AxleTypeOverloadFeeSchedule
            {
                OverloadMinKg = 10001, OverloadMaxKg = 20000,
                SteeringAxleFeeUsd = 480.00m, SingleDriveAxleFeeUsd = 720.00m,
                TandemAxleFeeUsd = 960.00m, TridemAxleFeeUsd = 1200.00m, QuadAxleFeeUsd = 1440.00m,
                LegalFramework = "EAC", EffectiveFrom = effectiveDate, EffectiveTo = null
            },
            new AxleTypeOverloadFeeSchedule
            {
                OverloadMinKg = 20001, OverloadMaxKg = null,
                SteeringAxleFeeUsd = 960.00m, SingleDriveAxleFeeUsd = 1440.00m,
                TandemAxleFeeUsd = 1920.00m, TridemAxleFeeUsd = 2400.00m, QuadAxleFeeUsd = 2880.00m,
                LegalFramework = "EAC", EffectiveFrom = effectiveDate, EffectiveTo = null
            }
        };

        foreach (var schedule in feeSchedules)
        {
            var existing = await _context.AxleTypeOverloadFeeSchedules
                .FirstOrDefaultAsync(f => f.OverloadMinKg == schedule.OverloadMinKg
                    && f.LegalFramework == schedule.LegalFramework);

            if (existing == null)
            {
                await _context.AxleTypeOverloadFeeSchedules.AddAsync(schedule);
                Console.WriteLine($"✓ Seeded axle type fee schedule: {schedule.OverloadMinKg}-{schedule.OverloadMaxKg ?? 999999}kg");
            }
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds demerit point schedules per Kenya Traffic Act Cap 403 Section 117A.
    /// Points are assigned based on violation type and overload severity.
    /// </summary>
    private async Task SeedDemeritPointSchedulesAsync()
    {
        var effectiveDate = DateTime.UtcNow.Date;

        // Kenya Traffic Act Cap 403 Section 117A - NTSA Demerit Points System
        var demeritSchedules = new[]
        {
            // Steering axle violations (lower impact = lower points)
            new DemeritPointSchedule { ViolationType = "STEERING", OverloadMinKg = 0, OverloadMaxKg = 2000, Points = 1, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "STEERING", OverloadMinKg = 2001, OverloadMaxKg = 5000, Points = 2, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "STEERING", OverloadMinKg = 5001, OverloadMaxKg = 10000, Points = 3, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "STEERING", OverloadMinKg = 10001, OverloadMaxKg = 20000, Points = 5, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "STEERING", OverloadMinKg = 20001, OverloadMaxKg = null, Points = 10, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },

            // Single drive axle violations
            new DemeritPointSchedule { ViolationType = "SINGLE_DRIVE", OverloadMinKg = 0, OverloadMaxKg = 2000, Points = 1, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "SINGLE_DRIVE", OverloadMinKg = 2001, OverloadMaxKg = 5000, Points = 2, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "SINGLE_DRIVE", OverloadMinKg = 5001, OverloadMaxKg = 10000, Points = 3, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "SINGLE_DRIVE", OverloadMinKg = 10001, OverloadMaxKg = 20000, Points = 5, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "SINGLE_DRIVE", OverloadMinKg = 20001, OverloadMaxKg = null, Points = 10, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },

            // Tandem axle violations (grouped = stricter)
            new DemeritPointSchedule { ViolationType = "TANDEM", OverloadMinKg = 0, OverloadMaxKg = 2000, Points = 2, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TANDEM", OverloadMinKg = 2001, OverloadMaxKg = 5000, Points = 3, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TANDEM", OverloadMinKg = 5001, OverloadMaxKg = 10000, Points = 4, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TANDEM", OverloadMinKg = 10001, OverloadMaxKg = 20000, Points = 6, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TANDEM", OverloadMinKg = 20001, OverloadMaxKg = null, Points = 12, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },

            // Tridem axle violations (highest impact group)
            new DemeritPointSchedule { ViolationType = "TRIDEM", OverloadMinKg = 0, OverloadMaxKg = 2000, Points = 2, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TRIDEM", OverloadMinKg = 2001, OverloadMaxKg = 5000, Points = 3, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TRIDEM", OverloadMinKg = 5001, OverloadMaxKg = 10000, Points = 5, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TRIDEM", OverloadMinKg = 10001, OverloadMaxKg = 20000, Points = 7, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TRIDEM", OverloadMinKg = 20001, OverloadMaxKg = null, Points = 15, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },

            // GVW violations (overall vehicle weight)
            new DemeritPointSchedule { ViolationType = "GVW", OverloadMinKg = 0, OverloadMaxKg = 2000, Points = 1, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "GVW", OverloadMinKg = 2001, OverloadMaxKg = 5000, Points = 2, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "GVW", OverloadMinKg = 5001, OverloadMaxKg = 10000, Points = 3, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "GVW", OverloadMinKg = 10001, OverloadMaxKg = 20000, Points = 5, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "GVW", OverloadMinKg = 20001, OverloadMaxKg = null, Points = 10, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },

            // ── EAC Vehicle Load Control Act 2016 ──
            // Slightly higher demerit points for cross-border transit violations
            new DemeritPointSchedule { ViolationType = "STEERING", OverloadMinKg = 0, OverloadMaxKg = 2000, Points = 1, LegalFramework = "EAC", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "STEERING", OverloadMinKg = 2001, OverloadMaxKg = 5000, Points = 2, LegalFramework = "EAC", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "STEERING", OverloadMinKg = 5001, OverloadMaxKg = 10000, Points = 4, LegalFramework = "EAC", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "STEERING", OverloadMinKg = 10001, OverloadMaxKg = 20000, Points = 6, LegalFramework = "EAC", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "STEERING", OverloadMinKg = 20001, OverloadMaxKg = null, Points = 12, LegalFramework = "EAC", EffectiveFrom = effectiveDate },

            new DemeritPointSchedule { ViolationType = "SINGLE_DRIVE", OverloadMinKg = 0, OverloadMaxKg = 2000, Points = 1, LegalFramework = "EAC", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "SINGLE_DRIVE", OverloadMinKg = 2001, OverloadMaxKg = 5000, Points = 2, LegalFramework = "EAC", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "SINGLE_DRIVE", OverloadMinKg = 5001, OverloadMaxKg = 10000, Points = 4, LegalFramework = "EAC", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "SINGLE_DRIVE", OverloadMinKg = 10001, OverloadMaxKg = 20000, Points = 6, LegalFramework = "EAC", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "SINGLE_DRIVE", OverloadMinKg = 20001, OverloadMaxKg = null, Points = 12, LegalFramework = "EAC", EffectiveFrom = effectiveDate },

            new DemeritPointSchedule { ViolationType = "TANDEM", OverloadMinKg = 0, OverloadMaxKg = 2000, Points = 2, LegalFramework = "EAC", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TANDEM", OverloadMinKg = 2001, OverloadMaxKg = 5000, Points = 3, LegalFramework = "EAC", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TANDEM", OverloadMinKg = 5001, OverloadMaxKg = 10000, Points = 5, LegalFramework = "EAC", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TANDEM", OverloadMinKg = 10001, OverloadMaxKg = 20000, Points = 8, LegalFramework = "EAC", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TANDEM", OverloadMinKg = 20001, OverloadMaxKg = null, Points = 14, LegalFramework = "EAC", EffectiveFrom = effectiveDate },

            new DemeritPointSchedule { ViolationType = "TRIDEM", OverloadMinKg = 0, OverloadMaxKg = 2000, Points = 2, LegalFramework = "EAC", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TRIDEM", OverloadMinKg = 2001, OverloadMaxKg = 5000, Points = 4, LegalFramework = "EAC", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TRIDEM", OverloadMinKg = 5001, OverloadMaxKg = 10000, Points = 6, LegalFramework = "EAC", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TRIDEM", OverloadMinKg = 10001, OverloadMaxKg = 20000, Points = 9, LegalFramework = "EAC", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TRIDEM", OverloadMinKg = 20001, OverloadMaxKg = null, Points = 15, LegalFramework = "EAC", EffectiveFrom = effectiveDate },

            new DemeritPointSchedule { ViolationType = "GVW", OverloadMinKg = 0, OverloadMaxKg = 2000, Points = 1, LegalFramework = "EAC", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "GVW", OverloadMinKg = 2001, OverloadMaxKg = 5000, Points = 3, LegalFramework = "EAC", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "GVW", OverloadMinKg = 5001, OverloadMaxKg = 10000, Points = 4, LegalFramework = "EAC", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "GVW", OverloadMinKg = 10001, OverloadMaxKg = 20000, Points = 6, LegalFramework = "EAC", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "GVW", OverloadMinKg = 20001, OverloadMaxKg = null, Points = 12, LegalFramework = "EAC", EffectiveFrom = effectiveDate }
        };

        foreach (var schedule in demeritSchedules)
        {
            var existing = await _context.DemeritPointSchedules
                .FirstOrDefaultAsync(d => d.ViolationType == schedule.ViolationType
                    && d.OverloadMinKg == schedule.OverloadMinKg
                    && d.LegalFramework == schedule.LegalFramework);

            if (existing == null)
            {
                await _context.DemeritPointSchedules.AddAsync(schedule);
                Console.WriteLine($"✓ Seeded demerit points: {schedule.ViolationType} {schedule.OverloadMinKg}-{schedule.OverloadMaxKg ?? 999999}kg = {schedule.Points} points");
            }
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds penalty schedules based on accumulated demerit points.
    /// Implements NTSA license management penalties per Traffic Act Cap 403 Section 117A.
    /// </summary>
    private async Task SeedPenaltySchedulesAsync()
    {
        var penaltySchedules = new[]
        {
            new PenaltySchedule
            {
                PointsMin = 1,
                PointsMax = 3,
                PenaltyDescription = "Warning letter issued. Transporter informed of violation and instructed to ensure future compliance.",
                SuspensionDays = null,
                RequiresCourt = false,
                AdditionalFineUsd = 0m,
                AdditionalFineKes = 0m
            },
            new PenaltySchedule
            {
                PointsMin = 4,
                PointsMax = 6,
                PenaltyDescription = "Vehicle inspection required. Transporter must present vehicle for inspection within 30 days.",
                SuspensionDays = null,
                RequiresCourt = false,
                AdditionalFineUsd = 50m,
                AdditionalFineKes = 7500m
            },
            new PenaltySchedule
            {
                PointsMin = 7,
                PointsMax = 9,
                PenaltyDescription = "Driver license under review. Driver must attend NTSA safety course within 60 days.",
                SuspensionDays = null,
                RequiresCourt = false,
                AdditionalFineUsd = 100m,
                AdditionalFineKes = 15000m
            },
            new PenaltySchedule
            {
                PointsMin = 10,
                PointsMax = 13,
                PenaltyDescription = "License suspension - 6 months. Driver prohibited from operating commercial vehicles.",
                SuspensionDays = 180,
                RequiresCourt = false,
                AdditionalFineUsd = 200m,
                AdditionalFineKes = 30000m
            },
            new PenaltySchedule
            {
                PointsMin = 14,
                PointsMax = 19,
                PenaltyDescription = "License suspension - 1 year. Driver prohibited from operating commercial vehicles. Transporter fined.",
                SuspensionDays = 365,
                RequiresCourt = true,
                AdditionalFineUsd = 500m,
                AdditionalFineKes = 75000m
            },
            new PenaltySchedule
            {
                PointsMin = 20,
                PointsMax = null,
                PenaltyDescription = "License suspension - 2 years. Mandatory court prosecution. Possible imprisonment per Traffic Act.",
                SuspensionDays = 730,
                RequiresCourt = true,
                AdditionalFineUsd = 1000m,
                AdditionalFineKes = 150000m
            }
        };

        foreach (var penalty in penaltySchedules)
        {
            var existing = await _context.PenaltySchedules
                .FirstOrDefaultAsync(p => p.PointsMin == penalty.PointsMin);

            if (existing == null)
            {
                await _context.PenaltySchedules.AddAsync(penalty);
                Console.WriteLine($"✓ Seeded penalty schedule: {penalty.PointsMin}-{penalty.PointsMax ?? 999} points = {penalty.PenaltyDescription.Substring(0, Math.Min(50, penalty.PenaltyDescription.Length))}...");
            }
        }

        await _context.SaveChangesAsync();
    }
}
