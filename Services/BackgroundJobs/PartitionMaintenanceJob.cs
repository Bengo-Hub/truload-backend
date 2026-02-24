using TruLoad.Backend.Services.Interfaces.Infrastructure;

namespace TruLoad.Backend.Services.BackgroundJobs;

/// <summary>
/// Hangfire recurring job for weighing_transactions partition lifecycle management.
/// - Monthly: ensures upcoming partitions exist (next 12 months)
/// - Quarterly: archives partitions older than 24 months
/// </summary>
public class PartitionMaintenanceJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PartitionMaintenanceJob> _logger;

    public PartitionMaintenanceJob(
        IServiceScopeFactory scopeFactory,
        ILogger<PartitionMaintenanceJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Monthly: creates missing partitions for next 12 months + previous 1 month.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[PartitionMaintenanceJob] Running monthly partition maintenance");
        using var scope = _scopeFactory.CreateScope();
        var mvService = scope.ServiceProvider.GetRequiredService<IMaterializedViewService>();
        await mvService.EnsurePartitionsAsync(ct);
        _logger.LogInformation("[PartitionMaintenanceJob] Monthly partition maintenance completed");
    }

    /// <summary>
    /// Quarterly: detaches partitions older than 24 months (without dropping data).
    /// </summary>
    public async Task ExecuteQuarterlyAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[PartitionMaintenanceJob] Running quarterly partition archival");
        using var scope = _scopeFactory.CreateScope();
        var mvService = scope.ServiceProvider.GetRequiredService<IMaterializedViewService>();
        await mvService.RunQuarterlyMaintenanceAsync(ct);
        _logger.LogInformation("[PartitionMaintenanceJob] Quarterly partition archival completed");
    }
}
