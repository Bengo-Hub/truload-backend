using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Models.Prosecution;
using TruLoad.Backend.Models.Financial;
using TruLoad.Backend.Services.Interfaces.Infrastructure;
using TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments;

namespace TruLoad.Backend.Services.Implementations.Infrastructure;

public class QuestPdfService : IPdfService
{
    private readonly TruLoadDbContext _context;

    public QuestPdfService(TruLoadDbContext context)
    {
        _context = context;
        // License is registered in Program.cs
    }

    public async Task<byte[]> GenerateWeightTicketAsync(WeighingTransaction transaction)
    {
        // Resolve organization name from station
        string? organizationName = null;
        if (transaction.StationId != Guid.Empty)
        {
            organizationName = await _context.Stations
                .Where(s => s.Id == transaction.StationId)
                .Select(s => s.Organization.Name)
                .FirstOrDefaultAsync();
        }

        return await Task.Run(() =>
        {
            var document = new WeightTicketDocument(transaction, organizationName);
            return document.Generate();
        });
    }

    public async Task<byte[]> GenerateProhibitionOrderAsync(ProhibitionOrder order)
    {
        return await Task.Run(() =>
        {
            var document = new ProhibitionOrderDocument(order);
            return document.Generate();
        });
    }

    public async Task<byte[]> GenerateLoadCorrectionMemoAsync(Guid caseRegisterId, WeighingTransaction originalWeighing, WeighingTransaction reweighing)
    {
        return await Task.Run(async () =>
        {
            // Get case number for the document
            var caseRegister = await _context.CaseRegisters
                .Where(c => c.Id == caseRegisterId)
                .Select(c => c.CaseNo)
                .FirstOrDefaultAsync();

            var document = new LoadCorrectionMemoDocument(
                originalWeighing,
                reweighing,
                caseRegister ?? caseRegisterId.ToString());
            return document.Generate();
        });
    }

    public async Task<byte[]> GenerateComplianceCertificateAsync(Guid caseRegisterId, WeighingTransaction reweighing)
    {
        return await Task.Run(async () =>
        {
            // Get case number for the document
            var caseRegister = await _context.CaseRegisters
                .Where(c => c.Id == caseRegisterId)
                .Select(c => c.CaseNo)
                .FirstOrDefaultAsync();

            var certificateNo = $"COMP-{caseRegister ?? caseRegisterId.ToString()}";

            var document = new ComplianceCertificateDocument(
                reweighing,
                caseRegister ?? caseRegisterId.ToString(),
                certificateNo);
            return document.Generate();
        });
    }

    public async Task<byte[]> GenerateSpecialReleaseCertificateAsync(SpecialRelease specialRelease)
    {
        return await Task.Run(() =>
        {
            var document = new SpecialReleaseCertificateDocument(specialRelease);
            return document.Generate();
        });
    }

    public async Task<byte[]> GenerateChargeSheetAsync(Guid prosecutionCaseId, CancellationToken ct = default)
    {
        var prosecutionCase = await _context.ProsecutionCases
            .Include(p => p.CaseRegister)
            .Include(p => p.Weighing)
                .ThenInclude(w => w!.Vehicle)
            .Include(p => p.Weighing)
                .ThenInclude(w => w!.Station)
            .Include(p => p.ProsecutionOfficer)
            .Include(p => p.Act)
            .FirstOrDefaultAsync(p => p.Id == prosecutionCaseId && p.DeletedAt == null, ct)
            ?? throw new InvalidOperationException($"Prosecution case {prosecutionCaseId} not found");

        var document = new ChargeSheetDocument(prosecutionCase, prosecutionCase.CaseRegister);
        return document.Generate();
    }

    public async Task<byte[]> GenerateCourtMinutesAsync(Guid hearingId, CancellationToken ct = default)
    {
        var hearing = await _context.CourtHearings
            .Include(h => h.CaseRegister)
            .Include(h => h.HearingType)
            .Include(h => h.HearingStatus)
            .Include(h => h.HearingOutcome)
            .FirstOrDefaultAsync(h => h.Id == hearingId && h.DeletedAt == null, ct)
            ?? throw new InvalidOperationException($"Court hearing {hearingId} not found");

        var document = new CourtMinutesDocument(hearing, hearing.CaseRegister);
        return document.Generate();
    }

    public async Task<byte[]> GenerateInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.ProsecutionCase)
                .ThenInclude(p => p!.CaseRegister)
            .Include(i => i.ProsecutionCase)
                .ThenInclude(p => p!.Weighing)
                    .ThenInclude(w => w!.Station)
            .Include(i => i.Receipts)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.DeletedAt == null, ct)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} not found");

        // Get organization name from station or default
        var organizationName = invoice.ProsecutionCase?.Weighing?.Station?.Name;

        var document = new InvoiceDocument(invoice, organizationName);
        return document.Generate();
    }

    public async Task<byte[]> GenerateReceiptAsync(Guid receiptId, CancellationToken ct = default)
    {
        var receipt = await _context.Receipts
            .Include(r => r.Invoice)
                .ThenInclude(i => i!.ProsecutionCase)
                    .ThenInclude(p => p!.CaseRegister)
            .Include(r => r.Invoice)
                .ThenInclude(i => i!.ProsecutionCase)
                    .ThenInclude(p => p!.Weighing)
                        .ThenInclude(w => w!.Vehicle)
            .Include(r => r.Invoice)
                .ThenInclude(i => i!.ProsecutionCase)
                    .ThenInclude(p => p!.Weighing)
                        .ThenInclude(w => w!.Station)
            .Include(r => r.Invoice)
                .ThenInclude(i => i!.Receipts)
            .Include(r => r.ReceivedBy)
            .FirstOrDefaultAsync(r => r.Id == receiptId && r.DeletedAt == null, ct)
            ?? throw new InvalidOperationException($"Receipt {receiptId} not found");

        var document = new ReceiptDocument(receipt);
        return document.Generate();
    }
}
