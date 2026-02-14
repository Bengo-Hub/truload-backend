using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace TruLoad.Backend.Middleware;

/// <summary>
/// Configuration for rate limiting middleware to prevent API abuse and ensure fair usage.
/// Uses ASP.NET Core's built-in rate limiting features from .NET 10.
/// All limits are read from RateLimitSettings singleton (populated from DB at startup,
/// refreshable via admin endpoint without restart).
///
/// Policy summary (per user, defaults shown):
///   Global (authenticated): 600/min  - baseline for all endpoints
///   Global (anonymous):      30/min  - stricter for unauthenticated
///   "dashboard":            800/min  - statistics/trend/analytics endpoints
///   "weighing":             600/min  - core weighing operations
///   "autoweigh":           1000/min  - machine-to-machine TruConnect traffic
///   "api":                  200/min  - general API endpoints
///   "search":               120/min  - search/list endpoints
///   "reports":               30/5min - heavy operations (PDF, exports)
///   "auth":                  10/5min - login/token endpoints (brute-force protection)
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
            // Global default policy - per-user partitioning
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var settings = httpContext.RequestServices.GetRequiredService<RateLimitSettings>();
                var userId = httpContext.User.Identity?.IsAuthenticated == true
                    ? httpContext.User.FindFirst("sub")?.Value ?? "anonymous"
                    : "anonymous";

                if (userId == "anonymous")
                {
                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: "anonymous",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = settings.GlobalAnonymousPermit,
                            Window = TimeSpan.FromMinutes(1),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 5
                        });
                }

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: userId,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.GlobalAuthenticatedPermit,
                        Window = TimeSpan.FromMinutes(settings.GlobalAuthenticatedWindowMinutes),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 30
                    });
            });

            // Dashboard statistics/trend endpoints
            options.AddFixedWindowLimiter("dashboard", opts =>
            {
                opts.PermitLimit = 800;
                opts.Window = TimeSpan.FromMinutes(1);
                opts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opts.QueueLimit = 40;
            });

            // API policy - general API endpoints
            options.AddFixedWindowLimiter("api", opts =>
            {
                opts.PermitLimit = 200;
                opts.Window = TimeSpan.FromMinutes(1);
                opts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opts.QueueLimit = 15;
            });

            // Weighing operations
            options.AddFixedWindowLimiter("weighing", opts =>
            {
                opts.PermitLimit = 600;
                opts.Window = TimeSpan.FromMinutes(1);
                opts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opts.QueueLimit = 30;
            });

            // Autoweigh/webhook - machine-to-machine traffic
            options.AddFixedWindowLimiter("autoweigh", opts =>
            {
                opts.PermitLimit = 1000;
                opts.Window = TimeSpan.FromMinutes(1);
                opts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opts.QueueLimit = 50;
            });

            // Authentication endpoints - strict limits for brute-force protection
            options.AddFixedWindowLimiter("auth", opts =>
            {
                opts.PermitLimit = 10;
                opts.Window = TimeSpan.FromMinutes(5);
                opts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opts.QueueLimit = 2;
            });

            // Heavy operations (reports, exports, PDF generation)
            options.AddFixedWindowLimiter("reports", opts =>
            {
                opts.PermitLimit = 30;
                opts.Window = TimeSpan.FromMinutes(5);
                opts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opts.QueueLimit = 5;
            });

            // Search endpoints
            options.AddFixedWindowLimiter("search", opts =>
            {
                opts.PermitLimit = 120;
                opts.Window = TimeSpan.FromMinutes(1);
                opts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opts.QueueLimit = 15;
            });

            // Rejection response
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                TimeSpan? retryAfter = null;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue))
                {
                    retryAfter = retryAfterValue;
                    context.HttpContext.Response.Headers.RetryAfter = retryAfterValue.TotalSeconds.ToString();
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
    /// Should be called after UseRouting() and before UseAuthentication().
    /// </summary>
    public static IApplicationBuilder UseTruLoadRateLimiting(this IApplicationBuilder app)
    {
        app.UseRateLimiter();
        return app;
    }
}
