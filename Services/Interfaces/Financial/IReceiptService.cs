using TruLoad.Backend.DTOs.Financial;
using TruLoad.Backend.DTOs.Shared;

namespace TruLoad.Backend.Services.Interfaces.Financial;

/// <summary>
/// Service interface for receipt/payment management.
/// </summary>
public interface IReceiptService
{
    Task<ReceiptDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<ReceiptDto>> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default);
    Task<PagedResponse<ReceiptDto>> SearchAsync(ReceiptSearchCriteria criteria, CancellationToken ct = default);
    Task<ReceiptDto> RecordPaymentAsync(Guid invoiceId, RecordPaymentRequest request, Guid userId, CancellationToken ct = default);
    Task<ReceiptDto> VoidReceiptAsync(Guid id, string reason, Guid userId, CancellationToken ct = default);
    Task<ReceiptStatisticsDto> GetStatisticsAsync(CancellationToken ct = default);
    Task<string> GenerateReceiptNumberAsync(CancellationToken ct = default);
}
