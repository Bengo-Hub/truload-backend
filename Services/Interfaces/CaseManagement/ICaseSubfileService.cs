using TruLoad.Backend.DTOs.CaseManagement;

namespace TruLoad.Backend.Services.Interfaces.CaseManagement;

/// <summary>
/// Service interface for case subfile (document) management.
/// Handles CRUD for case documents across subfile types B-J.
/// </summary>
public interface ICaseSubfileService
{
    Task<CaseSubfileDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<CaseSubfileDto>> GetByCaseIdAsync(Guid caseRegisterId, CancellationToken ct = default);
    Task<IEnumerable<CaseSubfileDto>> GetByCaseAndTypeAsync(Guid caseRegisterId, Guid subfileTypeId, CancellationToken ct = default);
    Task<IEnumerable<CaseSubfileDto>> SearchAsync(CaseSubfileSearchCriteria criteria, CancellationToken ct = default);
    Task<SubfileCompletionDto> GetSubfileCompletionAsync(Guid caseRegisterId, CancellationToken ct = default);
    Task<CaseSubfileDto> CreateAsync(CreateCaseSubfileRequest request, Guid userId, CancellationToken ct = default);
    Task<CaseSubfileDto> UpdateAsync(Guid id, UpdateCaseSubfileRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
