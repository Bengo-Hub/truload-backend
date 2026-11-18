using System.Security.Cryptography;
using Microsoft.AspNetCore.StaticFiles;
using TruLoad.Backend.Configuration;
using TruLoad.Backend.Services.Interfaces.Infrastructure;

namespace TruLoad.Backend.Services.Implementations.Infrastructure;

/// <summary>
/// <see cref="IBlobStorageService"/> on the private R2 bucket. Rows keep the same relative path
/// the local implementation returned (folder/uuid.ext); the object key is truload/files/{path}.
/// Registered instead of <see cref="LocalBlobStorageService"/> when object storage is configured.
/// </summary>
public class ObjectStorageBlobStorageService : IBlobStorageService
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    private readonly IObjectStorage _storage;
    private readonly ILogger<ObjectStorageBlobStorageService> _logger;

    public ObjectStorageBlobStorageService(IObjectStorage storage, ILogger<ObjectStorageBlobStorageService> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public async Task<(string FilePath, string Checksum, long FileSize)> SaveAsync(
        Stream fileStream, string fileName, string folder, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        var relativePath = $"{folder.Replace('\\', '/').Trim('/')}/{Guid.NewGuid()}{extension}";

        // Buffer once so the checksum, the size and the upload all see the same bytes.
        await using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer, 81920, cancellationToken);
        buffer.Position = 0;
        var checksum = Convert.ToHexString(await SHA256.HashDataAsync(buffer, cancellationToken)).ToLowerInvariant();
        buffer.Position = 0;

        if (!ContentTypes.TryGetContentType(fileName, out var contentType)) contentType = "application/octet-stream";
        await _storage.PutAsync(ObjectStorageOptions.FileKey(relativePath), buffer, contentType, cancellationToken);

        _logger.LogInformation("File saved to object storage: {FileName} -> {RelativePath} ({Size} bytes)",
            fileName, relativePath, buffer.Length);
        return (relativePath, checksum, buffer.Length);
    }

    public Task DeleteAsync(string filePath, CancellationToken cancellationToken = default) =>
        _storage.DeleteAsync(ObjectStorageOptions.FileKey(filePath), cancellationToken);

    public async Task<Stream> GetFileStreamAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var obj = await _storage.GetAsync(ObjectStorageOptions.FileKey(filePath), cancellationToken);
        return obj?.Content ?? throw new FileNotFoundException($"File not found: {filePath}", filePath);
    }

    public string GetFileUrl(string filePath) => $"/files/{filePath.Replace("\\", "/")}";

    public async Task<bool> ExistsAsync(string filePath, CancellationToken cancellationToken = default) =>
        await _storage.GetSizeAsync(ObjectStorageOptions.FileKey(filePath), cancellationToken) != null;
}
