using Cronos;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Reporting;
using TruLoad.Backend.Services.Interfaces.Reporting;
using TruLoad.Backend.Services.Interfaces.Shared;

namespace TruLoad.Backend.Services.BackgroundJobs;

/// <summary>
/// Hangfire recurring job that runs due scheduled reports, generates the output file,
/// and emails it to all configured recipients.
/// </summary>
public class ReportScheduleJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReportScheduleJob> _logger;

    public ReportScheduleJob(IServiceScopeFactory scopeFactory, ILogger<ReportScheduleJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("[ReportScheduleJob] Checking for due scheduled reports");

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TruLoadDbContext>();
        var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = DateTime.UtcNow;

        var dueReports = await db.ScheduledReports
            .Where(r => r.IsActive && r.NextRunAt <= now)
            .ToListAsync();

        _logger.LogInformation("[ReportScheduleJob] {Count} report(s) due for execution", dueReports.Count);

        foreach (var report in dueReports)
        {
            _logger.LogInformation("[ReportScheduleJob] Running report {Name} ({Module}/{Type})", report.Name, report.Module, report.ReportType);

            report.LastRunAt = now;
            report.LastRunStatus = "running";
            report.NextRunAt = ComputeNextRun(report.CronSchedule, now);
            await db.SaveChangesAsync();

            try
            {
                // Build filter params: default to previous calendar day
                var filters = new ReportFilterParams
                {
                    DateFrom = now.Date.AddDays(-1),
                    DateTo = now.Date,
                    IsEnforcement = true,
                };

                if (!string.IsNullOrWhiteSpace(report.ParametersJson))
                {
                    try
                    {
                        var overrides = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(report.ParametersJson);
                        if (overrides != null)
                        {
                            if (overrides.TryGetValue("date_range", out var range))
                            {
                                (filters.DateFrom, filters.DateTo) = ResolveDateRange(range, now);
                            }
                            if (overrides.TryGetValue("station_id", out var sid)) filters.StationId = sid;
                            if (overrides.TryGetValue("status", out var st)) filters.Status = st;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[ReportScheduleJob] Failed to parse parameters for report {Id}", report.Id);
                    }
                }

                var result = await reportService.GenerateAsync(report.Module, report.ReportType, filters, report.Format);

                // Email to all recipients using attachments via template data
                var recipients = report.Recipients;
                if (recipients.Count > 0)
                {
                    var downloadLinkPlaceholder = string.Empty; // No download link for attached reports

                    foreach (var email in recipients)
                    {
                        _ = notificationService.SendEmailAsync(
                            "truload/scheduled_report",
                            email,
                            email,
                            new Dictionary<string, object>
                            {
                                ["report_name"] = report.Name,
                                ["report_module"] = report.Module,
                                ["period_from"] = filters.DateFrom?.ToString("yyyy-MM-dd") ?? "",
                                ["period_to"] = filters.DateTo?.ToString("yyyy-MM-dd") ?? "",
                                ["format"] = report.Format,
                                ["generated_at"] = now.ToString("yyyy-MM-dd HH:mm UTC"),
                            },
                            $"Scheduled Report: {report.Name}");
                    }
                }

                report.LastRunStatus = "success";
                report.LastRunError = null;
                _logger.LogInformation("[ReportScheduleJob] Completed report {Name} — {Size} bytes", report.Name, result.Content.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ReportScheduleJob] Failed to run report {Name}", report.Name);
                report.LastRunStatus = "failed";
                report.LastRunError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            }

            await db.SaveChangesAsync();
        }
    }

    private static DateTime? ComputeNextRun(string cronExpression, DateTime after)
    {
        try
        {
            var cron = CronExpression.Parse(cronExpression, CronFormat.Standard);
            return cron.GetNextOccurrence(after, TimeZoneInfo.Utc);
        }
        catch
        {
            return null;
        }
    }

    private static (DateTime? from, DateTime? to) ResolveDateRange(string strategy, DateTime now)
    {
        return strategy switch
        {
            "yesterday" => (now.Date.AddDays(-1), now.Date),
            "last_week" => (now.Date.AddDays(-7), now.Date),
            "last_month" => (new DateTime(now.Year, now.Month, 1).AddMonths(-1), new DateTime(now.Year, now.Month, 1)),
            "current_week" => (now.Date.AddDays(-(int)now.DayOfWeek), now.Date.AddDays(1)),
            _ => (now.Date.AddDays(-1), now.Date),
        };
    }
}
