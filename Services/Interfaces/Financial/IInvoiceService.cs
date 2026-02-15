using TruLoad.Backend.DTOs.Financial;
using TruLoad.Backend.DTOs.Shared;

namespace TruLoad.Backend.Services.Interfaces.Financial;

/// <summary>
/// Service interface for invoice management.
/// </summary>
public interface IInvoiceService
{
    Task<InvoiceDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<InvoiceDto>> GetByProsecutionIdAsync(Guid prosecutionCaseId, CancellationToken ct = default);
    Task<PagedResponse<InvoiceDto>> SearchAsync(InvoiceSearchCriteria criteria, CancellationToken ct = default);
    Task<InvoiceDto> GenerateInvoiceAsync(Guid prosecutionCaseId, Guid userId, CancellationToken ct = default);
    Task<InvoiceDto> UpdateStatusAsync(Guid id, string status, Guid userId, CancellationToken ct = default);
    Task<InvoiceDto> VoidInvoiceAsync(Guid id, string reason, Guid userId, CancellationToken ct = default);
    Task<InvoiceStatisticsDto> GetStatisticsAsync(CancellationToken ct = default);
    Task<List<InvoiceAgingBucketDto>> GetAgingAsync(CancellationToken ct = default);
    Task<string> GenerateInvoiceNumberAsync(CancellationToken ct = default);
}
