using TruLoad.Backend.DTOs.Settings;

namespace TruLoad.Backend.Services.Interfaces.System;

/// <summary>
/// Service interface for managing application-wide settings.
/// Provides type-safe access to configuration values with caching.
/// </summary>
public interface ISettingsService
{
    // Generic settings access
    Task<List<ApplicationSettingDto>> GetAllSettingsAsync(CancellationToken ct = default);
    Task<List<ApplicationSettingDto>> GetSettingsByCategoryAsync(string category, CancellationToken ct = default);
    Task<ApplicationSettingDto?> GetSettingAsync(string key, CancellationToken ct = default);
    Task<T> GetSettingValueAsync<T>(string key, T defaultValue, CancellationToken ct = default);
    Task<ApplicationSettingDto> UpdateSettingAsync(string key, string value, Guid userId, CancellationToken ct = default);
    Task<List<ApplicationSettingDto>> UpdateSettingsBatchAsync(List<UpdateSettingRequest> settings, Guid userId, CancellationToken ct = default);

    // Password policy
    Task<PasswordPolicyDto> GetPasswordPolicyAsync(CancellationToken ct = default);
    Task<PasswordPolicyDto> UpdatePasswordPolicyAsync(UpdatePasswordPolicyRequest request, Guid userId, CancellationToken ct = default);

    // Shift settings
    Task<ShiftSettingsDto> GetShiftSettingsAsync(CancellationToken ct = default);
    Task<ShiftSettingsDto> UpdateShiftSettingsAsync(UpdateShiftSettingsRequest request, Guid userId, CancellationToken ct = default);

    // Backup settings
    Task<BackupSettingsDto> GetBackupSettingsAsync(CancellationToken ct = default);

    // Security overview
    Task<SecurityOverviewDto> GetSecurityOverviewAsync(CancellationToken ct = default);

    // Cache management
    void InvalidateCache();
}
