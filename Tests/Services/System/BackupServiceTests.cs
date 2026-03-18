using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.System;
using TruLoad.Backend.DTOs.Settings;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Services.Implementations.System;
using TruLoad.Backend.Services.Interfaces.System;
using TruLoad.Backend.Tests.Integration.Helpers;
using Xunit;
using FluentAssertions;

namespace TruLoad.Backend.Tests.Services.System;

public class BackupServiceTests : IAsyncLifetime
{
    private TruLoadDbContext _context = null!;
    private Mock<ISettingsService> _settingsServiceMock = null!;
    private Mock<IConfiguration> _configurationMock = null!;
    private Mock<ILogger<BackupService>> _loggerMock = null!;
    private BackupService _backupService = null!;

    public async Task InitializeAsync()
    {
        _context = TestDbContextFactory.Create();
        await _context.Database.EnsureCreatedAsync();

        _settingsServiceMock = new Mock<ISettingsService>();
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<BackupService>>();

        _backupService = new BackupService(
            _context,
            _settingsServiceMock.Object,
            _configurationMock.Object,
            _loggerMock.Object
        );
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetStatusAsync_ShouldReturnCorrectStatus()
    {
        // Arrange
        var settings = new BackupSettingsDto 
        { 
            Enabled = true, 
            ScheduleCron = "0 2 * * *", 
            StoragePath = "./backups", 
            RetentionDays = 30 
        };
        _settingsServiceMock.Setup(s => s.GetBackupSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        _settingsServiceMock.Setup(s => s.GetSettingValueAsync(SettingKeys.BackupStoragePath, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("./backups");
        _settingsServiceMock.Setup(s => s.GetSettingValueAsync(SettingKeys.BackupPgDumpPath, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("pg_dump");

        // Act
        var status = await _backupService.GetStatusAsync();

        // Assert
        status.IsEnabled.Should().BeTrue();
        status.ScheduleCron.Should().Be("0 2 * * *");
        status.StoragePath.Should().Be("./backups");
        status.RetentionDays.Should().Be(30);
    }

    [Fact]
    public async Task UpdateSettingsAsync_ShouldUpdateSettings()
    {
        // Arrange
        var request = new UpdateBackupSettingsRequest
        {
            IsEnabled = true,
            ScheduleCron = "0 3 * * *",
            StoragePath = "./new_backups",
            RetentionDays = 60,
            BackupPgDumpPath = "new_pg_dump"
        };
        var userId = Guid.NewGuid();

        // Act
        var result = await _backupService.UpdateSettingsAsync(request, userId);

        // Assert
        result.Should().BeTrue();
        _settingsServiceMock.Verify(s => s.UpdateSettingAsync(SettingKeys.BackupEnabled, "True", userId, It.IsAny<CancellationToken>()), Times.Once);
        _settingsServiceMock.Verify(s => s.UpdateSettingAsync(SettingKeys.BackupScheduleCron, "0 3 * * *", userId, It.IsAny<CancellationToken>()), Times.Once);
        _settingsServiceMock.Verify(s => s.UpdateSettingAsync(SettingKeys.BackupStoragePath, "./new_backups", userId, It.IsAny<CancellationToken>()), Times.Once);
        _settingsServiceMock.Verify(s => s.UpdateSettingAsync(SettingKeys.BackupRetentionDays, "60", userId, It.IsAny<CancellationToken>()), Times.Once);
        _settingsServiceMock.Verify(s => s.UpdateSettingAsync(SettingKeys.BackupPgDumpPath, "new_pg_dump", userId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
