using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.DTOs.Yard;

namespace TruLoad.Backend.Services.Interfaces.Yard;

/// <summary>
/// Service interface for yard entry management operations.
/// Handles impounding, release, and status tracking of vehicles in the yard.
/// </summary>
public interface IYardService
{
    /// <summary>
    /// Get a yard entry by its ID.
    /// </summary>
    Task<YardEntryDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Get yard entry by weighing transaction ID.
    /// </summary>
    Task<YardEntryDto?> GetByWeighingIdAsync(Guid weighingId, CancellationToken ct = default);

    /// <summary>
    /// Search yard entries with filters and pagination.
    /// </summary>
    Task<PagedResponse<YardEntryDto>> SearchAsync(SearchYardEntriesRequest request, Guid? tenantStationId, CancellationToken ct = default);

    /// <summary>
    /// Create a new yard entry for a weighing transaction.
    /// </summary>
    Task<YardEntryDto> CreateAsync(CreateYardEntryRequest request, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Release a vehicle from the yard.
    /// </summary>
    Task<YardEntryDto> ReleaseAsync(Guid id, ReleaseYardEntryRequest request, Guid releasedById, CancellationToken ct = default);

    /// <summary>
    /// Update the status of a yard entry.
    /// </summary>
    Task<YardEntryDto> UpdateStatusAsync(Guid id, string status, Guid updatedById, CancellationToken ct = default);

    /// <summary>
    /// Get yard statistics (pending count, released today, etc.).
    /// </summary>
    Task<YardStatisticsDto> GetStatisticsAsync(Guid? stationId, CancellationToken ct = default);
}

/// <summary>
/// Yard statistics summary.
/// </summary>
public class YardStatisticsDto
{
    public int TotalPending { get; set; }
    public int ReleasedToday { get; set; }
    public int TotalEntriesToday { get; set; }
    public int Escalated { get; set; }
}
