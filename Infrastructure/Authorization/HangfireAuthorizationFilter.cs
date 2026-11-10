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
        // No HTTP context means no user to check: deny rather than dereference null.
        if (httpContext == null) return false;

        // If unauthenticated: redirect to app login page instead of returning 401
        var identity = httpContext.User?.Identity;
        if (identity == null || !identity.IsAuthenticated)
        {
            var returnUrl = httpContext.Request?.Path + httpContext.Request?.QueryString;
            var redirectTo = $"/hangfire/login?returnUrl={System.Uri.EscapeDataString(returnUrl ?? "/hangfire")}";
            httpContext.Response.Redirect(redirectTo);
            return false; // response already set to redirect
        }

        // Authenticated users allowed here; per-app permission checks may still apply elsewhere
        return true;
    }
}
