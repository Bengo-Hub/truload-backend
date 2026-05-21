using Microsoft.Extensions.DependencyInjection;
using TruLoad.Backend.Services.Interfaces.Portal;

namespace TruLoad.Backend.Services.BackgroundJobs;

/// <summary>
/// Hangfire job that generates a bulk ticket ZIP for the portal and writes it to a temp file.
/// The controller polls the temp file path to determine when the job is ready.
/// </summary>
public class BulkDownloadJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BulkDownloadJob> _logger;

    // Temp directory used by both this job and the download/status endpoints.
    public static string TempDir => Path.Combine(Path.GetTempPath(), "truload-bulk-downloads");

    public BulkDownloadJob(IServiceScopeFactory scopeFactory, ILogger<BulkDownloadJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync(string jobId, Guid userId, DateTime fromDate, DateTime toDate)
    {
        Directory.CreateDirectory(TempDir);
        var zipPath = Path.Combine(TempDir, $"{jobId}.zip");
        var errorPath = Path.Combine(TempDir, $"{jobId}.error");

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var portalService = scope.ServiceProvider.GetRequiredService<ITransporterPortalService>();

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            var (zipBytes, _) = await portalService.BulkDownloadTicketsAsync(userId, fromDate, toDate, cts.Token);

            await File.WriteAllBytesAsync(zipPath, zipBytes);
            _logger.LogInformation("Bulk download job {JobId} completed ({Bytes} bytes)", jobId, zipBytes.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk download job {JobId} failed", jobId);
            await File.WriteAllTextAsync(errorPath, ex.Message);
            // Rethrow so Hangfire marks the job as failed in its dashboard.
            throw;
        }
    }
}
