using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace TruLoad.Backend.Middleware;

/// <summary>
/// Configuration for rate limiting middleware to prevent API abuse and ensure fair usage.
/// Uses ASP.NET Core's built-in rate limiting features from .NET 10.
/// All limits are read from RateLimitSettings singleton (populated from DB at startup,
/// refreshable via admin endpoint without restart).
///
/// Every policy is partitioned: per user (JWT "sub") when authenticated, otherwise per client
/// IP. Before 2026-10 the named policies were one bucket shared by every user of the system and
/// the limiter ran before authentication, so all traffic shared the 30/min anonymous bucket.
/// Limits are per pod (in-process state); ingress-nginx and Cloudflare are the outer layers.
///
/// Policy summary (per user or per IP, defaults shown):
///   Global (authenticated): 600/min  - baseline for all endpoints
///   Global (anonymous):      30/min  - stricter for unauthenticated
///   "dashboard":            800/min  - statistics/trend/analytics endpoints
///   "weighing":             600/min  - core weighing operations
///   "autoweigh":           1000/min  - machine-to-machine TruConnect traffic
///   "api":                  200/min  - general API endpoints
///   "search":               120/min  - search/list endpoints
///   "reports":               30/5min - heavy operations (PDF, exports)
///   "auth":                  10/5min - login, 2FA and password endpoints, per IP
/// </summary>
public static class RateLimitingConfiguration
{
    /// <summary>
    /// Configures rate limiting services with multiple policies for different use cases.
    /// All values are read from the RateLimitSettings singleton, which is populated
    /// from the database at startup and can be refreshed at runtime.
    /// </summary>
    public static IServiceCollection AddTruLoadRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                // Probes and the SignalR socket are not API calls.
                var path = httpContext.Request.Path;
                if (path.StartsWithSegments("/health") || path.StartsWithSegments("/hubs"))
                    return RateLimitPartition.GetNoLimiter("exempt");

