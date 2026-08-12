using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.Reporting;

/// <summary>
/// A user-saved structured custom-report-builder selection (column subset, chart option, filter
/// overrides) for a given module/report type. Deliberately its own entity rather than an overload
/// of <see cref="System.ScheduledReport"/> - that entity requires a cron schedule and recipient list
/// and carries Hangfire job-run state that doesn't apply to "just remember my column selection".
/// Tenant-aware (unlike <c>ScheduledReport</c>) since this is genuinely a per-org, per-user
/// artifact that must not leak across tenants sharing the test DB.
/// </summary>
public class SavedReportConfig : TenantAwareEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Report module (weighing, prosecution, cases, financial, yard, security, commercial).</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>Report type id within the module (e.g. "axle-load-analysis").</summary>
    public string ReportType { get; set; } = string.Empty;

    /// <summary>JSON string[] of selected column keys (see ReportColumnDefinition.Key).</summary>
    public string ColumnsJson { get; set; } = "[]";

    /// <summary>Selected chart option key (see ReportChartOption.Key), or null for the report's default.</summary>
    public string? ChartType { get; set; }

    /// <summary>Optional JSON object of filter overrides (stationId, weighingType, controlStatus, etc.).</summary>
    public string? FiltersJson { get; set; }

    /// <summary>Whether this is the user's default config for this module/report type combination.</summary>
    public bool IsDefault { get; set; }

    public Guid CreatedByUserId { get; set; }
}
