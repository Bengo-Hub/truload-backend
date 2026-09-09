namespace TruLoad.Backend.Services.Interfaces.Subscription;

/// <summary>
/// Subscription status returned from subscriptions-api.
/// </summary>
public record SubscriptionStatus(
    string Status,       // "ACTIVE" | "TRIAL" | "EXPIRED" | "CANCELLED" | "NONE"
    DateTime? ExpiresAt,
    string? PlanName
);

/// <summary>
/// Feature entitlements returned from subscriptions-api GET /features.
/// </summary>
public record SubscriptionFeatures(
    string Status,
    string? PlanCode,
    IReadOnlyList<string> FeatureCodes
)
{
    public bool Has(string featureCode) =>
        FeatureCodes.Contains(featureCode, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Client for the subscriptions-api.
/// Only used for CommercialWeighing tenants — enforcement tenants have no subscription check.
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Returns the current subscription status for a commercial tenant.
    /// </summary>
    Task<SubscriptionStatus> GetTenantSubscriptionAsync(string ssoTenantSlug, CancellationToken ct = default);

    /// <summary>
    /// Returns the full feature entitlement set for a tenant. Uses subscriptions-api
    /// GET /features, which is Redis-cached for 60 s on the subscriptions-api side.
    /// </summary>
    Task<SubscriptionFeatures> GetFeaturesAsync(string ssoTenantSlug, CancellationToken ct = default);

    /// <summary>
    /// Reports a metered usage event (e.g. one weighing transaction) to the subscriptions-api.
    /// Fire-and-forget safe — failures are logged but do not block the caller.
    /// </summary>
    Task ReportUsageAsync(string ssoTenantSlug, string metricType, int qty, object? metadata = null, CancellationToken ct = default);

    /// <summary>Lists all available subscription plans (public endpoint — no tenant auth).</summary>
    Task<string> GetPlansJsonAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets billing info (payment method, invoice history) for the tenant. Still forwards the
    /// user's SSO JWT to subscriptions-api - a known-broken path for truload specifically (see
    /// GetSubscriptionJsonAsync's doc comment for why), left as a follow-up since it's a lower-
    /// priority sub-feature than the plan catalog/current-plan display.
    /// </summary>
    Task<string> GetBillingJsonAsync(string userJwt, CancellationToken ct = default);

    /// <summary>
    /// Gets the current subscription for a commercial tenant via the same S2S path as
    /// GetTenantSubscriptionAsync/GetFeaturesAsync (tenant resolved by slug, X-API-Key auth) -
    /// NOT by forwarding the user's own JWT. truload-backend mints its own symmetric HS256 JWTs
    /// (see truload-subscription-uniform-integration.md), which subscriptions-api's JWKS-based
    /// validator can never verify, so forwarding it was never going to work regardless of
    /// SUBSCRIPTION_BASE_URL/credentials being configured.
    /// </summary>
    Task<string> GetSubscriptionJsonAsync(string ssoTenantSlug, CancellationToken ct = default);

    /// <summary>Changes the subscription plan. Forwards the user's SSO JWT.</summary>
    Task<string> ChangePlanJsonAsync(string userJwt, string planCode, CancellationToken ct = default);
}
