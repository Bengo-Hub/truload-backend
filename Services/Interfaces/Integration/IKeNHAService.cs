using TruLoad.Backend.DTOs.Integration;

namespace TruLoad.Backend.Services.Interfaces.Integration;

/// <summary>
/// Service for KeNHA (Kenya National Highways Authority) integration.
/// Provides vehicle tag verification via the KeNHA API.
/// Used to check if a vehicle has an existing KeNHA tag/prohibition during weighing.
/// </summary>
public interface IKeNHAService
{
    /// <summary>
    /// Verify if a vehicle has an existing KeNHA tag by registration number.
    /// Returns tag details if found, null if no tag exists.
    /// </summary>
    Task<KeNHATagVerificationResult?> VerifyVehicleTagAsync(string regNo, CancellationToken ct = default);

    /// <summary>
    /// Check if KeNHA integration is configured and active.
    /// Used to conditionally trigger tag checks only when integration is available.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// Test connectivity to the KeNHA API.
    /// Returns true if the API is reachable and credentials are valid.
    /// </summary>
    Task<IntegrationHealthResult> TestConnectivityAsync(CancellationToken ct = default);
}
