using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using TruLoad.Backend.Data;
using TruLoad.Backend.Services.Interfaces.Subscription;

namespace TruLoad.Backend.Middleware;

/// <summary>
/// Enforces active subscription for CommercialWeighing tenants.
/// Returns HTTP 402 if the subscription is expired, cancelled, or missing.
/// Enforcement tenants are never checked — they have no subscription requirement.
///
/// Redis cache key: sub:status:{orgId}  TTL: 60 seconds
/// </summary>
public class SubscriptionEnforcementMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SubscriptionEnforcementMiddleware> _logger;

    public SubscriptionEnforcementMiddleware(RequestDelegate next, ILogger<SubscriptionEnforcementMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context,
        TruLoadDbContext dbContext,
        ISubscriptionService subscriptionService,
        IConnectionMultiplexer redis)
    {
        // Only check authenticated API requests that are not the auth endpoints themselves
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            await _next(context);
            return;
        }

        var orgIdClaim = context.User.FindFirst("org_id")?.Value
                         ?? context.User.FindFirst(ClaimTypes.GroupSid)?.Value;
        if (!Guid.TryParse(orgIdClaim, out var orgId))
        {
            await _next(context);
            return;
        }

        // Skip paths that should always be accessible (auth, health, webhooks)
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/api/v1/auth", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/v1/portal", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/v1/payments/treasury-callback", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/hangfire", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Check Redis cache first
        var cacheKey = $"sub:status:{orgId}";
        var db = redis.GetDatabase();
        var cached = await db.StringGetAsync(cacheKey);
        string? cachedStatus = cached.HasValue ? cached.ToString() : null;

        if (cachedStatus == null)
        {
            // Load org from DB to check tenant type and SSO slug
            var org = await dbContext.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orgId);

            if (org == null || org.TenantType != "CommercialWeighing")
            {
                await _next(context);
                return;
            }

            if (string.IsNullOrWhiteSpace(org.SsoTenantSlug))
            {
                // No slug configured — allow access (misconfigured commercial tenant)
                await _next(context);
                return;
            }

            var sub = await subscriptionService.GetTenantSubscriptionAsync(org.SsoTenantSlug);
            cachedStatus = sub.Status;
            await db.StringSetAsync(cacheKey, cachedStatus, TimeSpan.FromSeconds(60));
        }

        if (cachedStatus is "EXPIRED" or "CANCELLED" or "NONE")
        {
            _logger.LogWarning("Subscription check failed for org {OrgId}: status={Status}", orgId, cachedStatus);
            context.Response.StatusCode = 402;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "subscription_required", status = cachedStatus }));
            return;
        }

        await _next(context);
    }
}

public static class SubscriptionEnforcementMiddlewareExtensions
{
    public static IApplicationBuilder UseSubscriptionEnforcement(this IApplicationBuilder app)
        => app.UseMiddleware<SubscriptionEnforcementMiddleware>();
}
