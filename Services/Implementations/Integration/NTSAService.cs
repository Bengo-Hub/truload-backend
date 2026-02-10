using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using StackExchange.Redis;
using TruLoad.Backend.DTOs.Integration;
using TruLoad.Backend.Services.Interfaces.Integration;
using TruLoad.Backend.Services.Interfaces.System;

namespace TruLoad.Backend.Services.Implementations.Integration;

/// <summary>
/// NTSA (National Transport and Safety Authority) vehicle search service.
/// Provides vehicle details lookup by registration number from NTSA database.
/// Follows KenLoad V2 integration pattern:
///   - POST to NTSA API with { "regno": "..." }
///   - Bearer token authentication
///   - Results cached in Redis (24 hour TTL)
///   - Search history tracked for audit trail
///
/// Based on KenLoad V2's NTSAVehicleSearchHistoryController pattern.
/// NTSA API endpoint: https://api.ntsa.go.ke/vsearch/sp/qregno
/// </summary>
public class NTSAService : INTSAService
{
    private const string ProviderName = "ntsa";
    private const string RedisCachePrefix = "ntsa:vehicle:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private readonly HttpClient _httpClient;
    private readonly IIntegrationConfigService _integrationConfigService;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<NTSAService> _logger;

    public NTSAService(
        HttpClient httpClient,
        IIntegrationConfigService integrationConfigService,
        IConnectionMultiplexer redis,
        ILogger<NTSAService> logger)
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

