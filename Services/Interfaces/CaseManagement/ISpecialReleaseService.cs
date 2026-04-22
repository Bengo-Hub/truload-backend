using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.DTOs.Shared;

namespace TruLoad.Backend.Services.Interfaces.CaseManagement;

public interface ISpecialReleaseService
{
    /// <summary>
    /// Get special release by ID
    /// </summary>
    Task<SpecialReleaseDto?> GetByIdAsync(Guid id);

    /// <summary>
    /// Get special release by certificate number
    /// </summary>
    Task<SpecialReleaseDto?> GetByCertificateNoAsync(string certificateNo);

    /// <summary>
    /// Get all special releases for a case
    /// </summary>
    Task<IEnumerable<SpecialReleaseDto>> GetByCaseRegisterIdAsync(Guid caseRegisterId);

    /// <summary>
    /// Get pending approvals with optional search filters
    /// </summary>
    Task<PagedResponse<SpecialReleaseDto>> GetPendingApprovalsAsync(
        string? caseNo = null,
        string? releaseType = null,
        DateTime? from = null,
        DateTime? to = null,
        int pageNumber = 1,
        int pageSize = 20);

    /// <summary>
    /// Request special release for a case
    /// </summary>
    Task<SpecialReleaseDto> RequestSpecialReleaseAsync(CreateSpecialReleaseRequest request, Guid userId);

    /// <summary>
    /// Approve special release
    /// </summary>
    Task<SpecialReleaseDto> ApproveSpecialReleaseAsync(Guid id, ApproveSpecialReleaseRequest request, Guid approvedById);

    /// <summary>
    /// Reject special release
    /// </summary>
    Task<SpecialReleaseDto> RejectSpecialReleaseAsync(Guid id, RejectSpecialReleaseRequest request, Guid rejectedById);

    /// <summary>
    /// Generate special release certificate PDF
    /// </summary>
    Task<byte[]> GenerateSpecialReleaseCertificatePdfAsync(Guid id);
}
