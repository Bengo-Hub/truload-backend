using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NATS.Client.Core;
using StackExchange.Redis;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Services.Background;

/// <summary>
/// Subscribes to the NATS tenant.subscription.updated event published by subscriptions-api
/// when a tenant's plan or status changes. Invalidates the Redis cache key sub:status:{orgId}
/// so the next request re-fetches from subscriptions-api rather than serving stale status.
///
/// Redis key pattern: sub:status:{orgId}  (matches SubscriptionEnforcementMiddleware)
/// NATS subject:      tenant.subscription.updated
/// Payload:           { "tenant_slug": "..." }  OR  { "payload": { "tenant_slug": "..." } }
/// </summary>
public class SubscriptionCacheInvalidationService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<SubscriptionCacheInvalidationService> _logger;

    private const string Subject = "tenant.subscription.updated";

    public SubscriptionCacheInvalidationService(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        IConnectionMultiplexer redis,
        ILogger<SubscriptionCacheInvalidationService> logger)
    {
        _configuration = configuration;
        _scopeFactory  = scopeFactory;
        _redis         = redis;
        _logger        = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool>("Nats:Enabled");
        if (!enabled)
        {
            _logger.LogInformation("NATS subscription cache invalidation disabled (Nats:Enabled=false)");
            return;
        }

        var url = _configuration["Nats:Url"] ?? "nats://localhost:4222";

        await using var nats = new NatsConnection(new NatsOpts { Url = url });

        try
        {
            await nats.ConnectAsync();
            _logger.LogInformation("NATS subscription cache invalidation connected to {Url}", url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NATS connection failed — subscription cache invalidation inactive");
            return;
        }

        await foreach (var msg in nats.SubscribeAsync<string>(Subject, cancellationToken: stoppingToken))
        {
            try
            {
                await HandleAsync(msg.Data, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling {Subject} message", Subject);
            }
        }
    }

    private async Task HandleAsync(string? payload, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return;

        string? tenantSlug = null;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            // Support both flat { "tenant_slug": "..." } and nested { "payload": { "tenant_slug": "..." } }
            if (root.TryGetProperty("tenant_slug", out var s1) && s1.ValueKind == JsonValueKind.String)
                tenantSlug = s1.GetString();
            else if (root.TryGetProperty("payload", out var nested) &&
                     nested.TryGetProperty("tenant_slug", out var s2))
                tenantSlug = s2.GetString();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse NATS payload: {Payload}", payload);
            return;
        }

        if (string.IsNullOrWhiteSpace(tenantSlug))
            return;

        // Resolve tenant_slug → org.Id  (truload Redis key is sub:status:{orgId}, not tenant:{slug})
        Guid? orgId = null;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TruLoadDbContext>();
            orgId = await db.Organizations
                .AsNoTracking()
                .Where(o => o.SsoTenantSlug == tenantSlug)
                .Select(o => (Guid?)o.Id)
                .FirstOrDefaultAsync(ct);
        }

        if (orgId is null)
        {
            _logger.LogDebug("No TruLoad org found for tenant_slug={Slug} — nothing to invalidate", tenantSlug);
            return;
        }

        var cacheKey = $"sub:status:{orgId}";
        var redisDb = _redis.GetDatabase();
        var deleted = await redisDb.KeyDeleteAsync(cacheKey);

        _logger.LogInformation(
            "Subscription cache invalidated for tenant={Slug} orgId={OrgId} key={Key} deleted={Deleted}",
            tenantSlug, orgId, cacheKey, deleted);
    }
}
