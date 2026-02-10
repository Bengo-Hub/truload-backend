using TruLoad.Backend.DTOs.CaseManagement;

namespace TruLoad.Backend.Services.Interfaces.CaseManagement;

/// <summary>
/// Service interface for case closure checklist management.
/// Handles checklist updates, review requests, and approval workflow.
/// </summary>
public interface ICaseClosureChecklistService
{
    Task<CaseClosureChecklistDto?> GetByCaseIdAsync(Guid caseRegisterId, CancellationToken ct = default);
    Task<CaseClosureChecklistDto> CreateOrUpdateAsync(Guid caseRegisterId, UpdateChecklistRequest request, Guid userId, CancellationToken ct = default);
    Task<CaseClosureChecklistDto> RequestReviewAsync(Guid caseRegisterId, RequestReviewRequest request, Guid userId, CancellationToken ct = default);
    Task<CaseClosureChecklistDto> ApproveReviewAsync(Guid caseRegisterId, ReviewDecisionRequest request, Guid userId, CancellationToken ct = default);
    Task<CaseClosureChecklistDto> RejectReviewAsync(Guid caseRegisterId, ReviewDecisionRequest request, Guid userId, CancellationToken ct = default);
}
