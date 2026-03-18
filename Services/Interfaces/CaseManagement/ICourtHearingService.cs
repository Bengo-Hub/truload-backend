using TruLoad.Backend.DTOs.CaseManagement;

namespace TruLoad.Backend.Services.Interfaces.CaseManagement;

/// <summary>
/// Service interface for court hearing management.
/// Handles scheduling, adjournment, and completion of court hearings.
/// </summary>
public interface ICourtHearingService
{
    /// <summary>
    /// Get court hearing by ID
    /// </summary>
    Task<CourtHearingDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Get all hearings for a case
    /// </summary>
    Task<IEnumerable<CourtHearingDto>> GetByCaseIdAsync(Guid caseRegisterId, CancellationToken ct = default);

    /// <summary>
    /// Get the next scheduled hearing for a case
    /// </summary>
    Task<CourtHearingDto?> GetNextScheduledAsync(Guid caseRegisterId, CancellationToken ct = default);

    /// <summary>
    /// Get hearings by court within a date range
    /// </summary>
    Task<IEnumerable<CourtHearingDto>> GetByCourtAsync(Guid courtId, DateTime fromDate, DateTime toDate, CancellationToken ct = default);

    /// <summary>
    /// Search hearings with filters
    /// </summary>
    Task<IEnumerable<CourtHearingDto>> SearchAsync(CourtHearingSearchCriteria criteria, CancellationToken ct = default);

    /// <summary>
    /// Schedule a new court hearing
    /// </summary>
    Task<CourtHearingDto> ScheduleHearingAsync(Guid caseRegisterId, CreateCourtHearingRequest request, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Update an existing hearing
    /// </summary>
    Task<CourtHearingDto> UpdateHearingAsync(Guid id, UpdateCourtHearingRequest request, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Adjourn a hearing with reason and next date
    /// </summary>
    Task<CourtHearingDto> AdjournHearingAsync(Guid id, AdjournHearingRequest request, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Complete a hearing with outcome
    /// </summary>
    Task<CourtHearingDto> CompleteHearingAsync(Guid id, CompleteHearingRequest request, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Delete a hearing (soft delete)
    /// </summary>
    Task<bool> DeleteHearingAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Get hearing statistics for dashboard
    /// </summary>
    Task<Dictionary<string, int>> GetHearingStatisticsAsync(DateTime? dateFrom = null, DateTime? dateTo = null, Guid? stationId = null, CancellationToken ct = default);
}
