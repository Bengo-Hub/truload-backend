using TruLoad.Backend.DTOs.Portal;

namespace TruLoad.Backend.Services.Interfaces.Portal;

/// <summary>
/// Service for the Transporter Self-Service Portal.
/// Provides cross-tenant read access to weighing data for registered transporters.
/// </summary>
public interface ITransporterPortalService
{
    /// <summary>
    /// Registers a portal account by matching an existing transporter record
    /// via email, phone, or transporter code. Sets PortalAccountId and PortalAccountEmail.
    /// </summary>
    Task<PortalRegistrationResult> RegisterAsync(Guid userId, string userEmail, PortalRegistrationRequest request);

    /// <summary>
    /// Gets paginated weighing history for the transporter across all organizations.
    /// </summary>
    Task<PortalPagedResult<PortalWeighingDto>> GetWeighingsAsync(
        Guid userId, int page, int pageSize,
        DateTime? fromDate, DateTime? toDate,
        Guid? vehicleId, Guid? organizationId);

    /// <summary>
    /// Gets a single weighing detail.
    /// </summary>
    Task<PortalWeighingDto> GetWeighingDetailAsync(Guid userId, Guid weighingId);

    /// <summary>
    /// Gets the transporter's vehicles.
    /// </summary>
    Task<List<PortalVehicleDto>> GetVehiclesAsync(Guid userId);

    /// <summary>
    /// Gets weight trend data for a specific vehicle.
    /// </summary>
    Task<List<PortalVehicleWeightTrendDto>> GetVehicleWeightTrendsAsync(Guid userId, Guid vehicleId);

    /// <summary>
    /// Gets the transporter's drivers (drivers who have driven this transporter's vehicles).
    /// </summary>
    Task<List<PortalDriverDto>> GetDriversAsync(Guid userId);

    /// <summary>
    /// Gets performance metrics for a specific driver.
    /// </summary>
    Task<PortalDriverPerformanceDto> GetDriverPerformanceAsync(Guid userId, Guid driverId);

    /// <summary>
    /// Gets consignment tracking data for the transporter.
    /// </summary>
    Task<PortalPagedResult<PortalConsignmentDto>> GetConsignmentsAsync(
        Guid userId, int page, int pageSize,
        DateTime? fromDate, DateTime? toDate);

    /// <summary>
    /// Gets the transporter's subscription status and feature access flags.
    /// </summary>
    Task<PortalSubscriptionDto> GetFeatureAccessAsync(Guid userId);
}

/// <summary>
/// Result of a portal registration attempt.
/// </summary>
public class PortalRegistrationResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public Guid? TransporterId { get; set; }
    public string? TransporterName { get; set; }
}

/// <summary>
/// Generic paged result wrapper for portal queries.
/// </summary>
public class PortalPagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
