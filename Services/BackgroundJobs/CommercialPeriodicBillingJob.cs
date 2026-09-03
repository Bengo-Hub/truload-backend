using Hangfire;
using TruLoad.Backend.Services.Interfaces.Weighing;

namespace TruLoad.Backend.Services.BackgroundJobs;

/// <summary>
/// Hangfire recurring job that rolls up every commercial tariff accrual whose billing period
/// (Daily/Weekly/Monthly) has fully elapsed into one invoice per org+transporter+period — the
/// deferred-invoicing half of CommercialTariffRule.BillingPeriod (the other half, accruing at
/// capture time, happens synchronously in CommercialWeighingService). Runs daily, which is
/// sufficient for all three period grains (a closed Weekly/Monthly period just waits at most one
/// extra day to be picked up — no tenant needs same-day invoicing for a period they've already
/// agreed is weekly or monthly).
/// </summary>
public class CommercialPeriodicBillingJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CommercialPeriodicBillingJob> _logger;

    public CommercialPeriodicBillingJob(IServiceScopeFactory scopeFactory, ILogger<CommercialPeriodicBillingJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 2)]
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("[CommercialPeriodicBillingJob] Starting periodic tariff billing rollup");

        using var scope = _scopeFactory.CreateScope();
        var commercialWeighingService = scope.ServiceProvider.GetRequiredService<ICommercialWeighingService>();

        var invoicesCreated = await commercialWeighingService.ProcessPendingPeriodicBillingAsync();

        _logger.LogInformation(
            "[CommercialPeriodicBillingJob] Completed — created {Count} periodic invoice(s)",
            invoicesCreated);
    }
}
