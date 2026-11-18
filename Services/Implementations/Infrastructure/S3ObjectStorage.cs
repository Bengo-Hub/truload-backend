using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using TruLoad.Backend.Configuration;
using TruLoad.Backend.Services.Interfaces.Infrastructure;

namespace TruLoad.Backend.Services.Implementations.Infrastructure;

/// <summary>
/// <see cref="IObjectStorage"/> on the platform's private R2 bucket through the AWS SDK.
/// Credentials come from OBJECT_STORAGE_* only and are never logged. Registered as a singleton
/// (the S3 client is thread safe and keeps its connection pool).
/// </summary>
public sealed class S3ObjectStorage : IObjectStorage, IDisposable
{
    private readonly ObjectStorageOptions _options;
    private readonly ILogger<S3ObjectStorage> _logger;
    private readonly AmazonS3Client? _client;

    public S3ObjectStorage(IConfiguration configuration, ILogger<S3ObjectStorage> logger)
        : this(ObjectStorageOptions.FromConfiguration(configuration), logger)
    {
    }

    public S3ObjectStorage(ObjectStorageOptions options, ILogger<S3ObjectStorage> logger)
    {
        _options = options;
        _logger = logger;
        if (!_options.IsConfigured) return;

        var config = new AmazonS3Config
        {
            ServiceURL = _options.Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = _options.Region,
            // R2 does not need the SDK's default request checksums; only send them when an
            // operation requires one.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
        _client = new AmazonS3Client(new BasicAWSCredentials(_options.AccessKeyId, _options.SecretAccessKey), config);
    }

    public bool IsConfigured => _client != null;

    private AmazonS3Client Client =>
        _client ?? throw new InvalidOperationException("Object storage is not configured (OBJECT_STORAGE_* is missing).");

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _options.PrivateBucket,
            Key = key,
            InputStream = content,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            AutoCloseStream = false,
            DisablePayloadSigning = true,
        };
        await Client.PutObjectAsync(request, ct);
        _logger.LogInformation("Object stored: {Key}", key);
    }

    public async Task<StoredObject?> GetAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var response = await Client.GetObjectAsync(_options.PrivateBucket, key, ct);
            var contentType = response.Headers.ContentType;
            return new StoredObject(
                response.ResponseStream,
                string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                (long?)response.ContentLength,
                response.ETag);
        }
        catch (AmazonS3Exception ex) when (IsNotFound(ex))
        {
            return null;
        }
    }

    public async Task<long?> GetSizeAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var response = await Client.GetObjectMetadataAsync(_options.PrivateBucket, key, ct);
            return (long?)response.ContentLength;
        }
        catch (AmazonS3Exception ex) when (IsNotFound(ex))
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<StoredObjectInfo>> ListAsync(string prefix, CancellationToken ct = default)
    {
        var result = new List<StoredObjectInfo>();
        string? token = null;
        do
        {
            var response = await Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _options.PrivateBucket,
                Prefix = prefix,
                ContinuationToken = token,
            }, ct);
            foreach (var o in response.S3Objects ?? new List<S3Object>())
            {
                result.Add(new StoredObjectInfo(o.Key, ((long?)o.Size) ?? 0, ((DateTime?)o.LastModified)?.ToUniversalTime()));
            }
            token = response.IsTruncated == true ? response.NextContinuationToken : null;
        } while (!string.IsNullOrEmpty(token));
        return result;
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await Client.DeleteObjectAsync(_options.PrivateBucket, key, ct);
        _logger.LogInformation("Object deleted: {Key}", key);
    }

    private static bool IsNotFound(AmazonS3Exception ex) =>
        ex.StatusCode == HttpStatusCode.NotFound ||
        string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.Ordinal) ||
        string.Equals(ex.ErrorCode, "NotFound", StringComparison.Ordinal);

    public void Dispose() => _client?.Dispose();
}
