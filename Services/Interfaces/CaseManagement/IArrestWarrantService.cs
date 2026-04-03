using TruLoad.Backend.DTOs.CaseManagement;

namespace TruLoad.Backend.Services.Interfaces.CaseManagement;

/// <summary>
/// Service interface for arrest warrant management.
/// Handles issuance, execution, and dropping of warrants.
/// </summary>
public interface IArrestWarrantService
{
    Task<ArrestWarrantDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<ArrestWarrantDto>> GetByCaseIdAsync(Guid caseRegisterId, CancellationToken ct = default);
    Task<IEnumerable<ArrestWarrantDto>> SearchAsync(ArrestWarrantSearchCriteria criteria, CancellationToken ct = default);
    Task<ArrestWarrantDto> CreateAsync(CreateArrestWarrantRequest request, Guid userId, CancellationToken ct = default);
    Task<ArrestWarrantDto> ExecuteAsync(Guid id, ExecuteWarrantRequest request, CancellationToken ct = default);
    Task<ArrestWarrantDto> DropAsync(Guid id, DropWarrantRequest request, CancellationToken ct = default);
    Task<ArrestWarrantDto> LiftAsync(Guid id, LiftWarrantRequest request, CancellationToken ct = default);
}
