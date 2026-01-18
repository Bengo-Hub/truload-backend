using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace TruLoad.Backend.Middleware;

/// <summary>
/// Configuration for rate limiting middleware to prevent API abuse and ensure fair usage.
/// Uses ASP.NET Core's built-in rate limiting features from .NET 8.
/// </summary>
public static class RateLimitingConfiguration
{
    /// <summary>
    /// Configures rate limiting services with multiple policies for different use cases.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddTruLoadRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Global default policy - stricter limits for unauthenticated requests
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var userId = httpContext.User.Identity?.IsAuthenticated == true
                    ? httpContext.User.FindFirst("sub")?.Value ?? "anonymous"
                    : "anonymous";

                // Anonymous users get lower limits
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

                // Authenticated users get higher limits
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: userId,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 10
                    });
            });

            // API policy - general API endpoints (100 requests per minute per user)
            options.AddFixedWindowLimiter("api", options =>
            {
                options.PermitLimit = 100;
                options.Window = TimeSpan.FromMinutes(1);
                options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                options.QueueLimit = 10;
            });

            // Weighing operations - higher limits for high-traffic operations
            options.AddFixedWindowLimiter("weighing", options =>
            {
                options.PermitLimit = 200;
                options.Window = TimeSpan.FromMinutes(1);
                options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                options.QueueLimit = 20;
            });

            // Authentication endpoints - stricter limits to prevent brute force
            options.AddFixedWindowLimiter("auth", options =>
            {
                options.PermitLimit = 10;
                options.Window = TimeSpan.FromMinutes(5);
                options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                options.QueueLimit = 2;
            });

            // Heavy operations (reports, exports) - lower limits
            options.AddFixedWindowLimiter("reports", options =>
            {
                options.PermitLimit = 20;
                options.Window = TimeSpan.FromMinutes(5);
                options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                options.QueueLimit = 5;
            });

            // Search endpoints - moderate limits
            options.AddFixedWindowLimiter("search", options =>
            {
                options.PermitLimit = 50;
                options.Window = TimeSpan.FromMinutes(1);
                options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                options.QueueLimit = 10;
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
    /// <param name="app">Application builder</param>
    /// <returns>Application builder for chaining</returns>
    public static IApplicationBuilder UseTruLoadRateLimiting(this IApplicationBuilder app)
    {
        app.UseRateLimiter();
        return app;
    }
}