                var settings = httpContext.RequestServices.GetRequiredService<RateLimitSettings>();
                var user = UserId(httpContext);
                return user == null
                    ? Fixed("anon:" + ClientIp(httpContext), settings.GlobalAnonymousPermit, 1, 5)
                    : Fixed("user:" + user, settings.GlobalAuthenticatedPermit, settings.GlobalAuthenticatedWindowMinutes, 30);
            });

            AddPolicy(options, "dashboard", s => s.DashboardPermit, _ => 1, 40);
            AddPolicy(options, "api", s => s.ApiPermit, _ => 1, 15);
            AddPolicy(options, "weighing", s => s.WeighingPermit, _ => 1, 30);
            AddPolicy(options, "autoweigh", s => s.AutoweighPermit, _ => 1, 50);
            AddPolicy(options, "reports", s => s.ReportsPermit, _ => 5, 5);
            AddPolicy(options, "search", s => s.SearchPermit, _ => 1, 15);

            // Credential endpoints: always per client IP (the caller is not signed in yet), no
            // queueing, so guessing passwords or 2FA codes is slow from any one address.
            options.AddPolicy("auth", httpContext =>
            {
                var s = httpContext.RequestServices.GetRequiredService<RateLimitSettings>();
                return Fixed("auth:" + ClientIp(httpContext), s.AuthPermit, s.AuthWindowMinutes, 0);
            });

            // Rejection response
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                TimeSpan? retryAfter = null;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue))
                {
                    retryAfter = retryAfterValue;
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfterValue.TotalSeconds)).ToString();
                }

                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "Too many requests",
                    message = "Rate limit exceeded. Please try again later.",
                    retryAfter = retryAfter?.TotalSeconds
                }, cancellationToken);
            };
        });

        return services;
    }

    private static void AddPolicy(RateLimiterOptions options, string name,
        Func<RateLimitSettings, int> permit, Func<RateLimitSettings, int> windowMinutes, int queue)
    {
        options.AddPolicy(name, httpContext =>
        {
            var s = httpContext.RequestServices.GetRequiredService<RateLimitSettings>();
            var user = UserId(httpContext);
            var key = name + ":" + (user != null ? "user:" + user : "ip:" + ClientIp(httpContext));
            return Fixed(key, permit(s), windowMinutes(s), queue);
        });
    }

    // The limit is part of the partition key, so values reloaded from settings apply right away
    // instead of only to callers never seen before (idle partitions are dropped by the runtime).
    private static RateLimitPartition<string> Fixed(string key, int permit, int windowMinutes, int queue) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: key + "|" + permit + "/" + windowMinutes,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, permit),
                Window = TimeSpan.FromMinutes(Math.Max(1, windowMinutes)),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = queue
            });

    private static string? UserId(HttpContext httpContext) =>
        httpContext.User.Identity?.IsAuthenticated == true
            ? httpContext.User.FindFirst("sub")?.Value
              ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            : null;

    /// <summary>
    /// Client IP for anonymous partitions. ingress-nginx sets X-Real-IP from Cloudflare's
    /// CF-Connecting-IP, which the client cannot forge; the first X-Forwarded-For hop can be
    /// forged and is never used.
    /// </summary>
    internal static string ClientIp(HttpContext httpContext)
    {
        var real = httpContext.Request.Headers["X-Real-IP"].ToString();
        if (!string.IsNullOrWhiteSpace(real))
            return real.Trim();
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>
    /// Loads rate limit values from the database into the RateLimitSettings singleton.
    /// Called at startup and can be called again to refresh values at runtime.
    /// </summary>
    public static async Task LoadRateLimitSettingsFromDbAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var settingsService = scope.ServiceProvider
            .GetRequiredService<Services.Interfaces.System.ISettingsService>();
        var rateLimitSettings = scope.ServiceProvider
            .GetRequiredService<RateLimitSettings>();

        rateLimitSettings.GlobalAuthenticatedPermit = await settingsService
            .GetSettingValueAsync(Models.System.SettingKeys.RateLimitGlobalAuthenticatedPermit, 600);
        rateLimitSettings.GlobalAuthenticatedWindowMinutes = await settingsService
            .GetSettingValueAsync(Models.System.SettingKeys.RateLimitGlobalAuthenticatedWindowMinutes, 1);
        rateLimitSettings.GlobalAnonymousPermit = await settingsService
            .GetSettingValueAsync(Models.System.SettingKeys.RateLimitGlobalAnonymousPermit, 30);
        rateLimitSettings.DashboardPermit = await settingsService
            .GetSettingValueAsync(Models.System.SettingKeys.RateLimitDashboardPermit, 800);
        rateLimitSettings.ApiPermit = await settingsService
            .GetSettingValueAsync(Models.System.SettingKeys.RateLimitApiPermit, 200);
        rateLimitSettings.WeighingPermit = await settingsService
            .GetSettingValueAsync(Models.System.SettingKeys.RateLimitWeighingPermit, 600);
        rateLimitSettings.AutoweighPermit = await settingsService
            .GetSettingValueAsync(Models.System.SettingKeys.RateLimitAutoweighPermit, 1000);
        rateLimitSettings.AuthPermit = await settingsService
            .GetSettingValueAsync(Models.System.SettingKeys.RateLimitAuthPermit, 10);
        rateLimitSettings.AuthWindowMinutes = await settingsService
            .GetSettingValueAsync(Models.System.SettingKeys.RateLimitAuthWindowMinutes, 5);
        rateLimitSettings.ReportsPermit = await settingsService
            .GetSettingValueAsync(Models.System.SettingKeys.RateLimitReportsPermit, 30);
        rateLimitSettings.SearchPermit = await settingsService
            .GetSettingValueAsync(Models.System.SettingKeys.RateLimitSearchPermit, 120);
    }

    /// <summary>
    /// Applies rate limiting middleware to the request pipeline.
    /// Must run after UseAuthentication() so policies can partition by the signed-in user.
    /// </summary>
    public static IApplicationBuilder UseTruLoadRateLimiting(this IApplicationBuilder app)
    {
        app.UseRateLimiter();
        return app;
    }
}
