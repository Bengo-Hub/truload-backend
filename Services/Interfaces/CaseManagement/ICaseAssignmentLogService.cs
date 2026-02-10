using TruLoad.Backend.DTOs.CaseManagement;

namespace TruLoad.Backend.Services.Interfaces.CaseManagement;

/// <summary>
/// Service interface for IO assignment audit trail.
/// Tracks chain of custody - history of which IOs were assigned to cases.
/// </summary>
public interface ICaseAssignmentLogService
{
    Task<IEnumerable<CaseAssignmentLogDto>> GetByCaseIdAsync(Guid caseRegisterId, CancellationToken ct = default);
    Task<CaseAssignmentLogDto?> GetCurrentAssignmentAsync(Guid caseRegisterId, CancellationToken ct = default);
    Task<CaseAssignmentLogDto> LogAssignmentAsync(Guid caseRegisterId, LogAssignmentRequest request, Guid assignedById, CancellationToken ct = default);
}
