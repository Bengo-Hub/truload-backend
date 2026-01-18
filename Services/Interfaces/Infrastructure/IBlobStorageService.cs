namespace TruLoad.Backend.Services.Interfaces.Infrastructure;

/// <summary>
/// Blob storage service interface for file operations.
/// Supports local file storage with SHA-256 checksums and file size tracking.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Saves a file to storage with SHA-256 checksum calculation.
    /// </summary>
    /// <param name="fileStream">Stream containing the file data</param>
    /// <param name="fileName">Original file name</param>
    /// <param name="folder">Storage folder path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple containing: relative file path, SHA-256 checksum, file size in bytes</returns>
    Task<(string FilePath, string Checksum, long FileSize)> SaveAsync(
        Stream fileStream,
        string fileName,
        string folder,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from storage.
    /// </summary>
    /// <param name="filePath">Relative path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a file stream for reading.
    /// </summary>
    /// <param name="filePath">Relative path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream for reading the file</returns>
    Task<Stream> GetFileStreamAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the public URL for accessing a file.
    /// </summary>
    /// <param name="filePath">Relative path to the file</param>
    /// <returns>URL for accessing the file</returns>
    string GetFileUrl(string filePath);

    /// <summary>
    /// Checks if a file exists in storage.
    /// </summary>
    /// <param name="filePath">Relative path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if file exists, false otherwise</returns>
    Task<bool> ExistsAsync(string filePath, CancellationToken cancellationToken = default);
}
