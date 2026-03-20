using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.System;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Services.Interfaces.System;

namespace TruLoad.Backend.Services.Implementations.System;

/// <summary>
/// Implementation of IBackupService using pg_dump/pg_restore for PostgreSQL.
/// </summary>
public class BackupService : IBackupService
{
    private readonly TruLoadDbContext _context;
    private readonly ISettingsService _settingsService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BackupService> _logger;

    public BackupService(
        TruLoadDbContext context,
        ISettingsService settingsService,
        IConfiguration configuration,
        ILogger<BackupService> logger)
    {
        _context = context;
        _settingsService = settingsService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<BackupSystemStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetBackupSettingsAsync(ct);

        // Get storage path and check for existing backups
        var storagePath = await GetStoragePathAsync(ct);
        var backupFiles = GetBackupFiles(storagePath);

        var lastBackup = backupFiles.OrderByDescending(f => f.CreationTime).FirstOrDefault();

        // Calculate next scheduled backup based on cron expression
        DateTime? nextScheduled = null;
        if (settings.Enabled && !string.IsNullOrEmpty(settings.ScheduleCron))
        {
            nextScheduled = CalculateNextRun(settings.ScheduleCron);
        }

        return new BackupSystemStatusDto(
            IsEnabled: settings.Enabled,
            ScheduleCron: settings.ScheduleCron ?? "0 2 * * *",
            StoragePath: storagePath,
            BackupPgDumpPath: await GetPgDumpPathAsync(ct),
            RetentionDays: settings.RetentionDays,
            LastBackupAt: lastBackup?.CreationTime,
            NextScheduledBackup: nextScheduled,
            TotalBackupsCount: backupFiles.Count,
            TotalStorageUsedBytes: backupFiles.Sum(f => f.Length)
        );
    }

    public async Task<BackupListResponse> ListBackupsAsync(CancellationToken ct = default)
    {
        var storagePath = await GetStoragePathAsync(ct);
        var backupFiles = GetBackupFiles(storagePath);

        var backups = backupFiles
            .OrderByDescending(f => f.CreationTime)
            .Select(f => new BackupInfoDto(
                FileName: f.Name,
                FilePath: f.FullName,
                FileSizeBytes: f.Length,
                CreatedAt: f.CreationTimeUtc,
                BackupType: DetermineBackupType(f.Name),
                Description: null
            ))
            .ToList();

        return new BackupListResponse(
            Backups: backups,
            TotalCount: backups.Count,
            TotalSizeBytes: backups.Sum(b => b.FileSizeBytes)
        );
    }

