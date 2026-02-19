using TruLoad.Backend.Services.Interfaces.System;

namespace TruLoad.Backend.Services.BackgroundJobs;

/// <summary>
/// Hangfire recurring job for automated database backups.
/// Follows the ExchangeRateSyncJob pattern using IServiceScopeFactory.
/// </summary>
public class BackupScheduleJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackupScheduleJob> _logger;

    public BackupScheduleJob(
        IServiceScopeFactory scopeFactory,
        ILogger<BackupScheduleJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("[BackupScheduleJob] Automated backup started");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();

            // Check if backup is enabled
            var backupSettings = await settingsService.GetBackupSettingsAsync();
            if (!backupSettings.Enabled)
            {
                _logger.LogInformation("[BackupScheduleJob] Backup is disabled in settings, skipping");
                return;
            }

            // Create automated backup
            var result = await backupService.CreateBackupAsync(
                new DTOs.System.CreateBackupRequest { BackupType = "full", Description = "Automated scheduled backup" },
                Guid.Empty); // System-initiated backup

            if (result.Success)
            {
                _logger.LogInformation("[BackupScheduleJob] Automated backup created: {FileName} ({Size} bytes)",
                    result.FileName, result.FileSizeBytes);
            }
            else
            {
                _logger.LogError("[BackupScheduleJob] Automated backup failed: {Message}", result.Message);
            }

            // Cleanup old backups based on retention policy
            await CleanupOldBackupsAsync(backupService, backupSettings.RetentionDays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BackupScheduleJob] Automated backup job failed");
            throw;
        }
    }

    private async Task CleanupOldBackupsAsync(IBackupService backupService, int retentionDays)
    {
        if (retentionDays <= 0) return;

        try
        {
            var backups = await backupService.ListBackupsAsync();
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            var expiredBackups = backups.Backups
                .Where(b => b.CreatedAt < cutoffDate)
                .ToList();

            foreach (var backup in expiredBackups)
            {
                var deleted = await backupService.DeleteBackupAsync(backup.FileName, Guid.Empty);
                if (deleted)
                {
                    _logger.LogInformation("[BackupScheduleJob] Deleted expired backup: {FileName} (created {CreatedAt})",
                        backup.FileName, backup.CreatedAt);
                }
            }

            if (expiredBackups.Count > 0)
            {
                _logger.LogInformation("[BackupScheduleJob] Cleaned up {Count} expired backups (retention: {Days} days)",
                    expiredBackups.Count, retentionDays);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BackupScheduleJob] Failed to cleanup old backups");
        }
    }
}
