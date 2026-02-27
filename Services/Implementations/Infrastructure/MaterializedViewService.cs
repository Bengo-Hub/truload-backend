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
}
