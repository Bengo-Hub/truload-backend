using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Models.CaseManagement;

namespace TruLoad.Backend.Services.Interfaces.Infrastructure;

public interface IPdfService
{
    // Existing methods
    Task<byte[]> GenerateWeightTicketAsync(WeighingTransaction transaction);
    Task<byte[]> GenerateProhibitionOrderAsync(ProhibitionOrder order);

    // New legal document methods
    Task<byte[]> GenerateLoadCorrectionMemoAsync(Guid caseRegisterId, WeighingTransaction originalWeighing, WeighingTransaction reweighing);
    Task<byte[]> GenerateComplianceCertificateAsync(Guid caseRegisterId, WeighingTransaction reweighing);
    Task<byte[]> GenerateSpecialReleaseCertificateAsync(SpecialRelease specialRelease);
}
