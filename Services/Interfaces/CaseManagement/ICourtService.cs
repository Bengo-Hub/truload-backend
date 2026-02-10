using TruLoad.Backend.DTOs.CaseManagement;

namespace TruLoad.Backend.Services.Interfaces.CaseManagement;

/// <summary>
/// Service interface for court registry management.
/// </summary>
public interface ICourtService
{
    Task<CourtDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CourtDto?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IEnumerable<CourtDto>> SearchAsync(CourtSearchCriteria criteria, CancellationToken ct = default);
    Task<CourtDto> CreateAsync(CreateCourtRequest request, CancellationToken ct = default);
    Task<CourtDto> UpdateAsync(Guid id, UpdateCourtRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
