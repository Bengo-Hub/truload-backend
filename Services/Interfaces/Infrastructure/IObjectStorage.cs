namespace TruLoad.Backend.Services.Interfaces.Infrastructure;

/// <summary>An object read from storage. Dispose the stream when done.</summary>
public sealed record StoredObject(Stream Content, string ContentType, long? Length, string? ETag);

/// <summary>A listed object (key and size in bytes).</summary>
public sealed record StoredObjectInfo(string Key, long Size, DateTime? LastModified);

/// <summary>
/// Private platform object storage (R2). Objects are never public: they are read back
/// through the API (streaming) after any access check.
/// </summary>
public interface IObjectStorage
{
    /// <summary>True when OBJECT_STORAGE_* is configured; every other call needs it.</summary>
    bool IsConfigured { get; }

    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Returns null when the object does not exist.</summary>
    Task<StoredObject?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>Size in bytes, or null when the object does not exist.</summary>
    Task<long?> GetSizeAsync(string key, CancellationToken ct = default);

    Task<IReadOnlyList<StoredObjectInfo>> ListAsync(string prefix, CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);
}
