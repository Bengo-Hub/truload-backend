using System.ComponentModel.DataAnnotations;

namespace TruLoad.Backend.DTOs.System;

/// <summary>
/// Information about a backup file.
/// </summary>
public record BackupInfoDto(
    string FileName,
    string FilePath,
    long FileSizeBytes,
    DateTime CreatedAt,
    string BackupType,
    string? Description
);

/// <summary>
/// List of backups with pagination.
/// </summary>
public record BackupListResponse(
    List<BackupInfoDto> Backups,
    int TotalCount,
    long TotalSizeBytes
);

/// <summary>
/// Request to create a manual backup.
/// </summary>
public record CreateBackupRequest
{
    /// <summary>
    /// Optional description for this backup.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; init; }

    /// <summary>
    /// Type of backup: Full, Differential, or ConfigOnly.
    /// </summary>
    [Required]
    public string BackupType { get; init; } = "Full";
}

/// <summary>
/// Response after creating a backup.
/// </summary>
public record CreateBackupResponse(
    bool Success,
    string? FileName,
    string? FilePath,
    long FileSizeBytes,
    string Message
);

/// <summary>
/// Request to restore from a backup.
/// </summary>
public record RestoreBackupRequest
{
    /// <summary>
    /// File name of the backup to restore.
    /// </summary>
    [Required]
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// Optional confirmation code (for safety).
    /// </summary>
    public string? ConfirmationCode { get; init; }
}

/// <summary>
/// Response after restore operation.
/// </summary>
public record RestoreBackupResponse(
    bool Success,
    string Message,
    string? RestoredFrom,
    DateTime? RestoredAt
);

/// <summary>
/// Status of the backup/restore system.
/// </summary>
public record BackupSystemStatusDto(
    bool IsEnabled,
    string ScheduleCron,
    string StoragePath,
    string BackupPgDumpPath,
    int RetentionDays,
    DateTime? LastBackupAt,
    DateTime? NextScheduledBackup,
    int TotalBackupsCount,
    long TotalStorageUsedBytes
);

/// <summary>
/// Request to update backup settings.
/// </summary>
public record UpdateBackupSettingsRequest
{
    public bool IsEnabled { get; init; } = true;

    [Required]
    public string ScheduleCron { get; init; } = "0 2 * * *";

    [Required]
    public string StoragePath { get; init; } = "./backups";

    public string? BackupPgDumpPath { get; init; }

    [Range(1, 365)]
    public int RetentionDays { get; init; } = 30;
}
