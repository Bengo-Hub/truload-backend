using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Financial;
using TruLoad.Backend.Services.Interfaces.Financial;
using TruLoad.Backend.Services.Interfaces.System;

namespace TruLoad.Backend.Services.Implementations.Financial;

/// <summary>
/// eCitizen/Pesaflow payment integration service.
/// Handles OAuth token management, invoice creation, checkout initiation,
/// IPN webhook processing, and payment reconciliation.
/// </summary>
public class ECitizenService : IECitizenService
{
    private const string ProviderName = "ecitizen_pesaflow";
    private const string RedisTokenKey = "ecitizen:oauth:token";
    private const string ServiceId = "235330"; // Pesaflow service ID

    private readonly HttpClient _httpClient;
    private readonly TruLoadDbContext _context;
    private readonly IIntegrationConfigService _integrationConfigService;
    private readonly IReceiptService _receiptService;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ECitizenService> _logger;

    public ECitizenService(
        HttpClient httpClient,
        TruLoadDbContext context,
        IIntegrationConfigService integrationConfigService,
        IReceiptService receiptService,
        IConnectionMultiplexer redis,
        ILogger<ECitizenService> logger)
    {
        _httpClient = httpClient;
        _context = context;
        _integrationConfigService = integrationConfigService;
        _receiptService = receiptService;
        _redis = redis;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var cachedToken = await db.StringGetAsync(RedisTokenKey);

        if (cachedToken.HasValue)
            return cachedToken.ToString();

        var credentials = await _integrationConfigService.GetDecryptedCredentialsAsync(ProviderName, ct);
        var config = await _integrationConfigService.GetByProviderAsync(ProviderName, ct)
            ?? throw new InvalidOperationException("eCitizen integration config not found");

        var apiKey = credentials.GetValueOrDefault("ApiKey")
            ?? throw new InvalidOperationException("ApiKey not found in eCitizen credentials");
        var apiSecret = credentials.GetValueOrDefault("ApiSecret")
            ?? throw new InvalidOperationException("ApiSecret not found in eCitizen credentials");

        // Pesaflow OAuth: POST JSON body with key + secret (not Basic auth)
        var oauthPayload = JsonSerializer.Serialize(new { key = apiKey, secret = apiSecret });

        var request = new HttpRequestMessage(HttpMethod.Post, $"{config.BaseUrl}/api/oauth/generate/token")
        {
            Content = new StringContent(oauthPayload, Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Pesaflow OAuth token request failed: {StatusCode} {Body}",
                response.StatusCode, responseBody);
            throw new HttpRequestException($"Pesaflow OAuth failed: {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        // Pesaflow returns { "token": "...", "expiry": 3599 }
        var token = (root.TryGetProperty("token", out var tok) ? tok.GetString() : null)
            ?? (root.TryGetProperty("access_token", out var at) ? at.GetString() : null)
            ?? throw new InvalidOperationException("No token in OAuth response");

        var expiresIn = root.TryGetProperty("expiry", out var exp) ? exp.GetInt32()
            : root.TryGetProperty("expires_in", out var ei) ? ei.GetInt32()
            : 3599;

        // Cache for the full validity period minus 60s buffer
        var cacheTtl = TimeSpan.FromSeconds(Math.Max(expiresIn - 60, 60));
        await db.StringSetAsync(RedisTokenKey, token, cacheTtl);

        _logger.LogInformation("Pesaflow OAuth token acquired, expires in {ExpiresIn}s, caching for {CacheTtl}s",
            expiresIn, cacheTtl.TotalSeconds);
        return token;
    }

    public async Task<PesaflowInvoiceResponse> CreatePesaflowInvoiceAsync(
        CreatePesaflowInvoiceRequest request, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(i => i.Id == request.LocalInvoiceId && i.DeletedAt == null, ct)
            ?? throw new InvalidOperationException($"Invoice {request.LocalInvoiceId} not found");

        var credentials = await _integrationConfigService.GetDecryptedCredentialsAsync(ProviderName, ct);
        var config = await _integrationConfigService.GetByProviderAsync(ProviderName, ct)
            ?? throw new InvalidOperationException("eCitizen integration config not found");

        var apiKey = credentials.GetValueOrDefault("ApiKey")!;
        var apiSecret = credentials.GetValueOrDefault("ApiSecret")!;
        var apiClientId = credentials.GetValueOrDefault("ApiClientId") ?? ServiceId;

        // Prefer explicit ServiceId from integration config when present
        string? serviceIdFromConfig = null;
        try
        {
            var prop = config.GetType().GetProperty("ServiceId");
            if (prop != null)
                serviceIdFromConfig = prop.GetValue(config)?.ToString();
        }
        catch { /* ignore reflection errors */ }

        var serviceId = !string.IsNullOrEmpty(serviceIdFromConfig)
            ? serviceIdFromConfig!
            : (credentials.GetValueOrDefault("ServiceId") ?? apiClientId ?? ServiceId);

        // Pesaflow expects amount with two decimal places
        var amount = invoice.AmountDue.ToString("F2");
        var clientIdNumber = request.ClientIdNumber ?? "";
        var billDesc = "Overload Fine";

        // Compose secure hash: apiClientID + amount + serviceID + clientIDNumber + currency + billRefNumber + billDesc + clientName + secret
        var dataString = $"{apiClientId}{amount}{serviceId}{clientIdNumber}{invoice.Currency}{invoice.InvoiceNo}{billDesc}{request.ClientName}{apiSecret}";
        var secureHash = ComputeSecureHash(dataString, apiKey);

        // Use iframe endpoint (correct Pesaflow flow) with form-urlencoded and camelCase keys
        var formData = new Dictionary<string, string>
        {
            ["apiClientID"] = apiClientId,
            ["serviceID"] = serviceId,
            ["billDesc"] = billDesc,
            ["currency"] = invoice.Currency,
            ["billRefNumber"] = invoice.InvoiceNo,
            ["clientMSISDN"] = request.ClientMsisdn ?? "",
            ["clientName"] = request.ClientName,
            ["clientIDNumber"] = clientIdNumber,
            ["clientEmail"] = request.ClientEmail ?? "",
            ["amountExpected"] = amount,
            ["callBackURLOnSuccess"] = config.CallbackUrl ?? "",
            ["callBackURLOnFailure"] = config.CallbackFailureUrl ?? "",
            ["callBackURLOnTimeout"] = config.CallbackTimeoutUrl ?? "",
            ["notificationURL"] = config.WebhookUrl ?? "",
            ["secureHash"] = secureHash,
            ["format"] = "json",
            ["sendSTK"] = "false"
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{config.BaseUrl}/PaymentAPI/iframev2.1.php")
        {
            Content = new FormUrlEncodedContent(formData)
        };

        try
        {
            var response = await _httpClient.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            _logger.LogInformation("Pesaflow iframe invoice creation response: {StatusCode} {Body}",
                response.StatusCode, responseBody);

            if (!response.IsSuccessStatusCode)
            {
                // Mark invoice for background sync retry
                invoice.PesaflowSyncStatus = "failed";
                invoice.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);

                return new PesaflowInvoiceResponse
                {
                    Success = false,
                    Message = $"Pesaflow API error: {response.StatusCode} - {responseBody}"
                };
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var pesaflowInvoiceNo = root.TryGetProperty("invoice_number", out var invNo)
                ? invNo.GetString() : null;
            var paymentLink = root.TryGetProperty("invoice_link", out var link)
                ? link.GetString() : null;
            var commission = root.TryGetProperty("commission", out var comm)
                ? decimal.Parse(comm.GetString() ?? "0") : 0m;
            var amountNet = root.TryGetProperty("amount_net", out var net)
                ? decimal.Parse(net.GetString() ?? "0") : 0m;
            var amountExpected = root.TryGetProperty("amount_expected", out var expected)
                ? decimal.Parse(expected.GetString() ?? "0") : 0m;

            // Update local invoice with complete Pesaflow details
            invoice.PesaflowInvoiceNumber = pesaflowInvoiceNo;
            invoice.PesaflowPaymentLink = paymentLink;
            invoice.PesaflowGatewayFee = commission;
            invoice.PesaflowAmountNet = amountNet;
            invoice.PesaflowTotalAmount = amountExpected;
            invoice.PesaflowSyncStatus = "synced";
            invoice.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            return new PesaflowInvoiceResponse
            {
                Success = true,
                PesaflowInvoiceNumber = pesaflowInvoiceNo,
                PaymentLink = paymentLink,
                GatewayFee = commission,
                AmountNet = amountNet,
                TotalAmount = amountExpected,
                Currency = invoice.Currency,
                Message = "Invoice created on Pesaflow via iframe endpoint"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pesaflow invoice creation failed for invoice {InvoiceId}", invoice.Id);

            // Mark invoice for background sync retry (queue background task)
            invoice.PesaflowSyncStatus = "pending";
            invoice.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            return new PesaflowInvoiceResponse
            {
                Success = false,
                Message = $"Pesaflow unreachable. Invoice saved locally. Background sync queued. Error: {ex.Message}"
            };
        }
    }

    public async Task<PesaflowPaymentStatusResponse?> QueryPaymentStatusAsync(
        string invoiceRefNo, CancellationToken ct = default)
    {
        var credentials = await _integrationConfigService.GetDecryptedCredentialsAsync(ProviderName, ct);
        var config = await _integrationConfigService.GetByProviderAsync(ProviderName, ct)
            ?? throw new InvalidOperationException("eCitizen integration config not found");

        var apiKey = credentials.GetValueOrDefault("ApiKey")!;
        var apiClientId = credentials.GetValueOrDefault("ApiClientId") ?? ServiceId;

        // Build secure hash for status query per Pesaflow spec Section 5:
        // data_string = api_client_id + ref_no (no secret appended)
        // HMAC key = ApiKey (consumer key)
        var dataString = $"{apiClientId}{invoiceRefNo}";
        var secureHash = ComputeSecureHash(dataString, apiKey);

        var url = $"{config.BaseUrl}/api/invoice/payment/status?api_client_id={apiClientId}&ref_no={Uri.EscapeDataString(invoiceRefNo)}&secure_hash={Uri.EscapeDataString(secureHash)}";

        var token = await GetAccessTokenAsync(ct);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        _logger.LogInformation("Pesaflow PaymentStatus for {InvoiceRef}: {StatusCode}",
            invoiceRefNo, response.StatusCode);

        if (!response.IsSuccessStatusCode)
            return null;

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        return new PesaflowPaymentStatusResponse
        {
            Status = root.TryGetProperty("status", out var s) ? s.GetString() : null,
            AmountPaid = root.TryGetProperty("amount_paid", out var ap) ? ap.GetDecimal() : 0,
            PaymentReference = root.TryGetProperty("payment_reference", out var pr) ? pr.GetString() : null,
            PaymentChannel = root.TryGetProperty("payment_channel", out var pc) ? pc.GetString() : null,
            PaymentDate = root.TryGetProperty("payment_date", out var pd) && pd.ValueKind != JsonValueKind.Null
                ? DateTime.TryParse(pd.GetString(), out var dt) ? dt : null
                : null
        };
    }

    /// <summary>
    /// Computes HMAC-SHA256 secure hash per Pesaflow specification.
    /// secureHash = Base64(hex(HMAC-SHA256(key=ApiKey, data=dataString)))
    /// </summary>
    public string ComputeSecureHash(string dataString, string apiKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(apiKey);
        var dataBytes = Encoding.UTF8.GetBytes(dataString);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);

        var hexHash = Convert.ToHexStringLower(hashBytes);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(hexHash));
    }

    /// <summary>
    /// Verifies webhook token_hash matches the expected HMAC computation.
    /// </summary>
    public bool VerifyWebhookToken(string tokenHash, string expectedData, string apiKey)
    {
        var computed = ComputeSecureHash(expectedData, apiKey);
        return string.Equals(computed, tokenHash, StringComparison.Ordinal);
    }

    public async Task<WebhookProcessingResult> ProcessWebhookNotificationAsync(
        PesaflowIpnPayload payload, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Processing IPN webhook: invoice_ref={InvoiceRef} payment_ref={PaymentRef} amount={Amount} status={Status}",
            payload.client_invoice_ref, payload.payment_reference, payload.amount_paid, payload.status);

        // 1. Find the local invoice by our InvoiceNo (sent as client_invoice_ref/bill_ref_number)
        var invoice = await _context.Invoices
            .Include(i => i.Receipts)
            .FirstOrDefaultAsync(i => i.InvoiceNo == payload.client_invoice_ref && i.DeletedAt == null, ct);

        // 2. Log webhook event to PaymentCallback table (audit trail)
        var callbackRecord = new TruLoad.Backend.Models.Financial.PaymentCallback
        {
            InvoiceId = invoice?.Id,
            CallbackType = "ipn_webhook",
            PesaflowInvoiceNumber = payload.invoice_number,
            PaymentReference = payload.payment_reference,
            Amount = payload.amount_paid,
            Currency = payload.currency,
            PaymentDate = DateTime.TryParse(payload.payment_date, out var pd) ? pd : null,
            RawPayload = JsonSerializer.Serialize(payload),
            SignatureVerified = null, // Will update after verification
            Metadata = JsonSerializer.Serialize(new
            {
                status = payload.status,
                payment_channel = payload.payment_channel,
                last_payment_amount = payload.last_payment_amount,
                invoice_amount = payload.invoice_amount
            })
        };
        _context.PaymentCallbacks.Add(callbackRecord);
        await _context.SaveChangesAsync(ct);

        if (invoice == null)
        {
            _logger.LogWarning("IPN: Invoice not found for ref {InvoiceRef}", payload.client_invoice_ref);
            return WebhookProcessingResult.InvoiceNotFound;
        }

        // 3. Verify token_hash (mandatory - reject webhooks without valid signatures)
        if (string.IsNullOrEmpty(payload.token_hash))
        {
            _logger.LogWarning("IPN: Missing token_hash for invoice {InvoiceRef} - rejecting unsigned webhook",
                payload.client_invoice_ref);
            
            callbackRecord.SignatureVerified = false;
            await _context.SaveChangesAsync(ct);
            return WebhookProcessingResult.InvalidSignature;
        }

        {
            var credentials = await _integrationConfigService.GetDecryptedCredentialsAsync(ProviderName, ct);
            var apiKey = credentials.GetValueOrDefault("ApiKey") ?? "";
            var apiSecret = credentials.GetValueOrDefault("ApiSecret") ?? "";

            // Token hash verification data: invoice_number + amount + secret
            var verificationData = $"{payload.invoice_number}{payload.amount_paid}{apiSecret}";
            var isValid = VerifyWebhookToken(payload.token_hash, verificationData, apiKey);
            
            callbackRecord.SignatureVerified = isValid;
            await _context.SaveChangesAsync(ct);
            
            if (!isValid)
            {
                _logger.LogWarning("IPN: Invalid token_hash for invoice {InvoiceRef}", payload.client_invoice_ref);
                return WebhookProcessingResult.InvalidSignature;
            }
        }

        // 4. Check payment status
        if (!string.Equals(payload.status, "PAID", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(payload.status, "SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("IPN: Non-success status {Status} for {InvoiceRef}",
                payload.status, payload.client_invoice_ref);
            return WebhookProcessingResult.PaymentFailed;
        }

        // 5. Idempotency check: existing receipt with same transaction reference
        if (!string.IsNullOrEmpty(payload.payment_reference))
        {
            var existingReceipt = await _context.Receipts
                .AnyAsync(r => r.TransactionReference == payload.payment_reference && r.DeletedAt == null, ct);

            if (existingReceipt)
            {
                _logger.LogInformation("IPN: Already processed payment_reference {PaymentRef}",
                    payload.payment_reference);
                return WebhookProcessingResult.AlreadyProcessed;
            }
        }

        // 6. Record payment via ReceiptService
        var paymentRequest = new RecordPaymentRequest
        {
            AmountPaid = payload.amount_paid,
            Currency = payload.currency ?? invoice.Currency,
            PaymentMethod = "pesaflow",
            TransactionReference = payload.payment_reference,
            IdempotencyKey = Guid.NewGuid() // Pesaflow doesn't send our idempotency key back
        };

        await _receiptService.RecordPaymentAsync(invoice.Id, paymentRequest, Guid.Empty, ct);

        // 7. Update the receipt with PaymentChannel (ReceiptService doesn't know about channels)
        if (!string.IsNullOrEmpty(payload.payment_channel))
        {
            var receipt = await _context.Receipts
                .Where(r => r.TransactionReference == payload.payment_reference && r.DeletedAt == null)
                .FirstOrDefaultAsync(ct);

            if (receipt != null)
            {
                receipt.PaymentChannel = payload.payment_channel;
                await _context.SaveChangesAsync(ct);
            }
        }

        // 8. Update invoice with Pesaflow references
        invoice.PesaflowInvoiceNumber ??= payload.invoice_number;
        invoice.PesaflowPaymentReference = payload.payment_reference;
        invoice.UpdatedAt = DateTime.UtcNow;

        // Update invoice status if fully paid
        var totalPaid = invoice.Receipts?.Where(r => r.DeletedAt == null).Sum(r => r.AmountPaid) ?? 0;
        totalPaid += payload.amount_paid; // Include current payment
        if (totalPaid >= invoice.AmountDue)
        {
            invoice.Status = "paid";
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "IPN: Payment recorded for invoice {InvoiceNo}: {Amount} {Currency} via {Channel}",
            invoice.InvoiceNo, payload.amount_paid, payload.currency, payload.payment_channel);

        return WebhookProcessingResult.Success;
    }

    public async Task<int> ReconcileUnpaidInvoicesAsync(CancellationToken ct = default)
    {
        var unpaidInvoices = await _context.Invoices
            .Where(i => i.Status == "pending" && i.DeletedAt == null && i.PesaflowInvoiceNumber != null)
            .ToListAsync(ct);

        var reconciled = 0;

        foreach (var invoice in unpaidInvoices)
        {
            try
            {
                var status = await QueryPaymentStatusAsync(invoice.InvoiceNo, ct);

                if (status != null && string.Equals(status.Status, "PAID", StringComparison.OrdinalIgnoreCase))
                {
                    // Check if receipt already exists
                    var hasReceipt = await _context.Receipts
                        .AnyAsync(r => r.InvoiceId == invoice.Id && r.DeletedAt == null, ct);

                    if (!hasReceipt && status.AmountPaid > 0)
                    {
                        var paymentRequest = new RecordPaymentRequest
                        {
                            AmountPaid = status.AmountPaid,
                            Currency = invoice.Currency,
                            PaymentMethod = "pesaflow",
                            TransactionReference = status.PaymentReference,
                            IdempotencyKey = Guid.NewGuid()
                        };

                        await _receiptService.RecordPaymentAsync(invoice.Id, paymentRequest, Guid.Empty, ct);

                        // Update PaymentChannel on the created receipt
                        if (!string.IsNullOrEmpty(status.PaymentChannel))
                        {
                            var receipt = await _context.Receipts
                                .Where(r => r.TransactionReference == status.PaymentReference && r.DeletedAt == null)
                                .FirstOrDefaultAsync(ct);

                            if (receipt != null)
                            {
                                receipt.PaymentChannel = status.PaymentChannel;
                                await _context.SaveChangesAsync(ct);
                            }
                        }

                        invoice.PesaflowPaymentReference = status.PaymentReference;
                        invoice.Status = "paid";
                        invoice.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync(ct);

                        reconciled++;
                        _logger.LogInformation("Reconciled invoice {InvoiceNo}: {Amount} paid",
                            invoice.InvoiceNo, status.AmountPaid);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to reconcile invoice {InvoiceNo}", invoice.InvoiceNo);
            }
        }

        _logger.LogInformation("Reconciliation complete: {Count} invoices reconciled", reconciled);
        return reconciled;
    }
}
