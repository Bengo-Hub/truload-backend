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

}
