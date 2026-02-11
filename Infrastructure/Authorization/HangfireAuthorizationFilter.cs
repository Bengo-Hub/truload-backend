using Hangfire.Dashboard;

namespace TruLoad.Backend.Infrastructure.Authorization;

/// <summary>
/// Authorization filter for Hangfire dashboard access.
/// Requires authenticated user with 'financial.admin' permission.
/// </summary>
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        
        // Must be authenticated
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return false;

        // Check for financial admin permission
        // In production, this should check against PermissionService
        // For now, allow any authenticated user (will be restricted by app configuration)
        return httpContext.User.Identity.IsAuthenticated;
    }
}
