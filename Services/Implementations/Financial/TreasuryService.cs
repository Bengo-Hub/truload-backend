using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TruLoad.Backend.Services.Interfaces.Financial;

namespace TruLoad.Backend.Services.Implementations.Financial;

/// <summary>
/// HTTP client for the treasury-api payment intent endpoints.
/// Used by commercial tenants (PaymentGateway = "treasury").
/// Service-to-service auth uses a bearer token from the TREASURY_SERVICE_JWT env var.
/// </summary>
public class TreasuryService : ITreasuryService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TreasuryService> _logger;

    public TreasuryService(HttpClient httpClient, IConfiguration configuration, ILogger<TreasuryService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(
        string tenantSlug,
        decimal amountKes,
        string referenceId,
        string description,
        CancellationToken ct = default)
    {
        var baseUrl = _configuration["Treasury:ApiUrl"]
            ?? throw new InvalidOperationException("Treasury:ApiUrl is not configured");
        var serviceJwt = _configuration["Treasury:ServiceJwt"]
            ?? throw new InvalidOperationException("Treasury:ServiceJwt is not configured");

        var body = new
        {
            reference_id = referenceId,
            reference_type = "weighing_invoice",
            payment_method = "pending",
            currency = "KES",
            amount = amountKes,
            source_service = "truload",
            description
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/v1/{tenantSlug}/payments/intents")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceJwt);

        var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Treasury CreatePaymentIntent failed ({Status}): {Body}", response.StatusCode, json);
            throw new HttpRequestException($"Treasury API error {response.StatusCode}: {json}");
        }

        var result = JsonSerializer.Deserialize<TreasuryIntentResponse>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Empty response from treasury-api");

        return new PaymentIntentResult(
            result.IntentId,
            result.Status,
            result.Amount,
            result.Currency);
    }

    public async Task<PaymentIntentResult> GetPaymentIntentAsync(
        string tenantSlug,
        string intentId,
        CancellationToken ct = default)
    {
        var baseUrl = _configuration["Treasury:ApiUrl"]
            ?? throw new InvalidOperationException("Treasury:ApiUrl is not configured");
        var serviceJwt = _configuration["Treasury:ServiceJwt"]
            ?? throw new InvalidOperationException("Treasury:ServiceJwt is not configured");

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{baseUrl}/api/v1/{tenantSlug}/payments/intents/{intentId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceJwt);

        var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Treasury GetPaymentIntent failed ({Status}): {Body}", response.StatusCode, json);
            throw new HttpRequestException($"Treasury API error {response.StatusCode}: {json}");
        }

        var result = JsonSerializer.Deserialize<TreasuryIntentResponse>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Empty response from treasury-api");

        return new PaymentIntentResult(
            result.IntentId,
            result.Status,
            result.Amount,
            result.Currency);
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private sealed class TreasuryIntentResponse
    {
        [JsonPropertyName("intent_id")]
        public string IntentId { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "KES";
    }
}
