using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.System;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Services.Interfaces.System;

namespace TruLoad.Backend.Services.Implementations.System;

/// <summary>
/// Implementation of IBackupService using pg_dump/pg_restore for PostgreSQL.
///
/// Where backups live:
/// - With a backup destination (an administrator-configured remote, or by default the platform's
///   private R2 bucket from OBJECT_STORAGE_*), the destination is the only durable copy.
///   pg_dump writes to the local staging directory (<c>backup.storage_path</c>, an emptyDir in
///   production), the file is uploaded and the local copy is deleted. If the upload fails the
///   newest dump stays in staging and is uploaded on the next successful run. Download, validate
///   and restore fetch the file back from the destination.
/// - Without any destination (local development), files stay in the storage path and the
///   retention job prunes them there, as before; each run logs a warning.
/// </summary>
public class BackupService : IBackupService
{
    private static readonly Regex BackupNamePattern =
        new(@"^truload_backup_[a-z]+_\d{8}_\d{6}\.(sql|dump)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly TruLoadDbContext _context;
    private readonly ISettingsService _settingsService;
    private readonly IConfiguration _configuration;
    private readonly IRcloneMirror _rcloneMirror;
    private readonly ILogger<BackupService> _logger;

    public BackupService(
        TruLoadDbContext context,
        ISettingsService settingsService,
        IConfiguration configuration,
        IRcloneMirror rcloneMirror,
        ILogger<BackupService> logger)
    {
        _context = context;
        _settingsService = settingsService;
        _configuration = configuration;
        _rcloneMirror = rcloneMirror;
        _logger = logger;
    }

    public async Task<BackupSystemStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var settings = await _settingsService.GetBackupSettingsAsync(ct);

        var storagePath = await GetStoragePathAsync(ct);
        var remote = await _rcloneMirror.ResolveRemoteAsync(ct);
        var backups = await ListAllAsync(storagePath, remote, ct);

        var lastBackup = backups.OrderByDescending(b => b.CreatedAt).FirstOrDefault();

        // Calculate next scheduled backup based on cron expression
        DateTime? nextScheduled = null;
        if (settings.Enabled && !string.IsNullOrEmpty(settings.ScheduleCron))
        {
            nextScheduled = CalculateNextRun(settings.ScheduleCron);
        }

        return new BackupSystemStatusDto(
            IsEnabled: settings.Enabled,
            ScheduleCron: settings.ScheduleCron ?? "0 2 * * *",
            StoragePath: remote != null ? Describe(remote) : storagePath,
            BackupPgDumpPath: await GetPgDumpPathAsync(ct),
            RetentionDays: settings.RetentionDays,
            LastBackupAt: lastBackup?.CreatedAt,
            NextScheduledBackup: nextScheduled,
            TotalBackupsCount: backups.Count,
            TotalStorageUsedBytes: backups.Sum(b => b.FileSizeBytes)
        );
    }

    public async Task<BackupListResponse> ListBackupsAsync(CancellationToken ct = default)
    {
        var storagePath = await GetStoragePathAsync(ct);
        var remote = await _rcloneMirror.ResolveRemoteAsync(ct);
        var backups = (await ListAllAsync(storagePath, remote, ct))
            .OrderByDescending(b => b.CreatedAt)
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
            var remote = await _rcloneMirror.ResolveRemoteAsync(ct);

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
                TryDelete(filePath);
                return new CreateBackupResponse(
                    Success: false,
                    FileName: null,
                    FilePath: null,
                    FileSizeBytes: 0,
                    Message: "Backup failed. Check server logs for details."
                );
            }

            var size = new FileInfo(filePath).Length;
            _logger.LogInformation("Backup created by user {UserId}: {FileName} ({Size} bytes)", userId, fileName, size);

            if (remote == null)
            {
                _logger.LogWarning(
                    "No backup destination is configured and platform object storage is not available; " +
                    "backup {FileName} is kept on local disk only at {StoragePath}", fileName, storagePath);
                return new CreateBackupResponse(
                    Success: true,
                    FileName: fileName,
                    FilePath: filePath,
                    FileSizeBytes: size,
                    Message: "Backup created on local disk only (no backup destination is configured)."
                );
            }

