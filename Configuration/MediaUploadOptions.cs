namespace TruLoad.Backend.Configuration;

/// <summary>
/// Options for media file uploads. In production, set StoragePath to a persistent volume (e.g. tuload-backend-media).
/// </summary>
public class MediaUploadOptions
{
    public const string SectionName = "Media";

    /// <summary>
    /// Base directory for uploads. Default: wwwroot/media. Production: mount tuload-backend-media and set to e.g. /mnt/tuload-backend-media.
    /// </summary>
    public string StoragePath { get; set; } = "wwwroot/media";

    /// <summary>
    /// Maximum file size in bytes. Default 5MB.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>
    /// Allowed image extensions for branding uploads.
    /// </summary>
    public string[] AllowedImageExtensions { get; set; } = { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg" };
}
