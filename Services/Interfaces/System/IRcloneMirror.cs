using TruLoad.Backend.DTOs.System;

namespace TruLoad.Backend.Services.Interfaces.System;

/// <summary>
/// Resolved (decrypted) remote backup destination used internally by the mirror
/// service. NEVER serialize this to an API response — it carries secrets in clear.
/// </summary>
public sealed record ResolvedBackupDestination(
    string Type,
    bool Enabled,
    string RemotePath,
    IReadOnlyDictionary<string, string> Params)
{
    /// <summary>True when a real remote (non-empty, non-"none" type) is enabled.</summary>
    public bool IsRemote =>
        Enabled &&
        !string.IsNullOrWhiteSpace(Type) &&
        !string.Equals(Type, "none", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when this is the platform's private R2 bucket (OBJECT_STORAGE_*), used because no
    /// administrator-configured destination exists.
    /// </summary>
    public bool IsPlatformDefault { get; init; }
}

/// <summary>A backup file held at the remote destination.</summary>
public sealed record RemoteBackupFile(string Name, long Size, DateTime ModifiedUtc);

/// <summary>
/// Talks to the backup destination (an administrator-configured remote, or the platform R2
/// bucket by default) through rclone. When a destination exists it is the only durable copy:
/// pg_dump writes to a local staging directory, the file is uploaded, and the local copy is
/// removed. Restores and downloads fetch the file back from the destination.
///
/// Credentials are passed to rclone exclusively via ephemeral RCLONE_CONFIG_* process
/// environment variables (nothing is written to a persistent rclone config file) and
/// are NEVER logged.
/// </summary>
public interface IRcloneMirror
{
    /// <summary>
    /// The destination backups go to, or null when there is none (local only).
    /// </summary>
    Task<ResolvedBackupDestination?> ResolveRemoteAsync(CancellationToken ct = default);

    /// <summary>
    /// Copies <paramref name="localFilePath"/> to the destination under RemotePath/objectName.
    /// Returns true only when the copy succeeded. Never throws; failures are logged without
    /// credential material.
    /// </summary>
    Task<bool> MirrorAsync(string localFilePath, string objectName, CancellationToken ct = default);

    /// <summary>Lists files directly under the destination's RemotePath; null when listing failed.</summary>
    Task<IReadOnlyList<RemoteBackupFile>?> ListAsync(ResolvedBackupDestination destination, CancellationToken ct = default);

    /// <summary>Downloads RemotePath/objectName to <paramref name="localFilePath"/>. True on success.</summary>
    Task<bool> DownloadAsync(ResolvedBackupDestination destination, string objectName, string localFilePath, CancellationToken ct = default);

    /// <summary>Deletes RemotePath/objectName. True on success.</summary>
    Task<bool> DeleteAsync(ResolvedBackupDestination destination, string objectName, CancellationToken ct = default);

    /// <summary>
    /// Tests reachability of a destination by listing its RemotePath (rclone lsd).
    /// The supplied destination is used directly (not the stored one) so an operator
    /// can validate a config before saving it. Returns a sanitized ok/message that
    /// never contains credential material.
    /// </summary>
    Task<BackupDestinationTestResult> TestConnectionAsync(ResolvedBackupDestination destination, CancellationToken ct = default);
}
