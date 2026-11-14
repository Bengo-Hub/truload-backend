using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using TruLoad.Backend.Services.Interfaces.Integration;

namespace TruLoad.Backend.Services.Implementations.Integration;

/// <summary>
/// HTTP client for marketflow-api's internal contact upsert.
/// Auth: X-API-Key header with the platform INTERNAL_SERVICE_KEY.
/// marketflow keys contacts by the auth-api tenant UUID, so the tenant slug is first resolved
/// through auth-api's public by-slug endpoint (the same lookup SubscriptionService uses).
/// </summary>
public class CrmService : ICrmService
{
    public const string DefaultMarketflowApiUrl = "http://marketflow-api.marketflow.svc.cluster.local:4000";

    private static readonly TimeSpan TenantIdCacheTtl = TimeSpan.FromHours(1);

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CrmService> _logger;

    public CrmService(
        HttpClient httpClient,
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<CrmService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
    }

    public async Task<CrmContactResult> ResolveCustomerAsync(
        string tenantSlug,
        string name,
        string? email,
        string? phone,
        CancellationToken ct = default)
    {
        email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        if (email == null && phone == null)
            throw new ArgumentException("An email or phone is required to file a CRM contact");

        var apiKey = _configuration["INTERNAL_SERVICE_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("INTERNAL_SERVICE_KEY not configured");

        var baseUrl = _configuration["MARKETFLOW_API_URL"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = DefaultMarketflowApiUrl;

        var tenantId = await ResolveTenantIdAsync(tenantSlug, ct);
        var (firstName, lastName) = SplitName(name);

        var body = new UpsertContactRequest
        {
            TenantId = tenantId.ToString(),
            Phone = phone,
            Email = email,
            FirstName = string.IsNullOrEmpty(firstName) ? null : firstName,
            LastName = string.IsNullOrEmpty(lastName) ? null : lastName
        };

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{baseUrl.TrimEnd('/')}/api/v1/internal/contacts/upsert")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, _jsonOptions),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("X-API-Key", apiKey);

        var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            // Only the status and marketflow's error code: the message or details could echo the
            // email or phone back.
            var code = ReadErrorCode(json);
            _logger.LogError("MarketFlow contact upsert failed ({Status}, code {Code})", response.StatusCode, code);
            throw new HttpRequestException($"MarketFlow API error {response.StatusCode} ({code})");
        }

        var result = JsonSerializer.Deserialize<UpsertContactResponse>(json, _jsonOptions);
        if (result == null || string.IsNullOrWhiteSpace(result.Id))
            throw new InvalidOperationException("MarketFlow returned no contact id");

        return new CrmContactResult(result.Id);
    }

    /// <summary>
    /// Splits a customer name into first and last name the way treasury's crm.SplitName does:
    /// the first word, then the rest joined by single spaces.
    /// </summary>
    public static (string FirstName, string LastName) SplitName(string? name)
    {
        var parts = (name ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => (string.Empty, string.Empty),
            1 => (parts[0], string.Empty),
            _ => (parts[0], string.Join(' ', parts[1..]))
        };
    }

    // marketflow errors look like {"error": "...", "code": "missing_identifier"}.
    private static string ReadErrorCode(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("code", out var codeEl)
                && codeEl.ValueKind == JsonValueKind.String)
            {
                return codeEl.GetString() ?? "unknown";
            }
        }
        catch (JsonException)
        {
            // Not JSON, for example a proxy error page.
        }
        return "unknown";
    }

    // Resolves the auth-api tenant UUID for a slug. Several TruLoad organisations can share one
    // SsoTenantSlug, so Organization.Id is not the auth tenant id and cannot be used here.
    private async Task<Guid> ResolveTenantIdAsync(string tenantSlug, CancellationToken ct)
    {
        var cacheKey = $"crm:auth-tenant-id:{tenantSlug}";
        if (_cache.TryGetValue(cacheKey, out Guid cached))
            return cached;

        var authBaseUrl = _configuration["Auth:BaseUrl"];
        if (string.IsNullOrWhiteSpace(authBaseUrl))
            authBaseUrl = _configuration["Auth:SsoIssuer"]; // public HTTPS fallback for local dev
        if (string.IsNullOrWhiteSpace(authBaseUrl))
            throw new InvalidOperationException("Auth:BaseUrl not configured; cannot resolve the tenant id for the CRM");

        var response = await _httpClient.GetAsync(
            $"{authBaseUrl.TrimEnd('/')}/api/v1/tenants/by-slug/{Uri.EscapeDataString(tenantSlug)}", ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"auth-api tenant lookup for {tenantSlug} failed ({response.StatusCode})");

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("id", out var idEl)
            || idEl.ValueKind != JsonValueKind.String
            || !Guid.TryParse(idEl.GetString(), out var tenantId)
            || tenantId == Guid.Empty)
        {
            throw new InvalidOperationException($"auth-api returned no tenant id for {tenantSlug}");
        }

        _cache.Set(cacheKey, tenantId, TenantIdCacheTtl);
        return tenantId;
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed class UpsertContactRequest
    {
        [JsonPropertyName("tenant_id")]
        public string TenantId { get; set; } = string.Empty;

        [JsonPropertyName("phone")]
        public string? Phone { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }
    }

    private sealed class UpsertContactResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}
