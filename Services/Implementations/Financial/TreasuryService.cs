using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TruLoad.Backend.Services.Interfaces.Financial;
using TruLoad.Backend.Services.Interfaces.System;

namespace TruLoad.Backend.Services.Implementations.Financial;

/// <summary>
/// HTTP client for treasury-api S2S payment intent endpoints.
/// Auth: X-API-Key header using credentials from IntegrationConfig DB,
/// falling back to INTERNAL_SERVICE_KEY env var.
/// </summary>
public class TreasuryService : ITreasuryService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IIntegrationConfigService _integrationConfigService;
    private readonly ILogger<TreasuryService> _logger;

    private const string ProviderName = "treasury_service";

    public TreasuryService(
        HttpClient httpClient,
        IConfiguration configuration,
        IIntegrationConfigService integrationConfigService,
        ILogger<TreasuryService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _integrationConfigService = integrationConfigService;
        _logger = logger;
    }

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(
        string tenantSlug,
        decimal amountKes,
        string referenceId,
        string description,
        string referenceType = "weighing_invoice",
        CancellationToken ct = default)
    {
        var (apiKey, baseUrl) = await ResolveConfigAsync(ct);

        var body = new
        {
            reference_id = referenceId,
            reference_type = referenceType,
            payment_method = "pending",
            currency = "KES",
            amount = amountKes.ToString("F2"),
            source_service = "truload",
            description
        };

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{baseUrl}/api/v1/s2s/{tenantSlug}/payments/intents")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("X-API-Key", apiKey);
        request.Headers.Add("Idempotency-Key", referenceId);

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
        var (apiKey, baseUrl) = await ResolveConfigAsync(ct);

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{baseUrl}/api/v1/s2s/{tenantSlug}/payments/intents/{intentId}");
        request.Headers.Add("X-API-Key", apiKey);

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

    public async Task<TreasuryInvoiceResult> CreateInvoiceAsync(
        string tenantSlug,
        TreasuryInvoiceRequest invoiceRequest,
        CancellationToken ct = default)
    {
        var (apiKey, baseUrl) = await ResolveConfigAsync(ct);

        var now = DateTime.UtcNow;
        var body = new
        {
            customer_id = invoiceRequest.CustomerId,
            crm_customer_id = invoiceRequest.CrmCustomerId,
            customer_name = invoiceRequest.CustomerName,
            customer_email = invoiceRequest.CustomerEmail,
            customer_phone = invoiceRequest.CustomerPhone,
            invoice_type = invoiceRequest.InvoiceType,
            invoice_date = now.ToString("O"),
            due_date = invoiceRequest.DueDate.ToString("O"),
            currency = "KES",
            reference_id = invoiceRequest.ReferenceId,
            reference_type = invoiceRequest.ReferenceType,
            lines = new[]
            {
                new
                {
                    description = invoiceRequest.Description,
                    quantity = "1",
                    unit_price = invoiceRequest.AmountKes.ToString("F2"),
                    tax_rate = "0"
                }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{baseUrl}/api/v1/s2s/{tenantSlug}/invoices")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("X-API-Key", apiKey);
        request.Headers.Add("Idempotency-Key", invoiceRequest.ReferenceId);

        var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Treasury CreateInvoice failed ({Status}): {Body}", response.StatusCode, json);
            throw new HttpRequestException($"Treasury API error {response.StatusCode}: {json}");
        }

        var result = JsonSerializer.Deserialize<TreasuryInvoiceResponse>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Empty response from treasury-api");

        return new TreasuryInvoiceResult(result.Id, result.InvoiceNumber);
    }

    public async Task SendInvoiceAsync(
        string tenantSlug,
        string treasuryInvoiceId,
        CancellationToken ct = default)
    {
        var (apiKey, baseUrl) = await ResolveConfigAsync(ct);

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{baseUrl}/api/v1/s2s/{tenantSlug}/invoices/{treasuryInvoiceId}/send")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-API-Key", apiKey);

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Treasury SendInvoice failed ({Status}): {Body}", response.StatusCode, json);
            throw new HttpRequestException($"Treasury API error {response.StatusCode}: {json}");
        }
    }

    public async Task RecordInvoicePaymentAsync(
        string tenantSlug,
        string treasuryInvoiceId,
        decimal amountKes,
        string method,
        string? reference = null,
        CancellationToken ct = default)
    {
        var (apiKey, baseUrl) = await ResolveConfigAsync(ct);

        var body = new
        {
            amount = amountKes.ToString("F2"),
            method,
            reference
        };

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{baseUrl}/api/v1/s2s/{tenantSlug}/invoices/{treasuryInvoiceId}/record-payment")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("X-API-Key", apiKey);
        if (!string.IsNullOrWhiteSpace(reference))
            request.Headers.Add("Idempotency-Key", $"{treasuryInvoiceId}:{reference}");

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Treasury RecordInvoicePayment failed ({Status}): {Body}", response.StatusCode, json);
            throw new HttpRequestException($"Treasury API error {response.StatusCode}: {json}");
        }
    }

    // Resolves (apiKey, baseUrl) from IntegrationConfig DB first, then falls back to env/config.
    private async Task<(string apiKey, string baseUrl)> ResolveConfigAsync(CancellationToken ct)
    {
        string? apiKey = null;
        string? baseUrl = null;

        try
        {
            var creds = await _integrationConfigService.GetDecryptedCredentialsAsync(ProviderName, ct);
            creds.TryGetValue("api_key", out apiKey);
            var config = await _integrationConfigService.GetByProviderAsync(ProviderName, ct);
            baseUrl = config?.BaseUrl;
        }
        catch (InvalidOperationException)
        {
            _logger.LogDebug("treasury_service integration config not found, using env fallback");
        }

        apiKey ??= _configuration["INTERNAL_SERVICE_KEY"]
            ?? throw new InvalidOperationException(
                "Treasury API key not configured. Set INTERNAL_SERVICE_KEY or configure treasury_service integration.");

        baseUrl ??= _configuration["Treasury:ApiUrl"]
            ?? throw new InvalidOperationException("Treasury:ApiUrl not configured");

        return (apiKey, baseUrl.TrimEnd('/'));
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private sealed class TreasuryInvoiceResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("invoice_number")]
        public string InvoiceNumber { get; set; } = string.Empty;
    }

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
