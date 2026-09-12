using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Middleware;

/// <summary>
/// Action filter that blocks enforcement-only API routes for CommercialWeighing tenants.
/// Commercial tenants may not access cases, prosecution, yard, demerit, axle fee schedules,
/// prohibition orders, or special releases.
///
/// `/api/v1/acts` is a special case, not a blanket block: a commercial tenant may optionally
/// opt into a legal framework (Organization.SelectedLegalFramework) for an axle-load
/// pre-compliance check - e.g. a transporter confirming their load would pass an enforcement
/// weighbridge's Traffic Act/EAC tolerances before they get there. That requires READ access
/// to the Act catalogue and tolerance settings even before a framework is chosen (to populate
/// the picker), so only the enforcement-administrative sub-routes and every write are blocked
/// for commercial tenants; see IsRestrictedActsRequest. Superusers bypass this check entirely.
/// </summary>
public class CommercialModeFilter : IMiddleware
{
    /// <summary>
    /// Route prefixes (relative to /api/v1/) that are restricted for CommercialWeighing tenants.
    /// </summary>
    private static readonly string[] RestrictedPrefixes =
    [
        "/api/v1/cases",
        "/api/v1/prosecution",
        "/api/v1/yard",
        "/api/v1/demerit",
        "/api/v1/axle-fee-schedules",
        "/api/v1/prohibition",
        "/api/v1/special-releases",
    ];

    /// <summary>
    /// `/api/v1/acts` sub-routes that stay enforcement-only even for a commercial tenant that
    /// has opted into a legal framework: violation fee schedules and demerit points have no
    /// meaning for a commercial pre-compliance check (no fees/demerits are ever charged from
    /// it), so there is no case for exposing them.
    /// </summary>
    private static readonly string[] RestrictedActsReadSubPaths =
    [
        "/api/v1/acts/fee-schedules",
        "/api/v1/acts/axle-type-fees",
        "/api/v1/acts/demerit-points",
    ];

    private readonly TruLoadDbContext _dbContext;
    private readonly ILogger<CommercialModeFilter> _logger;

    public CommercialModeFilter(TruLoadDbContext dbContext, ILogger<CommercialModeFilter> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Only enforce for authenticated, non-superuser requests on restricted paths
        if (IsRestrictedPath(context.Request.Path) || IsRestrictedActsRequest(context.Request.Path, context.Request.Method))
        {
            var user = context.User;
            if (user?.Identity?.IsAuthenticated == true &&
                !user.IsInRole("Superuser") &&
                !user.IsInRole("SUPERUSER"))
            {
                var orgId = ResolveOrgId(context);
                if (orgId.HasValue)
                {
                    var tenantType = await GetTenantTypeAsync(orgId.Value);
                    if (tenantType == "CommercialWeighing")
                    {
                        _logger.LogWarning(
                            "CommercialModeFilter blocked {Method} {Path} for org {OrgId} (CommercialWeighing tenant)",
                            context.Request.Method, context.Request.Path, orgId);

                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(new
                        {
                            error = "not_available_in_commercial_mode",
                            message = "This feature is not available for Commercial Weighing tenants."
                        });
                        return;
                    }
                }
            }
        }

        await next(context);
    }

    private static bool IsRestrictedPath(PathString path)
    {
        var value = path.Value ?? string.Empty;
        foreach (var prefix in RestrictedPrefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Fine-grained acts gate: any write under /api/v1/acts (editing the enforcement Act
    /// catalogue/tolerance policy, or the org-wide default Act) stays enforcement-only - a
    /// commercial tenant picks its own opt-in framework via
    /// PATCH /api/v1/organizations/current/commercial-settings instead, never these routes.
    /// Reads are open EXCEPT the enforcement-billing-specific sub-paths in
    /// RestrictedActsReadSubPaths (fee schedules, demerit points), which a commercial
    /// pre-compliance check has no use for.
    /// </summary>
    private static bool IsRestrictedActsRequest(PathString path, string method)
    {
        var value = path.Value ?? string.Empty;
        if (!value.StartsWith("/api/v1/acts", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!HttpMethods.IsGet(method))
            return true;

        foreach (var sub in RestrictedActsReadSubPaths)
        {
            if (value.StartsWith(sub, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static Guid? ResolveOrgId(HttpContext context)
    {
        // Try X-Org-ID header first
        if (context.Request.Headers.TryGetValue(TenantContextMiddleware.OrgIdHeader, out var headerVal) &&
            Guid.TryParse(headerVal.FirstOrDefault(), out var headerId))
            return headerId;

        // Fall back to JWT claim
        var claim = context.User.FindFirst("org_id")
            ?? context.User.FindFirst("organization_id")
            ?? context.User.FindFirst("tenant_id");

        if (claim != null && Guid.TryParse(claim.Value, out var claimId))
            return claimId;

        return null;
    }

    private async Task<string?> GetTenantTypeAsync(Guid orgId)
    {
        return await _dbContext.Organizations
            .AsNoTracking()
            .Where(o => o.Id == orgId && o.IsActive)
            .Select(o => o.TenantType)
            .FirstOrDefaultAsync();
    }
}

/// <summary>
/// Extension methods for CommercialModeFilter registration.
/// </summary>
public static class CommercialModeFilterExtensions
{
    public static IServiceCollection AddCommercialModeFilter(this IServiceCollection services)
    {
        services.AddScoped<CommercialModeFilter>();
        return services;
    }

    /// <summary>
    /// Register CommercialModeFilter middleware. Must be called after UseAuthentication() and UseTenantContext().
    /// </summary>
    public static IApplicationBuilder UseCommercialModeFilter(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CommercialModeFilter>();
    }
}
