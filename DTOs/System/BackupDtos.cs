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
    public string StoragePath { get; init; } = "/app/backups/truload";

    public string? BackupPgDumpPath { get; init; }

    [Range(1, 365)]
    public int RetentionDays { get; init; } = 30;
}

/// <summary>
/// Per-type parameters for a remote backup destination. Secret-bearing fields
/// (access keys, passwords, tokens, private keys) are stored ENCRYPTED at rest
/// and MASKED in API responses. Non-secret fields are stored/returned in clear.
/// </summary>
public record BackupDestinationParamsDto
{
    // S3 / S3-compatible (MinIO, R2, Wasabi, ...)
    public string? Bucket { get; init; }
    public string? Region { get; init; }
    public string? Endpoint { get; init; }
    public string? Provider { get; init; }
    public string? AccessKeyId { get; init; }   // secret
    public string? SecretAccessKey { get; init; } // secret

    // OneDrive / Google Drive (OAuth)
    public string? Token { get; init; }   // secret (rclone token JSON)
    public string? DriveId { get; init; }

    // WebDAV
    public string? Url { get; init; }

    // SFTP / SMB
    public string? Host { get; init; }
    public string? Port { get; init; }
    public string? Domain { get; init; }
    public string? Share { get; init; }

    // Shared credentials (webdav/sftp/smb)
    public string? User { get; init; }
    public string? Pass { get; init; } // secret
    public string? PrivateKey { get; init; } // secret (sftp PEM)
}

/// <summary>
/// Current remote backup destination configuration as returned by the API.
/// Secret parameters are masked (e.g. "********") and never returned in clear.
/// </summary>
public record BackupDestinationDto(
    string Type,
    bool Enabled,
    string RemotePath,
    BackupDestinationParamsDto Params
);

/// <summary>
/// Request to create/update the remote backup destination.
/// Omitted secret fields are preserved from the existing stored configuration
/// (so the masked values returned by GET can be sent back unchanged without
/// wiping a secret). Send an explicit empty string to clear a secret.
/// </summary>
public record UpdateBackupDestinationRequest
{
    /// <summary>One of: s3, onedrive, gdrive, webdav, sftp, smb, or empty/"none" to disable.</summary>
    [Required]
    [MaxLength(20)]
    public string Type { get; init; } = "none";

    public bool Enabled { get; init; }

    /// <summary>Folder/prefix on the remote that backup files are mirrored into.</summary>
    [MaxLength(500)]
    public string RemotePath { get; init; } = "truload-backups";

    public BackupDestinationParamsDto? Params { get; init; }
}

/// <summary>
/// Result of testing a remote backup destination connection.
/// </summary>
public record BackupDestinationTestResult(
    bool Ok,
    string Message
);
