using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Middleware;

/// <summary>
/// Action filter that blocks enforcement-only API routes for CommercialWeighing tenants.
/// Commercial tenants may not access cases, prosecution, yard, demerit, acts, axle fee
/// schedules, prohibition orders, or special releases.
/// Superusers bypass this check.
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
        "/api/v1/acts",
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
        if (IsRestrictedPath(context.Request.Path))
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
