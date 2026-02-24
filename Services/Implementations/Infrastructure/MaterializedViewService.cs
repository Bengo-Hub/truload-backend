using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Services.Interfaces.Infrastructure;

namespace TruLoad.Backend.Services.Implementations.Infrastructure;

/// <summary>
/// Manages PostgreSQL materialized views and weighing_transactions partition lifecycle.
/// </summary>
public class MaterializedViewService : IMaterializedViewService
{
    private readonly TruLoadDbContext _context;
    private readonly ILogger<MaterializedViewService> _logger;

    public MaterializedViewService(
        TruLoadDbContext context,
        ILogger<MaterializedViewService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task RefreshAllAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("[MV] Starting refresh of all materialized views...");
        try
        {
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT refresh_all_materialized_views();", ct);
            _logger.LogInformation("[MV] All materialized views refreshed in {Elapsed}ms", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MV] Failed to refresh materialized views after {Elapsed}ms", sw.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task EnsurePartitionsAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("[Partition] Ensuring weighing_transactions partitions (12 ahead, 1 behind)...");
        try
        {
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT create_weighing_partitions(12, 1);", ct);
            _logger.LogInformation("[Partition] Partition check complete in {Elapsed}ms", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Partition] Failed to ensure partitions after {Elapsed}ms", sw.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task RunQuarterlyMaintenanceAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("[Partition] Running quarterly archive of partitions older than 24 months...");
        try
        {
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT archive_old_weighing_partitions(24, false);", ct);
            _logger.LogInformation("[Partition] Quarterly maintenance complete in {Elapsed}ms", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Partition] Quarterly maintenance failed after {Elapsed}ms", sw.ElapsedMilliseconds);
            throw;
        }
    }
}
