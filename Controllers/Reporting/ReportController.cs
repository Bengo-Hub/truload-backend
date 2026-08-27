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
    /// Per-report-type role gating (5a) - on top of the flat "analytics.read" permission both
    /// GetCatalog and GenerateReport already require. A report definition with a null/empty
    /// AllowedRoles list has no additional restriction (every report type had this behaviour before
    /// AllowedRoles existed). Checked via ClaimsPrincipal.IsInRole against the caller's role claims,
    /// same convention as the rest of this app's role checks (e.g. User.IsInRole("Superuser"));
    /// Superuser/System Admin always bypass, matching the existing platform-owner-bypass convention.
    /// </summary>
    private bool CallerCanSeeReport(ReportDefinitionDto def)
    {
        if (def.AllowedRoles == null || def.AllowedRoles.Length == 0)
            return true;

        if (User.IsInRole("Superuser") || User.IsInRole("System Admin"))
            return true;

        return def.AllowedRoles.Any(role => User.IsInRole(role));
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
                var cachedCatalog = JsonSerializer.Deserialize<ReportCatalogResponse>(cached);
                // 5a role-gating is applied here (not baked into the cached payload) because the
                // cache key is per-org, not per-caller-role - two users in the same org with
                // different roles must see different report lists from the SAME cached entry.
                if (cachedCatalog != null)
                    ApplyReportRoleGating(cachedCatalog);
                return Ok(cachedCatalog);
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
            // Was 4 hours - too long for a catalog that changes shape whenever a report gains new
            // columns/filters/chart options (as this session's work did, several times in one day).
            // A stale entry from just before a deploy would keep serving the OLD catalog shape for
            // up to 4 hours after the fix was already live, indistinguishable from "not deployed".
            // 10 minutes still meaningfully caches within a browsing session while keeping that
            // window short; this endpoint is cheap to recompute (in-memory Def() lists + one
            // lightweight org lookup), so there's no real cost to caching it less aggressively.
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(catalog), TimeSpan.FromMinutes(10));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache report catalog");
        }

        // 5a: applied AFTER caching the tenant/module-filtered catalog above, so the cached entry
        // (shared by every caller in this org) stays role-agnostic and each caller's role gating is
        // computed fresh from their own claims on every request/cache-hit.
        ApplyReportRoleGating(catalog);

        return Ok(catalog);
    }

    /// <summary>
    /// Filters each module's report list down to the caller's role-visible reports (5a) - e.g. a
    /// Commercial Weighing Operator shouldn't even see "Transaction Audit Log" in the catalog.
    /// Mutates the given catalog in place.
    /// </summary>
    private void ApplyReportRoleGating(ReportCatalogResponse catalog)
    {
        foreach (var moduleCatalog in catalog.Modules)
        {
            moduleCatalog.Reports = moduleCatalog.Reports
                .Where(CallerCanSeeReport)
                .ToList();
        }
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
        [FromQuery] string? countyId = null,
        [FromQuery] string? subcountyId = null,
        [FromQuery] string? roadId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? weighingType = null,
        [FromQuery] string? controlStatus = null,
        [FromQuery] string? columns = null,
        [FromQuery] string? chartType = null,
        [FromQuery] bool useDefaults = true,
        CancellationToken ct = default)
    {
        // Verify the tenant has access to this report module
        if (!await IsReportModuleAllowedAsync(module))
        {
            _logger.LogWarning("Report module access denied for tenant {OrgId}: {Module}", _tenantContext.OrganizationId, module);
            return Forbid();
        }

        // 5a: verify the caller's role can see this specific report type (not just the flat
        // analytics.read permission gating the whole endpoint). Looked up directly from the
        // in-memory Def() catalog (bypassing the /catalog cache entirely) - cheap, same as the
        // catalog endpoint's own "no real cost to caching it less aggressively" reasoning.
        var reportDef = _reportService.GetCatalog(module).Modules
            .SelectMany(m => m.Reports)
            .FirstOrDefault(r => string.Equals(r.Id, reportType, StringComparison.OrdinalIgnoreCase));

        if (reportDef != null && !CallerCanSeeReport(reportDef))
        {
            _logger.LogWarning("Report type access denied for caller: {Module}/{ReportType}", module, reportType);
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
                    orgLogoFile = !string.IsNullOrEmpty(org.LogoUrl) ? Path.GetFileName(org.LogoUrl) : null;
                    isEnforcement = !string.Equals(org.TenantType,
                        TenantModules.TenantTypeCommercialWeighing, StringComparison.OrdinalIgnoreCase);
                }
            }

            var filters = new ReportFilterParams
            {
                DateFrom = dateFrom.HasValue ? DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : null,
                DateTo = dateTo.HasValue ? DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc) : null,
                StationId = stationId,
                CountyId = countyId,
                SubcountyId = subcountyId,
                RoadId = roadId,
                Status = status,
                WeighingType = weighingType,
                ControlStatus = controlStatus,
                Columns = string.IsNullOrWhiteSpace(columns)
                    ? null
                    : columns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                ChartType = chartType,
                UseDefaults = useDefaults,
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
