using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Repositories.CaseManagement;
using TruLoad.Backend.Services.Interfaces.CaseManagement;
using TruLoad.Backend.Services.Interfaces.Infrastructure;

namespace TruLoad.Backend.Services.Implementations.CaseManagement;

public class SpecialReleaseService : ISpecialReleaseService
{
    private readonly ISpecialReleaseRepository _specialReleaseRepository;
    private readonly ICaseRegisterRepository _caseRegisterRepository;
    private readonly IPdfService _pdfService;
    private readonly TruLoadDbContext _context;

    public SpecialReleaseService(
        ISpecialReleaseRepository specialReleaseRepository,
        ICaseRegisterRepository caseRegisterRepository,
        IPdfService pdfService,
        TruLoadDbContext context)
    {
        _specialReleaseRepository = specialReleaseRepository;
        _caseRegisterRepository = caseRegisterRepository;
        _pdfService = pdfService;
        _context = context;
    }

    public async Task<SpecialReleaseDto?> GetByIdAsync(Guid id)
    {
        var specialRelease = await _specialReleaseRepository.GetByIdAsync(id);
        return specialRelease == null ? null : MapToDto(specialRelease);
    }

    public async Task<SpecialReleaseDto?> GetByCertificateNoAsync(string certificateNo)
    {
        var specialRelease = await _specialReleaseRepository.GetByCertificateNoAsync(certificateNo);
        return specialRelease == null ? null : MapToDto(specialRelease);
    }

    public async Task<IEnumerable<SpecialReleaseDto>> GetByCaseRegisterIdAsync(Guid caseRegisterId)
    {
        var releases = await _specialReleaseRepository.GetByCaseRegisterIdAsync(caseRegisterId);
        return releases.Select(MapToDto);
    }

    public async Task<IEnumerable<SpecialReleaseDto>> GetPendingApprovalsAsync(int pageNumber = 1, int pageSize = 20)
    {
        var releases = await _specialReleaseRepository.GetPendingApprovalsAsync(pageNumber, pageSize);
        return releases.Select(MapToDto);
    }

    public async Task<SpecialReleaseDto> RequestSpecialReleaseAsync(CreateSpecialReleaseRequest request, Guid userId)
    {
        // Verify case exists and is eligible
        var caseRegister = await _caseRegisterRepository.GetByIdAsync(request.CaseRegisterId)
            ?? throw new InvalidOperationException($"Case {request.CaseRegisterId} not found");

        // Check if case is already closed
        var closedStatus = await _context.CaseStatuses.FirstOrDefaultAsync(cs => cs.Code == "CLOSED");
        if (closedStatus != null && caseRegister.CaseStatusId == closedStatus.Id)
            throw new InvalidOperationException("Cannot request special release for a closed case");

        // Generate certificate number
        var certificateNo = await _specialReleaseRepository.GenerateNextCertificateNumberAsync();

        var specialRelease = new SpecialRelease
        {
            Id = Guid.NewGuid(),
            CertificateNo = certificateNo,
            CaseRegisterId = request.CaseRegisterId,
            ReleaseTypeId = request.ReleaseTypeId,
            Reason = request.Reason,
            RedistributionAllowed = request.RequiresRedistribution,
            ReweighRequired = request.RequiresReweigh,
            AuthorizedById = userId,
            IssuedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _specialReleaseRepository.CreateAsync(specialRelease);

        // Update case register disposition to "SPECIAL_RELEASE"
        var specialReleaseDisposition = await _context.DispositionTypes
            .FirstOrDefaultAsync(dt => dt.Code == "SPECIAL_RELEASE");

        if (specialReleaseDisposition != null)
        {
            caseRegister.DispositionTypeId = specialReleaseDisposition.Id;
            await _caseRegisterRepository.UpdateAsync(caseRegister);
        }

        return MapToDto(created);
    }

    public async Task<SpecialReleaseDto> ApproveSpecialReleaseAsync(Guid id, ApproveSpecialReleaseRequest request, Guid approvedById)
    {
        var specialRelease = await _specialReleaseRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Special release {id} not found");

        // Note: The model doesn't have explicit approval fields,
        // so we just update the authorizedById if needed
        specialRelease.AuthorizedById = approvedById;
        specialRelease.IssuedAt = DateTime.UtcNow;

        var updated = await _specialReleaseRepository.UpdateAsync(specialRelease);
        return MapToDto(updated);
    }

    public async Task<SpecialReleaseDto> RejectSpecialReleaseAsync(Guid id, RejectSpecialReleaseRequest request, Guid rejectedById)
    {
        // For rejection, we could either delete the record or add a rejection flag in the future
        // For now, we'll just throw an exception indicating the operation isn't fully supported yet
        throw new NotImplementedException("Rejection workflow requires model update to add rejection fields");
    }

    public async Task<byte[]> GenerateSpecialReleaseCertificatePdfAsync(Guid id)
    {
        var specialRelease = await _specialReleaseRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Special release {id} not found");

        return await _pdfService.GenerateSpecialReleaseCertificateAsync(specialRelease);
    }

    private SpecialReleaseDto MapToDto(SpecialRelease specialRelease)
    {
        return new SpecialReleaseDto
        {
            Id = specialRelease.Id,
            CertificateNo = specialRelease.CertificateNo,
            CaseRegisterId = specialRelease.CaseRegisterId,
            CaseNo = specialRelease.CaseRegister?.CaseNo ?? string.Empty,
            ReleaseTypeId = specialRelease.ReleaseTypeId,
            ReleaseType = specialRelease.ReleaseType?.Name ?? string.Empty,
            Reason = specialRelease.Reason,
            RequiresRedistribution = specialRelease.RedistributionAllowed,
            RequiresReweigh = specialRelease.ReweighRequired,
            LoadCorrectionMemoId = null, // Not in current model
            ComplianceCertificateId = null, // Not in current model
            AuthorizedById = specialRelease.AuthorizedById,
            AuthorizedAt = specialRelease.IssuedAt,
            IsApproved = true, // Simplified - any issued release is considered approved
            ApprovedById = specialRelease.AuthorizedById,
            ApprovedAt = specialRelease.IssuedAt,
            IsRejected = false, // Not supported in current model
            RejectedById = null,
            RejectedAt = null,
            RejectionReason = null,
            CreatedById = specialRelease.AuthorizedById, // Using authorizedBy as creator
            CreatedAt = specialRelease.CreatedAt,
            UpdatedAt = specialRelease.CreatedAt
        };
    }
}
