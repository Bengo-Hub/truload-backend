namespace TruLoad.Backend.DTOs.Reporting;

public class SavedReportConfigDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public string[] Columns { get; set; } = [];
    public string? ChartType { get; set; }
    public string? FiltersJson { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SaveReportConfigRequest
{
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public string[] Columns { get; set; } = [];
    public string? ChartType { get; set; }
    public string? FiltersJson { get; set; }
    public bool IsDefault { get; set; }
}
