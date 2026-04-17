using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.DTOs.Reporting;
using TruLoad.Backend.Services.Interfaces.Reporting;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.Constants;
using TruLoad.Backend.Data;
using TruLoad.Backend.Middleware;
using System.Text.Json;
using TruLoad.Backend.Services.Interfaces;

namespace TruLoad.Backend.Controllers.Reporting;

/// <summary>
/// Controller for generating and downloading reports across all modules.
/// Supports PDF, CSV, and Excel (xlsx) output formats.
/// Filters available reports by the tenant's enabled modules for commercial tenants.
/// </summary>
[ApiController]
[Route("api/v1/reports")]
[Authorize]
public class ReportController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly ICacheService _cache;
    private readonly ILogger<ReportController> _logger;
    private readonly ITenantContext _tenantContext;
    private readonly TruLoadDbContext _dbContext;

    public ReportController(
        IReportService reportService,
        ICacheService cache,
        ILogger<ReportController> logger,
        ITenantContext tenantContext,
        TruLoadDbContext dbContext)
    {
        _reportService = reportService;
        _cache = cache;
        _logger = logger;
        _tenantContext = tenantContext;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Maps tenant-level enabled modules to the report modules they grant access to.
    /// </summary>
    private static HashSet<string> GetAllowedReportModules(List<string> enabledTenantModules)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tm in enabledTenantModules)
        {
            switch (tm)
            {
                case TenantModules.Weighing:
                    allowed.Add(ReportModules.Weighing);
                    break;
                case TenantModules.Prosecution:
                    allowed.Add(ReportModules.Prosecution);
                    break;
                case TenantModules.Cases:
                case TenantModules.CaseManagement:
                    allowed.Add(ReportModules.Cases);
                    break;
                case TenantModules.FinancialInvoices:
                case TenantModules.FinancialReceipts:
                    allowed.Add(ReportModules.Financial);
                    break;
            }
        }

        return allowed;
    }

    /// <summary>
    /// Resolves enabled tenant modules for the current organization, matching the pattern in AuthController.
    /// </summary>
    private async Task<(List<string> enabledModules, bool isEnforcement)> ResolveOrgModulesAsync()
    {
        var orgId = _tenantContext.OrganizationId;
        if (orgId == Guid.Empty)
            return (TenantModules.AllModules.ToList(), true);

        var org = await _dbContext.Organizations
            .AsNoTracking()
            .Where(o => o.Id == orgId)
            .Select(o => new { o.TenantType, o.EnabledModulesJson })
            .FirstOrDefaultAsync();

        if (org == null)
            return (TenantModules.AllModules.ToList(), true);

        var isEnforcement = !string.Equals(org.TenantType, TenantModules.TenantTypeCommercialWeighing, StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(org.EnabledModulesJson))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(org.EnabledModulesJson);
                if (list != null && list.Count > 0)
                    return (list, isEnforcement);
            }
            catch { /* use defaults */ }
        }

        if (!isEnforcement)
            return (TenantModules.DefaultCommercialWeighingModules.ToList(), false);

        return (TenantModules.AllModules.ToList(), true);
    }

    /// <summary>
    /// Checks if the given report module is allowed for the current tenant.
    /// </summary>
    private async Task<bool> IsReportModuleAllowedAsync(string reportModule)
    {
        var (enabledModules, isEnforcement) = await ResolveOrgModulesAsync();

        // Yard and security are enforcement-only (no specific tenant module mapping)
        if (string.Equals(reportModule, ReportModules.Yard, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(reportModule, ReportModules.Security, StringComparison.OrdinalIgnoreCase))
        {
            return isEnforcement;
        }

        // Commercial reports are only for commercial tenants with weighing enabled
        if (string.Equals(reportModule, ReportModules.Commercial, StringComparison.OrdinalIgnoreCase))
        {
            return !isEnforcement && enabledModules.Contains(TenantModules.Weighing, StringComparer.OrdinalIgnoreCase);
        }

        var allowed = GetAllowedReportModules(enabledModules);
        return allowed.Contains(reportModule);
    }

    /// <summary>
    /// Get the report catalog (available reports per module).
    /// Optionally filter by module name.
    /// </summary>
    [HttpGet("catalog")]
    [HasPermission("analytics.read")]
    [ProducesResponseType(typeof(ReportCatalogResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportCatalogResponse>> GetCatalog([FromQuery] string? module = null)
    {
        var orgId = _tenantContext.OrganizationId;
        var cacheKey = $"report_catalog_{orgId}_{module ?? "all"}";

        try
        {
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                return Ok(JsonSerializer.Deserialize<ReportCatalogResponse>(cached));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get report catalog from cache");
        }

        var catalog = _reportService.GetCatalog(module);

        // Filter catalog modules by the tenant's enabled modules
        var (enabledModules, isEnforcement) = await ResolveOrgModulesAsync();
        var allowedReportModules = GetAllowedReportModules(enabledModules);

        // Enforcement tenants also get yard and security reports
        if (isEnforcement)
        {
            allowedReportModules.Add(ReportModules.Yard);
            allowedReportModules.Add(ReportModules.Security);
        }

        // Commercial tenants with weighing enabled get commercial reports
        if (!isEnforcement && enabledModules.Contains(TenantModules.Weighing, StringComparer.OrdinalIgnoreCase))
        {
            allowedReportModules.Add(ReportModules.Commercial);
        }

        catalog.Modules = catalog.Modules
            .Where(m => allowedReportModules.Contains(m.Module))
            .ToList();

        // For commercial tenants, filter out enforcement-specific weighing reports
        if (!isEnforcement)
        {
            var enforcementOnlyWeighingReports = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "axle-overload", "overloaded-vehicles", "reweigh-statement", "special-release"
            };

            foreach (var moduleCatalog in catalog.Modules)
            {
                if (string.Equals(moduleCatalog.Module, ReportModules.Weighing, StringComparison.OrdinalIgnoreCase))
                {
                    moduleCatalog.Reports = moduleCatalog.Reports
                        .Where(r => !enforcementOnlyWeighingReports.Contains(r.Id))
                        .ToList();
                }
            }
        }

        try
        {
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(catalog), TimeSpan.FromHours(4));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache report catalog");
        }

        return Ok(catalog);
    }

    /// <summary>
    /// Generate and download a report.
    /// </summary>
    /// <param name="module">Report module (weighing, prosecution, cases, financial, yard, security)</param>
    /// <param name="reportType">Report type ID (e.g. daily-summary, weighbridge-register)</param>
    /// <param name="dateFrom">Optional start date filter</param>
    /// <param name="dateTo">Optional end date filter</param>
    /// <param name="format">Output format: pdf or csv (default: pdf)</param>
    /// <param name="stationId">Optional station filter (GUID)</param>
    /// <param name="status">Optional status filter</param>
    /// <param name="weighingType">Optional weighing type (e.g. multideck, mobile) for weighing reports</param>
    /// <param name="controlStatus">Optional control status (e.g. LEGAL, OVERLOAD) for weighing reports</param>
    /// <param name="ct">Cancellation token</param>
    [HttpGet("{module}/{reportType}")]
    [HasPermission("analytics.read")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateReport(
        [FromRoute] string module,
        [FromRoute] string reportType,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string format = "pdf",
        [FromQuery] string? stationId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? weighingType = null,
        [FromQuery] string? controlStatus = null,
        CancellationToken ct = default)
    {
        // Verify the tenant has access to this report module
        if (!await IsReportModuleAllowedAsync(module))
        {
            _logger.LogWarning("Report module access denied for tenant {OrgId}: {Module}", _tenantContext.OrganizationId, module);
            return Forbid();
        }

        try
        {
            // Resolve org context for report branding
            var orgId = _tenantContext.OrganizationId;
            string? orgName = null;
            string? orgLogoFile = null;
            var isEnforcement = true;

            if (orgId != Guid.Empty)
            {
                var org = await _dbContext.Organizations
                    .AsNoTracking()
                    .Where(o => o.Id == orgId)
                    .Select(o => new { o.Name, o.TenantType, o.LogoUrl })
                    .FirstOrDefaultAsync(ct);

                if (org != null)
                {
                    orgName = org.Name;
                    orgLogoFile = org.LogoUrl;
                    isEnforcement = !string.Equals(org.TenantType,
                        TenantModules.TenantTypeCommercialWeighing, StringComparison.OrdinalIgnoreCase);
                }
            }

            var filters = new ReportFilterParams
            {
                DateFrom = dateFrom.HasValue ? DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : null,
                DateTo = dateTo.HasValue ? DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc) : null,
                StationId = stationId,
                Status = status,
                WeighingType = weighingType,
                ControlStatus = controlStatus,
                OrganizationName = orgName,
                OrgLogoFile = orgLogoFile,
                IsEnforcement = isEnforcement
            };

            var result = await _reportService.GenerateAsync(module, reportType, filters, format, ct);

            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid report request: {Module}/{ReportType}", module, reportType);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report: {Module}/{ReportType}", module, reportType);
            return StatusCode(500, new { message = "Failed to generate report" });
        }
    }
}