    public async Task<CreateBackupResponse> CreateBackupAsync(CreateBackupRequest request, Guid userId, CancellationToken ct = default)
    {
        try
        {
            var storagePath = await GetStoragePathAsync(ct);
            EnsureDirectoryExists(storagePath);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var backupType = request.BackupType.ToLowerInvariant();
            var fileName = $"truload_backup_{backupType}_{timestamp}.sql";
            var filePath = Path.Combine(storagePath, fileName);

            // Get connection string details
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            var (host, port, database, username, password) = ParseConnectionString(connectionString);

            // Execute pg_dump
            var success = await ExecutePgDumpAsync(host, port, database, username, password, filePath, ct);

            if (!success)
            {
                return new CreateBackupResponse(
                    Success: false,
                    FileName: null,
                    FilePath: null,
                    FileSizeBytes: 0,
                    Message: "Backup failed. Check server logs for details."
                );
            }

            var fileInfo = new FileInfo(filePath);

            _logger.LogInformation("Backup created by user {UserId}: {FileName} ({Size} bytes)",
                userId, fileName, fileInfo.Length);

            return new CreateBackupResponse(
                Success: true,
                FileName: fileName,
                FilePath: filePath,
                FileSizeBytes: fileInfo.Length,
                Message: "Backup created successfully."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating backup");
            return new CreateBackupResponse(
                Success: false,
                FileName: null,
                FilePath: null,
                FileSizeBytes: 0,
                Message: $"Backup failed: {ex.Message}"
            );
        }
    }

    public async Task<bool> DeleteBackupAsync(string fileName, Guid userId, CancellationToken ct = default)
    {
        try
        {
            var storagePath = await GetStoragePathAsync(ct);
            var filePath = Path.Combine(storagePath, fileName);

            // Security: ensure the file is in the backup directory
            var fullPath = Path.GetFullPath(filePath);
            var fullStoragePath = Path.GetFullPath(storagePath);

            if (!fullPath.StartsWith(fullStoragePath))
            {
                _logger.LogWarning("Attempted path traversal attack: {FilePath}", fileName);
                return false;
            }

            if (!File.Exists(fullPath))
            {
                return false;
            }

            File.Delete(fullPath);

            _logger.LogInformation("Backup deleted by user {UserId}: {FileName}", userId, fileName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting backup {FileName}", fileName);
            return false;
        }
    }

    public async Task<(Stream Stream, string FileName, string ContentType)?> DownloadBackupAsync(string fileName, CancellationToken ct = default)
    {
        try
        {
            var storagePath = await GetStoragePathAsync(ct);
            var filePath = Path.Combine(storagePath, fileName);

            // Security: ensure the file is in the backup directory
            var fullPath = Path.GetFullPath(filePath);
            var fullStoragePath = Path.GetFullPath(storagePath);

            if (!fullPath.StartsWith(fullStoragePath))
            {
                _logger.LogWarning("Attempted path traversal attack: {FilePath}", fileName);
                return null;
            }

            if (!File.Exists(fullPath))
            {
                return null;
            }

            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return (stream, fileName, "application/sql");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading backup {FileName}", fileName);
            return null;
        }
    }

    public async Task<bool> ValidateBackupAsync(string fileName, CancellationToken ct = default)
    {
        var storagePath = await GetStoragePathAsync(ct);
        var filePath = Path.Combine(storagePath, fileName);

        if (!File.Exists(filePath))
        {
            return false;
        }

        // Basic validation: check file has content and starts with PostgreSQL comment
        try
        {
            using var reader = new StreamReader(filePath);
            var firstLine = await reader.ReadLineAsync(ct);
            return !string.IsNullOrEmpty(firstLine) &&
                   (firstLine.StartsWith("--") || firstLine.StartsWith("/*"));
        }
        catch
        {
            return false;
        }
    }

    public async Task<RestoreBackupResponse> RestoreBackupAsync(RestoreBackupRequest request, Guid userId, CancellationToken ct = default)
    {
        // This is a dangerous operation - add additional safeguards in production
        _logger.LogWarning("Restore operation initiated by user {UserId} from backup {FileName}",
            userId, request.FileName);

        try
        {
            var storagePath = await GetStoragePathAsync(ct);
            var filePath = Path.Combine(storagePath, request.FileName);

            // Security: ensure the file is in the backup directory
            var fullPath = Path.GetFullPath(filePath);
            var fullStoragePath = Path.GetFullPath(storagePath);

            if (!fullPath.StartsWith(fullStoragePath))
            {
                return new RestoreBackupResponse(
                    Success: false,
                    Message: "Invalid backup file path.",
                    RestoredFrom: null,
                    RestoredAt: null
                );
            }

            if (!File.Exists(fullPath))
            {
                return new RestoreBackupResponse(
                    Success: false,
                    Message: "Backup file not found.",
                    RestoredFrom: null,
                    RestoredAt: null
                );
            }

            // Validate backup before restore
            if (!await ValidateBackupAsync(request.FileName, ct))
            {
                return new RestoreBackupResponse(
                    Success: false,
                    Message: "Backup file validation failed.",
                    RestoredFrom: null,
                    RestoredAt: null
                );
            }

            // Get connection string details
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            var (host, port, database, username, password) = ParseConnectionString(connectionString);

            // Execute pg_restore (or psql for SQL dumps)
            var success = await ExecutePsqlRestoreAsync(host, port, database, username, password, fullPath, ct);

            if (!success)
            {
                return new RestoreBackupResponse(
                    Success: false,
                    Message: "Restore operation failed. Check server logs for details.",
                    RestoredFrom: null,
                    RestoredAt: null
                );
            }

            _logger.LogInformation("Database restored by user {UserId} from backup {FileName}",
                userId, request.FileName);

            return new RestoreBackupResponse(
                Success: true,
                Message: "Database restored successfully.",
                RestoredFrom: request.FileName,
                RestoredAt: DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring from backup {FileName}", request.FileName);
            return new RestoreBackupResponse(
                Success: false,
                Message: $"Restore failed: {ex.Message}",
                RestoredFrom: null,
                RestoredAt: null
            );
        }
    }

    public async Task<bool> UpdateSettingsAsync(UpdateBackupSettingsRequest request, Guid userId, CancellationToken ct = default)
    {
        try
        {
            await UpsertSettingAsync(SettingKeys.BackupEnabled, request.IsEnabled.ToString(), "Boolean", SettingKeys.CategoryBackup, "Backup Enabled", userId, ct);
            await UpsertSettingAsync(SettingKeys.BackupScheduleCron, request.ScheduleCron, "String", SettingKeys.CategoryBackup, "Backup Schedule (Cron)", userId, ct);
            await UpsertSettingAsync(SettingKeys.BackupStoragePath, request.StoragePath, "String", SettingKeys.CategoryBackup, "Backup Storage Path", userId, ct);
            await UpsertSettingAsync(SettingKeys.BackupRetentionDays, request.RetentionDays.ToString(), "Integer", SettingKeys.CategoryBackup, "Backup Retention Days", userId, ct);

            if (request.BackupPgDumpPath != null)
            {
                await UpsertSettingAsync(SettingKeys.BackupPgDumpPath, request.BackupPgDumpPath, "String", SettingKeys.CategoryBackup, "pg_dump Executable Path", userId, ct);
            }

            _logger.LogInformation("Backup settings updated by user {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating backup settings");
            return false;
        }
    }

    /// <summary>
    /// Update an existing setting or create it if it doesn't exist.
    /// Handles the case where backup settings haven't been seeded yet.
    /// </summary>
    private async Task UpsertSettingAsync(string key, string value, string settingType, string category, string displayName, Guid userId, CancellationToken ct)
    {
        try
        {
            await _settingsService.UpdateSettingAsync(key, value, userId, ct);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogInformation("Setting {Key} not found, creating it", key);
            var setting = new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = key,
                SettingValue = value,
                SettingType = settingType,
                Category = category,
                DisplayName = displayName,
                DefaultValue = value,
                IsEditable = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Set<ApplicationSettings>().Add(setting);
            await _context.SaveChangesAsync(ct);
        }
    }

    private async Task<string> GetStoragePathAsync(CancellationToken ct)
    {
        var path = await _settingsService.GetSettingValueAsync(
            SettingKeys.BackupStoragePath,
            "./backups",
            ct);

        return path;
    }

    private async Task<string> GetPgDumpPathAsync(CancellationToken ct)
    {
        var defaultPath = OperatingSystem.IsWindows() 
            ? @"C:\Program Files\PostgreSQL\17\bin\pg_dump.exe" 
            : "pg_dump";

        return await _settingsService.GetSettingValueAsync(
            SettingKeys.BackupPgDumpPath,
            defaultPath,
            ct);
    }

    private async Task<string> GetPsqlPathAsync(CancellationToken ct)
    {
        var defaultPath = OperatingSystem.IsWindows() 
            ? @"C:\Program Files\PostgreSQL\17\bin\psql.exe" 
            : "psql";

        // We use the same directory as pg_dump for psql by default
        var pgDumpPath = await GetPgDumpPathAsync(ct);
        if (Path.IsPathRooted(pgDumpPath))
        {
            var dir = Path.GetDirectoryName(pgDumpPath);
            if (!string.IsNullOrEmpty(dir))
            {
                defaultPath = Path.Combine(dir, OperatingSystem.IsWindows() ? "psql.exe" : "psql");
            }
        }

        return defaultPath;
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private static List<FileInfo> GetBackupFiles(string storagePath)
    {
        if (!Directory.Exists(storagePath))
        {
            return new List<FileInfo>();
        }

        var directory = new DirectoryInfo(storagePath);
        return directory.GetFiles("truload_backup_*.sql")
            .Concat(directory.GetFiles("truload_backup_*.dump"))
            .ToList();
    }

    private static string DetermineBackupType(string fileName)
    {
        if (fileName.Contains("_full_", StringComparison.OrdinalIgnoreCase))
            return "Full";
        if (fileName.Contains("_differential_", StringComparison.OrdinalIgnoreCase))
            return "Differential";
        if (fileName.Contains("_configonly_", StringComparison.OrdinalIgnoreCase))
            return "ConfigOnly";
        return "Unknown";
    }

    private static DateTime? CalculateNextRun(string cronExpression)
    {
        try
        {
            var expression = Cronos.CronExpression.Parse(cronExpression);
            return expression.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc);
        }
        catch (Exception)
        {
            // Fallback: if cron parsing fails, assume next midnight
            return DateTime.UtcNow.Date.AddDays(1);
        }
    }

    private static (string Host, string Port, string Database, string Username, string Password) ParseConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Database connection string not configured");
        }

        var parts = connectionString.Split(';')
            .Select(p => p.Split('='))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim().ToLowerInvariant(), p => p[1].Trim());

        return (
            Host: parts.GetValueOrDefault("host") ?? parts.GetValueOrDefault("server") ?? "localhost",
            Port: parts.GetValueOrDefault("port") ?? "5432",
            Database: parts.GetValueOrDefault("database") ?? parts.GetValueOrDefault("initial catalog") ?? "truload",
            Username: parts.GetValueOrDefault("username") ?? parts.GetValueOrDefault("user id") ?? "postgres",
            Password: parts.GetValueOrDefault("password") ?? ""
        );
    }

