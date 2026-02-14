using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Tests.Integration.Helpers;
using Xunit;
using FluentAssertions;

namespace TruLoad.Backend.Tests.Integration.Security;

/// <summary>
/// Integration tests for system security settings CRUD operations using the ApplicationSettings model.
/// Tests persistence of password policy, 2FA enforcement, and lockout configuration
/// stored as key-value pairs in the application_settings table.
/// </summary>
public class SecuritySettingsTests : IAsyncLifetime
{
    private TruLoadDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _context = TestDbContextFactory.Create();
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    #region Helpers

    /// <summary>
    /// Seeds default security settings that mirror a typical production seed.
    /// </summary>
    private async Task SeedDefaultSecuritySettings()
    {
        var defaults = new[]
        {
            new ApplicationSettings
            {
                SettingKey = SettingKeys.PasswordMinLength,
                SettingValue = "8",
                SettingType = "Integer",
                Category = SettingKeys.CategorySecurity,
                DisplayName = "Minimum Password Length",
                Description = "Minimum number of characters required for passwords",
                IsEditable = true,
                DefaultValue = "8",
                SortOrder = 1
            },
            new ApplicationSettings
            {
                SettingKey = SettingKeys.PasswordRequireUppercase,
                SettingValue = "true",
                SettingType = "Boolean",
                Category = SettingKeys.CategorySecurity,
                DisplayName = "Require Uppercase",
                Description = "Require at least one uppercase letter in passwords",
                IsEditable = true,
                DefaultValue = "true",
                SortOrder = 2
            },
            new ApplicationSettings
            {
                SettingKey = SettingKeys.PasswordRequireLowercase,
                SettingValue = "true",
                SettingType = "Boolean",
                Category = SettingKeys.CategorySecurity,
                DisplayName = "Require Lowercase",
                Description = "Require at least one lowercase letter in passwords",
                IsEditable = true,
                DefaultValue = "true",
                SortOrder = 3
            },
            new ApplicationSettings
            {
                SettingKey = SettingKeys.PasswordRequireDigit,
                SettingValue = "true",
                SettingType = "Boolean",
                Category = SettingKeys.CategorySecurity,
                DisplayName = "Require Digit",
                Description = "Require at least one digit in passwords",
                IsEditable = true,
                DefaultValue = "true",
                SortOrder = 4
            },
            new ApplicationSettings
            {
                SettingKey = SettingKeys.PasswordRequireSpecial,
                SettingValue = "true",
                SettingType = "Boolean",
                Category = SettingKeys.CategorySecurity,
                DisplayName = "Require Special Character",
                Description = "Require at least one special character in passwords",
                IsEditable = true,
                DefaultValue = "true",
                SortOrder = 5
            },
            new ApplicationSettings
            {
                SettingKey = SettingKeys.PasswordLockoutThreshold,
                SettingValue = "5",
                SettingType = "Integer",
                Category = SettingKeys.CategorySecurity,
                DisplayName = "Lockout Threshold",
                Description = "Number of failed login attempts before account lockout",
                IsEditable = true,
                DefaultValue = "5",
                SortOrder = 6
            },
            new ApplicationSettings
            {
                SettingKey = SettingKeys.PasswordLockoutMinutes,
                SettingValue = "30",
                SettingType = "Integer",
                Category = SettingKeys.CategorySecurity,
                DisplayName = "Lockout Duration (minutes)",
                Description = "Duration of account lockout after exceeding failed attempts",
                IsEditable = true,
                DefaultValue = "30",
                SortOrder = 7
            },
            new ApplicationSettings
            {
                SettingKey = SettingKeys.TwoFactorEnabled,
                SettingValue = "false",
                SettingType = "Boolean",
                Category = SettingKeys.CategorySecurity,
                DisplayName = "Enable Two-Factor Authentication",
                Description = "Allow users to enable two-factor authentication",
                IsEditable = true,
                DefaultValue = "false",
                SortOrder = 8
            },
            new ApplicationSettings
            {
                SettingKey = SettingKeys.TwoFactorEnforceForAdmin,
                SettingValue = "false",
                SettingType = "Boolean",
                Category = SettingKeys.CategorySecurity,
                DisplayName = "Enforce 2FA for Admins",
                Description = "Require two-factor authentication for all admin users",
                IsEditable = true,
                DefaultValue = "false",
                SortOrder = 9
            },
            new ApplicationSettings
            {
                SettingKey = SettingKeys.TwoFactorBackupCodesCount,
                SettingValue = "10",
                SettingType = "Integer",
                Category = SettingKeys.CategorySecurity,
                DisplayName = "Backup Codes Count",
                Description = "Number of backup codes generated when 2FA is enabled",
                IsEditable = true,
                DefaultValue = "10",
                SortOrder = 10
            }
        };

        _context.ApplicationSettings.AddRange(defaults);
        await _context.SaveChangesAsync();
    }

