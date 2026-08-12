namespace TruLoad.Backend.DTOs.Reporting;

/// <summary>
/// Available report modules.
/// </summary>
public static class ReportModules
{
    public const string Weighing = "weighing";
    public const string Prosecution = "prosecution";
    public const string Cases = "cases";
    public const string Financial = "financial";
    public const string Yard = "yard";
    public const string Security = "security";
    public const string Commercial = "commercial";

    public static readonly string[] All = [Weighing, Prosecution, Cases, Financial, Yard, Security, Commercial];
}

/// <summary>
/// Describes a single report definition for the catalog.
/// </summary>
public class ReportDefinitionDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string[] SupportedFormats { get; set; } = ["pdf", "csv"];

    /// <summary>
    /// Optional curated column catalog for the structured custom-report builder - present only on
    /// report types that opted in (a report with no catalog can only be generated with its
    /// existing fixed/default column set). Absent (not an empty list) when a report hasn't been
    /// migrated onto the builder yet, so the frontend can tell "no picker available" apart from
    /// "picker available, nothing optional".
    /// </summary>
    public List<ReportColumnDefinition>? Columns { get; set; }

    /// <summary>Optional curated chart-option catalog for this report type.</summary>
    public List<ReportChartOption>? ChartOptions { get; set; }
}

/// <summary>
/// One selectable column in a report's structured custom-report-builder catalog. <see cref="Key"/>
/// must equal the report's actual header text for that column (the shared column-selection
/// mechanism matches by header string - see <c>BaseReportGenerator.ApplyColumnSelection</c>) so it
/// stays valid even if the report's query/projection changes shape later, as long as the header
/// text itself is kept stable.
/// </summary>
public class ReportColumnDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool DefaultSelected { get; set; } = true;
}

/// <summary>One selectable chart/visual-emphasis option in a report's builder catalog.</summary>
public class ReportChartOption
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// Report catalog grouped by module.
/// </summary>
public class ReportCatalogResponse
{
    public List<ReportModuleCatalog> Modules { get; set; } = [];
}

public class ReportModuleCatalog
{
    public string Module { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<ReportDefinitionDto> Reports { get; set; } = [];
}

/// <summary>
/// Query parameters for report generation.
/// </summary>
public class ReportFilterParams
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? StationId { get; set; }
    public string? Status { get; set; }
    /// <summary>Weighing type filter (e.g. static, multideck, mobile) for weighing reports.</summary>
    public string? WeighingType { get; set; }
    /// <summary>Control status filter (e.g. LEGAL, OVERLOAD, WARNING) for weighing reports.</summary>
    public string? ControlStatus { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 1000;

    /// <summary>
    /// Structured custom-report-builder column selection - header text values matching a subset of
    /// the report's <see cref="ReportColumnDefinition.Key"/> catalog entries. Ignored (full default
    /// column set used) whenever <see cref="UseDefaults"/> is true, which is the default.
    /// </summary>
    public string[]? Columns { get; set; }

    /// <summary>Structured custom-report-builder chart-option selection (see <see cref="ReportChartOption"/>).</summary>
    public string? ChartType { get; set; }

    /// <summary>
    /// When true (the default), <see cref="Columns"/>/<see cref="ChartType"/> are ignored entirely
    /// and a report generates its normal fixed/default output - guarantees the builder's
    /// "Use Recommended Defaults" toggle produces byte-identical output to the plain catalog path.
    /// </summary>
    public bool UseDefaults { get; set; } = true;

    /// <summary>Organization name for report headers.</summary>
    public string? OrganizationName { get; set; }
    /// <summary>Organization logo filename for report branding.</summary>
    public string? OrgLogoFile { get; set; }
    /// <summary>Whether the tenant is enforcement (shows "REPUBLIC OF KENYA") or commercial.</summary>
    public bool IsEnforcement { get; set; } = true;
}

/// <summary>
/// Result of report generation.
/// </summary>
public class ReportResult
{
    public byte[] Content { get; set; } = [];
    public string ContentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}
