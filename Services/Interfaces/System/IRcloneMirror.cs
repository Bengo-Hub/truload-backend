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
}

/// <summary>
/// Mirrors locally-written pg_dump backup files to an operator-configured remote
/// destination via rclone. Mirroring is BEST-EFFORT: the local StoragePath copy is
/// always the durable primary + fallback, so a remote failure never fails a backup.
///
/// Credentials are passed to rclone exclusively via ephemeral RCLONE_CONFIG_* process
/// environment variables (nothing is written to a persistent rclone config file) and
/// are NEVER logged.
/// </summary>
public interface IRcloneMirror
{
    /// <summary>
    /// Copies <paramref name="localFilePath"/> to the configured remote under
    /// RemotePath/objectName. Best-effort: on any failure (no remote configured,
    /// rclone missing, transfer error, timeout) it logs a WARN and swallows the
    /// error so the caller never fails the backup.
    /// </summary>
    Task MirrorAsync(string localFilePath, string objectName, CancellationToken ct = default);

    /// <summary>
    /// Tests reachability of a destination by listing its RemotePath (rclone lsd).
    /// The supplied destination is used directly (not the stored one) so an operator
    /// can validate a config before saving it. Returns a sanitized ok/message that
    /// never contains credential material.
    /// </summary>
    Task<BackupDestinationTestResult> TestConnectionAsync(ResolvedBackupDestination destination, CancellationToken ct = default);
}
