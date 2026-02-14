using TruLoad.Backend.DTOs.Reporting;
using TruLoad.Backend.Services.Interfaces.Reporting;

namespace TruLoad.Backend.Services.Implementations.Reporting;

/// <summary>
/// Orchestrates report generation by delegating to per-module report generators.
/// </summary>
public class ReportService : IReportService
{
    private readonly Dictionary<string, IModuleReportGenerator> _generators;
    private readonly ILogger<ReportService> _logger;

    private static readonly Dictionary<string, string> ModuleDisplayNames = new()
    {
        [ReportModules.Weighing] = "Weighing Operations",
        [ReportModules.Prosecution] = "Prosecution",
        [ReportModules.Cases] = "Case Management",
        [ReportModules.Financial] = "Financial",
        [ReportModules.Yard] = "Yard Management",
        [ReportModules.Security] = "Security & Audit",
    };

    public ReportService(
        IEnumerable<IModuleReportGenerator> generators,
        ILogger<ReportService> logger)
    {
        _generators = generators.ToDictionary(g => g.Module, g => g);
        _logger = logger;
    }

    public ReportCatalogResponse GetCatalog(string? module = null)
    {
        var moduleCatalogs = new List<ReportModuleCatalog>();

        var targetModules = module != null
            ? [module]
            : ReportModules.All;

        foreach (var mod in targetModules)
        {
            if (_generators.TryGetValue(mod, out var generator))
            {
                moduleCatalogs.Add(new ReportModuleCatalog
                {
                    Module = mod,
                    DisplayName = ModuleDisplayNames.GetValueOrDefault(mod, mod),
                    Reports = generator.GetDefinitions()
                });
            }
        }

        return new ReportCatalogResponse { Modules = moduleCatalogs };
    }

    public async Task<ReportResult> GenerateAsync(
        string module,
        string reportType,
        ReportFilterParams filters,
        string format,
        CancellationToken ct = default)
    {
        if (!_generators.TryGetValue(module.ToLowerInvariant(), out var generator))
        {
            throw new ArgumentException($"Unknown report module: {module}");
        }

        var validFormats = new[] { "pdf", "csv" };
        if (!validFormats.Contains(format.ToLowerInvariant()))
        {
            throw new ArgumentException($"Unsupported format: {format}. Supported formats: pdf, csv");
        }

        _logger.LogInformation(
            "Generating report: module={Module}, type={ReportType}, format={Format}, dateFrom={DateFrom}, dateTo={DateTo}",
            module, reportType, format, filters.DateFrom, filters.DateTo);

        return await generator.GenerateAsync(reportType.ToLowerInvariant(), filters, format.ToLowerInvariant(), ct);
    }
}
