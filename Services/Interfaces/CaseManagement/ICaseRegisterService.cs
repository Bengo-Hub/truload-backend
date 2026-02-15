using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.Models.CaseManagement;

namespace TruLoad.Backend.Services.Interfaces.CaseManagement;

public interface ICaseRegisterService
{
    /// <summary>
    /// Get case register by ID
    /// </summary>
    Task<CaseRegisterDto?> GetByIdAsync(Guid id);

    /// <summary>
    /// Get case register by case number
    /// </summary>
    Task<CaseRegisterDto?> GetByCaseNoAsync(string caseNo);

    /// <summary>
    /// Get case register by weighing ID
    /// </summary>
    Task<CaseRegisterDto?> GetByWeighingIdAsync(Guid weighingId);

    /// <summary>
    /// Search cases with filters
    /// </summary>
    Task<PagedResponse<CaseRegisterDto>> SearchCasesAsync(CaseSearchCriteria criteria);

    /// <summary>
    /// Create a new case register (manual entry)
    /// </summary>
    Task<CaseRegisterDto> CreateCaseAsync(CreateCaseRegisterRequest request, Guid userId);

    /// <summary>
    /// Auto-create case from weighing violation
    /// </summary>
    Task<CaseRegisterDto> CreateCaseFromWeighingAsync(Guid weighingId, Guid userId);

    /// <summary>
    /// Auto-create case from prohibition order
    /// </summary>
    Task<CaseRegisterDto> CreateCaseFromProhibitionAsync(Guid prohibitionOrderId, Guid userId);

    /// <summary>
    /// Update case register details
    /// </summary>
    Task<CaseRegisterDto> UpdateCaseAsync(Guid id, UpdateCaseRegisterRequest request, Guid userId);

    /// <summary>
    /// Close a case with disposition
    /// </summary>
    Task<CaseRegisterDto> CloseCaseAsync(Guid id, CloseCaseRequest request, Guid userId);

    /// <summary>
    /// Escalate case to case manager
    /// </summary>
    Task<CaseRegisterDto> EscalateToCaseManagerAsync(Guid id, Guid caseManagerId, Guid userId);

    /// <summary>
    /// Assign investigating officer (for court cases)
    /// </summary>
    Task<CaseRegisterDto> AssignInvestigatingOfficerAsync(Guid id, Guid officerId, Guid assignedById);

    /// <summary>
    /// Get case statistics
    /// </summary>
    Task<CaseStatisticsDto> GetCaseStatisticsAsync(DateTime? dateFrom = null, DateTime? dateTo = null);

    /// <summary>
    /// Delete a case (soft delete)
    /// </summary>
    Task<bool> DeleteCaseAsync(Guid id);
}
