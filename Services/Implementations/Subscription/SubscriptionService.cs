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

    public async Task<SubscriptionStatus> GetTenantSubscriptionAsync(string ssoTenantSlug, CancellationToken ct = default)
    {
        var baseUrl = _configuration["Subscriptions:ApiUrl"]
            ?? throw new InvalidOperationException("Subscriptions:ApiUrl is not configured");
        var serviceJwt = _configuration["Subscriptions:ServiceJwt"]
            ?? throw new InvalidOperationException("Subscriptions:ServiceJwt is not configured");

        var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/v1/subscription/");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceJwt);
        request.Headers.Add("X-Tenant-Slug", ssoTenantSlug);

        var response = await _httpClient.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new SubscriptionStatus("NONE", null, null);

        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Subscriptions API returned {Status} for tenant {Slug}: {Body}",
                response.StatusCode, ssoTenantSlug, json);
            // Fail open: return ACTIVE to not block user if subscriptions-api is degraded
            return new SubscriptionStatus("ACTIVE", null, null);
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var status = root.TryGetProperty("status", out var s) ? s.GetString() ?? "NONE" : "NONE";
        DateTime? expiresAt = null;
        if (root.TryGetProperty("expires_at", out var exp) && exp.ValueKind != JsonValueKind.Null)
        {
            if (DateTime.TryParse(exp.GetString(), out var dt))
                expiresAt = dt;
        }
        var planName = root.TryGetProperty("plan_name", out var p) ? p.GetString() : null;

        return new SubscriptionStatus(status, expiresAt, planName);
    }

    public async Task<SubscriptionFeatures> GetFeaturesAsync(string ssoTenantSlug, CancellationToken ct = default)
    {
        var baseUrl = _configuration["Subscriptions:ApiUrl"];
        var serviceJwt = _configuration["Subscriptions:ServiceJwt"];

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(serviceJwt))
        {
            _logger.LogWarning("Subscriptions API not configured — returning empty features for {Slug}", ssoTenantSlug);
            return new SubscriptionFeatures("UNKNOWN", null, []);
        }

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/v1/features");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceJwt);
            request.Headers.Add("X-Tenant-Slug", ssoTenantSlug);

            var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new SubscriptionFeatures("NONE", null, []);

            var json = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Subscriptions features API returned {Status} for {Slug}: {Body}",
                    response.StatusCode, ssoTenantSlug, json);
                // Fail open with basic access
                return new SubscriptionFeatures("ACTIVE", null, ["portal_access", "ticket_download", "email_notifications"]);
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

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
        var baseUrl = _configuration["Subscriptions:ApiUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            return "[]";

        try
        {
            var response = await _httpClient.GetAsync($"{baseUrl}/api/v1/plans?active=true", ct);
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

    public async Task<string> GetBillingJsonAsync(string userJwt, CancellationToken ct = default)
    {
        var baseUrl = _configuration["Subscriptions:ApiUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            return "{}";

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/v1/billing");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userJwt);
            var response = await _httpClient.SendAsync(request, ct);
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetBillingJsonAsync failed");
            return "{}";
        }
    }

    public async Task<string> GetSubscriptionJsonAsync(string userJwt, CancellationToken ct = default)
    {
        var baseUrl = _configuration["Subscriptions:ApiUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            return "{}";

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/v1/subscription/");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userJwt);
            var response = await _httpClient.SendAsync(request, ct);
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetSubscriptionJsonAsync failed");
            return "{}";
        }
    }

    public async Task<string> ChangePlanJsonAsync(string userJwt, string planCode, CancellationToken ct = default)
    {
        var baseUrl = _configuration["Subscriptions:ApiUrl"];
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
            var baseUrl = _configuration["Subscriptions:ApiUrl"];
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
