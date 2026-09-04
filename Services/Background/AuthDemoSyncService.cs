using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using TruLoad.Backend.Common;
using TruLoad.Backend.Constants;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.Identity;

namespace TruLoad.Backend.Services.Background;

/// <summary>
/// Syncs auth-api's codevertex-demo weighing-relevant personas
/// (commercial.operator@/commercial.finance@/quarry.operator@/quarry.finance@/waste.operator@/
/// waste.finance@demo.codevertexafrica.com, plus the commercial Manager/Supervisor/Auditor and
/// enforcement Weighing Operator/Station Manager personas added alongside the ENF outlet — see
/// <see cref="RoleMap"/>) into their outlet-scoped local Organization/Station, so a prospect can
/// practice/train on TruConnect against the platform-wide shared demo tenant without any risk of
/// fake data landing on a real organization.
///
/// codevertex-demo hosts MULTIPLE TruLoad-relevant outlets today (auth-api's
/// cmd/seed/seed_tenants.go outletsByTenant["codevertex-demo"]): the original generic
/// "demo-commercial" outlet, one per commercial vertical this platform's quarry prospect needs
/// demoed ("demo-quarry", "demo-waste" — see Constants/CommercialVerticals.cs), and one axle-load
/// enforcement outlet ("demo-enforcement", code "ENF", use_case "axle_load_enforcement" —
/// pre-existing in auth-api's seed, wired up here for the first time). Each non-primary outlet gets
/// its OWN local Organization + Station pair (see <see cref="OutletOrgMap"/> and
/// <see cref="EnsureOutletOrganizationsAsync"/>), replacing the old single-outlet assumption where
/// every persona landed in the one static TRULOAD-DEMO/DEMO-WB-01 pair
/// (Data/Seeders/UserManagement/UserManagementSeeder.cs) regardless of outlet. TRULOAD-DEMO stays
/// the primary/fallback org (per the plan's explicit instruction not to remove it until this
/// sync-based replacement is proven working end-to-end) and is still seeded there, not created here.
/// Note ENF's <see cref="OutletOrgTarget.TenantType"/> is AxleLoadEnforcement, not
/// CommercialWeighing like every other entry — <see cref="EnsureOutletOrganizationsAsync"/> reads it
/// per-target rather than assuming CommercialWeighing for every non-primary outlet, and
/// <see cref="OutletOrgTarget.PaymentGateway"/> is left null for ENF so the Organization model's own
/// default ("ecitizen_pesaflow", the gateway enforcement fee payments actually use) applies instead
/// of the commercial outlets' "treasury" value.
///
/// Subscribes to the "auth" JetStream stream (subjects "auth.>") — the same stream and subjects
/// hospital-api's own AuthEventHandler already consumes (see hospital-service/hospital-api/
/// internal/modules/identity/auth_events.go, the reference architecture this mirrors) — via a
/// durable, ack-explicit consumer. This is an upgrade over SubscriptionCacheInvalidationService's
/// fire-and-forget core-NATS pattern, justified because losing an identity-sync event here means
/// a demo persona silently never appears, with no retry.
///
/// IMPORTANT — investigated but deliberately NOT built as a live auth.tenant.*/auth.outlet.*
/// event subscription, despite that being this feature's original design intent: auth-api's seed
/// path (cmd/seed/seed_tenants.go's seedOutletsForTenant) never publishes ANY auth.outlet.* event
/// — only its live admin-API path (httpapi/handlers/outlet_handler.go) does. Confirmed against the
/// architectural template this service mirrors: hospital-api's AuthEventHandler subscribes ONLY to
/// auth.user.* subjects and resolves outlets via a lazy REST pull
/// (tenantSyncer.SyncOutlets -> GET /api/v1/tenants/{slug}/outlets) on a local cache miss, not via
/// any auth.outlet.* NATS event — proven by the just-shipped demo-chemist outlet (auth-api commit
/// f3158f3), which added a brand-new seeded outlet with zero event publishing and it still became
/// usable downstream. Building a NATS subscription for auth.outlet.* here would be exactly the
/// "inert consumer nothing ever feeds" anti-pattern this same audit's root-cause finding flagged
/// for IOwnershipCheckService. Instead, outlet-to-organization routing uses a small static map
/// (<see cref="OutletOrgMap"/>, mirroring <see cref="RoleMap"/>'s already-proven "small curated
/// demo mapping" shape) keyed by outlet_code — a human-readable field seed_users.go now adds to
/// the auth.user.* payload specifically for this, avoiding any need to reverse-engineer auth-api's
/// SHA1-based outlet UUIDs back into a slug.
///
/// Filter (deliberately tighter than hospital's — this service is scoped to exactly ONE tenant,
/// not "every tenant where our vertical applies", so it has no outlet-style secondary signal and
/// must not guess):
///   1. Hard gate: the event's tenant_slug must be exactly "codevertex-demo".
///   2. Role allowlist: see <see cref="RoleMap"/> for the exact auth-api role strings accepted —
///      all of them intentionally specific/prefixed (e.g. "enforcement_weighing_operator", never
///      bare "operator"/"manager") to avoid exactly the ambiguous-generic-name trap
///      [[hospital-demo-tenant-leak-and-fleet-backfill-cleanup-2026-08-30]] documents, since
///      codevertex-demo hosts many other verticals' demo staff under the same tenant and generic
///      role names are reused across them.
///
/// auth.user.created/updated: find-or-update the SAME ApplicationUser row for this exact email
/// (matched by email, never a second row for the same address), resolving org/station via
/// .IgnoreQueryFilters() because a BackgroundService has no request-scoped ITenantContext (same
/// pattern UserManagementSeeder.cs/UserSeeder.cs already use).
///
/// auth.user.deleted: deactivates (LockoutEnd = MaxValue) rather than hard-deletes — unlike
/// hospital-api's demo data, TruLoad has real FKs from users into weighing/case data that a
/// hard-delete could violate.
///
/// SSO login note: multiple Organizations now share SsoTenantSlug="codevertex-demo" (TRULOAD-DEMO
/// plus every outlet organization this service creates). AuthController.SsoExchange resolves the
/// correct one per-user via ApplicationUser.OrganizationId (set here) rather than by slug alone —
/// see that method's own doc comment for the full reasoning.
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

    /// <summary>outlet_code used by personas that predate outlet scoping, or whose event omits it.</summary>
    private const string PrimaryOutletCode = "COMM";

    /// <summary>
    /// One entry per TruLoad-relevant codevertex-demo outlet (auth-api's outletsByTenant
    /// ["codevertex-demo"], outlet.code) -> the local Organization/Station it syncs to. A small
    /// static map, not a live lookup — see the class doc comment for why. Add a new outlet here
    /// (and to auth-api's seed) the next time a new vertical needs demoing; every resolution path
    /// already falls back to PrimaryOutletCode for anything not listed, so this is purely additive.
    /// </summary>
    private static readonly Dictionary<string, OutletOrgTarget> OutletOrgMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // The original generic outlet — maps to the pre-existing TRULOAD-DEMO organisation
        // (Data/Seeders/UserManagement/UserManagementSeeder.cs) kept as the primary/fallback demo
        // org. Never created here (IsPrimary short-circuits creation — see
        // EnsureOutletOrganizationsAsync), only backfilled/repaired if it already exists.
        [PrimaryOutletCode] = new OutletOrgTarget("TRULOAD-DEMO", "TruLoad Demo Weighbridge", "DEMO-WB-01", Vertical: null, IsPrimary: true, TenantType: TenantModules.TenantTypeCommercialWeighing, PaymentGateway: "treasury"),
        ["QUARRY"] = new OutletOrgTarget("TRULOAD-DEMO-QUARRY", "Demo Quarry & Mining Weighbridge", "QUARRY-WB-01", CommercialVerticals.Quarry, IsPrimary: false, TenantType: TenantModules.TenantTypeCommercialWeighing, PaymentGateway: "treasury"),
        ["WASTE"] = new OutletOrgTarget("TRULOAD-DEMO-WASTE", "Demo Waste Management Weighbridge", "WASTE-WB-01", CommercialVerticals.WasteManagement, IsPrimary: false, TenantType: TenantModules.TenantTypeCommercialWeighing, PaymentGateway: "treasury"),
        // Axle-load enforcement outlet — auth-api's outlet name reused verbatim as the org name,
        // same convention as QUARRY/WASTE above. No vertical (CommercialVerticals doesn't apply to
        // enforcement); TenantType=AxleLoadEnforcement so module/report resolution matches every
        // other enforcement org (KURA/KENHA/KERRA); PaymentGateway left null so the Organization
        // model's own default ("ecitizen_pesaflow") applies, matching how real enforcement orgs are
        // configured, rather than the commercial outlets' "treasury" value.
        ["ENF"] = new OutletOrgTarget("TRULOAD-DEMO-ENF", "Demo Axle Load Enforcement Hub", "ENF-WB-01", Vertical: null, IsPrimary: false, TenantType: TenantModules.TenantTypeAxleLoadEnforcement, PaymentGateway: null),
    };

    // auth-api role string -> local TruLoad ApplicationRole.Name (Data/Seeders/RoleSeeder.cs).
    // Only these are demo-relevant for weighing — see truloadDemoStaff in
    // auth-service/auth-api/cmd/seed/seed_users.go. Values confirmed verbatim from that file and
    // from UserSeeder.cs's own FindByNameAsync/AddToRoleAsync calls, not guessed. Every key is
    // deliberately prefixed/specific (never bare "operator"/"manager"/"auditor") to avoid the
    // ambiguous-generic-name trap this file's own doc comments warn about.
    private static readonly Dictionary<string, string> RoleMap = new()
    {
        // Commercial weighing — demo-commercial/demo-quarry/demo-waste outlets.
        ["commercial_weighing_operator"] = "Commercial Weighing Operator",
        ["commercial_finance"] = "Commercial Finance",
        ["commercial_weighing_manager"] = "Commercial Weighing Manager",
        ["commercial_weighing_supervisor"] = "Commercial Supervisor",
        ["commercial_weighing_auditor"] = "Commercial Auditor",
        // Axle-load enforcement — demo-enforcement (ENF) outlet. Deliberately only these 2 of the
        // 5 enforcement roles: Weighing Operator can run a full enforcement weighing session
        // end-to-end (initiate/capture/result), Station Manager covers the supervisory demo path —
        // exhaustive enforcement-role coverage isn't needed for this initiative.
        ["enforcement_weighing_operator"] = "Weighing Operator",
        ["enforcement_station_manager"] = "Station Manager",
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

        // Ensure every mapped outlet's Organization/Station pair exists BEFORE consuming any
        // message — so the very first event for a Quarry/Waste persona never races an org that
        // isn't there yet. Also self-heals (backfills SsoTenantSlug/vertical metadata) on every
        // restart, matching the idempotent-reseed convention used everywhere else in this codebase.
        try
        {
            await EnsureOutletOrganizationsAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure outlet organizations — continuing anyway, per-event sync will retry lookups");
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

    /// <summary>
    /// Get-or-creates the Organization + Station pair for every entry in <see cref="OutletOrgMap"/>.
    /// The primary entry (TRULOAD-DEMO) is never created here — it's UserManagementSeeder.cs's job
    /// — only backfilled if found with a stale/missing SsoTenantSlug. Every other entry is created
    /// on first sight with its own <see cref="OutletOrgTarget.TenantType"/> (CommercialWeighing for
    /// QUARRY/WASTE, AxleLoadEnforcement for ENF — read per-target, not assumed uniform), vertical
    /// metadata via OrganizationMetadataHelper.MergeVertical when the target has one, and
    /// SsoTenantSlug="codevertex-demo" (the same slug TRULOAD-DEMO already carries — see
    /// AuthController.SsoExchange's per-user disambiguation for why multiple organisations safely
    /// sharing one slug is fine).
    /// </summary>
    private async Task EnsureOutletOrganizationsAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TruLoadDbContext>();

        foreach (var (outletCode, target) in OutletOrgMap)
        {
            var org = await db.Organizations.IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Code == target.OrgCode, ct);

            if (org is null)
            {
                if (target.IsPrimary)
                {
                    _logger.LogWarning(
                        "Primary organization {OrgCode} not found — UserManagementSeeder should have created it; outlet sync for {OutletCode} cannot proceed until it exists",
                        target.OrgCode, outletCode);
                    continue;
                }

                org = new Organization
                {
                    Id = Guid.NewGuid(),
                    Code = target.OrgCode,
                    Name = target.OrgName,
                    OrgType = "Private",
                    TenantType = target.TenantType,
                    SsoTenantSlug = DemoTenantSlug,
                    IsDemo = true,
                    IsActive = true,
                    MetadataJson = target.Vertical is not null
                        ? OrganizationMetadataHelper.MergeVertical(null, target.Vertical)
                        : null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                // Leave PaymentGateway at the Organization model's own default
                // ("ecitizen_pesaflow") when the target doesn't specify one (ENF) rather than
                // forcing every outlet org onto the commercial "treasury" value.
                if (target.PaymentGateway is not null)
                    org.PaymentGateway = target.PaymentGateway;
                db.Organizations.Add(org);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Created outlet organization {OrgCode} ({Vertical}) for codevertex-demo outlet {OutletCode}",
                    target.OrgCode, target.Vertical ?? "unclassified", outletCode);
            }
            else
            {
                var updated = false;
                if (string.IsNullOrEmpty(org.SsoTenantSlug))
                {
                    org.SsoTenantSlug = DemoTenantSlug;
                    updated = true;
                }
                if (target.Vertical is not null)
                {
                    var currentVertical = OrganizationMetadataHelper.GetVertical(org.MetadataJson);
                    if (!string.Equals(currentVertical, target.Vertical, StringComparison.Ordinal))
                    {
                        org.MetadataJson = OrganizationMetadataHelper.MergeVertical(org.MetadataJson, target.Vertical);
                        updated = true;
                    }
                }
                if (updated)
                {
                    org.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Backfilled outlet organization {OrgCode} (SsoTenantSlug/vertical)", target.OrgCode);
                }
            }

            if (target.IsPrimary)
                continue; // Station (DEMO-WB-01) is UserManagementSeeder.cs's responsibility too.

            var station = await db.Stations.IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Code == target.StationCode, ct);
            if (station is null)
            {
                db.Stations.Add(new Station
                {
                    Id = Guid.NewGuid(),
                    Code = target.StationCode,
                    Name = $"{target.OrgName} Station 01",
                    StationType = "weigh_bridge",
                    OrganizationId = org.Id,
                    IsDefault = true,
                    IsHq = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Created station {StationCode} for organization {OrgCode}", target.StationCode, target.OrgCode);
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
        string? outletCode = null;
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
                // Human-readable outlet code (e.g. "QUARRY") — seed_users.go's
                // seedTruLoadDemoStaff adds this specifically for outlet routing here. Absent for
                // personas seeded before outlet scoping existed / any event that omits it, which
                // HandleUpsertAsync treats as PrimaryOutletCode (today's existing single-org
                // behaviour, unchanged).
                if (p.TryGetProperty("outlet_code", out var oc) && oc.ValueKind == JsonValueKind.String)
                    outletCode = oc.GetString();
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
                await HandleUpsertAsync(authUserId, email, fullName ?? email, roles, outletCode, ct);
                break;
            case "deleted":
                await HandleDeleteAsync(email, ct);
                break;
            default:
                // pin_set and any other future auth.user.* subtype is irrelevant to this sync.
                return;
        }
    }

    private async Task HandleUpsertAsync(string? authUserId, string email, string fullName, List<string> roles, string? outletCode, CancellationToken ct)
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

        var target = (outletCode is not null && OutletOrgMap.TryGetValue(outletCode, out var mappedTarget))
            ? mappedTarget
            : OutletOrgMap[PrimaryOutletCode];

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TruLoadDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var org = await db.Organizations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Code == target.OrgCode, ct);
        if (org is null)
        {
            _logger.LogWarning("Organization {OrgCode} not found — cannot sync {Email} (outlet_code={OutletCode})", target.OrgCode, email, outletCode);
            return;
        }

        var station = await db.Stations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Code == target.StationCode, ct);

        // Match the SAME row this service already created/repaired for this exact email —
        // find-or-update, never a second conflicting record.
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
            _logger.LogInformation("Created demo user {Email} in organization {OrgCode} from auth.user.{EventType} event", email, target.OrgCode, "created/updated");
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

    /// <summary>
    /// One codevertex-demo TruLoad outlet's sync target — see <see cref="OutletOrgMap"/>.
    /// TenantType and PaymentGateway are per-target (not assumed CommercialWeighing/"treasury" for
    /// every non-primary entry) specifically so the ENF outlet can be AxleLoadEnforcement with the
    /// model's own default payment gateway instead of silently inheriting commercial-outlet values.
    /// </summary>
    private sealed record OutletOrgTarget(string OrgCode, string OrgName, string StationCode, string? Vertical, bool IsPrimary, string TenantType, string? PaymentGateway = null);
}
