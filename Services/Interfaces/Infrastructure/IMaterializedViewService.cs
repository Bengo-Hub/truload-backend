namespace TruLoad.Backend.Services.Interfaces.Infrastructure;

/// <summary>
/// Service for managing PostgreSQL materialized views and partition lifecycle.
/// Materialized views are pre-aggregated snapshots used by dashboard and report endpoints
/// to avoid expensive live aggregations on the base weighing_transactions table.
/// </summary>
public interface IMaterializedViewService
{
    /// <summary>
    /// Refreshes all 6 materialized views concurrently.
    /// Calls the DB function: SELECT refresh_all_materialized_views()
    /// Should run at least hourly via Hangfire.
    /// </summary>
    Task RefreshAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates monthly partitions for weighing_transactions covering the next 12 months
    /// and the previous 1 month. Safe to call repeatedly — skips existing partitions.
    /// Calls the DB function: SELECT create_weighing_partitions(12, 1)
    /// Should run on startup and monthly via Hangfire.
    /// </summary>
    Task EnsurePartitionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Archives or detaches weighing_transactions partitions older than 24 months.
    /// Calls the DB function: SELECT archive_old_weighing_partitions(24, false)
    /// Should run quarterly via Hangfire.
    /// </summary>
    Task RunQuarterlyMaintenanceAsync(CancellationToken ct = default);
}
