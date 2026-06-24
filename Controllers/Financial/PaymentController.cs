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
        // Capture the SPA host that initiated checkout so the post-payment redirect returns the
        // payer to the same frontend (preserving their session) instead of the API host.
        request.OriginBaseUrl = ResolveRequestOrigin();
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
    /// Supports an optional AlternateReference for cases where the payment was made
    /// to a different eCitizen reference (e.g. when the original checkout iframe failed).
    /// When AlternateReference is provided, Pesaflow is queried using that ref,
    /// the returned amount is validated against the invoice amount, and Notes is required.
    /// </summary>
    [HttpPost("api/v1/invoices/{invoiceId}/pesaflow-reconcile")]
    [HasPermission("invoice.update")]
    public async Task<IActionResult> ReconcileInvoice(
        Guid invoiceId,
        [FromBody] ReconcileInvoiceRequest request,
        CancellationToken ct)
    {
        try
        {
            var success = await _eCitizenService.ReconcileInvoiceAsync(
                invoiceId,
                request.TransactionReference,
                request.AmountPaid,
                request.AlternateReference,
                request.Notes,
                ct);

            if (!success)
                return BadRequest(new { success = false, message = "Reconciliation failed. Payment could not be verified on Pesaflow." });

            return Ok(new { success = true, message = "Invoice reconciled successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Resolves the scheme+host of the SPA that issued this request, preferring the Origin header
    /// and falling back to the Referer. Returns null when neither is present (e.g. server-to-server),
    /// in which case the service uses the configured AppBaseUrl.
    /// </summary>
    private string? ResolveRequestOrigin()
    {
        var origin = Request.Headers.Origin.ToString();
        if (!string.IsNullOrWhiteSpace(origin))
            return origin.TrimEnd('/');

        var referer = Request.Headers.Referer.ToString();
        if (!string.IsNullOrWhiteSpace(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var uri))
            return $"{uri.Scheme}://{uri.Authority}";

        return null;
    }
}

public class ReconcileInvoiceRequest
{
    public string? TransactionReference { get; set; }
    public decimal? AmountPaid { get; set; }
    /// <summary>
    /// Alternate eCitizen/Pesaflow reference to query when the original could not be paid
    /// (e.g. iframe blocked). Pesaflow is queried with this ref; amount must match within
    /// 10% of the invoice amount. Notes is required when this differs from the invoice ref.
    /// </summary>
    public string? AlternateReference { get; set; }
    /// <summary>
    /// Reconciliation notes — stored on the receipt. Required on the frontend when
    /// AlternateReference is provided and differs from the original invoice reference.
    /// </summary>
    public string? Notes { get; set; }
}
