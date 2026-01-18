using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Models.CaseManagement;
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
        return await Task.Run(() =>
        {
            var document = new WeightTicketDocument(transaction);
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
}
