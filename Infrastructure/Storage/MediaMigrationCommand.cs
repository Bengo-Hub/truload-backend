using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging.Abstractions;
using TruLoad.Backend.Configuration;
using TruLoad.Backend.Services.Implementations.Infrastructure;

namespace TruLoad.Backend.Infrastructure.Storage;

/// <summary>
/// One-off move of media files from local disk to the private R2 bucket.
///
///   dotnet truload-backend.dll migrate-media [--apply] [dir ...]
///
/// Every file under each directory (default: /app/uploads and the configured media path) is
/// uploaded to truload/media/{path relative to that directory}, the same path the /media URL
/// uses, so stored URLs keep working. Objects that already exist with the same size are skipped,
/// so it is safe to run again. Without --apply it only reports. It always ends with a check that
/// every local file has an object of the same size, prints counts and bytes, and exits 0 only
/// when everything matches. Runs without starting the web host (no migrations, no seeding).
/// </summary>
public static class MediaMigrationCommand
{
    public const string Name = "migrate-media";
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public static async Task<int> RunAsync(string[] args, IConfiguration configuration)
    {
        var apply = args.Contains("--apply");
        var dirs = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
        if (dirs.Count == 0)
        {
            dirs.Add("/app/uploads");
            var mediaPath = Environment.GetEnvironmentVariable("MEDIA_STORAGE_PATH");
            if (string.IsNullOrWhiteSpace(mediaPath)) mediaPath = configuration["Media:StoragePath"];
            if (string.IsNullOrWhiteSpace(mediaPath)) mediaPath = "wwwroot/media";
            if (!Path.IsPathRooted(mediaPath)) mediaPath = Path.Combine(AppContext.BaseDirectory, mediaPath);
            dirs.Add(mediaPath);
        }

        var options = ObjectStorageOptions.FromConfiguration(configuration);
        using var storage = new S3ObjectStorage(options, NullLogger<S3ObjectStorage>.Instance);
        if (!storage.IsConfigured)
        {
            Console.Error.WriteLine("OBJECT_STORAGE_* is not configured; nothing can be migrated.");
            return 1;
        }

        var files = new List<(string Local, string Key, long Size)>();
        foreach (var dir in dirs.Distinct())
        {
            if (!Directory.Exists(dir))
            {
                Console.WriteLine($"skip {dir}: does not exist");
                continue;
            }
            foreach (var path in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(path);
                if (name.StartsWith('.')) continue; // .write-test and similar markers
                var relative = Path.GetRelativePath(dir, path).Replace('\\', '/');
                files.Add((path, ObjectStorageOptions.MediaKey(relative), new FileInfo(path).Length));
            }
        }

        var duplicateKeys = files.GroupBy(f => f.Key).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateKeys.Count > 0)
        {
            foreach (var k in duplicateKeys) Console.Error.WriteLine($"conflict: two local files map to {k}");
            return 3;
        }

        Console.WriteLine($"mode={(apply ? "apply" : "report")} files={files.Count} bytes={files.Sum(f => f.Size)} bucket=private prefix={ObjectStorageOptions.MediaKey(string.Empty)}");

        int uploaded = 0, skipped = 0, pending = 0;
        foreach (var f in files)
        {
            var remoteSize = await storage.GetSizeAsync(f.Key);
            if (remoteSize == f.Size)
            {
                skipped++;
                continue;
            }
            if (!apply)
            {
                pending++;
                Console.WriteLine($"to upload: {f.Key} ({f.Size} bytes)");
                continue;
            }
            if (!ContentTypes.TryGetContentType(f.Local, out var contentType)) contentType = "application/octet-stream";
            await using (var stream = File.OpenRead(f.Local))
            {
                await storage.PutAsync(f.Key, stream, contentType);
            }
            uploaded++;
            Console.WriteLine($"uploaded: {f.Key} ({f.Size} bytes)");
        }

        // Verify: every local file has an object with the same size.
        int matched = 0, mismatched = 0;
        foreach (var f in files)
        {
            var remoteSize = await storage.GetSizeAsync(f.Key);
            if (remoteSize == f.Size) matched++;
            else
            {
                mismatched++;
                if (apply) Console.Error.WriteLine($"mismatch: {f.Key} local={f.Size} remote={(remoteSize?.ToString() ?? "missing")}");
            }
        }
        var remoteObjects = await storage.ListAsync(ObjectStorageOptions.MediaKey(string.Empty));

        Console.WriteLine($"uploaded={uploaded} already_present={skipped} pending={pending} verified={matched}/{files.Count} " +
                          $"remote_objects={remoteObjects.Count} remote_bytes={remoteObjects.Sum(o => o.Size)}");
        return mismatched == 0 ? 0 : 2;
    }
}
