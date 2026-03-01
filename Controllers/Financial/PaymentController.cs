using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.Financial;
using TruLoad.Backend.Services.Interfaces.Financial;

namespace TruLoad.Backend.Controllers.Financial;

/// <summary>
/// Payment integration endpoints for Pesaflow invoice creation, checkout, and reconciliation.
/// </summary>
[ApiController]
[Authorize]
public class PaymentController : ControllerBase
{
    private readonly IECitizenService _eCitizenService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IECitizenService eCitizenService,
        ILogger<PaymentController> logger)
    {
        _eCitizenService = eCitizenService;
        _logger = logger;
    }

    /// <summary>
    /// Create an invoice on Pesaflow for the given local invoice.
    /// </summary>
    [HttpPost("api/v1/invoices/{invoiceId}/pesaflow")]
    [HasPermission("invoice.create")]
    public async Task<IActionResult> CreatePesaflowInvoice(
        Guid invoiceId,
        [FromBody] CreatePesaflowInvoiceRequest request,
        CancellationToken ct)
    {
        request.LocalInvoiceId = invoiceId;
        var result = await _eCitizenService.CreatePesaflowInvoiceAsync(request, ct);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Query payment status from Pesaflow for a given invoice.
    /// </summary>
    [HttpGet("api/v1/invoices/{invoiceId}/payment-status")]
    [HasPermission("invoice.read")]
    public async Task<IActionResult> QueryPaymentStatus(
        Guid invoiceId,
        [FromQuery] string invoiceRefNo,
        CancellationToken ct)
    {
        var result = await _eCitizenService.QueryPaymentStatusAsync(invoiceRefNo, ct);

        if (result == null)
            return NotFound(new { message = "Payment status not available" });

        return Ok(result);
    }

    /// <summary>
    /// Manually trigger reconciliation of unpaid invoices against Pesaflow.
    /// </summary>
    [HttpPost("api/v1/payments/reconcile")]
    [HasPermission("financial.audit")]
    public async Task<IActionResult> ReconcilePayments(CancellationToken ct)
    {
        var reconciled = await _eCitizenService.ReconcileUnpaidInvoicesAsync(ct);
        return Ok(new { reconciled, message = $"{reconciled} invoices reconciled" });
    }

    /// <summary>
    /// Reconcile a single invoice against Pesaflow.
    /// </summary>
    [HttpPost("api/v1/invoices/{invoiceId}/reconcile")]
    [HasPermission("invoice.update")]
    public async Task<IActionResult> ReconcileInvoice(
        Guid invoiceId,
        [FromBody] ReconcileInvoiceRequest request,
        CancellationToken ct)
    {
        var success = await _eCitizenService.ReconcileInvoiceAsync(
            invoiceId, 
            request.TransactionReference, 
            request.AmountPaid, 
            ct);

        if (!success)
            return BadRequest(new { success = false, message = "Reconciliation failed. Payment could not be verified on Pesaflow." });

        return Ok(new { success = true, message = "Invoice reconciled successfully" });
    }
}

public class ReconcileInvoiceRequest
{
    public string? TransactionReference { get; set; }
    public decimal? AmountPaid { get; set; }
}
