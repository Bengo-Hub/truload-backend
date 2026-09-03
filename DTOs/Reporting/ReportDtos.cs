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

    /// <summary>
    /// Optional curated filter catalog for the structured custom-report builder - same
    /// present-only-if-opted-in convention as <see cref="Columns"/>. Lets the builder UI show only
    /// the filters a given report type actually supports (e.g. a pre-aggregated report with no
    /// per-vehicle rows has no "vehicle registration" filter) instead of one fixed generic set.
    /// </summary>
    public List<ReportFilterDefinition>? Filters { get; set; }

    /// <summary>
    /// Optional role-name allowlist restricting which roles may see/generate this specific report
    /// type, on top of the flat "analytics.read" permission both GetCatalog and GenerateReport
    /// already require (see reports.md's "Access by Role" table for Commercial reports). Null/empty
    /// means no additional restriction - visible to anyone with analytics.read, same as before this
    /// field existed. Checked via ClaimsPrincipal.IsInRole against the caller's role claims; a
    /// Superuser/System Admin caller always bypasses this, same convention as the rest of the app.
    /// </summary>
    public string[]? AllowedRoles { get; set; }

    /// <summary>
    /// Optional vertical allowlist restricting which classified commercial tenants may see/generate
    /// this specific report type, on top of <see cref="AllowedRoles"/> and the flat "analytics.read"
    /// permission - same null/empty-means-unrestricted convention as AllowedRoles. Checked against
    /// the org's Organization.MetadataJson "vertical" value (see
    /// TruLoad.Backend.Constants.CommercialVerticals); an org with no vertical classification set
    /// (every org before this field existed, and any org that hasn't picked one since) is never
    /// restricted by this field. No report currently sets this - the mechanism exists for a future
    /// vertical-specific report (e.g. a quarry-only blast-face tonnage report) to opt into, the same
    /// way individual reports already opt into AllowedRoles. A Superuser/System Admin caller always
    /// bypasses this, same convention as AllowedRoles.
    /// </summary>
    public string[]? AllowedVerticals { get; set; }
}

/// <summary>
/// One selectable filter in a report's structured custom-report-builder catalog. <see cref="Key"/>
/// matches a property on <see cref="ReportFilterParams"/> (e.g. "stationId", "countyId") - the
/// frontend uses <see cref="Kind"/> to pick which control to render (a station/county/subcounty
/// picker, a small fixed dropdown from <see cref="Options"/>, free text, or a date).
/// </summary>
public class ReportFilterDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    /// <summary>"station" | "county" | "subcounty" | "select" | "text" | "date".</summary>
    public string Kind { get; set; } = "text";

    /// <summary>Fixed option list for Kind == "select" (e.g. weighing type, control status).</summary>
    public List<ReportFilterOption>? Options { get; set; }
}

public class ReportFilterOption
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
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
    /// <summary>County filter - resolved via the station's CountyId (Weighing module) or a report's own CountyId FK where present.</summary>
    public string? CountyId { get; set; }
    /// <summary>Sub-county filter - resolved via the station's SubcountyId (Weighing module) or a report's own SubcountyId FK where present.</summary>
    public string? SubcountyId { get; set; }
    /// <summary>Road filter - resolved via the transaction's own RoadId (falling back to the station's RoadId) or a report's own RoadId FK where present.</summary>
    public string? RoadId { get; set; }
    public string? Status { get; set; }
    /// <summary>Weighing type filter (e.g. static, multideck, mobile) for weighing reports.</summary>
    public string? WeighingType { get; set; }
    /// <summary>Control status filter (e.g. LEGAL, OVERLOAD, WARNING) for weighing reports.</summary>
    public string? ControlStatus { get; set; }
    /// <summary>User ID filter (Guid string) - e.g. the Transaction Event Log report's "filtered by user" requirement.</summary>
    public string? UserId { get; set; }
    /// <summary>Ticket/transaction-reference substring filter - e.g. the Transaction Event Log report's "filtered by ... transaction reference" requirement.</summary>
    public string? TicketNumber { get; set; }
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