    #endregion

    #region Read Security Settings

    [Fact]
    public async Task ReadSecuritySettings_ShouldReturnDefaults()
    {
        // Arrange
        await SeedDefaultSecuritySettings();

        // Act
        var securitySettings = await _context.ApplicationSettings
            .Where(s => s.Category == SettingKeys.CategorySecurity)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();

        // Assert
        securitySettings.Should().HaveCount(10, "should have 10 default security settings");

        var minLength = securitySettings.First(s => s.SettingKey == SettingKeys.PasswordMinLength);
        minLength.SettingValue.Should().Be("8");
        minLength.SettingType.Should().Be("Integer");
        minLength.IsEditable.Should().BeTrue();

        var requireUppercase = securitySettings.First(s => s.SettingKey == SettingKeys.PasswordRequireUppercase);
        requireUppercase.SettingValue.Should().Be("true");
        requireUppercase.SettingType.Should().Be("Boolean");
    }

    [Fact]
    public async Task ReadSecuritySettings_ByKey_ShouldReturnSpecificSetting()
    {
        // Arrange
        await SeedDefaultSecuritySettings();

        // Act
        var lockoutThreshold = await _context.ApplicationSettings
            .FirstOrDefaultAsync(s => s.SettingKey == SettingKeys.PasswordLockoutThreshold);

        // Assert
        lockoutThreshold.Should().NotBeNull();
        lockoutThreshold!.SettingValue.Should().Be("5");
        lockoutThreshold.Category.Should().Be(SettingKeys.CategorySecurity);
        lockoutThreshold.DisplayName.Should().Be("Lockout Threshold");
    }

    [Fact]
    public async Task ReadSecuritySettings_EmptyDatabase_ShouldReturnEmpty()
    {
        // Act - no seeding
        var settings = await _context.ApplicationSettings
            .Where(s => s.Category == SettingKeys.CategorySecurity)
            .ToListAsync();

        // Assert
        settings.Should().BeEmpty("no settings seeded yet");
    }

    #endregion

    #region Update Password Policy

    [Fact]
    public async Task UpdatePasswordPolicy_MinLength_ShouldPersist()
    {
        // Arrange
        await SeedDefaultSecuritySettings();

        // Act - update minimum password length from 8 to 12
        var setting = await _context.ApplicationSettings
            .FirstAsync(s => s.SettingKey == SettingKeys.PasswordMinLength);

        setting.SettingValue = "12";
        setting.UpdatedAt = DateTime.UtcNow;
        _context.ApplicationSettings.Update(setting);
        await _context.SaveChangesAsync();

        // Assert
        var updated = await _context.ApplicationSettings
            .FirstOrDefaultAsync(s => s.SettingKey == SettingKeys.PasswordMinLength);

        updated.Should().NotBeNull();
        updated!.SettingValue.Should().Be("12");
    }

    [Fact]
    public async Task UpdatePasswordPolicy_DisableSpecialCharRequirement_ShouldPersist()
    {
        // Arrange
        await SeedDefaultSecuritySettings();

        // Act
        var setting = await _context.ApplicationSettings
            .FirstAsync(s => s.SettingKey == SettingKeys.PasswordRequireSpecial);

        setting.SettingValue = "false";
        setting.UpdatedAt = DateTime.UtcNow;
        _context.ApplicationSettings.Update(setting);
        await _context.SaveChangesAsync();

        // Assert
        var updated = await _context.ApplicationSettings
            .FirstOrDefaultAsync(s => s.SettingKey == SettingKeys.PasswordRequireSpecial);

        updated.Should().NotBeNull();
        updated!.SettingValue.Should().Be("false");
    }

