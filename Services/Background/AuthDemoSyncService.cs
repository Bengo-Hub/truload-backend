using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Identity;

namespace TruLoad.Backend.Services.Background;

/// <summary>
/// Syncs auth-api's codevertex-demo weighing-relevant personas
/// (commercial.operator@demo.codevertexafrica.com, commercial.finance@demo.codevertexafrica.com)
/// into the local TRULOAD-DEMO org / DEMO-WB-01 station, so a prospect can practice/train on
/// TruConnect against the platform-wide shared demo tenant without any risk of fake data landing
/// on a real organization.
///
/// Subscribes to the "auth" JetStream stream (subjects "auth.>") — the same stream and subjects
/// hospital-api's own AuthEventHandler already consumes (see hospital-service/hospital-api/
/// internal/modules/identity/auth_events.go, the reference architecture this mirrors) — via a
/// durable, ack-explicit consumer. This is an upgrade over SubscriptionCacheInvalidationService's
/// fire-and-forget core-NATS pattern, justified because losing an identity-sync event here means
/// a demo persona silently never appears, with no retry.
///
/// Filter (deliberately tighter than hospital's — this service is scoped to exactly ONE tenant,
/// not "every tenant where our vertical applies", so it has no outlet-style secondary signal and
/// must not guess):
///   1. Hard gate: the event's tenant_slug must be exactly "codevertex-demo".
///   2. Role allowlist: exactly "commercial_weighing_operator" / "commercial_finance" — the two
///      roles auth-api's cmd/seed/seed_users.go truloadDemoStaff actually publishes (confirmed by
///      reading that file, not guessed). Ambiguous generic role names (admin/manager/etc.) are
///      never accepted, since codevertex-demo hosts many other verticals' demo staff under the
///      same tenant and role names are reused across them.
///
/// auth.user.created/updated: find-or-update the SAME ApplicationUser row UserSeeder.cs's
/// SeedCommercialDemoStaffAsync already creates/repairs for this exact email (matched by email,
/// never a second row for the same address), resolving org/station via .IgnoreQueryFilters()
/// because a BackgroundService has no request-scoped ITenantContext (same pattern
/// UserManagementSeeder.cs/UserSeeder.cs already use).
///
/// auth.user.deleted: deactivates (LockoutEnd = MaxValue) rather than hard-deletes — unlike
/// hospital-api's demo data, TruLoad has real FKs from users into weighing/case data that a
/// hard-delete could violate.
/// </summary>
public class AuthDemoSyncService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuthDemoSyncService> _logger;

    private const string StreamName = "auth";
    private const string DurableName = "truload-auth-demo-sync";
    private const string FilterSubject = "auth.user.>";
    private const string DemoTenantSlug = "codevertex-demo";
    private const string DemoOrgCode = "TRULOAD-DEMO";
    private const string DemoStationCode = "DEMO-WB-01";

    // auth-api role string -> local TruLoad ApplicationRole.Name (Data/Seeders/RoleSeeder.cs).
    // Only these two are demo-relevant for commercial weighing — see truloadDemoStaff in
    // auth-service/auth-api/cmd/seed/seed_users.go. Values confirmed verbatim from that file and
    // from UserSeeder.cs's own FindByNameAsync/AddToRoleAsync calls, not guessed.
    private static readonly Dictionary<string, string> RoleMap = new()
    {
        ["commercial_weighing_operator"] = "Commercial Weighing Operator",
        ["commercial_finance"] = "Commercial Finance",
    };

    public AuthDemoSyncService(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<AuthDemoSyncService> logger)
    {
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool>("Nats:Enabled");
        if (!enabled)
        {
            _logger.LogInformation("NATS auth-demo sync disabled (Nats:Enabled=false)");
            return;
        }

        var url = _configuration["Nats:Url"] ?? "nats://localhost:4222";

        await using var nats = new NatsConnection(new NatsOpts { Url = url });

        try
        {
            await nats.ConnectAsync();
            _logger.LogInformation("NATS auth-demo sync connected to {Url}", url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NATS connection failed — auth-demo sync inactive");
            return;
        }

        var js = new NatsJSContext(nats);

        // Guard against a startup race with auth-api actually owning stream creation — mirrors
        // hospital-api's AuthEventHandler.SubscribeToAuthEvents guard exactly (same stream name,
        // subjects and retention settings), so two services racing to create it never disagree.
        try
        {
            await js.CreateStreamAsync(
                new StreamConfig(StreamName, new[] { "auth.>" })
                {
                    Retention = StreamConfigRetention.Limits,
                    MaxAge = TimeSpan.FromHours(72),
                    Storage = StreamConfigStorage.File,
                },
                stoppingToken);
        }
        catch (NatsJSApiException ex) when (ex.Error?.Code == 400)
        {
            // Stream already exists (created by auth-api or a peer replica) — expected on every
            // steady-state startup, not an error.
            _logger.LogDebug("auth stream already exists: {Message}", ex.Error?.Description);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not ensure 'auth' stream exists — continuing, consumer bind may still succeed");
        }

        INatsJSConsumer consumer;
        try
        {
            consumer = await js.CreateOrUpdateConsumerAsync(
                StreamName,
                new ConsumerConfig(DurableName)
                {
                    FilterSubject = FilterSubject,
                    AckPolicy = ConsumerConfigAckPolicy.Explicit,
                    AckWait = TimeSpan.FromSeconds(30),
                    MaxDeliver = 5,
                    DeliverPolicy = ConsumerConfigDeliverPolicy.All,
                },
                stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create/bind durable consumer {Durable} on stream {Stream} — auth-demo sync inactive", DurableName, StreamName);
            return;
        }

        _logger.LogInformation("auth-demo sync active: stream={Stream} durable={Durable} filter={Filter}", StreamName, DurableName, FilterSubject);

        await foreach (var msg in consumer.ConsumeAsync<string>(cancellationToken: stoppingToken))
        {
            try
            {
                await HandleAsync(msg.Data, stoppingToken);
                await msg.AckAsync(cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling {Subject} message — nak for redelivery", msg.Subject);
                try { await msg.NakAsync(cancellationToken: stoppingToken); } catch { /* best-effort */ }
            }
        }
    }

    private async Task HandleAsync(string? payload, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return;

        string? eventType = null;
        string? tenantSlug = null;
        string? authUserId = null;
        string? email = null;
        string? fullName = null;
        var roles = new List<string>();

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (root.TryGetProperty("event_type", out var et))
                eventType = et.GetString();

            // tenant_slug lives at the envelope's top level (shared-events Event.TenantSlug) AND
            // is duplicated inside payload by auth-api's seed — check both, top-level wins.
            if (root.TryGetProperty("tenant_slug", out var ts) && ts.ValueKind == JsonValueKind.String)
                tenantSlug = ts.GetString();

            if (root.TryGetProperty("payload", out var p) && p.ValueKind == JsonValueKind.Object)
            {
                if (string.IsNullOrEmpty(tenantSlug) && p.TryGetProperty("tenant_slug", out var pts))
                    tenantSlug = pts.GetString();
                if (p.TryGetProperty("user_id", out var uid))
                    authUserId = uid.GetString();
                if (p.TryGetProperty("email", out var em))
                    email = em.GetString();
                if (p.TryGetProperty("full_name", out var fn))
                    fullName = fn.GetString();
                if (p.TryGetProperty("roles", out var rolesEl) && rolesEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var r in rolesEl.EnumerateArray())
                    {
                        if (r.ValueKind == JsonValueKind.String && r.GetString() is { } rs)
                            roles.Add(rs);
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse auth event payload: {Payload}", payload);
            return;
        }

        // Hard gate #1: reject anything not exactly codevertex-demo.
        if (!string.Equals(tenantSlug, DemoTenantSlug, StringComparison.Ordinal))
            return;

        if (string.IsNullOrWhiteSpace(eventType) || string.IsNullOrWhiteSpace(email))
            return;

        switch (eventType)
        {
            case "created":
            case "updated":
                await HandleUpsertAsync(authUserId, email, fullName ?? email, roles, ct);
                break;
            case "deleted":
                await HandleDeleteAsync(email, ct);
                break;
            default:
                // pin_set and any other future auth.user.* subtype is irrelevant to this sync.
                return;
        }
    }

    private async Task HandleUpsertAsync(string? authUserId, string email, string fullName, List<string> roles, CancellationToken ct)
    {
        // Hard gate #2: role allowlist — reject ambiguous/unmapped roles entirely rather than
        // guessing a mapping (this is exactly the class of leak
        // [[hospital-demo-tenant-leak-and-fleet-backfill-cleanup-2026-08-30]] documents for a
        // generic-role-name false positive).
        string? localRoleName = null;
        foreach (var r in roles)
        {
            if (RoleMap.TryGetValue(r, out var mapped))
            {
                localRoleName = mapped;
                break;
            }
        }
        if (localRoleName is null)
        {
            _logger.LogDebug("Skipping codevertex-demo auth.user event for {Email} (auth_user_id={AuthUserId}): no weighing-relevant role in [{Roles}]",
                email, authUserId, string.Join(",", roles));
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TruLoadDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var org = await db.Organizations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Code == DemoOrgCode, ct);
        if (org is null)
        {
            _logger.LogWarning("TRULOAD-DEMO organization not found — cannot sync {Email}", email);
            return;
        }

        var station = await db.Stations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Code == DemoStationCode, ct);

        // Match the SAME row UserSeeder.cs's SeedCommercialDemoStaffAsync already creates/repairs
        // for this exact email — find-or-update, never a second conflicting record.
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                FullName = fullName,
                OrganizationId = org.Id,
                StationId = station?.Id,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                TwoFactorEnabled = false,
                LockoutEnabled = true,
            };

            // No usable credential arrives over the wire (production login for this account is
            // via SSO, same as every other codevertex-demo persona) — a random value satisfies
            // Identity's CreateAsync password-required contract without ever being a real,
            // guessable local credential.
            var randomPassword = "Sync!" + Guid.NewGuid().ToString("N") + "Aa1";
            var createResult = await userManager.CreateAsync(user, randomPassword);
            if (!createResult.Succeeded)
            {
                _logger.LogWarning("Failed to create demo user {Email}: {Errors}", email,
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return;
            }
            _logger.LogInformation("Created demo user {Email} from auth.user.{EventType} event", email, "created/updated");
        }
        else
        {
            var updated = false;
            if (user.OrganizationId != org.Id) { user.OrganizationId = org.Id; updated = true; }
            if (user.StationId is null && station is not null) { user.StationId = station.Id; updated = true; }
            if (!string.Equals(user.FullName, fullName, StringComparison.Ordinal)) { user.FullName = fullName; updated = true; }
            // Re-activate if this user had previously been deactivated by an auth.user.deleted
            // event and has now reappeared (e.g. a re-seed).
            if (user.LockoutEnd is not null && user.LockoutEnd > DateTimeOffset.UtcNow)
            {
                await userManager.SetLockoutEndDateAsync(user, null);
                updated = true;
            }
            if (updated)
                await userManager.UpdateAsync(user);
        }

        // Idempotently re-sync the role assignment on every update — corrects a stale role the
        // same way UserSeeder.cs's own repair logic does (e.g. a promotion/role correction
        // upstream in auth-api).
        var currentRoles = await userManager.GetRolesAsync(user);
        if (!currentRoles.Contains(localRoleName))
        {
            if (currentRoles.Any())
                await userManager.RemoveFromRolesAsync(user, currentRoles);
            await userManager.AddToRoleAsync(user, localRoleName);
            _logger.LogInformation("Synced role for {Email}: {Role}", email, localRoleName);
        }
    }

    private async Task HandleDeleteAsync(string email, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
            return;

        // Deactivate, never hard-delete — TruLoad has real FKs from users into weighing/case data
        // that a hard-delete could violate (unlike hospital-api's demo data).
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        _logger.LogInformation("Deactivated demo user {Email} from auth.user.deleted event", email);
    }
}
