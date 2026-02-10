using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Services.Interfaces.CaseManagement;

namespace TruLoad.Backend.Services.Implementations.CaseManagement;

/// <summary>
/// Service implementation for compliance certificate queries.
/// Read-only — certificates are auto-created after successful reweigh.
/// </summary>
public class ComplianceCertificateService : IComplianceCertificateService
{
    private readonly TruLoadDbContext _context;

    public ComplianceCertificateService(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<ComplianceCertificateDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var cert = await _context.ComplianceCertificates
            .Include(c => c.CaseRegister)
            .Include(c => c.Weighing)
            .Include(c => c.LoadCorrectionMemo)
            .Include(c => c.IssuedBy)
            .FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, ct);

        return cert == null ? null : MapToDto(cert);
    }

    public async Task<IEnumerable<ComplianceCertificateDto>> GetByCaseIdAsync(Guid caseRegisterId, CancellationToken ct = default)
    {
        var certs = await _context.ComplianceCertificates
            .Include(c => c.CaseRegister)
            .Include(c => c.Weighing)
            .Include(c => c.LoadCorrectionMemo)
            .Include(c => c.IssuedBy)
            .Where(c => c.CaseRegisterId == caseRegisterId && c.DeletedAt == null)
            .OrderByDescending(c => c.IssuedAt)
            .ToListAsync(ct);

        return certs.Select(MapToDto);
    }

    public async Task<ComplianceCertificateDto?> GetByWeighingIdAsync(Guid weighingId, CancellationToken ct = default)
    {
        var cert = await _context.ComplianceCertificates
            .Include(c => c.CaseRegister)
            .Include(c => c.Weighing)
            .Include(c => c.LoadCorrectionMemo)
            .Include(c => c.IssuedBy)
            .FirstOrDefaultAsync(c => c.WeighingId == weighingId && c.DeletedAt == null, ct);

        return cert == null ? null : MapToDto(cert);
    }

    private ComplianceCertificateDto MapToDto(ComplianceCertificate cert)
    {
        return new ComplianceCertificateDto
        {
            Id = cert.Id,
            CertificateNo = cert.CertificateNo,
            CaseRegisterId = cert.CaseRegisterId,
            CaseNo = cert.CaseRegister?.CaseNo,
            WeighingId = cert.WeighingId,
            WeighingTicketNo = cert.Weighing?.TicketNumber,
            LoadCorrectionMemoId = cert.LoadCorrectionMemoId,
            MemoNo = cert.LoadCorrectionMemo?.MemoNo,
            IssuedById = cert.IssuedById,
            IssuedByName = cert.IssuedBy?.FullName,
            IssuedAt = cert.IssuedAt,
            CreatedAt = cert.CreatedAt
        };
    }
}
