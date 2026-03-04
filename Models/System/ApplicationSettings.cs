using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.System;

/// <summary>
/// System-wide application settings stored as key-value pairs.
/// Supports multiple setting types including JSON for complex configurations.
/// Use for: Password policies, shift defaults, security settings, feature flags, etc.
/// </summary>
[Table("application_settings")]
public class ApplicationSettings : BaseEntity
{
    /// <summary>
    /// Unique setting key (e.g., "security.password_policy", "shifts.default_duration").
    /// Use dot notation for namespacing.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string SettingKey { get; set; } = string.Empty;

    /// <summary>
    /// Setting value stored as string. Complex values stored as JSON.
    /// </summary>
    [Required]
    public string SettingValue { get; set; } = string.Empty;

    /// <summary>
    /// Type of the setting value for proper deserialization.
    /// Options: String, Boolean, Integer, Decimal, Json, DateTime
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string SettingType { get; set; } = "String";

    /// <summary>
    /// Category for grouping settings in UI (e.g., "Security", "Shifts", "Notifications").
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name for the setting.
    /// </summary>
    [MaxLength(100)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Description of what this setting controls.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Whether this setting can be modified by administrators.
    /// System-critical settings should be false.
    /// </summary>
    public bool IsEditable { get; set; } = true;

    /// <summary>
    /// Default value for the setting (used when resetting to defaults).
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Optional validation rules (JSON schema or regex pattern).
    /// </summary>
    public string? ValidationRules { get; set; }

    /// <summary>
    /// Sort order for display in UI.
    /// </summary>
    public int SortOrder { get; set; } = 0;
}

/// <summary>
/// Well-known setting keys as constants for type-safe access.
/// </summary>
public static class SettingKeys
{
    // Security - Password Policy
    public const string PasswordMinLength = "security.password_min_length";
    public const string PasswordRequireUppercase = "security.password_require_uppercase";
    public const string PasswordRequireLowercase = "security.password_require_lowercase";
    public const string PasswordRequireDigit = "security.password_require_digit";
    public const string PasswordRequireSpecial = "security.password_require_special";
    public const string PasswordLockoutThreshold = "security.password_lockout_threshold";
    public const string PasswordLockoutMinutes = "security.password_lockout_minutes";
    public const string PasswordExpiryDays = "security.password_expiry_days";

    // Security - Two-Factor Authentication
    public const string TwoFactorEnabled = "security.two_factor_enabled";
    public const string TwoFactorEnforceForAdmin = "security.two_factor_enforce_admin";
    public const string TwoFactorBackupCodesCount = "security.two_factor_backup_codes_count";

    // Shifts
    public const string ShiftDefaultDuration = "shifts.default_duration_hours";
    public const string ShiftGraceMinutes = "shifts.grace_minutes";
    public const string ShiftEnforceOnLogin = "shifts.enforce_on_login";
    public const string ShiftBypassCheck = "shifts.bypass_shift_check";
    public const string ShiftExcludedRoles = "shifts.excluded_roles";
    public const string ShiftRequire2FA = "shifts.require_2fa";

    // Backup
    public const string BackupEnabled = "backup.enabled";
    public const string BackupScheduleCron = "backup.schedule_cron";
    public const string BackupRetentionDays = "backup.retention_days";
    public const string BackupStoragePath = "backup.storage_path";

    // Compliance
    public const string DefaultActCode = "compliance.default_act_code";

    // Rate Limiting
    public const string RateLimitGlobalAuthenticatedPermit = "ratelimit.global_authenticated_permit";
    public const string RateLimitGlobalAuthenticatedWindowMinutes = "ratelimit.global_authenticated_window_minutes";
    public const string RateLimitGlobalAnonymousPermit = "ratelimit.global_anonymous_permit";
    public const string RateLimitDashboardPermit = "ratelimit.dashboard_permit";
    public const string RateLimitApiPermit = "ratelimit.api_permit";
    public const string RateLimitWeighingPermit = "ratelimit.weighing_permit";
    public const string RateLimitAutoweighPermit = "ratelimit.autoweigh_permit";
    public const string RateLimitAuthPermit = "ratelimit.auth_permit";
    public const string RateLimitAuthWindowMinutes = "ratelimit.auth_window_minutes";
    public const string RateLimitReportsPermit = "ratelimit.reports_permit";
    public const string RateLimitSearchPermit = "ratelimit.search_permit";

    // Weighing
    public const string WeighingMaxReweighCycles = "weighing.max_reweigh_cycles";
    public const string WeighingOperationalToleranceKg = "weighing.operational_tolerance_kg";

    // Financial
    public const string FinancialDefaultForexRate = "financial.default_forex_rate";
    public const string FinancialInvoiceAgingCurrentDays = "financial.invoice_aging_current_days";
    public const string FinancialInvoiceAgingOverdueDays = "financial.invoice_aging_overdue_days";

    // Cache
    public const string CacheSettingsTtlMinutes = "cache.settings_ttl_minutes";
    public const string CachePermissionsTtlMinutes = "cache.permissions_ttl_minutes";
    public const string CacheIntegrationKenhaTtlMinutes = "cache.integration_kenha_ttl_minutes";
    public const string CacheIntegrationNtsaTtlHours = "cache.integration_ntsa_ttl_hours";
    public const string CacheSupersetTokenTtlHours = "cache.superset_token_ttl_hours";

    // Integration Timeouts
    public const string IntegrationEcitizenTimeoutSeconds = "integration.ecitizen_timeout_seconds";
    public const string IntegrationKenhaTimeoutSeconds = "integration.kenha_timeout_seconds";
    public const string IntegrationNtsaTimeoutSeconds = "integration.ntsa_timeout_seconds";
    public const string IntegrationOllamaTimeoutSeconds = "integration.ollama_timeout_seconds";

    // Notifications
    public const string NotificationEmailEnabled = "notification.email_enabled";
    public const string NotificationSmsEnabled = "notification.sms_enabled";
    public const string NotificationPushEnabled = "notification.push_enabled";
    public const string NotificationServiceUrl = "notification.service_url";
    public const string NotificationDefaultChannel = "notification.default_channel";

    // Prosecution defaults (courts, complainant, location hierarchy, road)
    public const string ProsecutionDefaultCourtId = "prosecution.default_court_id";
    public const string ProsecutionDefaultComplainantOfficerId = "prosecution.default_complainant_officer_id";
    public const string ProsecutionDefaultDistrict = "prosecution.default_district";
    public const string ProsecutionDefaultCountyId = "prosecution.default_county_id";
    public const string ProsecutionDefaultSubCountyId = "prosecution.default_subcounty_id";
    public const string ProsecutionDefaultRoadId = "prosecution.default_road_id";

    // Categories
    public const string CategorySecurity = "Security";
    public const string CategoryShifts = "Shifts";
    public const string CategoryBackup = "Backup";
    public const string CategoryNotifications = "Notifications";
    public const string CategoryIntegrations = "Integrations";
    public const string CategoryCompliance = "Compliance";
    public const string CategoryRateLimiting = "Rate Limiting";
    public const string CategoryWeighing = "Weighing";
    public const string CategoryFinancial = "Financial";
    public const string CategoryCache = "Cache";
    public const string CategoryProsecution = "Prosecution";
}
