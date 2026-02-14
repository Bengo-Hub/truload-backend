using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Settings;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Services.Interfaces.System;

namespace TruLoad.Backend.Services.Implementations.System;

/// <summary>
/// Implementation of ISettingsService with in-memory caching.
/// Settings are cached for 5 minutes to reduce database queries.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly TruLoadDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SettingsService> _logger;
    private const string CacheKeyPrefix = "Settings_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public SettingsService(
        TruLoadDbContext context,
        IMemoryCache cache,
        ILogger<SettingsService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<ApplicationSettingDto>> GetAllSettingsAsync(CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}All";

        if (_cache.TryGetValue(cacheKey, out List<ApplicationSettingDto>? cached) && cached != null)
        {
            return cached;
        }

        var settings = await _context.Set<ApplicationSettings>()
            .Where(s => s.IsActive && s.DeletedAt == null)
            .OrderBy(s => s.Category)
            .ThenBy(s => s.SortOrder)
            .Select(s => MapToDto(s))
            .ToListAsync(ct);

        _cache.Set(cacheKey, settings, CacheDuration);
        return settings;
    }

    public async Task<List<ApplicationSettingDto>> GetSettingsByCategoryAsync(string category, CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}Category_{category}";

        if (_cache.TryGetValue(cacheKey, out List<ApplicationSettingDto>? cached) && cached != null)
        {
            return cached;
        }

        var settings = await _context.Set<ApplicationSettings>()
            .Where(s => s.IsActive && s.DeletedAt == null && s.Category == category)
            .OrderBy(s => s.SortOrder)
            .Select(s => MapToDto(s))
            .ToListAsync(ct);

        _cache.Set(cacheKey, settings, CacheDuration);
        return settings;
    }

    public async Task<ApplicationSettingDto?> GetSettingAsync(string key, CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}Key_{key}";

        if (_cache.TryGetValue(cacheKey, out ApplicationSettingDto? cached))
        {
            return cached;
        }

        var setting = await _context.Set<ApplicationSettings>()
            .Where(s => s.SettingKey == key && s.IsActive && s.DeletedAt == null)
            .Select(s => MapToDto(s))
            .FirstOrDefaultAsync(ct);

        if (setting != null)
        {
            _cache.Set(cacheKey, setting, CacheDuration);
        }

        return setting;
    }

    public async Task<T> GetSettingValueAsync<T>(string key, T defaultValue, CancellationToken ct = default)
    {
        var setting = await GetSettingAsync(key, ct);
        if (setting == null)
        {
            return defaultValue;
        }

        try
        {
            return ConvertValue<T>(setting.SettingValue, setting.SettingType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert setting {Key} to type {Type}, using default", key, typeof(T).Name);
            return defaultValue;
        }
    }

    public async Task<ApplicationSettingDto> UpdateSettingAsync(string key, string value, Guid userId, CancellationToken ct = default)
    {
        var setting = await _context.Set<ApplicationSettings>()
            .FirstOrDefaultAsync(s => s.SettingKey == key && s.DeletedAt == null, ct);

        if (setting == null)
        {
            throw new KeyNotFoundException($"Setting '{key}' not found");
        }

        if (!setting.IsEditable)
        {
            throw new InvalidOperationException($"Setting '{key}' is not editable");
        }

        setting.SettingValue = value;
        setting.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        InvalidateCache();

        _logger.LogInformation("Setting {Key} updated by user {UserId}", key, userId);
        return MapToDto(setting);
    }

    public async Task<List<ApplicationSettingDto>> UpdateSettingsBatchAsync(
        List<UpdateSettingRequest> settings,
        Guid userId,
        CancellationToken ct = default)
    {
        var keys = settings.Select(s => s.SettingKey).ToList();
        var existingSettings = await _context.Set<ApplicationSettings>()
            .Where(s => keys.Contains(s.SettingKey) && s.DeletedAt == null)
            .ToListAsync(ct);

        var results = new List<ApplicationSettingDto>();

        foreach (var request in settings)
        {
            var setting = existingSettings.FirstOrDefault(s => s.SettingKey == request.SettingKey);
            if (setting == null)
            {
                _logger.LogWarning("Setting {Key} not found, skipping", request.SettingKey);
                continue;
            }

            if (!setting.IsEditable)
            {
                _logger.LogWarning("Setting {Key} is not editable, skipping", request.SettingKey);
                continue;
            }

            setting.SettingValue = request.SettingValue;
            setting.UpdatedAt = DateTime.UtcNow;
            results.Add(MapToDto(setting));
        }

        await _context.SaveChangesAsync(ct);
        InvalidateCache();

        _logger.LogInformation("Updated {Count} settings by user {UserId}", results.Count, userId);
        return results;
    }

    public async Task<PasswordPolicyDto> GetPasswordPolicyAsync(CancellationToken ct = default)
    {
        return new PasswordPolicyDto
        {
            MinLength = await GetSettingValueAsync(SettingKeys.PasswordMinLength, 8, ct),
            RequireUppercase = await GetSettingValueAsync(SettingKeys.PasswordRequireUppercase, true, ct),
            RequireLowercase = await GetSettingValueAsync(SettingKeys.PasswordRequireLowercase, true, ct),
            RequireDigit = await GetSettingValueAsync(SettingKeys.PasswordRequireDigit, true, ct),
            RequireSpecial = await GetSettingValueAsync(SettingKeys.PasswordRequireSpecial, false, ct),
            LockoutThreshold = await GetSettingValueAsync(SettingKeys.PasswordLockoutThreshold, 5, ct),
            LockoutMinutes = await GetSettingValueAsync(SettingKeys.PasswordLockoutMinutes, 15, ct),
        };
    }

    public async Task<PasswordPolicyDto> UpdatePasswordPolicyAsync(
        UpdatePasswordPolicyRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        var updates = new List<UpdateSettingRequest>
        {
            new() { SettingKey = SettingKeys.PasswordMinLength, SettingValue = request.MinLength.ToString() },
            new() { SettingKey = SettingKeys.PasswordRequireUppercase, SettingValue = request.RequireUppercase.ToString() },
            new() { SettingKey = SettingKeys.PasswordRequireLowercase, SettingValue = request.RequireLowercase.ToString() },
            new() { SettingKey = SettingKeys.PasswordRequireDigit, SettingValue = request.RequireDigit.ToString() },
            new() { SettingKey = SettingKeys.PasswordRequireSpecial, SettingValue = request.RequireSpecial.ToString() },
            new() { SettingKey = SettingKeys.PasswordLockoutThreshold, SettingValue = request.LockoutThreshold.ToString() },
            new() { SettingKey = SettingKeys.PasswordLockoutMinutes, SettingValue = request.LockoutMinutes.ToString() },
        };

        await UpdateSettingsBatchAsync(updates, userId, ct);
        return await GetPasswordPolicyAsync(ct);
    }

    public async Task<ShiftSettingsDto> GetShiftSettingsAsync(CancellationToken ct = default)
    {
        return new ShiftSettingsDto
        {
            DefaultShiftDuration = await GetSettingValueAsync(SettingKeys.ShiftDefaultDuration, 8, ct),
            GraceMinutes = await GetSettingValueAsync(SettingKeys.ShiftGraceMinutes, 15, ct),
            EnforceShiftOnLogin = await GetSettingValueAsync(SettingKeys.ShiftEnforceOnLogin, false, ct),
            BypassShiftCheck = await GetSettingValueAsync(SettingKeys.ShiftBypassCheck, false, ct),
            ExcludedRoles = await GetSettingValueAsync(SettingKeys.ShiftExcludedRoles, "", ct),
            Require2FA = await GetSettingValueAsync(SettingKeys.ShiftRequire2FA, false, ct),
        };
    }

    public async Task<ShiftSettingsDto> UpdateShiftSettingsAsync(
        UpdateShiftSettingsRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        var updates = new List<UpdateSettingRequest>
        {
            new() { SettingKey = SettingKeys.ShiftDefaultDuration, SettingValue = request.DefaultShiftDuration.ToString() },
            new() { SettingKey = SettingKeys.ShiftGraceMinutes, SettingValue = request.GraceMinutes.ToString() },
            new() { SettingKey = SettingKeys.ShiftEnforceOnLogin, SettingValue = request.EnforceShiftOnLogin.ToString() },
            new() { SettingKey = SettingKeys.ShiftBypassCheck, SettingValue = request.BypassShiftCheck.ToString() },
            new() { SettingKey = SettingKeys.ShiftExcludedRoles, SettingValue = request.ExcludedRoles ?? "" },
            new() { SettingKey = SettingKeys.ShiftRequire2FA, SettingValue = request.Require2FA.ToString() },
        };

        await UpdateSettingsBatchAsync(updates, userId, ct);
        return await GetShiftSettingsAsync(ct);
    }

    public async Task<BackupSettingsDto> GetBackupSettingsAsync(CancellationToken ct = default)
    {
        return new BackupSettingsDto
        {
            Enabled = await GetSettingValueAsync(SettingKeys.BackupEnabled, true, ct),
            ScheduleCron = await GetSettingValueAsync(SettingKeys.BackupScheduleCron, "0 2 * * *", ct),
            RetentionDays = await GetSettingValueAsync(SettingKeys.BackupRetentionDays, 30, ct),
            StoragePath = await GetSettingValueAsync(SettingKeys.BackupStoragePath, "./backups", ct),
        };
    }

    public async Task<SecurityOverviewDto> GetSecurityOverviewAsync(CancellationToken ct = default)
    {
        return new SecurityOverviewDto
        {
            PasswordPolicy = await GetPasswordPolicyAsync(ct),
            TwoFactorEnabled = await GetSettingValueAsync(SettingKeys.TwoFactorEnabled, false, ct),
            TwoFactorEnforcedForAdmin = await GetSettingValueAsync(SettingKeys.TwoFactorEnforceForAdmin, false, ct),
        };
    }

    public async Task<ApplicationSettingDto> RestoreDefaultAsync(string key, Guid userId, CancellationToken ct = default)
    {
        var setting = await _context.ApplicationSettings
            .FirstOrDefaultAsync(s => s.SettingKey == key && s.DeletedAt == null, ct)
            ?? throw new KeyNotFoundException($"Setting '{key}' not found");

        if (string.IsNullOrEmpty(setting.DefaultValue))
            throw new InvalidOperationException($"Setting '{key}' has no default value defined");

        setting.SettingValue = setting.DefaultValue;
        setting.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        InvalidateCache();
        _logger.LogInformation("Setting '{Key}' restored to default by user {UserId}", key, userId);
        return MapToDto(setting);
    }

    public async Task<List<ApplicationSettingDto>> RestoreCategoryDefaultsAsync(string category, Guid userId, CancellationToken ct = default)
    {
        var settings = await _context.ApplicationSettings
            .Where(s => s.Category == category && s.DeletedAt == null && s.IsEditable && s.DefaultValue != null)
            .ToListAsync(ct);

        if (!settings.Any())
            throw new KeyNotFoundException($"No restorable settings found for category '{category}'");

        foreach (var setting in settings)
        {
            setting.SettingValue = setting.DefaultValue!;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
        InvalidateCache();
        _logger.LogInformation("Category '{Category}' settings ({Count}) restored to defaults by user {UserId}",
            category, settings.Count, userId);
        return settings.Select(MapToDto).ToList();
    }

    public void InvalidateCache()
    {
        // Remove all cached settings
        // In a distributed environment, use IDistributedCache instead
        _logger.LogDebug("Invalidating settings cache");
    }

    private static ApplicationSettingDto MapToDto(ApplicationSettings setting)
    {
        return new ApplicationSettingDto
        {
            Id = setting.Id,
            SettingKey = setting.SettingKey,
            SettingValue = setting.SettingValue,
            SettingType = setting.SettingType,
            Category = setting.Category,
            DisplayName = setting.DisplayName,
            Description = setting.Description,
            IsEditable = setting.IsEditable,
            DefaultValue = setting.DefaultValue,
            UpdatedAt = setting.UpdatedAt,
        };
    }

    private static T ConvertValue<T>(string value, string settingType)
    {
        var targetType = typeof(T);

        if (targetType == typeof(string))
        {
            return (T)(object)value;
        }

        if (targetType == typeof(bool))
        {
            return (T)(object)bool.Parse(value);
        }

        if (targetType == typeof(int))
        {
            return (T)(object)int.Parse(value);
        }

        if (targetType == typeof(decimal))
        {
            return (T)(object)decimal.Parse(value);
        }

        if (targetType == typeof(DateTime))
        {
            return (T)(object)DateTime.Parse(value);
        }

        // For complex types, deserialize from JSON
        if (settingType == "Json")
        {
            return global::System.Text.Json.JsonSerializer.Deserialize<T>(value)!;
        }

        throw new InvalidOperationException($"Cannot convert setting value to type {targetType.Name}");
    }
}
