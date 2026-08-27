using TruLoad.Backend.DTOs.Financial;

namespace TruLoad.Backend.Services.Interfaces.Financial;

/// <summary>
/// Manages CommercialTariffRule rows for the current organisation — the tariff/rate engine
/// CreateCommercialInvoiceAsync resolves against when creating a commercial weighing invoice.
/// </summary>
public interface ICommercialTariffService
{
    Task<List<CommercialTariffRuleDto>> GetAllAsync(CancellationToken ct = default);
    Task<CommercialTariffRuleDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CommercialTariffRuleDto> CreateAsync(CreateCommercialTariffRuleRequest request, CancellationToken ct = default);
    Task<CommercialTariffRuleDto?> UpdateAsync(Guid id, UpdateCommercialTariffRuleRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
