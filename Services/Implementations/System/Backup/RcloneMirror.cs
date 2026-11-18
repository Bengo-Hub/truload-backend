using System.Diagnostics;
using System.Text;
using System.Text.Json;
using TruLoad.Backend.DTOs.System;
using TruLoad.Backend.Services.Interfaces.System;

namespace TruLoad.Backend.Services.Implementations.System.Backup;

/// <summary>
/// Uploads, lists, downloads and deletes pg_dump backup files at the backup destination
/// through the rclone CLI. The destination is the one an administrator configured, or the
/// platform's private R2 bucket when none is (see BackupDestinationStore.ResolveAsync).
/// BackupService decides what happens to the local staging copy.
///
/// Credentials are passed to rclone exclusively via ephemeral RCLONE_CONFIG_* process
/// environment variables (nothing is written to a persistent rclone config file) and
/// are NEVER logged. rclone passwords are stored via <c>rclone obscure</c>.
/// </summary>
public sealed class RcloneMirror : IRcloneMirror
{
    // Ephemeral, in-memory rclone remote name; its backend settings come from
    // RCLONE_CONFIG_TRULOADBKP_* env vars built per call (see BuildEnv).
    private const string RemoteName = "truloadbkp";
    private const string EnvPrefix = "RCLONE_CONFIG_TRULOADBKP_";

    private static readonly TimeSpan TransferTimeout = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ObscureTimeout = TimeSpan.FromSeconds(5);

    private readonly IBackupDestinationStore _store;
    private readonly ILogger<RcloneMirror> _logger;

