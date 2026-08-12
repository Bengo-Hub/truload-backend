namespace TruLoad.Backend.DTOs.Integration;

/// <summary>
/// Connection settings for the already-deployed Valhalla routing engine (namespace `logistics`,
/// Kenya-only OSM/Geofabrik data). Reused for road-name reverse lookup instead of standing up a
/// separate geocoding service - see <see cref="TruLoad.Backend.Services.Interfaces.Infrastructure.IGeocodingService"/>.
/// </summary>
public class ValhallaOptions
{
    public const string SectionName = "Valhalla";

    public string BaseUrl { get; set; } = "http://valhalla.logistics.svc.cluster.local:8002";
    public int TimeoutSeconds { get; set; } = 5;
}

/// <summary>Result of a reverse-geocode lookup for a single lat/lng coordinate.</summary>
public class GeocodeResult
{
    /// <summary>Nearest road/edge name from Valhalla's <c>/locate</c> response, if resolved.</summary>
    public string? RoadName { get; set; }

    /// <summary>County name, resolved via <see cref="Interfaces.Infrastructure.ICountyBoundaryResolver"/>.</summary>
    public string? CountyName { get; set; }

    /// <summary>Sub-county name, resolved via <see cref="Interfaces.Infrastructure.ICountyBoundaryResolver"/>.</summary>
    public string? SubcountyName { get; set; }
}
