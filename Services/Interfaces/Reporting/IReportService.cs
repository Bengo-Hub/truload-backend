using TruLoad.Backend.DTOs.Reporting;

namespace TruLoad.Backend.Services.Interfaces.Reporting;

public interface IReportService
{
    /// <summary>
    /// Get the full report catalog (all modules and their available reports).
    /// </summary>
    ReportCatalogResponse GetCatalog(string? module = null);

    /// <summary>
    /// Generate a report for the given module, report type, filters, and output format.
    /// </summary>
    Task<ReportResult> GenerateAsync(
        string module,
        string reportType,
        ReportFilterParams filters,
        string format,
        CancellationToken ct = default);
}

public interface IModuleReportGenerator
{
    string Module { get; }
    List<ReportDefinitionDto> GetDefinitions();
    Task<ReportResult> GenerateAsync(string reportType, ReportFilterParams filters, string format, CancellationToken ct = default);
}
