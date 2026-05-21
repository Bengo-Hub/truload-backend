using Hangfire;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Services.Interfaces.Shared;

namespace TruLoad.Backend.Services.BackgroundJobs;

/// <summary>
/// Hangfire recurring job that detects weight discrepancy anomalies (>5% from expected)
/// and sends alert emails to transporter portal accounts. Runs hourly.
/// </summary>
public class PortalAnomalyAlertJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PortalAnomalyAlertJob> _logger;

    private const double DiscrepancyThreshold = 0.05; // 5%

    public PortalAnomalyAlertJob(IServiceScopeFactory scopeFactory, ILogger<PortalAnomalyAlertJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 1)]
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("[PortalAnomalyAlertJob] Starting portal anomaly alert check");

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TruLoadDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var fromTime = DateTime.UtcNow.AddHours(-1);

        // Get all transporters with active portal accounts and email addresses
        var transporters = await db.Transporters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.PortalAccountId.HasValue && t.IsActive && t.Email != null)
            .ToListAsync();

        int alertsSent = 0;

        foreach (var transporter in transporters)
        {
            try
            {
                // Fetch completed commercial transactions from last hour that have expected weights
                var candidates = await db.WeighingTransactions
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(w => w.TransporterId == transporter.Id &&
                                w.WeighingMode == "commercial" &&
                                w.ControlStatus == "Complete" &&
                                w.WeighedAt >= fromTime &&
                                w.ExpectedNetWeightKg.HasValue &&
                                w.ExpectedNetWeightKg > 0 &&
                                w.NetWeightKg.HasValue)
                    .ToListAsync();

                // Filter for anomalies: |net - expected| / expected > 5%
                var anomalies = candidates
                    .Where(w =>
                    {
                        var net = (double)w.NetWeightKg!.Value;
                        var expected = (double)w.ExpectedNetWeightKg!.Value;
                        return Math.Abs(net - expected) / expected > DiscrepancyThreshold;
                    })
                    .ToList();

                if (!anomalies.Any()) continue;

                foreach (var tx in anomalies)
                {
                    try
                    {
                        var net = (double)tx.NetWeightKg!.Value;
                        var expected = (double)tx.ExpectedNetWeightKg!.Value;
                        var discrepancyPct = Math.Round(Math.Abs(net - expected) / expected * 100.0, 1);

                        var templateData = new Dictionary<string, object>
                        {
                            ["transporter_name"] = transporter.Name,
                            ["ticket_number"] = tx.TicketNumber,
                            ["vehicle_reg"] = tx.VehicleRegNumber,
                            ["net_weight_tonnes"] = Math.Round(net / 1000.0, 3),
                            ["expected_weight_tonnes"] = Math.Round(expected / 1000.0, 3),
                            ["discrepancy_pct"] = discrepancyPct
                        };

                        var sent = await notificationService.SendEmailAsync(
                            "truload/portal_anomaly_alert",
                            transporter.Email!,
                            transporter.Name,
                            templateData,
                            subject: $"[TruLoad Portal] Weight Anomaly Detected — Ticket {tx.TicketNumber}",
                            cancellationToken: CancellationToken.None,
                            tenantSlug: null);

                        if (sent)
                            alertsSent++;
                        else
                            _logger.LogWarning("[PortalAnomalyAlertJob] Email send returned false for ticket {TicketNumber}", tx.TicketNumber);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[PortalAnomalyAlertJob] Failed to send anomaly alert for ticket {TicketNumber}", tx.TicketNumber);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PortalAnomalyAlertJob] Failed to process transporter {Id}", transporter.Id);
            }
        }

        _logger.LogInformation("[PortalAnomalyAlertJob] Anomaly alert check complete. Sent {Count} alert(s)", alertsSent);
    }
}
