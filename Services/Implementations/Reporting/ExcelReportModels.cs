namespace TruLoad.Backend.Services.Implementations.Reporting;

/// <summary>
/// A titled summary/breakdown table rendered beneath a report's main data table
/// (e.g. the Axle Load Data Analysis template's vehicle-type-by-axle-count breakdown).
/// </summary>
public sealed class ExcelSummaryTable
{
    public required string Title { get; init; }
    public required string[] Headers { get; init; }
    public required IEnumerable<string[]> Rows { get; init; }
}

/// <summary>
/// Full request shape for <c>BaseReportGenerator.GenerateExcel</c>'s richer overload - adds
/// status-driven row colouring, a legend/key block, one or more summary tables, and tenant
/// branding (org name + logo) on top of the original flat title+headers+rows sheet.
/// </summary>
public sealed class ExcelReportRequest
{
    public required string ReportTitle { get; init; }
    public required string[] Headers { get; init; }
    public required IEnumerable<string[]> Rows { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }

    /// <summary>Index into each row identifying the status column that drives row-level colouring.</summary>
    public int? ConditionalStatusColumnIndex { get; init; }

    /// <summary>
    /// Column indexes (into the MAIN data table, not a summary table) that read as a percentage
    /// share - each gets ClosedXML's native DataBar conditional format, e.g. a "Compliance %"
    /// column on an aggregate report. Distinct from the per-summary-table data-bar behaviour
    /// already applied automatically to any summary table whose last header contains '%'.
    /// </summary>
    public int[]? PercentageDataBarColumnIndexes { get; init; }

    /// <summary>"Key:" legend entries as (fill colour hex, label) pairs.</summary>
    public (string colorHex, string label)[]? Legend { get; init; }

    /// <summary>Titled breakdown tables written beneath the legend, in order.</summary>
    public ExcelSummaryTable[]? SummaryTables { get; init; }

    public string? OrgName { get; init; }
    public string? OrgLogoFile { get; init; }

    /// <summary>
    /// Whether to apply the native ClosedXML DataBar visual to percentage summary-table columns.
    /// Wired to the structured custom-report builder's chart-option toggle (e.g. "Tables only, no
    /// visual emphasis") - true (the default) preserves today's behaviour unchanged.
    /// </summary>
    public bool IncludeChartVisuals { get; init; } = true;
}
