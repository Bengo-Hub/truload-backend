using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Authorization.Attributes;
using Microsoft.AspNetCore.StaticFiles;
using TruLoad.Backend.Configuration;
using TruLoad.Backend.Services.Interfaces.Infrastructure;

namespace TruLoad.Backend.Controllers;

/// <summary>
/// File upload for organisation branding (logos, login page image). With platform object storage
/// configured (production) files go to the private R2 bucket under truload/media/{folder}/{file} and
/// are streamed back at /media/{folder}/{file}; otherwise they are written to the local media path.
/// The returned URL is the same in both cases.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class MediaController : ControllerBase
{
    public sealed class MediaUploadRequest
    {
        public IFormFile? File { get; set; }
        public string Folder { get; set; } = "organisation-branding";
    }

    private readonly IWebHostEnvironment _env;
    private readonly MediaUploadOptions _options;
    private readonly IObjectStorage _objectStorage;
    private readonly ILogger<MediaController> _logger;
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public MediaController(
        IWebHostEnvironment env,
        Microsoft.Extensions.Options.IOptions<MediaUploadOptions> options,
        IObjectStorage objectStorage,
        ILogger<MediaController> logger)
    {
        _env = env;
        _options = options.Value;
        _objectStorage = objectStorage;
        _logger = logger;
    }

    private string GetResolvedStoragePath()
    {
        var path = _options.StoragePath;
        if (!Path.IsPathRooted(path))
            path = Path.Combine(_env.ContentRootPath, path);
        return path;
    }

    /// <summary>
    /// Upload a file for organisation branding. Allowed: images (png, jpg, jpeg, gif, webp, svg). Returns URL path to use in branding (e.g. /media/organisation-branding/xxx.png).
    /// </summary>
    [HttpPost("upload")]
    [HasPermission("config.update")]
    [RequestSizeLimit(5 * 1024 * 1024)] // 5MB
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(
        [FromForm] MediaUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var file = request.File;
        var folder = request.Folder;

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided" });

        if (file.Length > _options.MaxFileSizeBytes)
            return BadRequest(new { message = "File too large. Maximum size is 5MB." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !_options.AllowedImageExtensions.Contains(ext))
            return BadRequest(new { message = "Invalid file type. Allowed: " + string.Join(", ", _options.AllowedImageExtensions) });

        var safeFolder = string.Join("_", folder.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrEmpty(safeFolder)) safeFolder = "uploads";

        if (_objectStorage.IsConfigured)
        {
            var objectName = $"{Guid.NewGuid():N}{ext}";
            if (!ContentTypes.TryGetContentType(objectName, out var contentType)) contentType = "application/octet-stream";
            try
            {
                await using var buffer = new MemoryStream();
                await file.CopyToAsync(buffer, cancellationToken);
                buffer.Position = 0;
                await _objectStorage.PutAsync(ObjectStorageOptions.MediaKey($"{safeFolder}/{objectName}"), buffer, contentType, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store media upload in object storage: {Folder}/{File}", safeFolder, objectName);
                return StatusCode(500, new { message = "Failed to save file" });
            }
            var objectUrl = $"/media/{safeFolder}/{objectName}";
            _logger.LogInformation("Media uploaded to object storage: {UrlPath}", objectUrl);
            return Ok(new { url = objectUrl });
        }

        var basePath = GetResolvedStoragePath();
        var dir = Path.Combine(basePath, safeFolder);
        try
        {
            Directory.CreateDirectory(dir);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create media directory: {Dir}", dir);
            return StatusCode(500, new { message = "Failed to create upload directory" });
        }

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(dir, fileName);
        try
        {
            await using (var stream = global::System.IO.File.Create(fullPath))
                await file.CopyToAsync(stream, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save upload: {Path}", fullPath);
            return StatusCode(500, new { message = "Failed to save file" });
        }

        var urlPath = $"/media/{safeFolder}/{fileName}";
        _logger.LogInformation("Media uploaded: {UrlPath}", urlPath);
        return Ok(new { url = urlPath });
    }
}
