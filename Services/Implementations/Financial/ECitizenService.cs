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

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var config = await _integrationConfigService.GetByProviderAsync(ProviderName, ct);
            if (config == null || !config.IsActive)
            {
                _logger.LogInformation("Pesaflow integration is inactive or not configured");
                return false;
            }

            // Health check: try to acquire an OAuth token (uses the token endpoint)
            var token = await GetAccessTokenAsync(ct);
            return !string.IsNullOrEmpty(token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pesaflow integration health check failed: {Message}", ex.Message);
            return false;
        }
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var cachedToken = await db.StringGetAsync(RedisTokenKey);

            if (cachedToken.HasValue)
                return cachedToken.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read from Redis cache for Pesaflow OAuth token");
        }

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
        
        try
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync(RedisTokenKey, token, cacheTtl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache Pesaflow OAuth token to Redis");
        }

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
        var apiClientId = credentials.GetValueOrDefault("ApiClientId")
            ?? throw new InvalidOperationException("ApiClientId not found in eCitizen credentials");
        var serviceId = credentials.GetValueOrDefault("ServiceId") ?? ServiceId;

        // Pesaflow expects amount with two decimal places
        var amount = invoice.AmountDue.ToString("F2");
        var clientIdNumber = request.ClientIdNumber ?? "";
        var billDesc = "Overload Fine";

        // Log resolved identifiers (non-secret) to aid debugging when Pesaflow rejects service IDs
        _logger.LogDebug("Pesaflow identifiers resolved: apiClientId={ApiClientId}, serviceId={ServiceId}, invoiceNo={InvoiceNo}",
            apiClientId, serviceId, invoice.InvoiceNo);

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

        // Parse amount_paid - Pesaflow may return as string or number
        decimal amountPaid = 0;
        if (root.TryGetProperty("amount_paid", out var ap))
        {
            if (ap.ValueKind == JsonValueKind.Number)
                amountPaid = ap.GetDecimal();
            else if (ap.ValueKind == JsonValueKind.String)
                decimal.TryParse(ap.GetString(), out amountPaid);
        }

        return new PesaflowPaymentStatusResponse
        {
            Status = root.TryGetProperty("status", out var s) ? s.GetString() : null,
            AmountPaid = amountPaid,
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
                var status = await QueryPaymentStatusForAnyReferenceAsync(invoice, null, ct);

                if (IsPaymentConfirmed(status))
                {
                    var confirmedStatus = status!;
                    // Check if receipt already exists
                    var hasReceipt = await _context.Receipts
                        .AnyAsync(r => r.InvoiceId == invoice.Id && r.DeletedAt == null, ct);

                    if (!hasReceipt && confirmedStatus.AmountPaid > 0)
                    {
                        var paymentRequest = new RecordPaymentRequest
                        {
                            AmountPaid = confirmedStatus.AmountPaid,
                            Currency = invoice.Currency,
                            PaymentMethod = "pesaflow",
                            TransactionReference = confirmedStatus.PaymentReference,
                            IdempotencyKey = Guid.NewGuid()
                        };

                        await _receiptService.RecordPaymentAsync(invoice.Id, paymentRequest, Guid.Empty, ct);

                        // Update PaymentChannel on the created receipt
                        if (!string.IsNullOrEmpty(confirmedStatus.PaymentChannel))
                        {
                            var receipt = await _context.Receipts
                                .Where(r => r.TransactionReference == confirmedStatus.PaymentReference && r.DeletedAt == null)
                                .FirstOrDefaultAsync(ct);

                            if (receipt != null)
                            {
                                receipt.PaymentChannel = confirmedStatus.PaymentChannel;
                                await _context.SaveChangesAsync(ct);
                            }
                        }

                        invoice.PesaflowPaymentReference = confirmedStatus.PaymentReference;
                        invoice.Status = "paid";
                        invoice.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync(ct);

                        reconciled++;
                        _logger.LogInformation("Reconciled invoice {InvoiceNo}: {Amount} paid",
                            invoice.InvoiceNo, confirmedStatus.AmountPaid);
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

    public async Task<bool> ReconcileInvoiceAsync(Guid invoiceId, string? transactionReference, decimal? amountPaid, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Receipts)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.DeletedAt == null, ct)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} not found");

        if (invoice.Status == "paid")
        {
            await EnsureCaseClosedForPaidInvoiceAsync(invoice, Guid.Empty, ct);
            _logger.LogInformation("Invoice {InvoiceNo} is already paid", invoice.InvoiceNo);
            return true;
        }

        try
        {
            // Query Pesaflow status
            var status = await QueryPaymentStatusForAnyReferenceAsync(invoice, transactionReference, ct);

            if (IsPaymentConfirmed(status))
            {
                var confirmedStatus = status!;
                // Check if receipt already exists
                var effectiveAmount = amountPaid ?? confirmedStatus.AmountPaid;
                var effectiveReference = confirmedStatus.PaymentReference ?? transactionReference;

                if (string.IsNullOrEmpty(effectiveReference))
                {
                    _logger.LogWarning("Cannot reconcile invoice {InvoiceNo}: missing payment reference", invoice.InvoiceNo);
                    return false;
                }

                var existingReceipt = await _context.Receipts
                    .AnyAsync(r => r.InvoiceId == invoice.Id && r.TransactionReference == effectiveReference && r.DeletedAt == null, ct);

                if (!existingReceipt && effectiveAmount > 0)
                {
                    var paymentRequest = new RecordPaymentRequest
                    {
                        AmountPaid = effectiveAmount,
                        Currency = invoice.Currency,
                        PaymentMethod = "pesaflow",
                        TransactionReference = effectiveReference,
                        IdempotencyKey = Guid.NewGuid()
                    };

                    await _receiptService.RecordPaymentAsync(invoice.Id, paymentRequest, Guid.Empty, ct);

                    // Update PaymentChannel on the created receipt
                    if (!string.IsNullOrEmpty(confirmedStatus.PaymentChannel))
                    {
                        var receipt = await _context.Receipts
                            .Where(r => r.TransactionReference == effectiveReference && r.DeletedAt == null)
                            .FirstOrDefaultAsync(ct);

                        if (receipt != null)
                        {
                            receipt.PaymentChannel = confirmedStatus.PaymentChannel;
                        }
                    }

                    invoice.PesaflowPaymentReference = effectiveReference;
                    invoice.Status = "paid";
                    invoice.UpdatedAt = DateTime.UtcNow;
                    await EnsureCaseClosedForPaidInvoiceAsync(invoice, Guid.Empty, ct);
                    await _context.SaveChangesAsync(ct);

                    _logger.LogInformation("Successfully reconciled invoice {InvoiceNo} manually", invoice.InvoiceNo);
                    return true;
                }
                else if (existingReceipt)
                {
                    // If receipt exists but invoice wasn't marked paid, update it now
                    if (invoice.Status != "paid")
                    {
                        invoice.Status = "paid";
                        invoice.UpdatedAt = DateTime.UtcNow;
                        await EnsureCaseClosedForPaidInvoiceAsync(invoice, Guid.Empty, ct);
                        await _context.SaveChangesAsync(ct);
                    }
                    return true;
                }
            }
            
            _logger.LogWarning("Pesaflow did not confirm payment for invoice {InvoiceNo}. Status: {Status}", 
                invoice.InvoiceNo, status?.Status ?? "NULL");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to manually reconcile invoice {InvoiceNo}", invoice.InvoiceNo);
            throw;
        }
    }

    /// <summary>
    /// Treat Pesaflow payment status values that indicate successful settlement as paid.
    /// Supports varied upstream conventions (e.g., PAID, SUCCESS, SETTLED) and falls back
    /// to amount evidence where status is absent/unknown but a positive payment is reported.
    /// </summary>
    private static bool IsPaymentConfirmed(PesaflowPaymentStatusResponse? status)
    {
        if (status == null)
            return false;

        var normalized = status.Status?.Trim();
        if (!string.IsNullOrEmpty(normalized))
        {
            if (string.Equals(normalized, "PAID", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "SUCCESS", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "SETTLED", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return status.AmountPaid > 0;
    }

    /// <summary>
    /// Attempts payment status using multiple candidate references.
    /// Priority: Pesaflow invoice number -> provided transaction ref -> stored payment ref -> local invoice number.
    /// This supports manual reconciliation where payment references may be MPesa/bank refs instead of invoice refs.
    /// </summary>
    private async Task<PesaflowPaymentStatusResponse?> QueryPaymentStatusForAnyReferenceAsync(
        TruLoad.Backend.Models.Financial.Invoice invoice,
        string? transactionReference,
        CancellationToken ct)
    {
        var candidates = BuildPaymentStatusReferenceCandidates(invoice, transactionReference);

        foreach (var reference in candidates)
        {
            var status = await QueryPaymentStatusAsync(reference, ct);
            if (status == null)
                continue;

            if (IsPaymentConfirmed(status))
            {
                _logger.LogInformation(
                    "Payment status confirmed for invoice {InvoiceNo} using reference {Reference}",
                    invoice.InvoiceNo, reference);
                return status;
            }

            _logger.LogInformation(
                "Payment status not yet confirmed for invoice {InvoiceNo} using reference {Reference}. Status={Status} AmountPaid={AmountPaid}",
                invoice.InvoiceNo, reference, status.Status, status.AmountPaid);
        }

        return null;
    }

    private static List<string> BuildPaymentStatusReferenceCandidates(
        TruLoad.Backend.Models.Financial.Invoice invoice,
        string? transactionReference)
    {
        var refs = new List<string>();

        void AddIfPresent(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            var trimmed = value.Trim();
            if (!refs.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                refs.Add(trimmed);
        }

        AddIfPresent(invoice.PesaflowInvoiceNumber);
        AddIfPresent(transactionReference);
        AddIfPresent(invoice.PesaflowPaymentReference);
        AddIfPresent(invoice.InvoiceNo);

        return refs;
    }

    private async Task EnsureCaseClosedForPaidInvoiceAsync(
        TruLoad.Backend.Models.Financial.Invoice invoice,
        Guid userId,
        CancellationToken ct)
    {
        if (!invoice.CaseRegisterId.HasValue)
            return;

        var caseRegister = await _context.CaseRegisters
            .Include(c => c.CaseStatus)
            .FirstOrDefaultAsync(c => c.Id == invoice.CaseRegisterId.Value && c.DeletedAt == null, ct);
        if (caseRegister == null || caseRegister.ClosedAt.HasValue)
            return;

        var statusCode = caseRegister.CaseStatus?.Code;
        var closableStates = new[] { "OPEN", "INVESTIGATION", "ESCALATED" };
        if (string.IsNullOrWhiteSpace(statusCode) || !closableStates.Contains(statusCode))
            return;

        var closedStatusId = await _context.CaseStatuses
            .Where(cs => cs.Code == "CLOSED")
            .Select(cs => cs.Id)
            .FirstOrDefaultAsync(ct);
        var paidDispositionId = await _context.DispositionTypes
            .Where(dt => dt.Code == "PAID")
            .Select(dt => dt.Id)
            .FirstOrDefaultAsync(ct);
        if (closedStatusId == Guid.Empty || paidDispositionId == Guid.Empty)
            return;

        caseRegister.CaseStatusId = closedStatusId;
        caseRegister.DispositionTypeId = paidDispositionId;
        caseRegister.ClosingReason =
            $"Case auto-closed after invoice reconciliation. Invoice: {invoice.InvoiceNo}; " +
            $"Pesaflow reference: {invoice.PesaflowPaymentReference ?? "N/A"}.";
        caseRegister.ClosedAt = DateTime.UtcNow;
        caseRegister.ClosedById = userId == Guid.Empty ? null : userId;
        caseRegister.UpdatedAt = DateTime.UtcNow;
    }
}
