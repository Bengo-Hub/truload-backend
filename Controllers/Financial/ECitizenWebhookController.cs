using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.DTOs.Financial;
using TruLoad.Backend.Services.Interfaces.Financial;

namespace TruLoad.Backend.Controllers.Financial;

/// <summary>
/// Handles Pesaflow IPN (Instant Payment Notification) webhooks and payment callbacks.
/// These endpoints are unauthenticated - Pesaflow calls them directly.
/// Always returns HTTP 200 to prevent Pesaflow retries.
/// </summary>
[ApiController]
public class ECitizenWebhookController : ControllerBase
{
    private readonly IECitizenService _eCitizenService;
    private readonly ILogger<ECitizenWebhookController> _logger;

    public ECitizenWebhookController(
        IECitizenService eCitizenService,
        ILogger<ECitizenWebhookController> logger)
    {
        _eCitizenService = eCitizenService;
        _logger = logger;
    }

    /// <summary>
    /// Pesaflow IPN webhook endpoint. Receives payment notifications.
    /// Always returns 200 OK to prevent Pesaflow from retrying.
    /// </summary>
    [HttpPost("api/v1/payments/webhook/ecitizen-pesaflow")]
    [AllowAnonymous]
    public async Task<IActionResult> HandleWebhook([FromForm] PesaflowIpnPayload payload, CancellationToken ct)
    {
        _logger.LogInformation(
            "Received Pesaflow IPN: invoice_ref={InvoiceRef} payment_ref={PaymentRef} amount={Amount}",
            payload.client_invoice_ref, payload.payment_reference, payload.amount_paid);

        try
        {
            var result = await _eCitizenService.ProcessWebhookNotificationAsync(payload, ct);

            _logger.LogInformation("IPN processing result: {Result} for {InvoiceRef}",
                result, payload.client_invoice_ref);

            // Always return 200 to prevent retries
            return Ok(new { status = result.ToString(), message = "Webhook processed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Pesaflow IPN for {InvoiceRef}", payload.client_invoice_ref);

            // Still return 200 to prevent retries - log the error internally
            return Ok(new { status = "Error", message = "Webhook received" });
        }
    }

    /// <summary>
    /// Payment callback endpoint. Pesaflow redirects the user here after payment.
    /// Redirects to the frontend payment result page.
    /// </summary>
    [HttpGet("api/v1/payments/callback/ecitizen-pesaflow")]
    [AllowAnonymous]
    public IActionResult HandleCallback(
        [FromQuery] string? invoice_ref,
        [FromQuery] string? status)
    {
        _logger.LogInformation("Pesaflow callback: invoice_ref={InvoiceRef} status={Status}",
            invoice_ref, status);

        // Redirect to frontend payment result page
        var frontendUrl = $"/payments/result?invoice_ref={Uri.EscapeDataString(invoice_ref ?? "")}&status={Uri.EscapeDataString(status ?? "")}";
        return Redirect(frontendUrl);
    }
}
