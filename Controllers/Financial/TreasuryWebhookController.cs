using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Services.Interfaces.Shared;

namespace TruLoad.Backend.Controllers.Financial;

/// <summary>
/// Handles treasury-api payment webhook callbacks.
/// Unauthenticated — caller identity verified via HMAC-SHA256 signature on the request body.
/// </summary>
[ApiController]
[Route("api/v1/payments/treasury-callback")]
[AllowAnonymous]
public class TreasuryWebhookController : ControllerBase
{
    private readonly TruLoadDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TreasuryWebhookController> _logger;

    public TreasuryWebhookController(
        TruLoadDbContext dbContext,
        INotificationService notificationService,
        IConfiguration configuration,
        ILogger<TreasuryWebhookController> logger)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Receives payment status updates from treasury-api.
    /// Verifies HMAC-SHA256 signature in X-Treasury-Signature header.
    /// Updates the matching Invoice when status is "succeeded".
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> HandleWebhook()
    {
        // Read raw body for signature verification
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync();

        // Verify HMAC signature
        var webhookSecret = _configuration["Treasury:WebhookSecret"];
        if (!string.IsNullOrWhiteSpace(webhookSecret))
        {
            var signature = Request.Headers["X-Treasury-Signature"].FirstOrDefault();
            if (!VerifySignature(rawBody, signature, webhookSecret))
            {
                _logger.LogWarning("Treasury webhook signature verification failed");
                return Unauthorized();
            }
        }

        TreasuryWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<TreasuryWebhookPayload>(rawBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse treasury webhook payload");
            return BadRequest();
        }

        if (payload == null || string.IsNullOrWhiteSpace(payload.IntentId))
            return BadRequest();

        _logger.LogInformation(
            "Treasury webhook received: intent={IntentId} status={Status}",
            payload.IntentId, payload.Status);

        var invoice = await _dbContext.Invoices
            .FirstOrDefaultAsync(i => i.TreasuryIntentId == payload.IntentId);

        if (invoice == null)
        {
            _logger.LogWarning("No invoice found for treasury intent {IntentId}", payload.IntentId);
            return Ok(); // Acknowledge to prevent retries
        }

        invoice.TreasuryIntentStatus = payload.Status;

        if (payload.Status == "succeeded")
        {
            invoice.Status = "paid";
            _logger.LogInformation(
                "Invoice {InvoiceNo} marked paid via treasury intent {IntentId}",
                invoice.InvoiceNo, payload.IntentId);

            // Notify the officer who created the weighing session
            if (invoice.WeighingId.HasValue)
            {
                var weighing = await _dbContext.WeighingTransactions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(w => w.Id == invoice.WeighingId.Value);

                if (weighing?.WeighedByUserId != null)
                {
                    _ = _notificationService.SendInternalNotificationAsync(
                        weighing.WeighedByUserId,
                        "Payment Received",
                        $"Payment of KES {invoice.AmountDue:N2} received for weighing ticket {weighing.TicketNumber}. Invoice: {invoice.InvoiceNo}.",
                        "success",
                        $"/financial/invoices/{invoice.Id}");
                }
            }
        }
        else if (payload.Status == "failed")
        {
            invoice.Status = "pending"; // Remain pending — transporter can retry
        }

        await _dbContext.SaveChangesAsync();
        return Ok();
    }

    private static bool VerifySignature(string body, string? signature, string secret)
    {
        if (string.IsNullOrWhiteSpace(signature))
            return false;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expectedBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var expected = Convert.ToHexString(expectedBytes).ToLowerInvariant();

        // Support both "sha256=<hex>" and raw hex formats
        var incoming = signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? signature[7..]
            : signature;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(incoming.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(expected));
    }

    private sealed class TreasuryWebhookPayload
    {
        public string IntentId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "KES";
        public string? ReferenceId { get; set; }
    }
}