    private async Task<bool> ExecutePgDumpAsync(
        string host, string port, string database, string username, string password,
        string outputFile, CancellationToken ct)
    {
        try
        {
            var pgDumpPath = await GetPgDumpPathAsync(ct);
            var startInfo = new ProcessStartInfo
            {
                FileName = pgDumpPath,
                Arguments = $"-h {host} -p {port} -U {username} -d {database} -f \"{outputFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Environment = { ["PGPASSWORD"] = password }
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var error = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                _logger.LogError("pg_dump failed with exit code {ExitCode}: {Error}",
                    process.ExitCode, error);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing pg_dump");
            return false;
        }
    }

    private async Task<bool> ExecutePsqlRestoreAsync(
        string host, string port, string database, string username, string password,
        string inputFile, CancellationToken ct)
    {
        try
        {
            var psqlPath = await GetPsqlPathAsync(ct);
            var startInfo = new ProcessStartInfo
            {
                FileName = psqlPath,
                Arguments = $"-h {host} -p {port} -U {username} -d {database} -f \"{inputFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Environment = { ["PGPASSWORD"] = password }
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var error = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                _logger.LogError("psql restore failed with exit code {ExitCode}: {Error}",
                    process.ExitCode, error);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing psql restore");
            return false;
        }
    }
}
