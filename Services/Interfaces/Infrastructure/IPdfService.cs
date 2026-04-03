using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Models.CaseManagement;

namespace TruLoad.Backend.Services.Interfaces.Infrastructure;

public interface IPdfService
{
    // Weighing documents
    Task<byte[]> GenerateWeightTicketAsync(WeighingTransaction transaction);
    Task<byte[]> GenerateProhibitionOrderAsync(ProhibitionOrder order);
    Task<byte[]> GeneratePermitAsync(Permit permit);

    // Case management documents
    Task<byte[]> GenerateLoadCorrectionMemoAsync(Guid caseRegisterId, WeighingTransaction originalWeighing, WeighingTransaction reweighing);
    Task<byte[]> GenerateComplianceCertificateAsync(Guid caseRegisterId, WeighingTransaction reweighing);
    Task<byte[]> GenerateSpecialReleaseCertificateAsync(SpecialRelease specialRelease);

    // Prosecution documents
    Task<byte[]> GenerateChargeSheetAsync(Guid prosecutionCaseId, CancellationToken ct = default);
    Task<byte[]> GenerateCourtMinutesAsync(Guid hearingId, CancellationToken ct = default);

    // Case file documents
    Task<byte[]> GenerateCoverPageAsync(Guid caseRegisterId, CancellationToken ct = default);

    // Financial documents
    Task<byte[]> GenerateInvoiceAsync(Guid invoiceId, CancellationToken ct = default);
    Task<byte[]> GenerateReceiptAsync(Guid receiptId, CancellationToken ct = default);
}
