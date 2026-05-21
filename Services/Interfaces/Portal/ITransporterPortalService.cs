using Microsoft.AspNetCore.Http;
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

    /// <summary>
    /// Generates a PDF ticket for a specific weighing. Verifies transporter ownership.
    /// </summary>
    Task<(byte[] Bytes, string FileName)> DownloadWeighingPdfAsync(Guid userId, Guid weighingId);

    /// <summary>
    /// Gets all active team members for the transporter linked to userId.
    /// </summary>
    Task<List<PortalTeamMemberDto>> GetTeamMembersAsync(Guid userId);

    /// <summary>
    /// Invites a new team member by email. Only the owner (PortalAccountId) may invite.
    /// Sends an invitation email with a secure token link.
    /// </summary>
    Task<(bool Success, string Message)> InviteTeamMemberAsync(
        Guid userId, string userEmail, string userName, InviteTeamMemberRequest request);

    /// <summary>
    /// Removes a team member from the transporter's portal. Only the owner may remove members.
    /// </summary>
    Task<(bool Success, string Message)> RemoveTeamMemberAsync(Guid userId, Guid targetUserId);

    /// <summary>
    /// Accepts a portal invitation by token. Sets up a team membership for the calling user.
    /// </summary>
    Task<(bool Success, string Message)> AcceptInviteAsync(
        Guid userId, string userEmail, AcceptPortalInviteRequest request);

    /// <summary>
    /// Generates a ZIP archive of completed commercial weighing ticket PDFs in the given date range.
    /// Requires DataExport feature. Capped at 500 transactions.
    /// </summary>
    Task<(byte[] Bytes, string FileName)> BulkDownloadTicketsAsync(Guid userId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the count of completed commercial transactions in the given date range for the transporter.
    /// Used to decide sync vs async bulk download path.
    /// </summary>
    Task<int> CountBulkDownloadTicketsAsync(Guid userId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken);

    /// <summary>
    /// Imports vehicles from a CSV file (registration, make, model, axle_count, tare_weight_kg).
    /// Returns counts of imported/skipped rows plus per-row error messages.
    /// </summary>
    Task<(int Imported, int Skipped, List<string> Errors)> ImportVehiclesAsync(Guid userId, IFormFile file, CancellationToken cancellationToken);
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
