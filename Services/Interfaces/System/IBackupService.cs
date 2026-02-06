using TruLoad.Backend.DTOs.System;

namespace TruLoad.Backend.Services.Interfaces.System;

/// <summary>
/// Service interface for database backup and restore operations.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Gets the current backup system status.
    /// </summary>
    Task<BackupSystemStatusDto> GetStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Lists all available backups.
    /// </summary>
    Task<BackupListResponse> ListBackupsAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates a new backup.
    /// </summary>
    Task<CreateBackupResponse> CreateBackupAsync(CreateBackupRequest request, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a backup file.
    /// </summary>
    Task<bool> DeleteBackupAsync(string fileName, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Downloads a backup file as a stream.
    /// </summary>
    Task<(Stream Stream, string FileName, string ContentType)?> DownloadBackupAsync(string fileName, CancellationToken ct = default);

    /// <summary>
    /// Validates a backup file.
    /// </summary>
    Task<bool> ValidateBackupAsync(string fileName, CancellationToken ct = default);

    /// <summary>
    /// Restores from a backup (dangerous operation).
    /// </summary>
    Task<RestoreBackupResponse> RestoreBackupAsync(RestoreBackupRequest request, Guid userId, CancellationToken ct = default);
}