    public RcloneMirror(IBackupDestinationStore store, ILogger<RcloneMirror> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<ResolvedBackupDestination?> ResolveRemoteAsync(CancellationToken ct = default)
    {
        try
        {
            var dest = await _store.ResolveAsync(ct);
            return dest.IsRemote ? dest : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve the backup destination; treating backups as local only");
            return null;
        }
    }

    public async Task<bool> MirrorAsync(string localFilePath, string objectName, CancellationToken ct = default)
    {
        var dest = await ResolveRemoteAsync(ct);
        if (dest == null)
        {
            return false; // local-only or disabled: nothing to upload to.
        }

        var target = $"{RemoteName}:{BuildRemotePath(dest, objectName)}";
        var args = WithBaseFlags("copyto", localFilePath, target);

        try
        {
            var (exit, stderr, _) = await RunAsync(ResolveRcloneBin(), args, BuildEnv(dest), TransferTimeout, ct);
            if (exit != 0)
            {
                _logger.LogWarning("Backup upload failed (type={Type}, object={Object}): {Error}",
                    dest.Type, objectName, Sanitize(stderr, dest));
                return false;
            }
            _logger.LogInformation("Backup uploaded to destination (type={Type}, platform default={Platform}, object={Object})",
                dest.Type, dest.IsPlatformDefault, objectName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Backup upload error (type={Type}): {Error}", dest.Type, Sanitize(ex.Message, dest));
            return false;
        }
    }

    public async Task<IReadOnlyList<RemoteBackupFile>?> ListAsync(ResolvedBackupDestination destination, CancellationToken ct = default)
    {
        var target = $"{RemoteName}:{BuildRemotePath(destination, string.Empty)}";
        var args = WithBaseFlags("lsjson", "--files-only", "--no-mimetype", target);
        try
        {
            var (exit, stderr, stdout) = await RunAsync(ResolveRcloneBin(), args, BuildEnv(destination), TestTimeout, ct);
            if (exit != 0)
            {
                // An empty prefix on a bucket store reads as "directory not found".
                if (stderr.Contains("directory not found", StringComparison.OrdinalIgnoreCase))
                    return Array.Empty<RemoteBackupFile>();
                _logger.LogWarning("Listing backups at the destination failed (type={Type}): {Error}",
                    destination.Type, Sanitize(stderr, destination));
                return null;
            }

            var items = JsonSerializer.Deserialize<List<LsJsonItem>>(stdout, LsJsonOptions) ?? new List<LsJsonItem>();
            return items
                .Where(i => !i.IsDir && !string.IsNullOrEmpty(i.Name))
                .Select(i => new RemoteBackupFile(i.Name!, i.Size, i.ModTime.UtcDateTime))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Listing backups at the destination failed (type={Type}): {Error}",
                destination.Type, Sanitize(ex.Message, destination));
            return null;
        }
    }

    public async Task<bool> DownloadAsync(ResolvedBackupDestination destination, string objectName, string localFilePath, CancellationToken ct = default)
    {
        var source = $"{RemoteName}:{BuildRemotePath(destination, objectName)}";
        var args = WithBaseFlags("copyto", source, localFilePath);
        try
        {
            var (exit, stderr, _) = await RunAsync(ResolveRcloneBin(), args, BuildEnv(destination), TransferTimeout, ct);
            if (exit != 0)
            {
                _logger.LogWarning("Backup download failed (type={Type}, object={Object}): {Error}",
                    destination.Type, objectName, Sanitize(stderr, destination));
                return false;
            }
            return File.Exists(localFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Backup download error (type={Type}): {Error}", destination.Type, Sanitize(ex.Message, destination));
            return false;
        }
    }

    public async Task<bool> DeleteAsync(ResolvedBackupDestination destination, string objectName, CancellationToken ct = default)
    {
        var target = $"{RemoteName}:{BuildRemotePath(destination, objectName)}";
        var args = WithBaseFlags("deletefile", target);
        try
        {
            var (exit, stderr, _) = await RunAsync(ResolveRcloneBin(), args, BuildEnv(destination), TestTimeout, ct);
            if (exit != 0)
            {
                _logger.LogWarning("Deleting backup at the destination failed (type={Type}, object={Object}): {Error}",
                    destination.Type, objectName, Sanitize(stderr, destination));
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Deleting backup at the destination failed (type={Type}): {Error}",
                destination.Type, Sanitize(ex.Message, destination));
            return false;
        }
    }

    private static readonly JsonSerializerOptions LsJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed class LsJsonItem
    {
        public string? Name { get; set; }
        public long Size { get; set; }
        public DateTimeOffset ModTime { get; set; }
        public bool IsDir { get; set; }
    }

    public async Task<BackupDestinationTestResult> TestConnectionAsync(ResolvedBackupDestination destination, CancellationToken ct = default)
    {
        if (!destination.IsRemote)
        {
            return new BackupDestinationTestResult(false, "Destination is local-only or disabled; nothing to test.");
        }

        var bin = ResolveRcloneBin();
        if (bin == null)
        {
            return new BackupDestinationTestResult(false, "rclone binary is not available on the server.");
        }

        var target = $"{RemoteName}:{TrimSlashes(destination.RemotePath)}";
        var args = WithBaseFlags("lsd", target);

        try
        {
            var (exit, stderr, _) = await RunAsync(bin, args, BuildEnv(destination), TestTimeout, ct);
            return exit == 0
                ? new BackupDestinationTestResult(true, "Connection OK.")
                : new BackupDestinationTestResult(false, Sanitize(stderr, destination));
        }
        catch (Exception ex)
        {
            return new BackupDestinationTestResult(false, ex.Message);
        }
    }

    // -- rclone config / argv -------------------------------------------------

    private static string[] WithBaseFlags(params string[] args)
    {
        var nul = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";
        var baseFlags = new List<string>
        {
            "--config", nul,            // never read/write a persistent config file
            "--use-server-modtime",
            "--low-level-retries", "2",
            "--retries", "1",
        };
        baseFlags.AddRange(args);
        return baseFlags.ToArray();
    }

    /// <summary>
    /// Builds the RCLONE_CONFIG_TRULOADBKP_* environment for an ephemeral remote
    /// derived from the destination params. Nothing is written to disk; values are
    /// never logged. Passwords/passphrases are passed through <c>rclone obscure</c>.
    /// </summary>
    private Dictionary<string, string> BuildEnv(ResolvedBackupDestination dest)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [EnvPrefix + "TYPE"] = RcloneBackend(dest.Type),
        };

        string P(string key) => dest.Params.TryGetValue(key, out var v) ? v ?? string.Empty : string.Empty;
        void Set(string key, string? val) { if (!string.IsNullOrEmpty(val)) env[EnvPrefix + key.ToUpperInvariant()] = val!; }

        switch (dest.Type)
        {
            case "s3":
                Set("provider", string.IsNullOrEmpty(P("provider")) ? "AWS" : P("provider"));
                Set("access_key_id", P("access_key_id"));
                Set("secret_access_key", P("secret_access_key"));
                Set("region", P("region"));
                Set("endpoint", P("endpoint"));
                // Never try to create the bucket: scoped R2/S3 keys usually lack that right and
                // the bucket already exists.
                Set("no_check_bucket", "true");
                // The image's rclone (Debian 1.60) follows each upload with a HEAD that R2 answers
                // with 501 Not Implemented, so the copy reports failure although the object landed.
                // The upload still carries Content-MD5, so the integrity check is kept.
                Set("no_head", "true");
                break;
            case "onedrive":
                Set("token", P("token"));
                Set("drive_id", P("drive_id"));
                Set("drive_type", "business");
                break;
            case "gdrive":
                Set("token", P("token"));
                Set("root_folder_id", P("drive_id"));
                break;
            case "webdav":
                Set("url", P("url"));
                Set("vendor", "other");
                Set("user", P("user"));
                Set("pass", Obscure(P("pass")));
                break;
            case "sftp":
                Set("host", P("host"));
                Set("port", string.IsNullOrEmpty(P("port")) ? "22" : P("port"));
                Set("user", P("user"));
                Set("pass", Obscure(P("pass")));
                Set("key_pem", P("private_key"));
                break;
            case "smb":
                Set("host", P("host"));
                Set("port", string.IsNullOrEmpty(P("port")) ? "445" : P("port"));
                Set("user", P("user"));
                Set("pass", Obscure(P("pass")));
                Set("domain", P("domain"));
                break;
        }
        return env;
    }

