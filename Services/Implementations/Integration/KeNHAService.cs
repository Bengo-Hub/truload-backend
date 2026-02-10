using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using StackExchange.Redis;
using TruLoad.Backend.DTOs.Integration;
using TruLoad.Backend.Services.Interfaces.Integration;
using TruLoad.Backend.Services.Interfaces.System;

namespace TruLoad.Backend.Services.Implementations.Integration;

/// <summary>
/// KeNHA (Kenya National Highways Authority) vehicle tag verification service.
/// Checks if a vehicle has an existing KeNHA tag/prohibition.
/// Follows the same integration pattern as ECitizenService:
///   - Credentials managed via IntegrationConfigService (AES-256-GCM encrypted)
///   - Results cached in Redis (1 hour TTL per vehicle)
///   - Graceful degradation when API is unavailable
///
/// Based on KenLoad V2 tag verification patterns adapted for TruLoad architecture.
/// </summary>
public class KeNHAService : IKeNHAService
{
    private const string ProviderName = "kenha";
    private const string RedisCachePrefix = "kenha:tag:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private readonly HttpClient _httpClient;
    private readonly IIntegrationConfigService _integrationConfigService;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<KeNHAService> _logger;

    public KeNHAService(
        HttpClient httpClient,
        IIntegrationConfigService integrationConfigService,
        IConnectionMultiplexer redis,
        ILogger<KeNHAService> logger)
    {
        _httpClient = httpClient;
        _integrationConfigService = integrationConfigService;
        _redis = redis;
        _logger = logger;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var config = await _integrationConfigService.GetByProviderAsync(ProviderName, ct);
            return config is { IsActive: true } && !string.IsNullOrWhiteSpace(config.BaseUrl);
        }
        catch
        {
            return false;
        }
    }

    public async Task<KeNHATagVerificationResult?> VerifyVehicleTagAsync(string regNo, CancellationToken ct = default)
    {
        var normalizedRegNo = regNo.Trim().ToUpper().Replace(" ", "");

        // Check Redis cache first
        try
        {
            var db = _redis.GetDatabase();
            var cached = await db.StringGetAsync($"{RedisCachePrefix}{normalizedRegNo}");
            if (cached.HasValue)
            {
                _logger.LogDebug("KeNHA tag cache hit for {RegNo}", normalizedRegNo);
                return JsonSerializer.Deserialize<KeNHATagVerificationResult>(cached.ToString());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache read failed for KeNHA tag check, proceeding to API");
        }

        // Fetch from KeNHA API
        try
        {
            var credentials = await _integrationConfigService.GetDecryptedCredentialsAsync(ProviderName, ct);
            var config = await _integrationConfigService.GetByProviderAsync(ProviderName, ct)
                ?? throw new InvalidOperationException("KeNHA integration config not found");

            var apiKey = credentials.GetValueOrDefault("ApiKey")
                ?? throw new InvalidOperationException("ApiKey not found in KeNHA credentials");

            // Parse endpoints from config
            var endpoints = !string.IsNullOrEmpty(config.EndpointsJson)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(config.EndpointsJson)
                : null;

            var verifyTagEndpoint = endpoints?.GetValueOrDefault("VerifyTag") ?? "/api/v3/vehicle/tag/verify";

            // Build request - KeNHA API uses Bearer token + query param
            var url = $"{config.BaseUrl.TrimEnd('/')}{verifyTagEndpoint}";
            if (verifyTagEndpoint.Contains("{reg_no}"))
            {
                url = url.Replace("{reg_no}", Uri.EscapeDataString(normalizedRegNo));
            }
            else if (verifyTagEndpoint.Contains("{api_key}"))
            {
                url = url.Replace("{api_key}", Uri.EscapeDataString(apiKey))
                         .Replace("{reg_no}", Uri.EscapeDataString(normalizedRegNo));
            }
            else
            {
                url += $"?reg_no={Uri.EscapeDataString(normalizedRegNo)}";
            }

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            _logger.LogInformation("KeNHA tag verify for {RegNo}: {StatusCode} ({BodyLength} chars)",
                normalizedRegNo, response.StatusCode, responseBody.Length);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("KeNHA API returned {StatusCode} for {RegNo}: {Body}",
                    response.StatusCode, normalizedRegNo, responseBody);

                return new KeNHATagVerificationResult
                {
                    HasTag = false,
                    RegNo = normalizedRegNo,
                    RawResponse = responseBody
                };
            }

            var result = ParseTagVerificationResponse(normalizedRegNo, responseBody);

            // Cache result in Redis
            try
            {
                var db = _redis.GetDatabase();
                var json = JsonSerializer.Serialize(result);
                await db.StringSetAsync($"{RedisCachePrefix}{normalizedRegNo}", json, CacheTtl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache KeNHA tag result for {RegNo}", normalizedRegNo);
            }

            return result;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            _logger.LogWarning("KeNHA integration not configured: {Message}", ex.Message);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "KeNHA API request failed for {RegNo}", normalizedRegNo);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "KeNHA API request timed out for {RegNo}", normalizedRegNo);
            return null;
        }
    }

    public async Task<IntegrationHealthResult> TestConnectivityAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var config = await _integrationConfigService.GetByProviderAsync(ProviderName, ct);
            if (config == null || !config.IsActive)
            {
                return new IntegrationHealthResult
                {
                    IsHealthy = false,
                    ProviderName = ProviderName,
                    Message = config == null ? "Integration not configured" : "Integration is disabled",
                    ResponseTimeMs = (int)sw.ElapsedMilliseconds
                };
            }

            // Verify credentials can be decrypted
            var credentials = await _integrationConfigService.GetDecryptedCredentialsAsync(ProviderName, ct);
            var hasApiKey = credentials.ContainsKey("ApiKey") && !string.IsNullOrWhiteSpace(credentials["ApiKey"]);

            sw.Stop();
            return new IntegrationHealthResult
            {
                IsHealthy = hasApiKey,
                ProviderName = ProviderName,
                Message = hasApiKey
                    ? $"Configuration valid, BaseUrl={config.BaseUrl}"
                    : "ApiKey is missing or empty in credentials",
                ResponseTimeMs = (int)sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new IntegrationHealthResult
            {
                IsHealthy = false,
                ProviderName = ProviderName,
                Message = $"Health check failed: {ex.Message}",
                ResponseTimeMs = (int)sw.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Parse KeNHA tag verification response.
    /// Handles both KenLoad V2 style responses and standard JSON formats.
    /// </summary>
    private KeNHATagVerificationResult ParseTagVerificationResponse(string regNo, string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            // Handle array response (KenLoad V2 returns array of tags)
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var firstTag = root[0];
                return new KeNHATagVerificationResult
                {
                    HasTag = true,
                    RegNo = regNo,
                    TagStatus = GetJsonString(firstTag, "status") ?? "open",
                    TagCategory = GetJsonString(firstTag, "type") ?? GetJsonString(firstTag, "category"),
                    Reason = GetJsonString(firstTag, "reason"),
                    Station = GetJsonString(firstTag, "station"),
                    TagDate = GetJsonDateTime(firstTag, "datetime") ?? GetJsonDateTime(firstTag, "created_at"),
                    TagUid = GetJsonString(firstTag, "taguid") ?? GetJsonString(firstTag, "tag_uid"),
                    RawResponse = responseBody
                };
            }

            // Handle object response with tag data
            if (root.ValueKind == JsonValueKind.Object)
            {
                var hasTag = root.TryGetProperty("has_tag", out var ht) && ht.GetBoolean()
                          || root.TryGetProperty("found", out var f) && f.GetBoolean()
                          || root.TryGetProperty("status", out var s) && s.GetString()?.ToLower() == "open";

                return new KeNHATagVerificationResult
                {
                    HasTag = hasTag,
                    RegNo = regNo,
                    TagStatus = GetJsonString(root, "tag_status") ?? GetJsonString(root, "status"),
                    TagCategory = GetJsonString(root, "tag_category") ?? GetJsonString(root, "type"),
                    Reason = GetJsonString(root, "reason"),
                    Station = GetJsonString(root, "station"),
                    TagDate = GetJsonDateTime(root, "tag_date") ?? GetJsonDateTime(root, "datetime"),
                    TagUid = GetJsonString(root, "tag_uid") ?? GetJsonString(root, "taguid"),
                    RawResponse = responseBody
                };
            }

            // Empty array or no data = no tag
            return new KeNHATagVerificationResult
            {
                HasTag = false,
                RegNo = regNo,
                RawResponse = responseBody
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse KeNHA response for {RegNo}", regNo);
            return new KeNHATagVerificationResult
            {
                HasTag = false,
                RegNo = regNo,
                RawResponse = responseBody
            };
        }
    }

    private static string? GetJsonString(JsonElement el, string prop)
    {
        return el.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString()
            : null;
    }

    private static DateTime? GetJsonDateTime(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return null;
        if (val.ValueKind == JsonValueKind.String && DateTime.TryParse(val.GetString(), out var dt))
            return dt;
        return null;
    }
}
