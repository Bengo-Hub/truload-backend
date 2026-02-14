using TruLoad.Backend.Services.Interfaces.Financial;

namespace TruLoad.Backend.Services.BackgroundJobs;

/// <summary>
/// Hangfire recurring job for daily exchange rate sync.
/// Follows the PesaflowInvoiceSyncJob pattern using IServiceScopeFactory.
/// </summary>
public class ExchangeRateSyncJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExchangeRateSyncJob> _logger;

    public ExchangeRateSyncJob(
        IServiceScopeFactory scopeFactory,
        ILogger<ExchangeRateSyncJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Exchange rate sync job started");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var currencyService = scope.ServiceProvider.GetRequiredService<ICurrencyService>();
            await currencyService.FetchRatesFromApiAsync();

            _logger.LogInformation("Exchange rate sync job completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exchange rate sync job failed");
            throw;
        }
    }
}