    /// <summary>Maps our destination type to the rclone backend name (gdrive -&gt; drive).</summary>
    private static string RcloneBackend(string type) => type == "gdrive" ? "drive" : type;

    /// <summary>
    /// Builds the remote-side path for an object. For S3 the bucket is the first path
    /// segment (remote:bucket/prefix/object); for other backends the configured
    /// RemotePath is the prefix.
    /// </summary>
    private static string BuildRemotePath(ResolvedBackupDestination dest, string objectName)
    {
        var prefix = TrimSlashes(dest.RemotePath);
        if (dest.Type == "s3" && dest.Params.TryGetValue("bucket", out var bucket) && !string.IsNullOrWhiteSpace(bucket))
        {
            prefix = string.IsNullOrEmpty(prefix) ? TrimSlashes(bucket) : $"{TrimSlashes(bucket)}/{prefix}";
        }
        var obj = objectName.TrimStart('/');
        if (obj.Length == 0) return prefix;
        return string.IsNullOrEmpty(prefix) ? obj : $"{prefix}/{obj}";
    }

    private static string TrimSlashes(string? s) => string.IsNullOrWhiteSpace(s) ? string.Empty : s.Trim().Trim('/');

    /// <summary>
    /// Wraps a plaintext secret in rclone's reversible obscure() form. rclone's
    /// webdav/sftp/smb "pass" expects an obscured value; if rclone is unavailable we
    /// fall back to the raw value (rclone accepts already-obscured strings too).
    /// Never logged.
    /// </summary>
    private string Obscure(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;
        var bin = ResolveRcloneBin();
        if (bin == null) return plaintext;
        try
        {
            var (exit, _, stdout) = RunCapture(bin, new[] { "obscure", plaintext }, null, ObscureTimeout);
            return exit == 0 ? stdout.Trim() : plaintext;
        }
        catch
        {
            return plaintext;
        }
    }

    // -- process plumbing -----------------------------------------------------

    private static string ResolveRcloneBin()
    {
        var bin = Environment.GetEnvironmentVariable("RCLONE_BIN");
        if (string.IsNullOrWhiteSpace(bin)) bin = "rclone";
        // Let the OS resolve it from PATH; we only verify it launches at run time.
        return bin;
    }

    private static async Task<(int Exit, string Stderr, string Stdout)> RunAsync(string bin, string[] args, Dictionary<string, string>? extraEnv, TimeSpan timeout, CancellationToken ct)
    {
        using var proc = new Process();
        proc.StartInfo.FileName = bin;
        foreach (var a in args) proc.StartInfo.ArgumentList.Add(a);
        proc.StartInfo.RedirectStandardError = true;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.CreateNoWindow = true;
        if (extraEnv != null)
        {
            foreach (var (k, v) in extraEnv) proc.StartInfo.Environment[k] = v;
        }

        var stderr = new StringBuilder();
        var stdout = new StringBuilder();
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };

        proc.Start();
        proc.BeginErrorReadLine();
        proc.BeginOutputReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            await proc.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { if (!proc.HasExited) proc.Kill(true); } catch { /* ignore */ }
            return (124, "rclone timed out", string.Empty);
        }
        // WaitForExitAsync returns once the redirected streams reach EOF, so both buffers are complete.
        return (proc.ExitCode, stderr.ToString(), stdout.ToString());
    }

    private static (int Exit, string Stderr, string Stdout) RunCapture(string bin, string[] args, Dictionary<string, string>? extraEnv, TimeSpan timeout)
    {
        using var proc = new Process();
        proc.StartInfo.FileName = bin;
        foreach (var a in args) proc.StartInfo.ArgumentList.Add(a);
        proc.StartInfo.RedirectStandardError = true;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.CreateNoWindow = true;
        if (extraEnv != null)
        {
            foreach (var (k, v) in extraEnv) proc.StartInfo.Environment[k] = v;
        }
        proc.Start();
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        if (!proc.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try { proc.Kill(true); } catch { /* ignore */ }
            return (124, "timed out", string.Empty);
        }
        return (proc.ExitCode, stderr, stdout);
    }

    /// <summary>
    /// Turns rclone stderr into a short, user-safe message that never contains
    /// credential material: redacts any known secret param value, keeps the last line,
    /// and caps the length.
    /// </summary>
    private static string Sanitize(string stderr, ResolvedBackupDestination dest)
    {
        var msg = (stderr ?? string.Empty).Trim();
        if (msg.Length == 0) msg = "rclone failed";

        foreach (var key in new[] { "secret_access_key", "access_key_id", "token", "pass", "private_key" })
        {
            if (dest.Params.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
            {
                msg = msg.Replace(v, "***");
            }
        }

        var lines = msg.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length > 0) msg = lines[^1];
        return msg.Length > 300 ? msg[..300] + "…" : msg;
    }
}
