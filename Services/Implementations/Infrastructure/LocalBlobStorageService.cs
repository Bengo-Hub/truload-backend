using System.Security.Cryptography;
using TruLoad.Backend.Services.Interfaces.Infrastructure;

namespace TruLoad.Backend.Services.Implementations.Infrastructure;

/// <summary>
/// Local blob storage service for storing files in the file system.
/// Supports SHA-256 checksums, configurable base path (local folder, wwwroot, or K8s volumes).
/// </summary>
public class LocalBlobStorageService : IBlobStorageService
{
    private readonly string _basePath;
    private readonly ILogger<LocalBlobStorageService> _logger;

    public LocalBlobStorageService(IConfiguration configuration, ILogger<LocalBlobStorageService> logger)
    {
        _logger = logger;

        // Read from appsettings.json or environment variable (K8s mount override)
        _basePath = Environment.GetEnvironmentVariable("FILE_STORAGE_PATH")
                    ?? configuration["FileStorage:BasePath"]
                    ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

        // Ensure base directory exists
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
            _logger.LogInformation("Created file storage directory: {BasePath}", _basePath);
        }

        _logger.LogInformation("Local blob storage initialized with base path: {BasePath}", _basePath);
    }

    /// <summary>
    /// Saves a file to local storage with SHA-256 checksum calculation.
    /// </summary>
    public async Task<(string FilePath, string Checksum, long FileSize)> SaveAsync(
        Stream fileStream,
        string fileName,
        string folder,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate unique file name to avoid collisions
            var fileExtension = Path.GetExtension(fileName);
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";

            // Construct full folder path
            var folderPath = Path.Combine(_basePath, folder);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                _logger.LogInformation("Created subfolder: {FolderPath}", folderPath);
            }

            // Construct full file path
            var fullPath = Path.Combine(folderPath, uniqueFileName);

            // Calculate checksum while writing to disk
            string checksum;
            long fileSize;

            using (var sha256 = SHA256.Create())
            using (var fileWriteStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            {
                // Use CryptoStream to calculate checksum while writing
                using (var cryptoStream = new CryptoStream(fileWriteStream, sha256, CryptoStreamMode.Write))
                {
                    await fileStream.CopyToAsync(cryptoStream, 81920, cancellationToken);
                }

                checksum = BitConverter.ToString(sha256.Hash!).Replace("-", "").ToLowerInvariant();
                fileSize = fileWriteStream.Length;
            }

            // Return relative path (without base path) for database storage
            var relativePath = Path.Combine(folder, uniqueFileName).Replace("\\", "/");

            _logger.LogInformation(
                "File saved: {FileName} → {RelativePath} ({FileSize} bytes, checksum: {Checksum})",
                fileName, relativePath, fileSize, checksum);

            return (relativePath, checksum, fileSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file: {FileName} to folder: {Folder}", fileName, folder);
            throw;
        }
    }

    /// <summary>
    /// Deletes a file from local storage.
    /// </summary>
    public Task DeleteAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = Path.Combine(_basePath, filePath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogInformation("File deleted: {FilePath}", filePath);
            }
            else
            {
                _logger.LogWarning("File not found for deletion: {FilePath}", filePath);
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Gets a file stream for reading.
    /// </summary>
    public Task<Stream> GetFileStreamAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_basePath, filePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {filePath}", filePath);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        return Task.FromResult(stream);
    }

    /// <summary>
    /// Gets the URL for accessing a file.
    /// For local storage, returns relative path (frontend will construct full URL).
    /// </summary>
    public string GetFileUrl(string filePath)
    {
        // Return relative path for frontend URL construction
        // Frontend will prepend base URL (e.g., https://api.truload.com/files/)
        return $"/files/{filePath.Replace("\\", "/")}";
    }

    /// <summary>
    /// Checks if a file exists in storage.
    /// </summary>
    public Task<bool> ExistsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_basePath, filePath);
        return Task.FromResult(File.Exists(fullPath));
    }
}
