using TruLoad.Backend.DTOs.CaseManagement;

namespace TruLoad.Backend.Services.Interfaces.CaseManagement;

public interface ICaseDocumentService
{
    Task<List<CaseDocumentDto>> GetDocumentsByCaseIdAsync(Guid caseRegisterId, CancellationToken ct = default);
    Task<CaseDocumentSummaryDto> GetDocumentSummaryAsync(Guid caseRegisterId, CancellationToken ct = default);
}
