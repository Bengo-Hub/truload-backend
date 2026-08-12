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

    /// <summary>"Key:" legend entries as (fill colour hex, label) pairs.</summary>
    public (string colorHex, string label)[]? Legend { get; init; }

    /// <summary>Titled breakdown tables written beneath the legend, in order.</summary>
    public ExcelSummaryTable[]? SummaryTables { get; init; }

    public string? OrgName { get; init; }
    public string? OrgLogoFile { get; init; }
}