    [Fact]
    public async Task UpdatePasswordPolicy_LockoutSettings_ShouldPersist()
    {
        // Arrange
        await SeedDefaultSecuritySettings();

        // Act - update lockout threshold to 3 and duration to 60 minutes
        var threshold = await _context.ApplicationSettings
            .FirstAsync(s => s.SettingKey == SettingKeys.PasswordLockoutThreshold);
        threshold.SettingValue = "3";
        threshold.UpdatedAt = DateTime.UtcNow;

        var duration = await _context.ApplicationSettings
            .FirstAsync(s => s.SettingKey == SettingKeys.PasswordLockoutMinutes);
        duration.SettingValue = "60";
        duration.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Assert
        var updatedThreshold = await _context.ApplicationSettings
            .FirstOrDefaultAsync(s => s.SettingKey == SettingKeys.PasswordLockoutThreshold);
        updatedThreshold!.SettingValue.Should().Be("3");

        var updatedDuration = await _context.ApplicationSettings
            .FirstOrDefaultAsync(s => s.SettingKey == SettingKeys.PasswordLockoutMinutes);
        updatedDuration!.SettingValue.Should().Be("60");
    }

    #endregion

    #region Update 2FA Enforcement

    [Fact]
    public async Task Update2FAEnforcement_Enable_ShouldPersist()
    {
        // Arrange
        await SeedDefaultSecuritySettings();

        // Act - enable 2FA system-wide
        var setting = await _context.ApplicationSettings
            .FirstAsync(s => s.SettingKey == SettingKeys.TwoFactorEnabled);

        setting.SettingValue = "true";
        setting.UpdatedAt = DateTime.UtcNow;
        _context.ApplicationSettings.Update(setting);
        await _context.SaveChangesAsync();

        // Assert
        var updated = await _context.ApplicationSettings
            .FirstOrDefaultAsync(s => s.SettingKey == SettingKeys.TwoFactorEnabled);

        updated.Should().NotBeNull();
        updated!.SettingValue.Should().Be("true");
    }

    [Fact]
    public async Task Update2FAEnforcement_EnforceForAdmin_ShouldPersist()
    {
        // Arrange
        await SeedDefaultSecuritySettings();

        // Act - enforce 2FA for admin users
        var setting = await _context.ApplicationSettings
            .FirstAsync(s => s.SettingKey == SettingKeys.TwoFactorEnforceForAdmin);

        setting.SettingValue = "true";
        setting.UpdatedAt = DateTime.UtcNow;
        _context.ApplicationSettings.Update(setting);
        await _context.SaveChangesAsync();

        // Assert
        var updated = await _context.ApplicationSettings
            .FirstOrDefaultAsync(s => s.SettingKey == SettingKeys.TwoFactorEnforceForAdmin);

        updated.Should().NotBeNull();
        updated!.SettingValue.Should().Be("true");
    }

    [Fact]
    public async Task Update2FAEnforcement_BackupCodesCount_ShouldPersist()
    {
        // Arrange
        await SeedDefaultSecuritySettings();

        // Act - change backup codes count from 10 to 8
        var setting = await _context.ApplicationSettings
            .FirstAsync(s => s.SettingKey == SettingKeys.TwoFactorBackupCodesCount);

        setting.SettingValue = "8";
        setting.UpdatedAt = DateTime.UtcNow;
        _context.ApplicationSettings.Update(setting);
        await _context.SaveChangesAsync();

        // Assert
        var updated = await _context.ApplicationSettings
            .FirstOrDefaultAsync(s => s.SettingKey == SettingKeys.TwoFactorBackupCodesCount);

        updated.Should().NotBeNull();
        updated!.SettingValue.Should().Be("8");
    }

    #endregion

    #region Setting Metadata and Validation