            if (!await _rcloneMirror.MirrorAsync(filePath, fileName, ct))
            {
                // Keep this dump for the next run to upload, but only the newest one so a run of
                // failures can never fill the staging volume.
                PruneLocal(storagePath, keep: fileName);
                _logger.LogError("Backup {FileName} could not be uploaded to {Destination}; it stays in local staging until the next successful run",
                    fileName, Describe(remote));
                return new CreateBackupResponse(
                    Success: false,
                    FileName: fileName,
                    FilePath: filePath,
                    FileSizeBytes: size,
                    Message: "Backup was created but the upload to the backup destination failed. Check server logs."
                );
            }

            TryDelete(filePath);
            await UploadLeftoversAsync(storagePath, ct);

            return new CreateBackupResponse(
                Success: true,
                FileName: fileName,
                FilePath: $"{Describe(remote)}/{fileName}",
                FileSizeBytes: size,
                Message: "Backup created and stored at the backup destination."
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
            if (!IsBackupName(fileName))
            {
                _logger.LogWarning("Rejected backup delete for an invalid file name: {FileName}", fileName);
                return false;
            }

            var storagePath = await GetStoragePathAsync(ct);
            var localPath = Path.Combine(storagePath, fileName);
            var deleted = false;
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
                deleted = true;
            }

            var remote = await _rcloneMirror.ResolveRemoteAsync(ct);
            if (remote != null && await _rcloneMirror.DeleteAsync(remote, fileName, ct))
            {
                deleted = true;
            }

            if (deleted)
            {
                _logger.LogInformation("Backup deleted by user {UserId}: {FileName}", userId, fileName);
            }
            return deleted;
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
            var fetched = await FetchAsync(fileName, ct);
            if (fetched == null)
            {
                return null;
            }

