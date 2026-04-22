using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.Financial;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Services.Interfaces.Financial;
using TruLoad.Backend.Services.Interfaces.Infrastructure;

namespace TruLoad.Backend.Controllers.Financial;

/// <summary>
/// API controller for invoice management.
/// Handles invoice generation, status updates, and PDF export.
/// </summary>
[ApiController]
[Authorize]
public class InvoiceController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly IPdfService _pdfService;
    private readonly ITenantContext _tenantContext;

    public InvoiceController(
        IInvoiceService invoiceService,
        IPdfService pdfService,
        ITenantContext tenantContext)
    {
        _invoiceService = invoiceService;
        _pdfService = pdfService;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Get invoice by ID
    /// </summary>
    [HttpGet("api/v1/invoices/{id}")]
    [HasPermission("invoice.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var invoice = await _invoiceService.GetByIdAsync(id, ct);
        if (invoice == null) return NotFound();
        return Ok(invoice);
    }

    /// <summary>
    /// Get invoices for a prosecution case
    /// </summary>
    [HttpGet("api/v1/prosecutions/{prosecutionId}/invoices")]
    [HasPermission("invoice.read")]
    public async Task<IActionResult> GetByProsecutionId(Guid prosecutionId, CancellationToken ct)
    {
        var invoices = await _invoiceService.GetByProsecutionIdAsync(prosecutionId, ct);
        return Ok(invoices);
    }

    /// <summary>
    /// Search invoices with filters
    /// </summary>
    [HttpPost("api/v1/invoices/search")]
    [HasPermission("invoice.read")]
    public async Task<IActionResult> Search([FromBody] InvoiceSearchCriteria criteria, CancellationToken ct)
    {
        var invoices = await _invoiceService.SearchAsync(criteria, ct);
        return Ok(invoices);
    }

    /// <summary>
    /// Generate a new invoice for a prosecution case
    /// </summary>
    [HttpPost("api/v1/prosecutions/{prosecutionId}/invoices")]
    [HasPermission("invoice.create")]
    public async Task<IActionResult> GenerateInvoice(Guid prosecutionId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        try
        {
            var invoice = await _invoiceService.GenerateInvoiceAsync(prosecutionId, userId, ct);
            return CreatedAtAction(nameof(GetById), new { id = invoice.Id }, invoice);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Update invoice status
    /// </summary>
    [HttpPut("api/v1/invoices/{id}/status")]
    [HasPermission("invoice.update")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateInvoiceStatusRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetCurrentUserId();

        try
        {
            var invoice = await _invoiceService.UpdateStatusAsync(id, request.Status, userId, ct);
            return Ok(invoice);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Void an invoice
    /// </summary>
    [HttpPost("api/v1/invoices/{id}/void")]
    [HasPermission("invoice.void")]
    public async Task<IActionResult> VoidInvoice(
        Guid id,
        [FromBody] VoidInvoiceRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetCurrentUserId();

        try
        {
            var invoice = await _invoiceService.VoidInvoiceAsync(id, request.Reason, userId, ct);
            return Ok(invoice);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Get invoice statistics for dashboard
    /// </summary>
    [HttpGet("api/v1/invoices/statistics")]
    [HasPermission("invoice.read")]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? stationId,
        CancellationToken ct)
    {
        var isHqOrAdmin = User.FindFirst("is_hq_user")?.Value == "true" || User.IsInRole("Superuser") || User.IsInRole("System Admin");
        var effectiveStationId = (stationId == null && isHqOrAdmin) ? null : (stationId ?? _tenantContext.StationId);
        var stats = await _invoiceService.GetStatisticsAsync(dateFrom, dateTo, effectiveStationId, ct);
        return Ok(stats);
    }

    /// <summary>
    /// Get invoice aging breakdown for dashboard chart
    /// </summary>
    [HttpGet("api/v1/invoices/aging")]
    [HasPermission("invoice.read")]
    public async Task<IActionResult> GetAging(CancellationToken ct)
    {
        var aging = await _invoiceService.GetAgingAsync(ct);
        return Ok(aging);
    }

    /// <summary>
    /// Download invoice PDF
    /// </summary>
    [HttpGet("api/v1/invoices/{id}/pdf")]
    [HasPermission("invoice.read")]
    public async Task<IActionResult> DownloadPdf(Guid id, CancellationToken ct)
    {
        try
        {
            var pdfBytes = await _pdfService.GenerateInvoiceAsync(id, ct);
            var invoice = await _invoiceService.GetByIdAsync(id, ct);
            var fileName = $"Invoice_{invoice?.InvoiceNo ?? id.ToString()}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Manually reconcile a commercial invoice (cash / offline payment).
    /// Marks the invoice as paid and records the payment channel and reference.
    /// </summary>
    [HttpPost("api/v1/invoices/{id}/reconcile")]
    [HasPermission("invoice.update")]
    public async Task<IActionResult> ManualReconcile(
        Guid id,
        [FromBody] ManualReconcileRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var invoice = await _invoiceService.GetByIdAsync(id, ct);
            if (invoice == null) return NotFound("Invoice not found");

            // Only allow reconciliation of pending invoices
            if (invoice.Status?.ToLower() != "pending")
                return BadRequest($"Invoice is already {invoice.Status}. Only pending invoices can be reconciled.");

            // Record as paid with provided channel and reference
            var updated = await _invoiceService.MarkAsPaidAsync(
                id,
                request.AmountPaid,
                request.Channel,
                request.Reference,
                request.Notes,
                GetCurrentUserId(),
                ct);

            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            throw new UnauthorizedAccessException("User ID not found in claims");
        return Guid.Parse(userIdClaim);
    }
}

/// <summary>
/// Request to update invoice status
/// </summary>
public class UpdateInvoiceStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Request to void an invoice
/// </summary>
public class VoidInvoiceRequest
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Request to manually reconcile a commercial invoice against a cash / offline payment
/// </summary>
public class ManualReconcileRequest
{
    [Required]
    [Range(0.01, 10_000_000)]
    public decimal AmountPaid { get; set; }

    [Required]
    [MaxLength(50)]
    public string Channel { get; set; } = "cash";

    [MaxLength(100)]
    public string? Reference { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
