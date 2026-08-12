using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Services.Interfaces.Infrastructure;

namespace TruLoad.Backend.Services.BackgroundJobs;

/// <summary>
/// Best-effort, fire-and-forget backfill of a weighing transaction's LocationCounty/LocationSubcounty
/// (and Road) from its captured LocationLat/LocationLng, via <see cref="IGeocodingService"/>. Enqueued
/// on-demand (not a recurring job) right after a weighing is captured with coordinates but no
/// county/sub-county already resolved (mobile-unit captures). Runs in its own DI scope - never shares
/// the request-scoped DbContext from the capture request, per this project's standing rule for
/// background dispatch.
/// </summary>
public class GeocodeBackfillJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GeocodeBackfillJob> _logger;

    public GeocodeBackfillJob(IServiceScopeFactory scopeFactory, ILogger<GeocodeBackfillJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task GeocodeAsync(Guid weighingTransactionId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TruLoadDbContext>();
        var geocodingService = scope.ServiceProvider.GetRequiredService<IGeocodingService>();

        // Cross-tenant-filter safe: this job is keyed by a globally-unique transaction id, not a
        // "rows belonging to my tenant" query, so it must ignore the tenant query filter the same
        // way any get-or-create/global lookup in this codebase must.
        var transaction = await dbContext.WeighingTransactions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.Id == weighingTransactionId, ct);

        if (transaction == null)
        {
            _logger.LogWarning("[GeocodeBackfillJob] Weighing transaction {Id} not found - skipping", weighingTransactionId);
            return;
        }

        if (transaction.LocationLat == null || transaction.LocationLng == null)
        {
            _logger.LogDebug("[GeocodeBackfillJob] Weighing transaction {Id} has no coordinates - nothing to backfill", weighingTransactionId);
            return;
        }

        if (!string.IsNullOrWhiteSpace(transaction.LocationCounty) && !string.IsNullOrWhiteSpace(transaction.LocationSubcounty))
        {
            return; // already resolved (e.g. client-supplied) - nothing to do
        }

        var result = await geocodingService.ReverseGeocodeAsync(
            transaction.LocationLat.Value, transaction.LocationLng.Value, ct);

        var changed = false;
        if (string.IsNullOrWhiteSpace(transaction.LocationCounty) && !string.IsNullOrWhiteSpace(result.CountyName))
        {
            transaction.LocationCounty = result.CountyName;
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(transaction.LocationSubcounty) && !string.IsNullOrWhiteSpace(result.SubcountyName))
        {
            transaction.LocationSubcounty = result.SubcountyName;
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(transaction.LocationTown) && !string.IsNullOrWhiteSpace(result.RoadName))
        {
            // No dedicated "resolved road name" snapshot field exists on WeighingTransaction beyond
            // the RoadId FK (which needs a Roads table match, not a free-text name) - LocationTown is
            // the closest existing free-text "where" field for a client that supplied only coordinates.
            transaction.LocationTown = result.RoadName;
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("[GeocodeBackfillJob] Backfilled location fields for weighing {Id}", weighingTransactionId);
        }
    }
}
