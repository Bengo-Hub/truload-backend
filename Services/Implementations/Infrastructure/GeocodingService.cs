using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TruLoad.Backend.DTOs.Integration;
using TruLoad.Backend.Services.Interfaces.Infrastructure;

namespace TruLoad.Backend.Services.Implementations.Infrastructure;

/// <summary>
/// Reverse-geocodes via the already-deployed Valhalla routing engine (road name, real Kenya OSM
/// data) plus <see cref="ICountyBoundaryResolver"/> (county/sub-county). Mirrors <c>KeNHAService</c>'s
/// integration pattern: typed HttpClient, graceful degradation - any failure returns nulls rather
/// than throwing, since this is always a best-effort backfill, never a request-blocking dependency.
/// </summary>
public class GeocodingService : IGeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly ICountyBoundaryResolver _boundaryResolver;
    private readonly ILogger<GeocodingService> _logger;

    public GeocodingService(
        HttpClient httpClient,
        ICountyBoundaryResolver boundaryResolver,
        ILogger<GeocodingService> logger)
    {
        _httpClient = httpClient;
        _boundaryResolver = boundaryResolver;
        _logger = logger;
    }

    public async Task<GeocodeResult> ReverseGeocodeAsync(decimal lat, decimal lng, CancellationToken ct = default)
    {
        var result = new GeocodeResult();

        try
        {
            result.RoadName = await ResolveRoadNameAsync(lat, lng, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Valhalla road-name lookup failed for ({Lat},{Lng})", lat, lng);
        }

        try
        {
            var (county, subcounty) = await _boundaryResolver.ResolveAsync(lat, lng, ct);
            result.CountyName = county;
            result.SubcountyName = subcounty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "County/sub-county boundary resolution failed for ({Lat},{Lng})", lat, lng);
        }

        return result;
    }

    /// <summary>
    /// Calls Valhalla's <c>/locate</c> action, which returns the nearest matching OSM edges (road
    /// segments) for a coordinate, including their names - real road data, no reverse-geocoding
    /// service needed for this half of the lookup.
    /// </summary>
    private async Task<string?> ResolveRoadNameAsync(decimal lat, decimal lng, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            locations = new[] { new { lat, lon = lng } },
            costing = "auto"
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/locate")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogDebug("Valhalla /locate returned {StatusCode} for ({Lat},{Lng})", response.StatusCode, lat, lng);
            return null;
        }

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseBody);

        // Response shape: [ { edges: [ { names: ["Mombasa Road", "A109"], ... }, ... ], nodes: [...] } ]
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            return null;

        var first = doc.RootElement[0];
        if (!first.TryGetProperty("edges", out var edges) || edges.ValueKind != JsonValueKind.Array || edges.GetArrayLength() == 0)
            return null;

        var firstEdge = edges[0];
        if (!firstEdge.TryGetProperty("names", out var names) || names.ValueKind != JsonValueKind.Array || names.GetArrayLength() == 0)
            return null;

        return names[0].GetString();
    }
}
