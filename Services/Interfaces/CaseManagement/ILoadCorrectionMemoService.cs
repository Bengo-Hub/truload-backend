using TruLoad.Backend.DTOs.CaseManagement;

namespace TruLoad.Backend.Services.Interfaces.CaseManagement;

/// <summary>
/// Service interface for load correction memo queries.
/// Read-only — memos are auto-created by the weighing/receipt workflow.
/// </summary>
public interface ILoadCorrectionMemoService
{
    Task<LoadCorrectionMemoDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<LoadCorrectionMemoDto>> GetByCaseIdAsync(Guid caseRegisterId, CancellationToken ct = default);
    Task<LoadCorrectionMemoDto?> GetByWeighingIdAsync(Guid weighingId, CancellationToken ct = default);
}
