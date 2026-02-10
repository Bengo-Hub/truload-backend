using TruLoad.Backend.DTOs.CaseManagement;

namespace TruLoad.Backend.Services.Interfaces.CaseManagement;

/// <summary>
/// Service interface for compliance certificate queries.
/// Read-only — certificates are auto-created after successful reweigh.
/// </summary>
public interface IComplianceCertificateService
{
    Task<ComplianceCertificateDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<ComplianceCertificateDto>> GetByCaseIdAsync(Guid caseRegisterId, CancellationToken ct = default);
    Task<ComplianceCertificateDto?> GetByWeighingIdAsync(Guid weighingId, CancellationToken ct = default);
}
