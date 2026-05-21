using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Services.Interfaces.Shared;

namespace TruLoad.Backend.Services.BackgroundJobs;

/// <summary>
/// Hangfire recurring job that detects commercial weighing transactions stuck at first-weight-only
/// for longer than the configured threshold and emails the station manager(s).
/// Runs every 30 minutes. Prevents duplicate alerts via StaleAlertSentAt flag.
/// </summary>
public class StaleWeighingNotificationJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StaleWeighingNotificationJob> _logger;

    private const int DefaultThresholdHours = 8;

    public StaleWeighingNotificationJob(
        IServiceScopeFactory scopeFactory,
        ILogger<StaleWeighingNotificationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[StaleWeighingNotificationJob] Starting stale weighing check");

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TruLoadDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var thresholdHours = DefaultThresholdHours;
        var cutoff = DateTime.UtcNow.AddHours(-thresholdHours);

        // Find stale transactions: first weight captured, still open, within double threshold (don't notify forever)
        var doubleCutoff = DateTime.UtcNow.AddHours(-thresholdHours * 4);

        var staleTransactions = await db.WeighingTransactions
            .AsNoTracking()
            .Include(t => t.Vehicle)
            .Include(t => t.Station)
            .Where(t =>
                t.WeighingMode == "commercial" &&
                t.CaptureStatus == "first_weight_captured" &&
                t.VoidedAt == null &&
                t.FirstWeightAt.HasValue &&
                t.FirstWeightAt.Value <= cutoff &&
                t.FirstWeightAt.Value >= doubleCutoff &&
                t.StaleAlertSentAt == null)
            .ToListAsync(ct);

        if (staleTransactions.Count == 0)
        {
            _logger.LogInformation("[StaleWeighingNotificationJob] No stale transactions found");
            return;
        }

        _logger.LogInformation("[StaleWeighingNotificationJob] Found {Count} stale transaction(s) needing alerts", staleTransactions.Count);

        // Group by org so we can look up managers per org
        var byOrg = staleTransactions.GroupBy(t => t.OrganizationId);

        foreach (var orgGroup in byOrg)
        {
            var orgId = orgGroup.Key;

            var org = await db.Organizations
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == orgId, ct);

            if (org == null) continue;

            // Find users with COMMERCIAL_MANAGER or Station_Manager roles in this org
            var managerRoleNames = new[] { "Commercial Weighing Manager", "Station Manager" };

            var managers = await db.Users
                .AsNoTracking()
                .Where(u =>
                    u.OrganizationId == orgId &&
                    u.DeletedAt == null &&
                    !string.IsNullOrEmpty(u.Email) &&
                    db.UserRoles
                        .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
                        .Where(x => x.UserId == u.Id && managerRoleNames.Contains(x.Name))
                        .Any())
                .Select(u => new { u.Email, u.FullName })
                .ToListAsync(ct);

            if (managers.Count == 0)
            {
                _logger.LogWarning("[StaleWeighingNotificationJob] No managers found for org {OrgId} to notify about stale transactions", orgId);
                continue;
            }

            foreach (var transaction in orgGroup)
            {
                var elapsedHours = (DateTime.UtcNow - transaction.FirstWeightAt!.Value).TotalHours;
                var plateNo = transaction.Vehicle?.RegNo ?? transaction.VehicleRegNumber ?? "UNKNOWN";
                var stationName = transaction.Station?.Name ?? "Unknown Station";
                var weightKg = transaction.FirstWeightKg ?? 0;
                var weightType = transaction.FirstWeightType ?? "unknown";

                var templateData = new Dictionary<string, object>
                {
                    ["ticket_number"] = transaction.TicketNumber ?? transaction.Id.ToString(),
                    ["vehicle_plate"] = plateNo,
                    ["first_weight_kg"] = weightKg,
                    ["first_weight_type"] = weightType,
                    ["elapsed_hours"] = Math.Round(elapsedHours, 1),
                    ["station_name"] = stationName,
                    ["org_name"] = org.Name,
                    ["threshold_hours"] = thresholdHours,
                };

                // Derive the tenant slug from the org code so the notifications-api
                // resolves the correct SMTP settings and branding (not the platform default).
                var tenantSlug = org.Code.ToLowerInvariant();

                foreach (var manager in managers)
                {
                    var sent = await notificationService.SendEmailAsync(
                        templateName: "truload/stale_weighing_alert",
                        recipientEmail: manager.Email!,
                        recipientName: manager.FullName ?? "Manager",
                        templateData: templateData,
                        subject: $"[TruLoad] Stale Weighing Transaction — {plateNo}",
                        cancellationToken: ct,
                        tenantSlug: tenantSlug);

                    if (!sent)
                        _logger.LogWarning("[StaleWeighingNotificationJob] Failed to send stale alert to {Email} for transaction {Id}", manager.Email, transaction.Id);
                }

                // Mark as alerted so we don't spam
                await db.WeighingTransactions
                    .Where(t => t.Id == transaction.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.StaleAlertSentAt, DateTime.UtcNow), ct);
            }
        }

        _logger.LogInformation("[StaleWeighingNotificationJob] Stale weighing check complete");
    }
}
