using System.ComponentModel.DataAnnotations;
using TruLoad.Backend.DTOs.Shared;
namespace TruLoad.Backend.DTOs.Settings;


/// <summary>
/// DTO for application settings returned from API.
/// </summary>
public record ApplicationSettingDto
{
    public Guid Id { get; init; }
    public string SettingKey { get; init; } = string.Empty;
    public string SettingValue { get; init; } = string.Empty;
    public string SettingType { get; init; } = "String";
    public string Category { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public bool IsEditable { get; init; }
    public string? DefaultValue { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Request to update a single setting.
/// </summary>
public record UpdateSettingRequest
{
    [Required]
    public string SettingKey { get; init; } = string.Empty;

    [Required]
    public string SettingValue { get; init; } = string.Empty;
}

/// <summary>
/// Request to update multiple settings at once.
/// </summary>
public record UpdateSettingsBatchRequest
{
    [Required]
    public List<UpdateSettingRequest> Settings { get; init; } = new();
}

/// <summary>
/// Password policy configuration DTO.
/// </summary>
public record PasswordPolicyDto
{
    public int MinLength { get; init; } = 8;
    public bool RequireUppercase { get; init; } = true;
    public bool RequireLowercase { get; init; } = true;
    public bool RequireDigit { get; init; } = true;
    public bool RequireSpecial { get; init; } = false;
    public int LockoutThreshold { get; init; } = 5;
    public int LockoutMinutes { get; init; } = 15;
}

/// <summary>
/// Request to update password policy.
/// </summary>
public record UpdatePasswordPolicyRequest
{
    [Range(6, 50)]
    public int MinLength { get; init; } = 8;

    public bool RequireUppercase { get; init; } = true;
    public bool RequireLowercase { get; init; } = true;
    public bool RequireDigit { get; init; } = true;
    public bool RequireSpecial { get; init; } = false;

    [Range(0, 100)]
    public int LockoutThreshold { get; init; } = 5;

    [Range(0, 1440)]
    public int LockoutMinutes { get; init; } = 15;
}

/// <summary>
/// Shift settings configuration DTO.
/// Includes lockout/bypass fields adapted from KenloadV2.
/// </summary>
public record ShiftSettingsDto
{
    public int DefaultShiftDuration { get; init; } = 8;
    public int GraceMinutes { get; init; } = 15;
    public bool EnforceShiftOnLogin { get; init; } = false;
    public bool BypassShiftCheck { get; init; } = false;
    public string ExcludedRoles { get; init; } = string.Empty;
    public bool Require2FA { get; init; } = false;
}

/// <summary>
/// Request to update shift settings.
/// </summary>
public record UpdateShiftSettingsRequest
{
    [Range(1, 24)]
    public int DefaultShiftDuration { get; init; } = 8;

    [Range(0, 120)]
    public int GraceMinutes { get; init; } = 15;

    /// <summary>When true, users outside their shift hours are locked out at login.</summary>
    public bool EnforceShiftOnLogin { get; init; } = false;

    /// <summary>Global bypass flag — temporarily disables all shift checks (e.g. during shift transitions).</summary>
    public bool BypassShiftCheck { get; init; } = false;

    /// <summary>Comma-separated role codes that bypass shift enforcement.</summary>
    [MaxLength(500)]
    public string ExcludedRoles { get; init; } = string.Empty;

    public bool Require2FA { get; init; } = false;
}

/// <summary>
/// Backup settings configuration DTO.
/// </summary>
public record BackupSettingsDto
{
    public bool Enabled { get; init; } = true;
    public string ScheduleCron { get; init; } = "0 2 * * *"; // Daily at 2 AM
    public int RetentionDays { get; init; } = 30;
    public string StoragePath { get; init; } = "./backups";
}

/// <summary>
/// Security overview DTO combining multiple security-related settings.
/// </summary>
public record SecurityOverviewDto
{
    public PasswordPolicyDto PasswordPolicy { get; init; } = new();
    public bool TwoFactorEnabled { get; init; }
    public bool TwoFactorEnforcedForAdmin { get; init; }
}

/// <summary>
/// Settings category group for UI display.
/// </summary>
public record SettingsCategoryDto
{
    public string Category { get; init; } = string.Empty;
    public List<ApplicationSettingDto> Settings { get; init; } = new();
}