    [Fact]
    public async Task SecuritySetting_NonEditable_ShouldBeStorable()
    {
        // Arrange - some system-critical settings should be non-editable
        var criticalSetting = new ApplicationSettings
        {
            SettingKey = "security.encryption_algorithm",
            SettingValue = "AES-256",
            SettingType = "String",
            Category = SettingKeys.CategorySecurity,
            DisplayName = "Encryption Algorithm",
            Description = "System encryption algorithm (read-only)",
            IsEditable = false,
            DefaultValue = "AES-256",
            SortOrder = 100
        };

        // Act
        _context.ApplicationSettings.Add(criticalSetting);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _context.ApplicationSettings
            .FirstOrDefaultAsync(s => s.SettingKey == "security.encryption_algorithm");

        saved.Should().NotBeNull();
        saved!.IsEditable.Should().BeFalse();
        saved.SettingValue.Should().Be("AES-256");
    }

    [Fact]
    public async Task SecuritySetting_DefaultValue_ShouldSupportReset()
    {
        // Arrange
        await SeedDefaultSecuritySettings();

        // Act - change setting, then reset to default
        var setting = await _context.ApplicationSettings
            .FirstAsync(s => s.SettingKey == SettingKeys.PasswordMinLength);

        setting.SettingValue = "16"; // changed from 8
        await _context.SaveChangesAsync();

        // Simulate "reset to default"
        setting.SettingValue = setting.DefaultValue!;
        setting.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Assert
        var reset = await _context.ApplicationSettings
            .FirstOrDefaultAsync(s => s.SettingKey == SettingKeys.PasswordMinLength);

        reset.Should().NotBeNull();
        reset!.SettingValue.Should().Be("8", "should be reset to default value");
        reset.SettingValue.Should().Be(reset.DefaultValue);
    }

    [Fact]
    public async Task SecuritySetting_ValidationRules_ShouldBePersistable()
    {
        // Arrange - setting with validation rules (e.g., min/max range)
        var setting = new ApplicationSettings
        {
            SettingKey = "security.session_timeout_minutes",
            SettingValue = "30",
            SettingType = "Integer",
            Category = SettingKeys.CategorySecurity,
            DisplayName = "Session Timeout",
            Description = "Minutes of inactivity before session expires",
            IsEditable = true,
            DefaultValue = "30",
            ValidationRules = "{\"min\": 5, \"max\": 480}",
            SortOrder = 20
        };

        // Act
        _context.ApplicationSettings.Add(setting);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _context.ApplicationSettings
            .FirstOrDefaultAsync(s => s.SettingKey == "security.session_timeout_minutes");

        saved.Should().NotBeNull();
        saved!.ValidationRules.Should().Contain("\"min\": 5");
        saved.ValidationRules.Should().Contain("\"max\": 480");
    }

    [Fact]
    public async Task SecuritySettings_SortOrder_ShouldMaintainDisplayOrder()
    {
        // Arrange
        await SeedDefaultSecuritySettings();

        // Act
        var orderedSettings = await _context.ApplicationSettings
            .Where(s => s.Category == SettingKeys.CategorySecurity)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();

        // Assert - settings should be in the expected display order
        orderedSettings.Should().HaveCount(10);
        orderedSettings[0].SettingKey.Should().Be(SettingKeys.PasswordMinLength);
        orderedSettings[1].SettingKey.Should().Be(SettingKeys.PasswordRequireUppercase);
        orderedSettings[2].SettingKey.Should().Be(SettingKeys.PasswordRequireLowercase);
        orderedSettings[3].SettingKey.Should().Be(SettingKeys.PasswordRequireDigit);
        orderedSettings[4].SettingKey.Should().Be(SettingKeys.PasswordRequireSpecial);
        orderedSettings[5].SettingKey.Should().Be(SettingKeys.PasswordLockoutThreshold);
        orderedSettings[6].SettingKey.Should().Be(SettingKeys.PasswordLockoutMinutes);
        orderedSettings[7].SettingKey.Should().Be(SettingKeys.TwoFactorEnabled);
        orderedSettings[8].SettingKey.Should().Be(SettingKeys.TwoFactorEnforceForAdmin);
        orderedSettings[9].SettingKey.Should().Be(SettingKeys.TwoFactorBackupCodesCount);
    }

    #endregion
}
