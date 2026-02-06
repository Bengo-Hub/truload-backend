using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Financial;
using TruLoad.Backend.Models.Financial;
using TruLoad.Backend.Services.Interfaces.Financial;

namespace TruLoad.Backend.Services.Implementations.Financial;

/// <summary>
/// Service implementation for receipt/payment management.
/// </summary>
public class ReceiptService : IReceiptService
{
    private readonly TruLoadDbContext _context;

    public ReceiptService(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<ReceiptDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var receipt = await _context.Receipts
            .Include(r => r.Invoice)
            .Include(r => r.ReceivedBy)
            .FirstOrDefaultAsync(r => r.Id == id && r.DeletedAt == null, ct);

        return receipt == null ? null : MapToDto(receipt);
    }

    public async Task<IEnumerable<ReceiptDto>> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var receipts = await _context.Receipts
            .Include(r => r.Invoice)
            .Include(r => r.ReceivedBy)
            .Where(r => r.InvoiceId == invoiceId && r.DeletedAt == null)
            .OrderByDescending(r => r.PaymentDate)
            .ToListAsync(ct);

        return receipts.Select(MapToDto);
    }

    public async Task<IEnumerable<ReceiptDto>> SearchAsync(ReceiptSearchCriteria criteria, CancellationToken ct = default)
    {
        var query = _context.Receipts
            .Include(r => r.Invoice)
            .Include(r => r.ReceivedBy)
            .Where(r => r.DeletedAt == null)
            .AsQueryable();

        if (criteria.InvoiceId.HasValue)
            query = query.Where(r => r.InvoiceId == criteria.InvoiceId.Value);

        if (!string.IsNullOrWhiteSpace(criteria.PaymentMethod))
            query = query.Where(r => r.PaymentMethod == criteria.PaymentMethod);

        if (criteria.PaymentDateFrom.HasValue)
            query = query.Where(r => r.PaymentDate >= criteria.PaymentDateFrom.Value);

        if (criteria.PaymentDateTo.HasValue)
            query = query.Where(r => r.PaymentDate <= criteria.PaymentDateTo.Value);

        if (criteria.ReceivedById.HasValue)
            query = query.Where(r => r.ReceivedById == criteria.ReceivedById.Value);

        var receipts = await query
            .OrderByDescending(r => r.PaymentDate)
            .Skip(criteria.Skip)
            .Take(criteria.PageSize)
            .ToListAsync(ct);

        return receipts.Select(MapToDto);
    }

    public async Task<ReceiptDto> RecordPaymentAsync(Guid invoiceId, RecordPaymentRequest request, Guid userId, CancellationToken ct = default)
    {
        // Check for duplicate using idempotency key
        var existingReceipt = await _context.Receipts
            .FirstOrDefaultAsync(r => r.IdempotencyKey == request.IdempotencyKey && r.DeletedAt == null, ct);

        if (existingReceipt != null)
        {
            // Return existing receipt (idempotent behavior)
            return (await GetByIdAsync(existingReceipt.Id, ct))!;
        }

        var invoice = await _context.Invoices
            .Include(i => i.Receipts)
            .Include(i => i.ProsecutionCase)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.DeletedAt == null, ct)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} not found");

        if (invoice.Status == "paid")
            throw new InvalidOperationException("Invoice is already fully paid");

        if (invoice.Status == "void" || invoice.Status == "cancelled")
            throw new InvalidOperationException($"Cannot record payment for {invoice.Status} invoice");

        // Calculate current paid amount
        var currentPaid = invoice.Receipts?.Where(r => r.DeletedAt == null).Sum(r => r.AmountPaid) ?? 0;
        var remaining = invoice.AmountDue - currentPaid;

        if (request.AmountPaid > remaining)
            throw new InvalidOperationException($"Payment amount ({request.AmountPaid:C}) exceeds remaining balance ({remaining:C})");

        var receiptNo = await GenerateReceiptNumberAsync(ct);

        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            ReceiptNo = receiptNo,
            InvoiceId = invoiceId,
            AmountPaid = request.AmountPaid,
            Currency = request.Currency,
            PaymentMethod = request.PaymentMethod,
            TransactionReference = request.TransactionReference,
            IdempotencyKey = request.IdempotencyKey,
            ReceivedById = userId,
            PaymentDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Receipts.Add(receipt);

        // Update invoice status if fully paid
        var newTotalPaid = currentPaid + request.AmountPaid;
        if (newTotalPaid >= invoice.AmountDue)
        {
            invoice.Status = "paid";

            // Update prosecution case status if linked
            if (invoice.ProsecutionCase != null)
            {
                invoice.ProsecutionCase.Status = "paid";
                invoice.ProsecutionCase.UpdatedAt = DateTime.UtcNow;
            }
        }

        invoice.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return (await GetByIdAsync(receipt.Id, ct))!;
    }

    public async Task<ReceiptDto> VoidReceiptAsync(Guid id, string reason, Guid userId, CancellationToken ct = default)
    {
        var receipt = await _context.Receipts
            .Include(r => r.Invoice)
            .ThenInclude(i => i!.ProsecutionCase)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new InvalidOperationException($"Receipt {id} not found");

        if (receipt.DeletedAt != null)
            throw new InvalidOperationException("Receipt is already voided");

        receipt.DeletedAt = DateTime.UtcNow;
        receipt.UpdatedAt = DateTime.UtcNow;

        // Update invoice status if it was marked as paid
        if (receipt.Invoice != null && receipt.Invoice.Status == "paid")
        {
            receipt.Invoice.Status = "pending";
            receipt.Invoice.UpdatedAt = DateTime.UtcNow;

            // Update prosecution case status if linked
            if (receipt.Invoice.ProsecutionCase != null)
            {
                receipt.Invoice.ProsecutionCase.Status = "invoiced";
                receipt.Invoice.ProsecutionCase.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(ct);

        return MapToDto(receipt);
    }

    public async Task<Dictionary<string, object>> GetStatisticsAsync(CancellationToken ct = default)
    {
        var stats = new Dictionary<string, object>();

        var total = await _context.Receipts.CountAsync(r => r.DeletedAt == null, ct);
        stats["total"] = total;

        var today = DateTime.UtcNow.Date;
        var todayCount = await _context.Receipts
            .CountAsync(r => r.PaymentDate >= today && r.DeletedAt == null, ct);
        stats["todayCount"] = todayCount;

        var todayAmount = await _context.Receipts
            .Where(r => r.PaymentDate >= today && r.DeletedAt == null)
            .SumAsync(r => r.AmountPaid, ct);
        stats["todayAmount"] = todayAmount;

        var totalCollected = await _context.Receipts
            .Where(r => r.DeletedAt == null)
            .SumAsync(r => r.AmountPaid, ct);
        stats["totalCollected"] = totalCollected;

        // Payment method breakdown
        var byMethod = await _context.Receipts
            .Where(r => r.DeletedAt == null)
            .GroupBy(r => r.PaymentMethod)
            .Select(g => new { Method = g.Key, Count = g.Count(), Amount = g.Sum(r => r.AmountPaid) })
            .ToListAsync(ct);
        stats["byPaymentMethod"] = byMethod;

        return stats;
    }

    public async Task<string> GenerateReceiptNumberAsync(CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var count = await _context.Receipts
            .CountAsync(r => r.CreatedAt.Year == year, ct);

        return $"RCP-{year}-{(count + 1):D6}";
    }

    private ReceiptDto MapToDto(Receipt receipt)
    {
        return new ReceiptDto
        {
            Id = receipt.Id,
            ReceiptNo = receipt.ReceiptNo,
            InvoiceId = receipt.InvoiceId,
            InvoiceNo = receipt.Invoice?.InvoiceNo,
            AmountPaid = receipt.AmountPaid,
            Currency = receipt.Currency,
            PaymentMethod = receipt.PaymentMethod,
            TransactionReference = receipt.TransactionReference,
            IdempotencyKey = receipt.IdempotencyKey,
            ReceivedById = receipt.ReceivedById,
            ReceivedByName = receipt.ReceivedBy?.FullName,
            PaymentDate = receipt.PaymentDate,
            CreatedAt = receipt.CreatedAt,
            UpdatedAt = receipt.UpdatedAt
        };
    }
}
