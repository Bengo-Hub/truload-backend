using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Middleware;

/// <summary>
/// Provides access to the current tenant (Organization) and outlet (Station) context.
/// Resolves IDs with layered fallback: Request Headers → User Claims → Default (KURA).
/// Inject ITenantContext in services/controllers to access current org/station.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Current Organization ID (tenant). Never null after middleware runs.
    /// </summary>
    Guid OrganizationId { get; }

    /// <summary>
    /// Current Station ID (outlet/branch). May be null if not specified.
    /// </summary>
    Guid? StationId { get; }

    /// <summary>
    /// Organization code for the current tenant (e.g., "KENHA", "KURA").
    /// </summary>
    string OrganizationCode { get; }

    /// <summary>
    /// Indicates whether the tenant was resolved from headers (explicit) or fallback.
    /// </summary>
    bool IsExplicitTenant { get; }
}

/// <summary>
/// Scoped implementation of ITenantContext - populated by TenantContextMiddleware.
/// </summary>
public class TenantContext : ITenantContext
{
    public Guid OrganizationId { get; set; }
    public Guid? StationId { get; set; }
    public string OrganizationCode { get; set; } = string.Empty;
    public bool IsExplicitTenant { get; set; }
}

/// <summary>
/// Middleware that resolves and populates tenant context for each request.
/// Resolution order:
/// 1. X-Org-ID / X-Station-ID headers (explicit tenant selection)
/// 2. User claims (org_id, station_id from JWT)
/// 3. Default organization (KURA) as fallback
/// </summary>
public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantContextMiddleware> _logger;

    // Header names for explicit tenant selection
    public const string OrgIdHeader = "X-Org-ID";
    public const string StationIdHeader = "X-Station-ID";

    // Default organization code when no tenant can be resolved
    public const string DefaultOrgCode = "KURA";

    public TenantContextMiddleware(RequestDelegate next, ILogger<TenantContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, TruLoadDbContext dbContext)
    {
        // Skip tenant resolution for public/anonymous endpoints
        if (ShouldSkipTenantResolution(context))
        {
            await _next(context);
            return;
        }

        await ResolveTenantAsync(context, tenantContext, dbContext);

        _logger.LogDebug(
            "Tenant context resolved: OrgId={OrgId}, OrgCode={OrgCode}, StationId={StationId}, Explicit={IsExplicit}",
            tenantContext.OrganizationId, tenantContext.OrganizationCode, tenantContext.StationId, tenantContext.IsExplicitTenant);

        await _next(context);
    }

    private bool ShouldSkipTenantResolution(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        return path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
               context.Request.Method == HttpMethods.Options;
    }

    private async Task ResolveTenantAsync(HttpContext context, TenantContext tenantContext, TruLoadDbContext dbContext)
    {
        Guid? orgId = null;
        Guid? stationId = null;
        bool isExplicit = false;

        // Layer 1: Try request headers (explicit tenant selection)
        if (context.Request.Headers.TryGetValue(OrgIdHeader, out var orgIdHeader) &&
            Guid.TryParse(orgIdHeader.FirstOrDefault(), out var headerOrgId))
        {
            orgId = headerOrgId;
            isExplicit = true;
            _logger.LogDebug("Resolved OrgId from header: {OrgId}", orgId);
        }

        if (context.Request.Headers.TryGetValue(StationIdHeader, out var stationIdHeader) &&
            Guid.TryParse(stationIdHeader.FirstOrDefault(), out var headerStationId))
        {
            stationId = headerStationId;
            _logger.LogDebug("Resolved StationId from header: {StationId}", stationId);
        }

        // Layer 2: Try user claims from JWT (if not resolved from headers)
        if (!orgId.HasValue)
        {
            var orgIdClaim = context.User.FindFirst("org_id") ??
                            context.User.FindFirst("organization_id") ??
                            context.User.FindFirst("tenant_id");

            if (orgIdClaim != null && Guid.TryParse(orgIdClaim.Value, out var claimOrgId))
            {
                orgId = claimOrgId;
                _logger.LogDebug("Resolved OrgId from claims: {OrgId}", orgId);
            }
        }

        if (!stationId.HasValue)
        {
            var stationIdClaim = context.User.FindFirst("station_id") ??
                                context.User.FindFirst("outlet_id");

            if (stationIdClaim != null && Guid.TryParse(stationIdClaim.Value, out var claimStationId))
            {
                stationId = claimStationId;
                _logger.LogDebug("Resolved StationId from claims: {StationId}", stationId);
            }
        }

        // Layer 3: Fallback to default organization (KURA)
        if (!orgId.HasValue)
        {
            var defaultOrg = await dbContext.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Code == DefaultOrgCode && o.IsActive);

            if (defaultOrg != null)
            {
                orgId = defaultOrg.Id;
                tenantContext.OrganizationCode = defaultOrg.Code;
                _logger.LogDebug("Resolved OrgId from default ({DefaultOrgCode}): {OrgId}", DefaultOrgCode, orgId);
            }
            else
            {
                _logger.LogWarning("Default organization {DefaultOrgCode} not found in database", DefaultOrgCode);
            }
        }
        else
        {
            // Fetch org code for the resolved org ID
            var org = await dbContext.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orgId && o.IsActive);

            if (org != null)
            {
                tenantContext.OrganizationCode = org.Code;
            }
            else
            {
                _logger.LogWarning("Organization {OrgId} not found or inactive", orgId);
            }
        }

        // Validate station belongs to organization (if both specified)
        if (orgId.HasValue && stationId.HasValue)
        {
            var stationBelongsToOrg = await dbContext.Stations
                .AsNoTracking()
                .AnyAsync(s => s.Id == stationId && s.OrganizationId == orgId && s.IsActive);

            if (!stationBelongsToOrg)
            {
                _logger.LogWarning(
                    "Station {StationId} does not belong to Organization {OrgId}, clearing station",
                    stationId, orgId);
                stationId = null;
            }
        }

        // Populate tenant context
        tenantContext.OrganizationId = orgId ?? Guid.Empty;
        tenantContext.StationId = stationId;
        tenantContext.IsExplicitTenant = isExplicit;
    }
}

/// <summary>
/// Extension methods for registering TenantContext services and middleware.
/// </summary>
public static class TenantContextExtensions
{
    /// <summary>
    /// Registers ITenantContext as a scoped service.
    /// Call this in Program.cs before builder.Build().
    /// </summary>
    public static IServiceCollection AddTenantContext(this IServiceCollection services)
    {
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        return services;
    }

    /// <summary>
    /// Adds TenantContextMiddleware to the request pipeline.
    /// Call this after UseAuthentication() and before UseAuthorization().
    /// </summary>
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantContextMiddleware>();
    }
}
