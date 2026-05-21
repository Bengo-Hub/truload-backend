using Hangfire;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Services.Interfaces.Shared;

namespace TruLoad.Backend.Services.BackgroundJobs;

/// <summary>
/// Hangfire recurring job that sends a daily weighing summary email to each transporter
/// with an active portal account. Runs daily at 04:00 UTC (07:00 EAT).
/// </summary>
public class PortalDailySummaryJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PortalDailySummaryJob> _logger;

    public PortalDailySummaryJob(IServiceScopeFactory scopeFactory, ILogger<PortalDailySummaryJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 2)]
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("[PortalDailySummaryJob] Starting daily portal summary job");

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TruLoadDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var yesterdayEnd = yesterday.AddDays(1);

        // Get all transporters with active portal accounts and email addresses
        var transporters = await db.Transporters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.PortalAccountId.HasValue && t.IsActive && t.Email != null)
            .ToListAsync();

        _logger.LogInformation("[PortalDailySummaryJob] Found {Count} transporters with portal accounts", transporters.Count);

        foreach (var transporter in transporters)
        {
            try
            {
                var transactions = await db.WeighingTransactions
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(w => w.TransporterId == transporter.Id &&
                                w.WeighingMode == "commercial" &&
                                w.ControlStatus == "Complete" &&
                                w.WeighedAt >= yesterday &&
                                w.WeighedAt < yesterdayEnd)
                    .ToListAsync();

                if (!transactions.Any()) continue;

                var totalNetKg = transactions.Sum(t => t.NetWeightKg ?? 0);
                var templateData = new Dictionary<string, object>
                {
                    ["transporter_name"] = transporter.Name,
                    ["date"] = yesterday.ToString("MMMM d, yyyy"),
                    ["transaction_count"] = transactions.Count,
                    ["total_net_tonnes"] = Math.Round(totalNetKg / 1000.0, 2),
                    ["vehicles_count"] = transactions.Select(t => t.VehicleRegNumber).Distinct().Count()
                };

                var sent = await notificationService.SendEmailAsync(
                    "truload/portal_daily_summary",
                    transporter.Email!,
                    transporter.Name,
                    templateData,
                    subject: $"[TruLoad Portal] Daily Summary — {yesterday:MMMM d, yyyy}",
                    cancellationToken: CancellationToken.None,
                    tenantSlug: null);

                if (!sent)
                    _logger.LogWarning("[PortalDailySummaryJob] Email send returned false for transporter {Id}", transporter.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PortalDailySummaryJob] Failed to send daily summary for transporter {Id}", transporter.Id);
            }
        }

        _logger.LogInformation("[PortalDailySummaryJob] Daily portal summary job complete");
    }
}
