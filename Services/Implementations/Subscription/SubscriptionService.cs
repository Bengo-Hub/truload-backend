using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TruLoad.Backend.Services.Interfaces.Subscription;

namespace TruLoad.Backend.Services.Implementations.Subscription;

/// <summary>
/// HTTP client for the subscriptions-api.
/// Auth: X-Tenant-Slug header identifies the tenant; service JWT in Authorization header.
/// </summary>
public class SubscriptionService : ISubscriptionService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(HttpClient httpClient, IConfiguration configuration, ILogger<SubscriptionService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Resolves a tenant's id/subscription-plan/status/expiry via auth-api's PUBLIC (no auth
    /// required) tenant-by-slug endpoint. auth-api projects subscription status onto its own
    /// tenant record (synced from subscriptions-api), so this single cheap call covers
    /// GetTenantSubscriptionAsync entirely, and supplies the tenant id GetFeaturesAsync needs to
    /// then call subscriptions-api directly for feature codes. Returns null on any failure/missing
    /// config - callers decide their own fail-open behavior.
    /// </summary>
    private async Task<JsonElement?> ResolvePublicTenantAsync(string ssoTenantSlug, CancellationToken ct)
    {
        var authBaseUrl = _configuration["Auth:BaseUrl"];
        if (string.IsNullOrWhiteSpace(authBaseUrl))
            authBaseUrl = _configuration["Auth:SsoIssuer"]; // public HTTPS fallback for local dev

        if (string.IsNullOrWhiteSpace(authBaseUrl))
            return null;

        try
        {
            var response = await _httpClient.GetAsync(
                $"{authBaseUrl}/api/v1/tenants/by-slug/{Uri.EscapeDataString(ssoTenantSlug)}", ct);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve tenant {Slug} via auth-api", ssoTenantSlug);
            return null;
        }
    }

    public async Task<SubscriptionStatus> GetTenantSubscriptionAsync(string ssoTenantSlug, CancellationToken ct = default)
    {
        var tenant = await ResolvePublicTenantAsync(ssoTenantSlug, ct);
        if (tenant == null)
        {
            // Fail open: a misconfigured/unreachable auth-api must never block commercial weighing.
            _logger.LogWarning("Could not resolve tenant {Slug} via auth-api — treating as ACTIVE", ssoTenantSlug);
            return new SubscriptionStatus("ACTIVE", null, null);
        }

        var root = tenant.Value;
        var status = root.TryGetProperty("subscription_status", out var s) && s.ValueKind == JsonValueKind.String
            ? s.GetString() ?? "NONE"
            : "NONE";
        DateTime? expiresAt = null;
        if (root.TryGetProperty("subscription_expires_at", out var exp) && exp.ValueKind == JsonValueKind.String
            && DateTime.TryParse(exp.GetString(), out var dt))
        {
            expiresAt = dt;
        }
        var planName = root.TryGetProperty("subscription_plan", out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

        return new SubscriptionStatus(status, expiresAt, planName);
    }

    public async Task<SubscriptionFeatures> GetFeaturesAsync(string ssoTenantSlug, CancellationToken ct = default)
    {
        var tenant = await ResolvePublicTenantAsync(ssoTenantSlug, ct);
        if (tenant == null || !tenant.Value.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.String)
        {
            _logger.LogWarning("Could not resolve tenant {Slug} — returning basic feature access", ssoTenantSlug);
            return new SubscriptionFeatures("ACTIVE", null, ["portal_access", "ticket_download", "email_notifications"]);
        }

        var subscriptionsBaseUrl = _configuration["SUBSCRIPTION_BASE_URL"];
        var internalServiceKey = _configuration["INTERNAL_SERVICE_KEY"];
        if (string.IsNullOrWhiteSpace(subscriptionsBaseUrl) || string.IsNullOrWhiteSpace(internalServiceKey))
        {
            _logger.LogWarning("Subscriptions API/INTERNAL_SERVICE_KEY not configured — returning basic feature access for {Slug}", ssoTenantSlug);
            return new SubscriptionFeatures("ACTIVE", null, ["portal_access", "ticket_download", "email_notifications"]);
        }

        try
        {
            var tenantId = idEl.GetString();
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{subscriptionsBaseUrl}/api/v1/tenants/{tenantId}/subscription?include_usage=false");
            request.Headers.Add("X-API-Key", internalServiceKey);

            var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new SubscriptionFeatures("NONE", null, []);

            var json = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Subscriptions features API returned {Status} for {Slug}: {Body}",
                    response.StatusCode, ssoTenantSlug, json);
                return new SubscriptionFeatures("ACTIVE", null, ["portal_access", "ticket_download", "email_notifications"]);
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // subscriptions-api's tenant-subscription response is snake_case (a different casing
            // convention than the plans-module JSON, verified against subscriptions-api's own
            // internal/clients response shape used by auth-api's proven-working caller).
            var status = root.TryGetProperty("status", out var s) ? s.GetString() ?? "NONE" : "NONE";
            var planCode = root.TryGetProperty("plan_code", out var pc) ? pc.GetString() : null;

            var featureCodes = new List<string>();
            if (root.TryGetProperty("features", out var featuresEl) && featuresEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var f in featuresEl.EnumerateArray())
                {
                    var code = f.ValueKind == JsonValueKind.String
                        ? f.GetString()
                        : f.TryGetProperty("feature_code", out var fc) ? fc.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(code))
                        featureCodes.Add(code!);
                }
            }

            return new SubscriptionFeatures(status, planCode, featureCodes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetFeaturesAsync failed for {Slug} — failing open", ssoTenantSlug);
            return new SubscriptionFeatures("ACTIVE", null, ["portal_access", "ticket_download", "email_notifications"]);
        }
    }

    public async Task<string> GetPlansJsonAsync(CancellationToken ct = default)
    {
        var baseUrl = _configuration["SUBSCRIPTION_BASE_URL"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            return "[]";

        try
        {
            var response = await _httpClient.GetAsync($"{baseUrl}/api/v1/plans?service=truload&active=true", ct);
            if (!response.IsSuccessStatusCode)
                return "[]";
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPlansJsonAsync failed");
            return "[]";
        }
    }

    public async Task<string> GetBillingJsonAsync(string ssoTenantSlug, CancellationToken ct = default)
    {
        var tenant = await ResolvePublicTenantAsync(ssoTenantSlug, ct);
        if (tenant == null || !tenant.Value.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.String)
            return "{}";

        var subscriptionsBaseUrl = _configuration["SUBSCRIPTION_BASE_URL"];
        var internalServiceKey = _configuration["INTERNAL_SERVICE_KEY"];
        if (string.IsNullOrWhiteSpace(subscriptionsBaseUrl) || string.IsNullOrWhiteSpace(internalServiceKey))
            return "{}";

        try
        {
            // GetBilling resolves its tenant via resolveTenantID(r): an X-API-Key caller is treated
            // as a platform identity, for which the X-Tenant-ID header is the (optional-for-platform,
            // but here always supplied) tenant selector - same mechanism as the tenant-subscription
            // S2S route, just via a header instead of a path segment.
            var request = new HttpRequestMessage(HttpMethod.Get, $"{subscriptionsBaseUrl}/api/v1/billing");
            request.Headers.Add("X-API-Key", internalServiceKey);
            request.Headers.Add("X-Tenant-ID", idEl.GetString());
            var response = await _httpClient.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return "{}";
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetBillingJsonAsync failed for {Slug}", ssoTenantSlug);
            return "{}";
        }
    }

    public async Task<string> GetSubscriptionJsonAsync(string ssoTenantSlug, CancellationToken ct = default)
    {
        var tenant = await ResolvePublicTenantAsync(ssoTenantSlug, ct);
        if (tenant == null || !tenant.Value.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.String)
            return "{}";

        var subscriptionsBaseUrl = _configuration["SUBSCRIPTION_BASE_URL"];
        var internalServiceKey = _configuration["INTERNAL_SERVICE_KEY"];
        if (string.IsNullOrWhiteSpace(subscriptionsBaseUrl) || string.IsNullOrWhiteSpace(internalServiceKey))
            return "{}";

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{subscriptionsBaseUrl}/api/v1/tenants/{idEl.GetString()}/subscription?include_usage=false");
            request.Headers.Add("X-API-Key", internalServiceKey);
            var response = await _httpClient.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return "{}";
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetSubscriptionJsonAsync failed for {Slug}", ssoTenantSlug);
            return "{}";
        }
    }

    public async Task<string> ChangePlanJsonAsync(string userJwt, string planCode, CancellationToken ct = default)
    {
        var baseUrl = _configuration["SUBSCRIPTION_BASE_URL"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("Subscriptions API is not configured");

        var body = new { plan_code = planCode };
        var request = new HttpRequestMessage(HttpMethod.Put, $"{baseUrl}/api/v1/subscription/plan")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userJwt);

        var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Plan change failed: {json}");

        return json;
    }

    public async Task ReportUsageAsync(string ssoTenantSlug, string metricType, int qty, object? metadata = null, CancellationToken ct = default)
    {
        try
        {
            var baseUrl = _configuration["SUBSCRIPTION_BASE_URL"];
            var serviceJwt = _configuration["Subscriptions:ServiceJwt"];
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(serviceJwt))
            {
                _logger.LogWarning("Subscriptions API not configured — skipping usage report for {Slug}", ssoTenantSlug);
                return;
            }

            var body = new
            {
                tenant_slug = ssoTenantSlug,
                metric_type = metricType,
                quantity = qty,
                metadata
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/v1/usage")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceJwt);

            var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var resp = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Usage report failed ({Status}) for tenant {Slug}: {Body}",
                    response.StatusCode, ssoTenantSlug, resp);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Usage report exception for tenant {Slug}", ssoTenantSlug);
            // Swallow — usage reporting must never block weighing operations
        }
    }
}
