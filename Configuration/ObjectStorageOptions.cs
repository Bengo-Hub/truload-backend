namespace TruLoad.Backend.Configuration;

/// <summary>
/// Platform object storage (Cloudflare R2, S3 compatible). Values come from the shared
/// <c>object-storage</c> Secret that every namespace gets (OBJECT_STORAGE_* environment
/// variables, see devops-k8s docs/object-storage.md). Nothing here is hard-coded: when the
/// variables are absent the service keeps using local disk.
/// </summary>
public sealed class ObjectStorageOptions
{
    /// <summary>First segment of every key this service writes (keys look like truload/media/...).</summary>
    public const string ServicePrefix = "truload";

    public string Endpoint { get; init; } = string.Empty;
    public string AccessKeyId { get; init; } = string.Empty;
    public string SecretAccessKey { get; init; } = string.Empty;
    public string Region { get; init; } = "auto";
    public string PrivateBucket { get; init; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) &&
        !string.IsNullOrWhiteSpace(AccessKeyId) &&
        !string.IsNullOrWhiteSpace(SecretAccessKey) &&
        !string.IsNullOrWhiteSpace(PrivateBucket);

    /// <summary>Reads OBJECT_STORAGE_* from configuration (environment variables included).</summary>
    public static ObjectStorageOptions FromConfiguration(IConfiguration configuration)
    {
        string Get(string key) => (configuration[key] ?? string.Empty).Trim();
        var region = Get("OBJECT_STORAGE_REGION");
        return new ObjectStorageOptions
        {
            Endpoint = Get("OBJECT_STORAGE_ENDPOINT"),
            AccessKeyId = Get("OBJECT_STORAGE_ACCESS_KEY_ID"),
            SecretAccessKey = Get("OBJECT_STORAGE_SECRET_ACCESS_KEY"),
            Region = string.IsNullOrEmpty(region) ? "auto" : region,
            PrivateBucket = Get("OBJECT_STORAGE_PRIVATE_BUCKET"),
        };
    }

    /// <summary>Object key for a branding or other media file served at /media/{relativePath}.</summary>
    public static string MediaKey(string relativePath) =>
        $"{ServicePrefix}/media/{relativePath.Replace('\\', '/').TrimStart('/')}";

    /// <summary>Object key for a file saved through IBlobStorageService.</summary>
    public static string FileKey(string relativePath) =>
        $"{ServicePrefix}/files/{relativePath.Replace('\\', '/').TrimStart('/')}";

    /// <summary>Key prefix (no trailing slash) for pg_dump backups of one database.</summary>
    public static string BackupPrefix(string database) => $"{ServicePrefix}/backups/{database}";
}
