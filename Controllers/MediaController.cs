using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.Configuration;

namespace TruLoad.Backend.Controllers;

/// <summary>
/// File upload for organisation branding (logos, login page image). Files are stored in configurable media path (production: tuload-backend-media).
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class MediaController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly MediaUploadOptions _options;
    private readonly ILogger<MediaController> _logger;

    public MediaController(
        IWebHostEnvironment env,
        Microsoft.Extensions.Options.IOptions<MediaUploadOptions> options,
        ILogger<MediaController> logger)
    {
        _env = env;
        _options = options.Value;
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
        [FromForm] IFormFile? file,
        [FromForm] string folder = "organisation-branding",
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided" });

        if (file.Length > _options.MaxFileSizeBytes)
            return BadRequest(new { message = "File too large. Maximum size is 5MB." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !_options.AllowedImageExtensions.Contains(ext))
            return BadRequest(new { message = "Invalid file type. Allowed: " + string.Join(", ", _options.AllowedImageExtensions) });

        var basePath = GetResolvedStoragePath();
        var safeFolder = string.Join("_", folder.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrEmpty(safeFolder)) safeFolder = "uploads";
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
