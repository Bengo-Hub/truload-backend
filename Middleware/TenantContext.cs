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

        // Enforce cross-station access: authenticated users can only access their own station
        // unless they have a privileged role (Superuser, System Admin)
        if (!await IsStationAccessAllowed(context, tenantContext))
        {
            _logger.LogWarning(
                "Cross-station access denied: User station claim does not match requested StationId={StationId}",
                tenantContext.StationId);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "Access denied: you do not have permission to access this station's data" });
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// Checks if the authenticated user is allowed to access the resolved station.
    /// Returns true if: unauthenticated, no station in context, user has no station claim,
    /// stations match, or user has Superuser/System Admin role.
    /// </summary>
    private Task<bool> IsStationAccessAllowed(HttpContext context, TenantContext tenantContext)
    {
        // No enforcement needed if user isn't authenticated or no station was resolved
        if (!context.User.Identity?.IsAuthenticated ?? true)
            return Task.FromResult(true);

        if (!tenantContext.StationId.HasValue)
            return Task.FromResult(true);

        // Only enforce when station was explicitly requested via header (not from JWT fallback)
        if (!tenantContext.IsExplicitTenant)
            return Task.FromResult(true);

        // Get user's station from JWT claim
        var userStationClaim = context.User.FindFirst("station_id");
        if (userStationClaim == null || !Guid.TryParse(userStationClaim.Value, out var userStationId))
            return Task.FromResult(true); // No station claim → don't enforce

        // Station matches → allowed
        if (userStationId == tenantContext.StationId.Value)
            return Task.FromResult(true);

        // HQ users can access any station (drill-down)
        if (context.User.FindFirst("is_hq_user")?.Value == "true")
            return Task.FromResult(true);

        // Privileged roles can access any station
        var roles = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        if (roles.Contains("Superuser") || roles.Contains("System Admin"))
            return Task.FromResult(true);

        return Task.FromResult(false);
    }

    private bool ShouldSkipTenantResolution(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        return path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/v1/docs", StringComparison.OrdinalIgnoreCase) ||
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

        // Do not fall back to station from claims when user is HQ (is_hq_user): they see all stations unless X-Station-ID is sent for drill-down
        if (!stationId.HasValue)
        {
            var isHqUser = context.User.FindFirst("is_hq_user")?.Value == "true";
            if (!isHqUser)
            {
                var stationIdClaim = context.User.FindFirst("station_id") ??
                                    context.User.FindFirst("outlet_id");

                if (stationIdClaim != null && Guid.TryParse(stationIdClaim.Value, out var claimStationId))
                {
                    stationId = claimStationId;
                    _logger.LogDebug("Resolved StationId from claims: {StationId}", stationId);
                }
            }
        }

        // Layer 3: Try Hostname/Domain-based resolution (mss.masterspace.co.ke, etc.)
        if (!orgId.HasValue)
        {
            var host = context.Request.Host.Host.ToLower();
            string? slug = null;

            if (host.Contains("mss.")) slug = "mss";
            else if (host.Contains("urbanloft.")) slug = "urban-loft";
            else if (host.Contains("kura.")) slug = "kura";
            else if (host.Contains("ultichange.")) slug = "ultichange";
            else if (host.Contains("codevertexitsolutions.com") || host.Contains("codevertex.")) slug = "codevertex";

            if (slug != null)
            {
                var org = await dbContext.Organizations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Code == slug.ToUpper() && o.IsActive);

                if (org != null)
                {
                    orgId = org.Id;
                    tenantContext.OrganizationCode = org.Code;
                    _logger.LogDebug("Resolved OrgId from Domain ({Host}): {OrgId}", host, orgId);
                }
            }
        }

        // Layer 4: Fallback to default organization (KURA)
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
