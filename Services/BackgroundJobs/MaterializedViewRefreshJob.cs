using TruLoad.Backend.Services.Interfaces.Infrastructure;

namespace TruLoad.Backend.Services.BackgroundJobs;

/// <summary>
/// Hangfire recurring job that refreshes all PostgreSQL materialized views.
/// Scheduled to run every hour. Views are used by dashboard and report endpoints
/// instead of expensive live aggregations on weighing_transactions.
/// </summary>
public class MaterializedViewRefreshJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MaterializedViewRefreshJob> _logger;

    public MaterializedViewRefreshJob(
        IServiceScopeFactory scopeFactory,
        ILogger<MaterializedViewRefreshJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Refreshes all 6 materialized views concurrently via the DB function.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[MaterializedViewRefreshJob] Starting scheduled MV refresh");
        using var scope = _scopeFactory.CreateScope();
        var mvService = scope.ServiceProvider.GetRequiredService<IMaterializedViewService>();
        await mvService.RefreshAllAsync(ct);
        _logger.LogInformation("[MaterializedViewRefreshJob] Scheduled MV refresh completed");
    }
}
