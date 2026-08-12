using TruLoad.Backend.Services.Interfaces.Infrastructure;

namespace TruLoad.Backend.Services.Implementations.Infrastructure;

/// <summary>
/// Default <see cref="ICountyBoundaryResolver"/> - always returns (null, null).
///
/// Point-in-polygon county/sub-county resolution needs a real Kenya administrative-boundary
/// GeoJSON dataset (e.g. HDX/OCHA Kenya county+subcounty boundaries). No such dataset exists
/// anywhere in this repo or its dependencies today (confirmed during the reporting/geo audit -
/// the seeded `Counties`/`Subcounty` tables have names/codes only, no geometry). Fabricating
/// approximate polygon coordinates from memory was deliberately rejected: a wrong county/sub-county
/// attribution on a weighing has real legal-enforcement consequences (case venue/jurisdiction),
/// so "unresolved" is the correct degrade-to behaviour until real boundary data is supplied.
///
/// To activate: replace this DI registration (<c>Program.cs</c>) with an implementation that loads
/// a boundary GeoJSON (via NetTopologySuite point-in-polygon) from a configured path and matches
/// each feature's county/subcounty property against the existing seeded <c>Counties</c>/<c>Subcounty</c>
/// tables by name. No other code needs to change - <see cref="GeocodingService"/> and
/// <see cref="TruLoad.Backend.Services.BackgroundJobs.GeocodeBackfillJob"/> already consume this
/// interface, not a concrete class.
/// </summary>
public class NotConfiguredCountyBoundaryResolver : ICountyBoundaryResolver
{
    private static bool _hasLoggedOnce;
    private readonly ILogger<NotConfiguredCountyBoundaryResolver> _logger;

    public NotConfiguredCountyBoundaryResolver(ILogger<NotConfiguredCountyBoundaryResolver> logger)
    {
        _logger = logger;
    }

    public Task<(string? County, string? Subcounty)> ResolveAsync(decimal lat, decimal lng, CancellationToken ct = default)
    {
        if (!_hasLoggedOnce)
        {
            _hasLoggedOnce = true;
            _logger.LogInformation(
                "County/sub-county reverse-geocoding is not active - no boundary GeoJSON dataset " +
                "configured. Road-name resolution (Valhalla) still works. See NotConfiguredCountyBoundaryResolver " +
                "for how to activate once a real Kenya admin-boundary dataset is available.");
        }

        return Task.FromResult<(string?, string?)>((null, null));
    }
}
