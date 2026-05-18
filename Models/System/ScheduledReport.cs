using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.System;

/// <summary>
/// Defines an automated report that runs on a cron schedule and emails results to recipients.
/// </summary>
[Table("scheduled_reports")]
public class ScheduledReport : BaseEntity
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Report module: weighing, cases, financial, compliance, commercial</summary>
    [Required, MaxLength(50)]
    public string Module { get; set; } = string.Empty;

    /// <summary>Report type identifier matching the reporting service (e.g., "WeighingTransactions")</summary>
    [Required, MaxLength(100)]
    public string ReportType { get; set; } = string.Empty;

    /// <summary>Output format: PDF, CSV, XLSX</summary>
    [Required, MaxLength(10)]
    public string Format { get; set; } = "PDF";

    /// <summary>Cron expression (e.g., "0 6 * * 1" = every Monday at 6 AM)</summary>
    [Required, MaxLength(50)]
    public string CronSchedule { get; set; } = string.Empty;

    /// <summary>Human-readable schedule description (e.g., "Every Monday at 06:00")</summary>
    [MaxLength(100)]
    public string? ScheduleDescription { get; set; }

    /// <summary>JSON array of recipient email addresses</summary>
    [Required]
    public string RecipientsJson { get; set; } = "[]";

    /// <summary>Optional report parameters as JSON (date range strategy, filters, etc.)</summary>
    public string? ParametersJson { get; set; }

    /// <summary>UTC timestamp of the next scheduled run</summary>
    public DateTime? NextRunAt { get; set; }

    /// <summary>UTC timestamp of the last completed run</summary>
    public DateTime? LastRunAt { get; set; }

    /// <summary>Status of the last run: success, failed, running</summary>
    [MaxLength(20)]
    public string? LastRunStatus { get; set; }

    /// <summary>Error message from the last failed run</summary>
    [MaxLength(500)]
    public string? LastRunError { get; set; }

    public bool IsActive { get; set; } = true;

    [NotMapped]
    public List<string> Recipients
    {
        get => string.IsNullOrEmpty(RecipientsJson)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(RecipientsJson) ?? new List<string>();
        set => RecipientsJson = JsonSerializer.Serialize(value);
    }
}
