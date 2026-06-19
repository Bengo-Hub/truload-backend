using System.Text.Json;
using System.Text.Json.Serialization;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.System;
using TruLoad.Backend.Infrastructure.Security;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Services.Interfaces.System;

namespace TruLoad.Backend.Services.Implementations.System.Backup;

/// <summary>
/// Stores the remote backup destination as a single JSON document in the application
/// settings store (<c>SettingKeys.BackupDestination</c>). Secret parameters are encrypted
/// at rest with the app's AES-256-GCM <see cref="IEncryptionService"/>; non-secret params
/// are kept in clear. Secrets are masked for API display.
/// </summary>
public class BackupDestinationStore : IBackupDestinationStore
{
    private const string Mask = "********";

    private readonly TruLoadDbContext _context;
    private readonly ISettingsService _settingsService;
    private readonly IEncryptionService _encryption;
    private readonly ILogger<BackupDestinationStore> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Parameter keys whose VALUES are secrets: encrypted at rest, masked in responses.
    /// </summary>
    private static readonly HashSet<string> SecretKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "secret_access_key", "access_key_id", "token", "pass", "private_key"
    };

    public BackupDestinationStore(
        TruLoadDbContext context,
        ISettingsService settingsService,
        IEncryptionService encryption,
        ILogger<BackupDestinationStore> logger)
    {
        _context = context;
        _settingsService = settingsService;
        _encryption = encryption;
        _logger = logger;
    }

    public async Task<BackupDestinationDto> GetForDisplayAsync(CancellationToken ct = default)
    {
        var stored = await LoadStoredAsync(ct);
        var masked = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in stored.Params)
        {
            masked[k] = SecretKeys.Contains(k) && !string.IsNullOrEmpty(v) ? Mask : v;
        }
        return ToDto(stored.Type, stored.Enabled, stored.RemotePath, masked);
    }

    public async Task<ResolvedBackupDestination> ResolveAsync(CancellationToken ct = default)
    {
        var stored = await LoadStoredAsync(ct);
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in stored.Params)
        {
            resolved[k] = DecryptIfSecret(k, v);
        }
        return new ResolvedBackupDestination(stored.Type, stored.Enabled, stored.RemotePath, resolved);
    }

    public async Task<ResolvedBackupDestination> ResolveFromRequestAsync(UpdateBackupDestinationRequest request, CancellationToken ct = default)
    {
        var stored = await LoadStoredAsync(ct);
        var merged = MergePlaintextParams(request, stored);
        return new ResolvedBackupDestination(
            NormalizeType(request.Type),
            request.Enabled,
            NormalizeRemotePath(request.RemotePath),
            merged);
    }

    public async Task<BackupDestinationDto> SaveAsync(UpdateBackupDestinationRequest request, Guid userId, CancellationToken ct = default)
    {
        var stored = await LoadStoredAsync(ct);
        var plaintext = MergePlaintextParams(request, stored);

        // Encrypt secrets for at-rest storage; keep non-secrets in clear.
        var atRest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in plaintext)
        {
            if (string.IsNullOrEmpty(v)) continue;
            atRest[k] = SecretKeys.Contains(k) ? _encryption.Encrypt(v) : v;
        }

        var doc = new StoredDestination
        {
            Type = NormalizeType(request.Type),
            Enabled = request.Enabled,
            RemotePath = NormalizeRemotePath(request.RemotePath),
            Params = atRest
        };

        var json = JsonSerializer.Serialize(doc, JsonOpts);
        await UpsertSettingAsync(SettingKeys.BackupDestination, json, userId, ct);

        _logger.LogInformation("Backup remote destination updated by user {UserId}: type={Type} enabled={Enabled}",
            userId, doc.Type, doc.Enabled);

        return await GetForDisplayAsync(ct);
    }

    // -- internals -----------------------------------------------------------

    /// <summary>
    /// Loads the stored destination doc as-is (secrets still encrypted). Tolerant of a
    /// missing/blank/corrupt setting — returns a disabled "none" destination.
    /// </summary>
    private async Task<StoredDestination> LoadStoredAsync(CancellationToken ct)
    {
        var json = await _settingsService.GetSettingValueAsync(SettingKeys.BackupDestination, string.Empty, ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new StoredDestination();
        }

        try
        {
            var doc = JsonSerializer.Deserialize<StoredDestination>(json, JsonOpts);
            if (doc == null) return new StoredDestination();
            doc.Params ??= new(StringComparer.OrdinalIgnoreCase);
            doc.Type = NormalizeType(doc.Type);
            doc.RemotePath = NormalizeRemotePath(doc.RemotePath);
            return doc;
        }
        catch (Exception ex)
        {
            // Never log the document (it contains encrypted secrets); log only that it failed.
            _logger.LogWarning(ex, "Failed to parse stored backup destination config; treating as unset");
            return new StoredDestination();
        }
    }

    /// <summary>
    /// Builds the full plaintext param set for a request: takes provided (non-null)
    /// request fields, and for secret fields that are omitted (null) preserves the
    /// existing decrypted secret. An explicit empty string clears a value.
    /// </summary>
    private Dictionary<string, string> MergePlaintextParams(UpdateBackupDestinationRequest request, StoredDestination stored)
    {
        var p = request.Params ?? new BackupDestinationParamsDto();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void Apply(string key, string? incoming)
        {
            if (incoming != null)
            {
                result[key] = incoming; // includes explicit "" to clear
                return;
            }
            // Omitted: preserve existing (decrypting secrets so the at-rest layer re-encrypts).
            if (stored.Params.TryGetValue(key, out var existing) && !string.IsNullOrEmpty(existing))
            {
                result[key] = DecryptIfSecret(key, existing);
            }
        }

        Apply("bucket", p.Bucket);
        Apply("region", p.Region);
        Apply("endpoint", p.Endpoint);
        Apply("provider", p.Provider);
        Apply("access_key_id", p.AccessKeyId);
        Apply("secret_access_key", p.SecretAccessKey);
        Apply("token", p.Token);
        Apply("drive_id", p.DriveId);
        Apply("url", p.Url);
        Apply("host", p.Host);
        Apply("port", p.Port);
        Apply("domain", p.Domain);
        Apply("share", p.Share);
        Apply("user", p.User);
        Apply("pass", p.Pass);
        Apply("private_key", p.PrivateKey);

        // Drop empties so we never persist blank keys.
        return result.Where(kv => !string.IsNullOrEmpty(kv.Value))
                     .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
    }

    private string DecryptIfSecret(string key, string value)
    {
        if (!SecretKeys.Contains(key) || string.IsNullOrEmpty(value)) return value;
        try
        {
            return _encryption.Decrypt(value);
        }
        catch
        {
            // Tolerate a value that was somehow stored un-encrypted (or with a rotated key):
            // never throw from a read path. Return as-is.
            return value;
        }
    }

    private static BackupDestinationDto ToDto(string type, bool enabled, string remotePath, IReadOnlyDictionary<string, string> p)
    {
        string? Get(string k) => p.TryGetValue(k, out var v) ? v : null;
        return new BackupDestinationDto(
            Type: type,
            Enabled: enabled,
            RemotePath: remotePath,
            Params: new BackupDestinationParamsDto
            {
                Bucket = Get("bucket"),
                Region = Get("region"),
                Endpoint = Get("endpoint"),
                Provider = Get("provider"),
                AccessKeyId = Get("access_key_id"),
                SecretAccessKey = Get("secret_access_key"),
                Token = Get("token"),
                DriveId = Get("drive_id"),
                Url = Get("url"),
                Host = Get("host"),
                Port = Get("port"),
                Domain = Get("domain"),
                Share = Get("share"),
                User = Get("user"),
                Pass = Get("pass"),
                PrivateKey = Get("private_key"),
            });
    }

    private static string NormalizeType(string? type) =>
        string.IsNullOrWhiteSpace(type) ? "none" : type.Trim().ToLowerInvariant();

    private static string NormalizeRemotePath(string? remotePath) =>
        string.IsNullOrWhiteSpace(remotePath) ? "truload-backups" : remotePath.Trim().Trim('/');

    /// <summary>
    /// Update the setting or create it if it doesn't exist yet (mirrors BackupService).
    /// </summary>
    private async Task UpsertSettingAsync(string key, string value, Guid userId, CancellationToken ct)
    {
        try
        {
            await _settingsService.UpdateSettingAsync(key, value, userId, ct);
        }
        catch (KeyNotFoundException)
        {
            var setting = new ApplicationSettings
            {
                Id = Guid.NewGuid(),
                SettingKey = key,
                SettingValue = value,
                SettingType = "Json",
                Category = SettingKeys.CategoryBackup,
                DisplayName = "Backup Remote Destination",
                Description = "Remote mirror destination for backups (secrets encrypted at rest).",
                DefaultValue = string.Empty,
                IsEditable = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Set<ApplicationSettings>().Add(setting);
            await _context.SaveChangesAsync(ct);
            _settingsService.InvalidateCache();
        }
    }

    /// <summary>
    /// At-rest shape: secret param values are AES-GCM ciphertext; non-secret params are clear.
    /// </summary>
    private sealed class StoredDestination
    {
        public string Type { get; set; } = "none";
        public bool Enabled { get; set; }
        public string RemotePath { get; set; } = "truload-backups";
        public Dictionary<string, string> Params { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