            var (path, temporary) = fetched.Value;
            // A copy fetched from the destination is removed as soon as the response stream closes.
            var options = temporary ? FileOptions.DeleteOnClose | FileOptions.Asynchronous : FileOptions.Asynchronous;
            Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920, options);
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
        var fetched = await FetchAsync(fileName, ct);
        if (fetched == null)
        {
            return false;
        }

        var (path, temporary) = fetched.Value;
        try
        {
            return await ValidateFileAsync(path, ct);
        }
        finally
        {
            if (temporary) TryDelete(path);
        }
    }

    public async Task<RestoreBackupResponse> RestoreBackupAsync(RestoreBackupRequest request, Guid userId, CancellationToken ct = default)
    {
        // This is a dangerous operation - add additional safeguards in production
        _logger.LogWarning("Restore operation initiated by user {UserId} from backup {FileName}",
            userId, request.FileName);

        string? temporaryPath = null;
        try
        {
            if (!IsBackupName(request.FileName))
            {
                return new RestoreBackupResponse(
                    Success: false,
                    Message: "Invalid backup file path.",
                    RestoredFrom: null,
                    RestoredAt: null
                );
            }

            var fetched = await FetchAsync(request.FileName, ct);
            if (fetched == null)
            {
                return new RestoreBackupResponse(
                    Success: false,
                    Message: "Backup file not found.",
                    RestoredFrom: null,
                    RestoredAt: null
                );
            }

            var (fullPath, temporary) = fetched.Value;
            if (temporary) temporaryPath = fullPath;

            // Validate backup before restore
            if (!await ValidateFileAsync(fullPath, ct))
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
        finally
        {
            if (temporaryPath != null) TryDelete(temporaryPath);
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

    // -- destination helpers ----------------------------------------------------

    private static bool IsBackupName(string? fileName) =>
        !string.IsNullOrEmpty(fileName) && BackupNamePattern.IsMatch(fileName);

    /// <summary>Where backups go, for display and logs. Never contains credentials.</summary>
    private static string Describe(ResolvedBackupDestination remote)
    {
        var path = remote.RemotePath.Trim('/');
        if (remote.Params.TryGetValue("bucket", out var bucket) && !string.IsNullOrWhiteSpace(bucket))
        {
            path = string.IsNullOrEmpty(path) ? bucket.Trim('/') : $"{bucket.Trim('/')}/{path}";
        }
        var label = remote.IsPlatformDefault ? "r2" : remote.Type;
        return $"{label}:{path}";
    }

    /// <summary>
    /// Backups at the destination plus any dump still waiting in local staging (a failed upload),
    /// or only the local files when there is no destination.
    /// </summary>
    private async Task<List<BackupInfoDto>> ListAllAsync(string storagePath, ResolvedBackupDestination? remote, CancellationToken ct)
    {
        var result = new Dictionary<string, BackupInfoDto>(StringComparer.Ordinal);

        if (remote != null)
        {
            var remoteFiles = await _rcloneMirror.ListAsync(remote, ct);
            if (remoteFiles == null)
            {
                _logger.LogWarning("Could not list backups at {Destination}; showing local staging only", Describe(remote));
            }
            else
            {
                var location = Describe(remote);
                foreach (var f in remoteFiles.Where(f => IsBackupName(f.Name)))
                {
                    result[f.Name] = new BackupInfoDto(
                        FileName: f.Name,
                        FilePath: $"{location}/{f.Name}",
                        FileSizeBytes: f.Size,
                        CreatedAt: f.ModifiedUtc,
                        BackupType: DetermineBackupType(f.Name),
                        Description: null);
                }
            }
        }

        foreach (var f in GetBackupFiles(storagePath).Where(f => IsBackupName(f.Name)))
        {
            result.TryAdd(f.Name, new BackupInfoDto(
                FileName: f.Name,
                FilePath: f.FullName,
                FileSizeBytes: f.Length,
                CreatedAt: f.CreationTimeUtc,
                BackupType: DetermineBackupType(f.Name),
                Description: remote != null ? "Waiting in local staging for upload" : null));
        }

        return result.Values.ToList();
    }

    /// <summary>
    /// Returns a local path for the backup: the staging copy when one exists, otherwise a
    /// temporary download from the destination (Temporary = true; the caller deletes it).
    /// Null when the name is invalid or the file is in neither place.
    /// </summary>
    private async Task<(string Path, bool Temporary)?> FetchAsync(string fileName, CancellationToken ct)
    {
        if (!IsBackupName(fileName))
        {
            _logger.LogWarning("Rejected backup access for an invalid file name: {FileName}", fileName);
            return null;
        }

        var storagePath = await GetStoragePathAsync(ct);
        var localPath = Path.Combine(storagePath, fileName);
        if (File.Exists(localPath))
        {
            return (localPath, false);
        }

        var remote = await _rcloneMirror.ResolveRemoteAsync(ct);
        if (remote == null)
        {
            return null;
        }

        EnsureDirectoryExists(storagePath);
        // Dot-prefixed so it never shows up as a backup in the staging listing.
        var temporaryPath = Path.Combine(storagePath, $".download-{Guid.NewGuid():N}-{fileName}");
        if (await _rcloneMirror.DownloadAsync(remote, fileName, temporaryPath, ct))
        {
            return (temporaryPath, true);
        }

        TryDelete(temporaryPath);
        return null;
    }

    /// <summary>Uploads dumps left in staging by an earlier failed upload, removing each one that lands.</summary>
    private async Task UploadLeftoversAsync(string storagePath, CancellationToken ct)
    {
        foreach (var f in GetBackupFiles(storagePath).Where(f => IsBackupName(f.Name)))
        {
            if (await _rcloneMirror.MirrorAsync(f.FullName, f.Name, ct))
            {
                TryDelete(f.FullName);
                _logger.LogInformation("Uploaded backup {FileName} left in staging by an earlier run", f.Name);
            }
        }
    }

    /// <summary>Removes every staged dump except <paramref name="keep"/>.</summary>
    private void PruneLocal(string storagePath, string keep)
    {
        foreach (var f in GetBackupFiles(storagePath).Where(f => f.Name != keep))
        {
            _logger.LogWarning("Dropping older staged backup {FileName}; only the newest is kept while uploads fail", f.Name);
            TryDelete(f.FullName);
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete local file {Path}", path);
        }
    }

    private static async Task<bool> ValidateFileAsync(string path, CancellationToken ct)
    {
        // Basic validation: check file has content and starts with PostgreSQL comment
        try
        {
            using var reader = new StreamReader(path);
            var firstLine = await reader.ReadLineAsync(ct);
            return !string.IsNullOrEmpty(firstLine) &&
                   (firstLine.StartsWith("--") || firstLine.StartsWith("/*"));
        }
        catch
        {
            return false;
        }
    }

    // -- settings and process helpers -------------------------------------------

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
            "/app/backups/truload",
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
