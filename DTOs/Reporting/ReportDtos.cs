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

    public static readonly string[] All = [Weighing, Prosecution, Cases, Financial, Yard, Security];
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
