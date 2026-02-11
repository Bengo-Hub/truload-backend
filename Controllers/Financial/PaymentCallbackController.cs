using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Financial;

namespace TruLoad.Backend.Controllers.Financial;

/// <summary>
/// Handles payment callback redirects from Pesaflow.
/// These are UI redirects (success/failure/timeout), not IPN webhooks.
/// </summary>
[ApiController]
[Route("api/v1/payments/callback")]
public class PaymentCallbackController : ControllerBase
{
    private readonly TruLoadDbContext _context;
    private readonly ILogger<PaymentCallbackController> _logger;

    public PaymentCallbackController(
        TruLoadDbContext context,
        ILogger<PaymentCallbackController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Payment success callback endpoint.
    /// Redirected here by Pesaflow after successful payment.
    /// </summary>
    [HttpGet("success")]
    [HttpPost("success")]
    public async Task<IActionResult> Success(
        [FromQuery] string? invoiceNumber,
        [FromQuery] string? paymentReference,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "[PaymentCallback] Success callback received. Invoice: {InvoiceNumber}, Reference: {PaymentReference}",
            invoiceNumber, paymentReference);

        if (string.IsNullOrEmpty(invoiceNumber))
        {
            return BadRequest(new { message = "Invoice number is required" });
        }

        // Find invoice by Pesaflow invoice number
        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(i => i.PesaflowInvoiceNumber == invoiceNumber &&  i.DeletedAt == null, ct);

        if (invoice == null)
        {
            _logger.LogWarning("[PaymentCallback] Invoice {InvoiceNumber} not found", invoiceNumber);
            
            // Log callback even if invoice not found (audit trail)
            _context.PaymentCallbacks.Add(new PaymentCallback
            {
                InvoiceId = null,
                CallbackType = "success",
                PesaflowInvoiceNumber = invoiceNumber,
                PaymentReference = paymentReference,
                RawPayload = JsonSerializer.Serialize(new
                {
                    invoiceNumber,
                    paymentReference,
                    source = "ui_redirect"
                }),
                Metadata = JsonSerializer.Serialize(new { error = "invoice_not_found" })
            });
            await _context.SaveChangesAsync(ct);
            
            return NotFound(new { message = "Invoice not found" });
        }

        // Log callback event (audit trail - does not update invoice status)
        _context.PaymentCallbacks.Add(new PaymentCallback
        {
            InvoiceId = invoice.Id,
            CallbackType = "success",
            PesaflowInvoiceNumber = invoiceNumber,
            PaymentReference = paymentReference,
            RawPayload = JsonSerializer.Serialize(new
            {
                invoiceNumber,
                paymentReference,
                source = "ui_redirect"
            }),
            Metadata = JsonSerializer.Serialize(new
            {
                note = "Callback logged - awaiting IPN webhook for authoritative confirmation"
            })
        });
        await _context.SaveChangesAsync(ct);

        // Log callback but don't update status yet (wait for IPN webhook for authoritative confirmation)
        _logger.LogInformation(
            "[PaymentCallback] Payment pending confirmation for invoice {InvoiceNo}. Awaiting IPN webhook.",
            invoice.InvoiceNo);

        // Return success page or redirect to frontend
        return Ok(new
        {
            success = true,
            message = "Payment received. Processing confirmation...",
            invoiceNo = invoice.InvoiceNo,
            pesaflowInvoiceNumber = invoiceNumber,
            paymentReference
        });
    }

    /// <summary>
    /// Payment failure callback endpoint.
    /// Redirected here by Pesaflow after failed payment attempt.
    /// </summary>
    [HttpGet("failure")]
    [HttpPost("failure")]
    public async Task<IActionResult> Failure(
        [FromQuery] string? invoiceNumber,
        [FromQuery] string? reason,
        CancellationToken ct)
    {
        _logger.LogWarning(
            "[PaymentCallback] Failure callback received. Invoice: {InvoiceNumber}, Reason: {Reason}",
            invoiceNumber, reason);

        if (string.IsNullOrEmpty(invoiceNumber))
        {
            return BadRequest(new { message = "Invoice number is required" });
        }

        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(i => i.PesaflowInvoiceNumber == invoiceNumber && i.DeletedAt == null, ct);

        // Log callback event (audit trail)
        _context.PaymentCallbacks.Add(new PaymentCallback
        {
            InvoiceId = invoice?.Id,
            CallbackType = "failure",
            PesaflowInvoiceNumber = invoiceNumber,
            RawPayload = JsonSerializer.Serialize(new
            {
                invoiceNumber,
                reason,
                source = "ui_redirect"
            }),
            Metadata = JsonSerializer.Serialize(new
            {
                failure_reason = reason
            })
        });
        await _context.SaveChangesAsync(ct);

        if (invoice != null)
        {
            _logger.LogInformation(
                "[PaymentCallback] Payment failed for invoice {InvoiceNo}. Reason: {Reason}",
                invoice.InvoiceNo, reason);
        }

        return Ok(new
        {
            success = false,
            message = "Payment failed. Please try again.",
            invoiceNumber,
            reason
        });
    }

    /// <summary>
    /// Payment timeout callback endpoint.
    /// Redirected here by Pesaflow when payment session expires.
    /// </summary>
    [HttpGet("timeout")]
    [HttpPost("timeout")]
    public async Task<IActionResult> Timeout(
        [FromQuery] string? invoiceNumber,
        CancellationToken ct)
    {
        _logger.LogWarning(
            "[PaymentCallback] Timeout callback received. Invoice: {InvoiceNumber}",
            invoiceNumber);

        if (string.IsNullOrEmpty(invoiceNumber))
        {
            return BadRequest(new { message = "Invoice number is required" });
        }

        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(i => i.PesaflowInvoiceNumber == invoiceNumber && i.DeletedAt == null, ct);

        // Log callback event (audit trail)
        _context.PaymentCallbacks.Add(new PaymentCallback
        {
            InvoiceId = invoice?.Id,
            CallbackType = "timeout",
            PesaflowInvoiceNumber = invoiceNumber,
            RawPayload = JsonSerializer.Serialize(new
            {
                invoiceNumber,
                source = "ui_redirect"
            }),
            Metadata = JsonSerializer.Serialize(new
            {
                note = "Payment session expired"
            })
        });
        await _context.SaveChangesAsync(ct);

        if (invoice != null)
        {
            _logger.LogInformation(
                "[PaymentCallback] Payment timeout for invoice {InvoiceNo}",
                invoice.InvoiceNo);
        }

        return Ok(new
        {
            success = false,
            message = "Payment session expired. Please initiate payment again.",
            invoiceNumber
        });
    }
}
