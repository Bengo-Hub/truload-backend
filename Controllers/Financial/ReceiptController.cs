using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Financial;
using TruLoad.Backend.DTOs.Weighing;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Services.Interfaces.Financial;
using TruLoad.Backend.Services.Interfaces.Infrastructure;

namespace TruLoad.Backend.Controllers.Financial;

/// <summary>
/// API controller for receipt/payment management.
/// Handles payment recording with idempotency support.
/// </summary>
[ApiController]
[Authorize]
public class ReceiptController : ControllerBase
{
    private readonly IReceiptService _receiptService;
    private readonly IPdfService _pdfService;
    private readonly ITenantContext _tenantContext;
    private readonly TruLoadDbContext _context;

    public ReceiptController(
        IReceiptService receiptService,
        IPdfService pdfService,
        ITenantContext tenantContext,
        TruLoadDbContext context)
    {
        _receiptService = receiptService;
        _pdfService = pdfService;
        _tenantContext = tenantContext;
        _context = context;
    }

    /// <summary>
    /// Get receipt by ID
    /// </summary>
    [HttpGet("api/v1/receipts/{id}")]
    [HasPermission("receipt.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var receipt = await _receiptService.GetByIdAsync(id, ct);
        if (receipt == null) return NotFound();
        return Ok(receipt);
    }

    /// <summary>
    /// Get receipts for an invoice
    /// </summary>
    [HttpGet("api/v1/invoices/{invoiceId}/payments")]
    [HasPermission("receipt.read")]
    public async Task<IActionResult> GetByInvoiceId(Guid invoiceId, CancellationToken ct)
    {
        var receipts = await _receiptService.GetByInvoiceIdAsync(invoiceId, ct);
        return Ok(receipts);
    }

    /// <summary>
    /// Search receipts with filters
    /// </summary>
    [HttpPost("api/v1/receipts/search")]
    [HasPermission("receipt.read")]
    public async Task<IActionResult> Search([FromBody] ReceiptSearchCriteria criteria, CancellationToken ct)
    {
        var receipts = await _receiptService.SearchAsync(criteria, ct);
        return Ok(receipts);
    }

    /// <summary>
    /// Record a payment for an invoice.
    /// Supports idempotency via the IdempotencyKey in the request.
    /// </summary>
    [HttpPost("api/v1/invoices/{invoiceId}/payments")]
    [HasPermission("receipt.create")]
    public async Task<IActionResult> RecordPayment(
        Guid invoiceId,
        [FromBody] RecordPaymentRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetCurrentUserId();

        try
        {
            var receipt = await _receiptService.RecordPaymentAsync(invoiceId, request, userId, ct);
            return CreatedAtAction(nameof(GetById), new { id = receipt.Id }, receipt);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Void a receipt
    /// </summary>
    [HttpPost("api/v1/receipts/{id}/void")]
    [HasPermission("receipt.void")]
    public async Task<IActionResult> VoidReceipt(
        Guid id,
        [FromBody] VoidReceiptRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetCurrentUserId();

        try
        {
            var receipt = await _receiptService.VoidReceiptAsync(id, request.Reason, userId, ct);
            return Ok(receipt);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Get receipt statistics for dashboard
    /// </summary>
    [HttpGet("api/v1/receipts/statistics")]
    [HasAnyPermission("analytics.read", "receipt.read")]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? stationId,
        CancellationToken ct)
    {
        var isHqOrAdmin = User.FindFirst("is_hq_user")?.Value == "true" || User.IsInRole("Superuser") || User.IsInRole("System Admin");
        var effectiveStationId = (stationId == null && isHqOrAdmin) ? null : (stationId ?? _tenantContext.StationId);
        var stats = await _receiptService.GetStatisticsAsync(dateFrom, dateTo, effectiveStationId, ct);
        return Ok(stats);
    }

    /// <summary>
    /// Get revenue breakdown by station
    /// </summary>
    [HttpGet("api/v1/receipts/revenue-by-station")]
    [HasAnyPermission("analytics.read", "receipt.read")]
    public async Task<IActionResult> GetRevenueByStation(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken ct)
    {
        try
        {
            var from = dateFrom.HasValue ? DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
            var to = dateTo.HasValue
                ? DateTime.SpecifyKind(dateTo.Value.Date, DateTimeKind.Utc).AddDays(1).AddTicks(-1)
                : DateTime.UtcNow;

            var rawReceipts = await _context.Receipts
                .AsNoTracking()
                .Include(r => r.Station)
                .Where(r => r.DeletedAt == null && r.PaymentDate >= from && r.PaymentDate <= to)
                .Select(r => new { StationId = r.StationId, StationName = r.Station != null ? r.Station.Name : "Unknown", r.Currency, r.AmountPaid })
                .ToListAsync(ct);

            var grouped = rawReceipts
                .GroupBy(r => new { r.StationId, r.StationName })
                .Select(g => new RevenueByStationDto
                {
                    StationId = g.Key.StationId ?? Guid.Empty,
                    StationName = g.Key.StationName,
                    RevenueKes = g.Where(r => r.Currency == "KES").Sum(r => r.AmountPaid),
                    RevenueUsd = g.Where(r => r.Currency == "USD").Sum(r => r.AmountPaid),
                    Count = g.Count()
                })
                .OrderByDescending(g => g.RevenueKes + g.RevenueUsd)
                .ToList();

            var total = grouped.Sum(g => g.RevenueKes + g.RevenueUsd);
            foreach (var g in grouped)
                g.Percentage = total > 0 ? Math.Round((g.RevenueKes + g.RevenueUsd) / total * 100, 1) : 0;

            return Ok(grouped);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    /// <summary>
    /// Get monthly revenue trend
    /// </summary>
    [HttpGet("api/v1/receipts/monthly-revenue")]
    [HasAnyPermission("analytics.read", "receipt.read")]
    public async Task<IActionResult> GetMonthlyRevenue(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken ct)
    {
        try
        {
            var from = dateFrom.HasValue ? DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddMonths(-12);
            var to = dateTo.HasValue
                ? DateTime.SpecifyKind(dateTo.Value.Date, DateTimeKind.Utc).AddDays(1).AddTicks(-1)
                : DateTime.UtcNow;

            var rawMonthly = await _context.Receipts
                .AsNoTracking()
                .Where(r => r.DeletedAt == null && r.PaymentDate >= from && r.PaymentDate <= to)
                .Select(r => new { r.PaymentDate.Year, r.PaymentDate.Month, r.Currency, r.AmountPaid })
                .ToListAsync(ct);

            var monthly = rawMonthly
                .GroupBy(r => new { r.Year, r.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new MonthlyRevenueDto
                {
                    Name = new DateTime(g.Key.Year, g.Key.Month, 1, 0, 0, 0, DateTimeKind.Utc).ToString("MMM yyyy"),
                    RevenueKes = g.Where(r => r.Currency == "KES").Sum(r => r.AmountPaid),
                    RevenueUsd = g.Where(r => r.Currency == "USD").Sum(r => r.AmountPaid),
                    TransactionCount = g.Count()
                })
                .ToList();

            return Ok(monthly);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    /// <summary>
    /// Get payment method distribution
    /// </summary>
    [HttpGet("api/v1/receipts/payment-methods")]
    [HasAnyPermission("analytics.read", "receipt.read")]
    public async Task<IActionResult> GetPaymentMethods(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken ct)
    {
        try
        {
            var from = dateFrom.HasValue ? DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(-30);
            var to = dateTo.HasValue
                ? DateTime.SpecifyKind(dateTo.Value.Date, DateTimeKind.Utc).AddDays(1).AddTicks(-1)
                : DateTime.UtcNow;

            var methods = await _context.Receipts
                .AsNoTracking()
                .Where(r => r.DeletedAt == null && r.PaymentDate >= from && r.PaymentDate <= to)
                .GroupBy(r => r.PaymentMethod ?? "Unknown")
                .Select(g => new PaymentMethodDistributionDto
                {
                    Name = g.Key,
                    Amount = g.Sum(r => r.AmountPaid),
                    Count = g.Count(),
                    Percentage = 0
                })
                .ToListAsync(ct);

            var total = methods.Sum(m => m.Amount);
            foreach (var m in methods)
            {
                m.Percentage = total > 0 ? Math.Round(m.Amount / total * 100, 1) : 0;
            }

            return Ok(methods);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    /// <summary>
    /// Download receipt PDF
    /// </summary>
    [HttpGet("api/v1/receipts/{id}/pdf")]
    [HasPermission("receipt.read")]
    public async Task<IActionResult> DownloadPdf(Guid id, CancellationToken ct)
    {
        try
        {
            var pdfBytes = await _pdfService.GenerateReceiptAsync(id, ct);
            var receipt = await _receiptService.GetByIdAsync(id, ct);
            var fileName = $"Receipt_{receipt?.ReceiptNo ?? id.ToString()}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to generate receipt PDF: {ex.Message}");
        }
    }

    /// <summary>
    /// Permanently delete a receipt and all its related records from the database.
    /// Superuser-only — this action is irreversible.
    /// </summary>
    [HttpDelete("api/v1/receipts/{id}/hard")]
    [Authorize(Roles = "Superuser")]
    public async Task<IActionResult> HardDelete(Guid id, CancellationToken ct)
    {
        var receipt = await _context.Receipts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (receipt == null)
            return NotFound(new { message = "Receipt not found" });

        _context.Receipts.Remove(receipt);
        await _context.SaveChangesAsync(ct);

        return NoContent();
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
/// Request to void a receipt
/// </summary>
public class VoidReceiptRequest
{
    public string Reason { get; set; } = string.Empty;
}
