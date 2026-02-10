using TruLoad.Backend.DTOs.CaseManagement;

namespace TruLoad.Backend.Services.Interfaces.CaseManagement;

/// <summary>
/// Service interface for managing case parties (officers, defendants, witnesses).
/// </summary>
public interface ICasePartyService
{
    Task<IEnumerable<CasePartyDto>> GetByCaseIdAsync(Guid caseRegisterId, CancellationToken ct = default);
    Task<CasePartyDto> AddPartyAsync(Guid caseRegisterId, AddCasePartyRequest request, CancellationToken ct = default);
    Task<CasePartyDto> UpdatePartyAsync(Guid partyId, UpdateCasePartyRequest request, CancellationToken ct = default);
    Task<bool> RemovePartyAsync(Guid partyId, CancellationToken ct = default);
}
