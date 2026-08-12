using TruLoad.Backend.DTOs.Integration;

namespace TruLoad.Backend.Services.Interfaces.Infrastructure;

/// <summary>
/// Reverse-geocodes a lat/lng coordinate to a road name, county, and sub-county - used to backfill
/// <c>WeighingTransaction.LocationCounty/LocationSubcounty</c> for mobile-unit weighings that only
/// captured raw coordinates. Self-hosted, OSM-based only (never Google Maps): road name comes from
/// the already-deployed Valhalla routing engine; county/sub-county from <see cref="ICountyBoundaryResolver"/>.
/// Station-scoped reports never need this - they read the station's own registered County/Subcounty/Road
/// FKs directly.
/// </summary>
public interface IGeocodingService
{
    /// <summary>
    /// Resolves what's available for the given coordinate. Individual fields on the result may be
    /// null when unresolvable (e.g. no boundary dataset configured yet) - callers must treat this
    /// as best-effort, never a hard dependency.
    /// </summary>
    Task<GeocodeResult> ReverseGeocodeAsync(decimal lat, decimal lng, CancellationToken ct = default);
}

/// <summary>
/// Resolves a coordinate to a Kenya County/Sub-County name by point-in-polygon match against real
/// administrative boundary geometry. Separate from <see cref="IGeocodingService"/> so the (currently
/// unavailable) boundary-dataset dependency can be swapped in later without touching the Valhalla
/// road-lookup half of geocoding, which works today.
/// </summary>
public interface ICountyBoundaryResolver
{
    /// <summary>Returns (county, subcounty) names, or (null, null) if no boundary dataset is configured/matched.</summary>
    Task<(string? County, string? Subcounty)> ResolveAsync(decimal lat, decimal lng, CancellationToken ct = default);
}
