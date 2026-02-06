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
            CreatedById = userId,
            IssuedAt = DateTime.UtcNow,
            IsApproved = false,
            IsRejected = false,
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

        if (specialRelease.IsRejected)
            throw new InvalidOperationException("Cannot approve a rejected special release");

        if (specialRelease.IsApproved)
            throw new InvalidOperationException("Special release is already approved");

        specialRelease.IsApproved = true;
        specialRelease.ApprovedById = approvedById;
        specialRelease.ApprovedAt = DateTime.UtcNow;
        specialRelease.UpdatedAt = DateTime.UtcNow;

        var updated = await _specialReleaseRepository.UpdateAsync(specialRelease);
        return MapToDto(updated);
    }

    public async Task<SpecialReleaseDto> RejectSpecialReleaseAsync(Guid id, RejectSpecialReleaseRequest request, Guid rejectedById)
    {
        var specialRelease = await _specialReleaseRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Special release {id} not found");

        if (specialRelease.IsApproved)
            throw new InvalidOperationException("Cannot reject an approved special release");

        if (specialRelease.IsRejected)
            throw new InvalidOperationException("Special release is already rejected");

        specialRelease.IsRejected = true;
        specialRelease.RejectedById = rejectedById;
        specialRelease.RejectedAt = DateTime.UtcNow;
        specialRelease.RejectionReason = request.RejectionReason;
        specialRelease.UpdatedAt = DateTime.UtcNow;

        // Reset case disposition back to pending if rejected
        var caseRegister = await _caseRegisterRepository.GetByIdAsync(specialRelease.CaseRegisterId);
        if (caseRegister != null)
        {
            var pendingDisposition = await _context.DispositionTypes
                .FirstOrDefaultAsync(dt => dt.Code == "PENDING");

            if (pendingDisposition != null)
            {
                caseRegister.DispositionTypeId = pendingDisposition.Id;
                await _caseRegisterRepository.UpdateAsync(caseRegister);
            }
        }

        var updated = await _specialReleaseRepository.UpdateAsync(specialRelease);
        return MapToDto(updated);
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
            LoadCorrectionMemoId = specialRelease.LoadCorrectionMemoId,
            ComplianceCertificateId = specialRelease.ComplianceCertificateId,
            AuthorizedById = specialRelease.AuthorizedById,
            AuthorizedAt = specialRelease.IssuedAt,
            IsApproved = specialRelease.IsApproved,
            ApprovedById = specialRelease.ApprovedById,
            ApprovedAt = specialRelease.ApprovedAt,
            IsRejected = specialRelease.IsRejected,
            RejectedById = specialRelease.RejectedById,
            RejectedAt = specialRelease.RejectedAt,
            RejectionReason = specialRelease.RejectionReason,
            CreatedById = specialRelease.CreatedById,
            CreatedAt = specialRelease.CreatedAt,
            UpdatedAt = specialRelease.UpdatedAt
        };
    }
}
