using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.Services.Interfaces.CaseManagement;

namespace TruLoad.Backend.Services.Implementations.CaseManagement;

/// <summary>
/// Aggregates documents from all case-related sources (weighing, prosecution,
/// invoices, receipts, court hearings, subfiles, special releases).
/// </summary>
public class CaseDocumentService : ICaseDocumentService
{
    private readonly TruLoadDbContext _context;

    public CaseDocumentService(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<List<CaseDocumentDto>> GetDocumentsByCaseIdAsync(Guid caseRegisterId, CancellationToken ct = default)
    {
        var documents = new List<CaseDocumentDto>();

        var caseRegister = await _context.CaseRegisters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == caseRegisterId && c.DeletedAt == null, ct);

        if (caseRegister == null) return documents;

        // 1. Weight Ticket (from weighing transaction)
        if (caseRegister.WeighingId.HasValue)
        {
            var weighing = await _context.WeighingTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == caseRegister.WeighingId && w.DeletedAt == null, ct);

            if (weighing != null)
            {
                documents.Add(new CaseDocumentDto
                {
                    Id = weighing.Id,
                    DocumentType = "WeightTicket",
                    DisplayName = $"Weight Ticket - {weighing.TicketNumber ?? weighing.Id.ToString()[..8]}",
                    ReferenceNo = weighing.TicketNumber,
                    DownloadUrl = $"/api/v1/weighing-transactions/{weighing.Id}/ticket/pdf",
                    Status = weighing.ControlStatus,
                    CreatedAt = weighing.CreatedAt
                });
            }
        }

        // 2. Prosecution Charge Sheet
        var prosecution = await _context.ProsecutionCases
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.CaseRegisterId == caseRegisterId && p.DeletedAt == null, ct);

        if (prosecution != null)
        {
            documents.Add(new CaseDocumentDto
            {
                Id = prosecution.Id,
                DocumentType = "ChargeSheet",
                DisplayName = $"Charge Sheet - {prosecution.CertificateNo ?? prosecution.Id.ToString()[..8]}",
                ReferenceNo = prosecution.CertificateNo,
                DownloadUrl = $"/api/v1/prosecutions/{prosecution.Id}/charge-sheet",
                Status = prosecution.Status,
                CreatedAt = prosecution.CreatedAt
            });
        }

        // 3. Invoices
        var invoices = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.CaseRegisterId == caseRegisterId && i.DeletedAt == null)
            .ToListAsync(ct);

        foreach (var invoice in invoices)
        {
            documents.Add(new CaseDocumentDto
            {
                Id = invoice.Id,
                DocumentType = "Invoice",
                DisplayName = $"Invoice - {invoice.InvoiceNo}",
                ReferenceNo = invoice.InvoiceNo,
                DownloadUrl = $"/api/v1/invoices/{invoice.Id}/pdf",
                Status = invoice.Status,
                CreatedAt = invoice.CreatedAt
            });
        }

        // 4. Receipts (via invoices)
        var invoiceIds = invoices.Select(i => i.Id).ToList();
        if (invoiceIds.Count > 0)
        {
            var receipts = await _context.Receipts
                .AsNoTracking()
                .Where(r => invoiceIds.Contains(r.InvoiceId) && r.DeletedAt == null)
                .ToListAsync(ct);

            foreach (var receipt in receipts)
            {
                documents.Add(new CaseDocumentDto
                {
                    Id = receipt.Id,
                    DocumentType = "Receipt",
                    DisplayName = $"Receipt - {receipt.ReceiptNo}",
                    ReferenceNo = receipt.ReceiptNo,
                    DownloadUrl = $"/api/v1/receipts/{receipt.Id}/pdf",
                    Status = "Paid",
                    CreatedAt = receipt.CreatedAt
                });
            }
        }

        // 5. Court Minutes (hearings with minute notes)
        var hearings = await _context.CourtHearings
            .AsNoTracking()
            .Where(h => h.CaseRegisterId == caseRegisterId && h.DeletedAt == null && h.MinuteNotes != null)
            .ToListAsync(ct);

        foreach (var hearing in hearings)
        {
            documents.Add(new CaseDocumentDto
            {
                Id = hearing.Id,
                DocumentType = "CourtMinutes",
                DisplayName = $"Court Minutes - {hearing.HearingDate:yyyy-MM-dd}",
                DownloadUrl = $"/api/v1/hearings/{hearing.Id}/minutes",
                CreatedAt = hearing.CreatedAt
            });
        }

        // 6. Special Release Certificates
        var releases = await _context.SpecialReleases
            .AsNoTracking()
            .Include(r => r.ReleaseType)
            .Where(r => r.CaseRegisterId == caseRegisterId && r.DeletedAt == null)
            .ToListAsync(ct);

        foreach (var release in releases)
        {
            documents.Add(new CaseDocumentDto
            {
                Id = release.Id,
                DocumentType = "SpecialReleaseCertificate",
                DisplayName = $"Special Release - {release.CertificateNo ?? release.Id.ToString()[..8]}",
                ReferenceNo = release.CertificateNo,
                DownloadUrl = $"/api/v1/case/special-releases/{release.Id}/certificate/pdf",
                Status = release.IsApproved ? "Approved" : release.IsRejected ? "Rejected" : "Pending",
                CreatedAt = release.CreatedAt
            });
        }

        // 7. Subfiles (uploaded documents)
        var subfiles = await _context.CaseSubfiles
            .AsNoTracking()
            .Where(s => s.CaseRegisterId == caseRegisterId && s.DeletedAt == null)
            .ToListAsync(ct);

        foreach (var subfile in subfiles)
        {
            documents.Add(new CaseDocumentDto
            {
                Id = subfile.Id,
                DocumentType = "Subfile",
                DisplayName = subfile.SubfileName,
                DownloadUrl = subfile.FileUrl ?? string.Empty,
                CreatedAt = subfile.CreatedAt
            });
        }

        return documents.OrderByDescending(d => d.CreatedAt).ToList();
    }

    public async Task<CaseDocumentSummaryDto> GetDocumentSummaryAsync(Guid caseRegisterId, CancellationToken ct = default)
    {
        var docs = await GetDocumentsByCaseIdAsync(caseRegisterId, ct);

        return new CaseDocumentSummaryDto
        {
            TotalDocuments = docs.Count,
            WeightTickets = docs.Count(d => d.DocumentType == "WeightTicket"),
            ChargeSheets = docs.Count(d => d.DocumentType == "ChargeSheet"),
            Invoices = docs.Count(d => d.DocumentType == "Invoice"),
            Receipts = docs.Count(d => d.DocumentType == "Receipt"),
            CourtMinutes = docs.Count(d => d.DocumentType == "CourtMinutes"),
            SpecialReleaseCertificates = docs.Count(d => d.DocumentType == "SpecialReleaseCertificate"),
            Subfiles = docs.Count(d => d.DocumentType == "Subfile"),
        };
    }
}
