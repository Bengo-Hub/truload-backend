using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace TruLoad.Backend.Middleware;

/// <summary>
/// Configuration for rate limiting middleware to prevent API abuse and ensure fair usage.
/// Uses ASP.NET Core's built-in rate limiting features from .NET 10.
///
/// Policy summary (per user, per minute unless noted):
///   Global (authenticated): 300/min  - baseline for all endpoints
///   Global (anonymous):      30/min  - stricter for unauthenticated
///   "weighing":             600/min  - core weighing operations (dashboard, transactions, compliance)
///   "autoweigh":           1000/min  - machine-to-machine TruConnect middleware traffic
///   "api":                  200/min  - general API endpoints
///   "search":               120/min  - search/list endpoints
///   "reports":               30/5min - heavy operations (PDF, exports)
///   "auth":                  10/5min - login/token endpoints (brute-force protection)
/// </summary>
public static class RateLimitingConfiguration
{
    /// <summary>
    /// Configures rate limiting services with multiple policies for different use cases.
    /// </summary>
    public static IServiceCollection AddTruLoadRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Global default policy - per-user partitioning
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var userId = httpContext.User.Identity?.IsAuthenticated == true
                    ? httpContext.User.FindFirst("sub")?.Value ?? "anonymous"
                    : "anonymous";

                // Anonymous users get stricter limits
                if (userId == "anonymous")
                {
                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: "anonymous",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 30,
                            Window = TimeSpan.FromMinutes(1),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 5
                        });
                }

                // Authenticated users - generous limit so essential workflows aren't blocked
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: userId,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 300,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 20
                    });
            });

            // API policy - general API endpoints
            options.AddFixedWindowLimiter("api", opts =>
            {
                opts.PermitLimit = 200;
                opts.Window = TimeSpan.FromMinutes(1);
                opts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opts.QueueLimit = 15;
            });

            // Weighing operations - high limits for core operational workflows
            // (dashboard polling, transaction creation, weight capture, compliance checks)
            options.AddFixedWindowLimiter("weighing", opts =>
            {
                opts.PermitLimit = 600;
                opts.Window = TimeSpan.FromMinutes(1);
                opts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opts.QueueLimit = 30;
            });

            // Autoweigh/webhook - highest limits for machine-to-machine traffic
            // (TruConnect middleware sends bursts of autoweigh data)
            options.AddFixedWindowLimiter("autoweigh", opts =>
            {
                opts.PermitLimit = 1000;
                opts.Window = TimeSpan.FromMinutes(1);
                opts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opts.QueueLimit = 50;
            });

            // Authentication endpoints - strict limits to prevent brute force
            options.AddFixedWindowLimiter("auth", opts =>
            {
                opts.PermitLimit = 10;
                opts.Window = TimeSpan.FromMinutes(5);
                opts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opts.QueueLimit = 2;
            });

            // Heavy operations (reports, exports, PDF generation) - lower limits
            options.AddFixedWindowLimiter("reports", opts =>
            {
                opts.PermitLimit = 30;
                opts.Window = TimeSpan.FromMinutes(5);
                opts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opts.QueueLimit = 5;
            });

            // Search endpoints - moderate limits
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
    /// Applies rate limiting middleware to the request pipeline.
    /// Should be called after UseRouting() and before UseAuthentication().
    /// </summary>
    public static IApplicationBuilder UseTruLoadRateLimiting(this IApplicationBuilder app)
    {
        app.UseRateLimiter();
        return app;
    }
}
