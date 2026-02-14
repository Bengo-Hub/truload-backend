using TruLoad.Backend.DTOs.Financial;

namespace TruLoad.Backend.Services.Interfaces.Financial;

public interface ICurrencyService
{
    Task<CurrentRateResponse> GetCurrentRateAsync(string from = "USD", string to = "KES", CancellationToken ct = default);
    Task<List<ExchangeRateDto>> GetRateHistoryAsync(string from = "USD", string to = "KES", int days = 30, CancellationToken ct = default);
    Task<ExchangeRateDto> SetManualRateAsync(SetManualRateRequest request, Guid userId, CancellationToken ct = default);
    Task<decimal> ConvertAsync(decimal amount, string from, string to, CancellationToken ct = default);
    Task<ExchangeRateApiSettingsDto?> GetApiSettingsAsync(CancellationToken ct = default);
    Task<ExchangeRateApiSettingsDto> UpdateApiSettingsAsync(UpdateApiSettingsRequest request, Guid userId, CancellationToken ct = default);
    Task FetchRatesFromApiAsync(CancellationToken ct = default);
}