    public async Task<NTSAVehicleSearchResult?> SearchVehicleAsync(string regNo, CancellationToken ct = default)
    {
        var normalizedRegNo = regNo.Trim().ToUpper().Replace(" ", "");

        // Check Redis cache first
        try
        {
            var db = _redis.GetDatabase();
            var cached = await db.StringGetAsync($"{RedisCachePrefix}{normalizedRegNo}");
            if (cached.HasValue)
            {
                _logger.LogDebug("NTSA vehicle cache hit for {RegNo}", normalizedRegNo);
                return JsonSerializer.Deserialize<NTSAVehicleSearchResult>(cached.ToString());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache read failed for NTSA vehicle search, proceeding to API");
        }

        // Fetch from NTSA API
        try
        {
            var credentials = await _integrationConfigService.GetDecryptedCredentialsAsync(ProviderName, ct);
            var config = await _integrationConfigService.GetByProviderAsync(ProviderName, ct)
                ?? throw new InvalidOperationException("NTSA integration config not found");

            var apiKey = credentials.GetValueOrDefault("ApiKey")
                ?? throw new InvalidOperationException("ApiKey not found in NTSA credentials");

            // Parse endpoints from config
            var endpoints = !string.IsNullOrEmpty(config.EndpointsJson)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(config.EndpointsJson)
                : null;

            var searchEndpoint = endpoints?.GetValueOrDefault("VehicleSearch")
                ?? endpoints?.GetValueOrDefault("VehicleDetails")
                ?? "/vsearch/sp/qregno";

            // KenLoad V2 pattern: POST with JSON body { "regno": "..." }
            var url = $"{config.BaseUrl.TrimEnd('/')}{searchEndpoint}";

            // Handle URL template parameters (legacy config format)
            if (url.Contains("{reg_no}"))
            {
                url = url.Replace("{reg_no}", Uri.EscapeDataString(normalizedRegNo))
                         .Replace("{api_key}", Uri.EscapeDataString(apiKey));

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                return await ExecuteNTSARequest(request, normalizedRegNo, ct);
            }

            // Default: POST with JSON body (KenLoad V2 style)
            var postPayload = JsonSerializer.Serialize(new { regno = normalizedRegNo });
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(postPayload, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            return await ExecuteNTSARequest(httpRequest, normalizedRegNo, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            _logger.LogWarning("NTSA integration not configured: {Message}", ex.Message);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "NTSA API request failed for {RegNo}", normalizedRegNo);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "NTSA API request timed out for {RegNo}", normalizedRegNo);
            return null;
        }
    }

    private async Task<NTSAVehicleSearchResult?> ExecuteNTSARequest(
        HttpRequestMessage request, string regNo, CancellationToken ct)
    {
        var response = await _httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        _logger.LogInformation("NTSA vehicle search for {RegNo}: {StatusCode} ({BodyLength} chars)",
            regNo, response.StatusCode, responseBody.Length);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("NTSA API returned {StatusCode} for {RegNo}: {Body}",
                response.StatusCode, regNo, responseBody);

            return new NTSAVehicleSearchResult
            {
                Found = false,
                RegNo = regNo,
                RawResponse = responseBody
            };
        }

        var result = ParseVehicleSearchResponse(regNo, responseBody);

        // Cache result in Redis
        try
        {
            var db = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(result);
            await db.StringSetAsync($"{RedisCachePrefix}{regNo}", json, CacheTtl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache NTSA vehicle result for {RegNo}", regNo);
        }

        return result;
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
    /// Parse NTSA vehicle search response.
    /// Handles the NTSA API JSON structure from KenLoad V2 reference.
    /// </summary>
    private NTSAVehicleSearchResult ParseVehicleSearchResponse(string regNo, string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            // NTSA may return nested structure or flat object
            var vehicleData = root;
            if (root.TryGetProperty("data", out var data))
                vehicleData = data;
            if (root.TryGetProperty("vehicle", out var vehicle))
                vehicleData = vehicle;

            var result = new NTSAVehicleSearchResult
            {
                Found = true,
                RegNo = regNo,
                RawResponse = responseBody,

                // Owner information
                OwnerFirstName = GetJsonString(vehicleData, "owner_first_name")
                    ?? GetJsonString(vehicleData, "ownerFirstName")
                    ?? GetJsonString(root, "owner_first_name"),
                OwnerLastName = GetJsonString(vehicleData, "owner_last_name")
                    ?? GetJsonString(vehicleData, "ownerLastName")
                    ?? GetJsonString(root, "owner_last_name"),
                OwnerType = GetJsonString(vehicleData, "owner_type")
                    ?? GetJsonString(vehicleData, "ownerType"),
                OwnerAddress = GetJsonString(vehicleData, "address"),
                OwnerTown = GetJsonString(vehicleData, "town"),
                OwnerPhone = GetJsonString(vehicleData, "phone")
                    ?? GetJsonString(vehicleData, "msisdn"),

                // Vehicle information
                ChassisNo = GetJsonString(vehicleData, "chassis_no")
                    ?? GetJsonString(vehicleData, "chassisNo"),
                Make = GetJsonString(vehicleData, "make"),
                Model = GetJsonString(vehicleData, "model"),
                BodyType = GetJsonString(vehicleData, "body_type")
                    ?? GetJsonString(vehicleData, "bodyType"),
                YearOfManufacture = GetJsonInt(vehicleData, "year_of_manufacture")
                    ?? GetJsonInt(vehicleData, "yearOfManufacture"),
                RegistrationDate = GetJsonDateTime(vehicleData, "registration_date")
                    ?? GetJsonDateTime(vehicleData, "registrationDate"),
                LogbookNumber = GetJsonString(vehicleData, "logbook_number")
                    ?? GetJsonString(vehicleData, "logbookNumber"),
                PassengerCapacity = GetJsonInt(vehicleData, "passenger_capacity")
                    ?? GetJsonInt(vehicleData, "passengerCapacity"),

                // Inspection
                InspectionCenter = GetJsonString(vehicleData, "inspection_center")
                    ?? GetJsonString(vehicleData, "inspectionCenter"),
                InspectionDate = GetJsonDateTime(vehicleData, "inspection_date")
                    ?? GetJsonDateTime(vehicleData, "inspectionDate"),
                InspectionExpiryDate = GetJsonDateTime(vehicleData, "inspection_expiry_date")
                    ?? GetJsonDateTime(vehicleData, "inspectionExpiryDate"),
                InspectionStatus = GetJsonString(vehicleData, "inspection_status")
                    ?? GetJsonString(vehicleData, "inspectionStatus"),

                // Caveat
                CaveatReason = GetJsonString(vehicleData, "caveat_reason")
                    ?? GetJsonString(vehicleData, "caveatReason"),
                CaveatStatus = GetJsonString(vehicleData, "caveat_status")
                    ?? GetJsonString(vehicleData, "caveatStatus"),
                CaveatType = GetJsonString(vehicleData, "caveat_type")
                    ?? GetJsonString(vehicleData, "caveatType")
            };

            // If no meaningful data was extracted, mark as not found
            if (string.IsNullOrEmpty(result.OwnerFirstName) && string.IsNullOrEmpty(result.ChassisNo)
                && string.IsNullOrEmpty(result.Make))
            {
                // Check for error responses
                var errorMsg = GetJsonString(root, "error") ?? GetJsonString(root, "message");
                if (!string.IsNullOrEmpty(errorMsg))
                {
                    result.Found = false;
                }
            }

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse NTSA response for {RegNo}", regNo);
            return new NTSAVehicleSearchResult
            {
                Found = false,
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

    private static int? GetJsonInt(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return null;
        if (val.ValueKind == JsonValueKind.Number) return val.GetInt32();
        if (val.ValueKind == JsonValueKind.String && int.TryParse(val.GetString(), out var i)) return i;
        return null;
    }

    private static DateTime? GetJsonDateTime(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var val)) return null;
        if (val.ValueKind == JsonValueKind.String && DateTime.TryParse(val.GetString(), out var dt))
            return dt;
        return null;
    }
}
