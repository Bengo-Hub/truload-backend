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
    /// Reports a metered usage event (e.g. one weighing transaction) to the subscriptions-api.
    /// Fire-and-forget safe — failures are logged but do not block the caller.
    /// </summary>
    Task ReportUsageAsync(string ssoTenantSlug, string metricType, int qty, object? metadata = null, CancellationToken ct = default);
}
