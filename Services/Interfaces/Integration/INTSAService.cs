using TruLoad.Backend.DTOs.Integration;

namespace TruLoad.Backend.Services.Interfaces.Integration;

/// <summary>
/// Service for NTSA (National Transport and Safety Authority) integration.
/// Provides vehicle search (owner/details lookup) and demerit points synchronization.
/// Based on KenLoad V2 integration patterns with NTSA API.
/// </summary>
public interface INTSAService
{
    /// <summary>
    /// Search for vehicle details from NTSA by registration number.
    /// Returns owner info, vehicle details, inspection status, and caveat info.
    /// Results are cached in Redis for 24 hours to reduce API calls.
    /// </summary>
    Task<NTSAVehicleSearchResult?> SearchVehicleAsync(string regNo, CancellationToken ct = default);

    /// <summary>
    /// Check if NTSA integration is configured and active.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// Test connectivity to the NTSA API.
    /// </summary>
    Task<IntegrationHealthResult> TestConnectivityAsync(CancellationToken ct = default);
}
