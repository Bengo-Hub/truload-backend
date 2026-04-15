using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Financial;
using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.Models.Financial;
using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Services.Interfaces.Financial;

namespace TruLoad.Backend.Services.Implementations.Financial;

/// <summary>
/// Service implementation for receipt/payment management.
/// </summary>
public class ReceiptService : IReceiptService
{
    private readonly TruLoadDbContext _context;
    private readonly ILogger<ReceiptService> _logger;

    public ReceiptService(TruLoadDbContext context, ILogger<ReceiptService> logger)
    {
        _context = context;
        _logger = logger;
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

    public async Task<PagedResponse<ReceiptDto>> SearchAsync(ReceiptSearchCriteria criteria, CancellationToken ct = default)
    {
        var query = _context.Receipts
            .Include(r => r.Invoice)
            .Include(r => r.ReceivedBy)
            .Where(r => r.DeletedAt == null)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(criteria.ReceiptNo))
            query = query.Where(r => r.ReceiptNo.Contains(criteria.ReceiptNo));

        if (criteria.InvoiceId.HasValue)
            query = query.Where(r => r.InvoiceId == criteria.InvoiceId.Value);

        if (criteria.StationId.HasValue)
            query = query.Where(r => r.Invoice != null && r.Invoice.Weighing != null && r.Invoice.Weighing.StationId == criteria.StationId.Value);

        if (!string.IsNullOrWhiteSpace(criteria.PaymentMethod))
            query = query.Where(r => r.PaymentMethod == criteria.PaymentMethod);

        if (criteria.PaymentDateFrom.HasValue)
            query = query.Where(r => r.PaymentDate >= criteria.PaymentDateFrom.Value);

        if (criteria.PaymentDateTo.HasValue)
            query = query.Where(r => r.PaymentDate <= criteria.PaymentDateTo.Value);

        if (criteria.ReceivedById.HasValue)
            query = query.Where(r => r.ReceivedById == criteria.ReceivedById.Value);

        var totalCount = await query.CountAsync(ct);

        var receipts = await query
            .OrderByDescending(r => r.PaymentDate)
            .Skip(criteria.Skip)
            .Take(criteria.PageSize)
            .ToListAsync(ct);

        return PagedResponse<ReceiptDto>.Create(
            receipts.Select(MapToDto).ToList(),
            totalCount,
            criteria.PageNumber,
            criteria.PageSize);
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
            .ThenInclude(p => p!.CaseRegister)
            .ThenInclude(c => c!.CaseStatus)
            .Include(i => i.CaseRegister)
            .ThenInclude(c => c!.CaseStatus)
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
        {
            var currency = string.IsNullOrWhiteSpace(invoice.Currency) ? request.Currency : invoice.Currency;
            throw new InvalidOperationException(
                $"Payment amount ({currency} {request.AmountPaid:N2}) exceeds remaining balance ({currency} {remaining:N2})");
        }

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
            // System-driven flows (webhook/reconcile jobs) may not have an authenticated officer context.
            // Persist null instead of Guid.Empty to avoid FK violations on asp_net_users.
            ReceivedById = userId == Guid.Empty ? null : userId,
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

            await TryCloseLinkedCaseOnFullPaymentAsync(invoice, receiptNo, request, userId, ct);
        }

        invoice.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        // Auto-create Load Correction Memo when invoice is fully paid
        // Per FRD: Memo is issued after payment, enabling the reweigh process
        if (invoice.Status == "paid" && invoice.ProsecutionCase != null)
        {
            try
            {
                var prosecution = invoice.ProsecutionCase;
                var weighingId = prosecution.WeighingId;
                if (weighingId.HasValue)
                {
                    var existingMemo = await _context.LoadCorrectionMemos
                        .AnyAsync(m => m.WeighingId == weighingId.Value, ct);
                    if (!existingMemo)
                    {
                        var weighing = await _context.WeighingTransactions
                            .AsNoTracking()
                            .FirstOrDefaultAsync(w => w.Id == weighingId.Value, ct);

                        var year = DateTime.UtcNow.Year;
                        var memoCount = await _context.LoadCorrectionMemos
                            .CountAsync(m => m.CreatedAt.Year == year, ct);
                        var memo = new LoadCorrectionMemo
                        {
                            MemoNo = $"LCM-{year}-{(memoCount + 1):D6}",
                            CaseRegisterId = prosecution.CaseRegisterId,
                            WeighingId = weighingId.Value,
                            OverloadKg = weighing?.OverloadKg ?? 0,
                            RedistributionType = "redistribute",
                            IssuedById = userId,
                            IssuedAt = DateTime.UtcNow
                        };
                        _context.LoadCorrectionMemos.Add(memo);
                        await _context.SaveChangesAsync(ct);
                        _logger.LogInformation(
                            "Auto-created load correction memo {MemoNo} for case {CaseId} after payment (overload: {OverloadKg}kg)",
                            memo.MemoNo, prosecution.CaseRegisterId, memo.OverloadKg);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to auto-create load correction memo after payment for invoice {InvoiceId}. Manual intervention required.",
                    invoiceId);
                // Don't throw — payment was already recorded successfully
            }
        }

        return (await GetByIdAsync(receipt.Id, ct))!;
    }

    private async Task TryCloseLinkedCaseOnFullPaymentAsync(
        Invoice invoice,
        string receiptNo,
        RecordPaymentRequest request,
        Guid userId,
        CancellationToken ct)
    {
        var caseRegister = invoice.CaseRegister ?? invoice.ProsecutionCase?.CaseRegister;
        if (caseRegister == null || caseRegister.ClosedAt.HasValue)
            return;

        var currentStatusCode = caseRegister.CaseStatus?.Code;
        if (string.IsNullOrWhiteSpace(currentStatusCode))
        {
            currentStatusCode = await _context.CaseStatuses
                .Where(cs => cs.Id == caseRegister.CaseStatusId)
                .Select(cs => cs.Code)
                .FirstOrDefaultAsync(ct);
        }

        var closableStates = new[] { "OPEN", "INVESTIGATION", "ESCALATED" };
        if (string.IsNullOrWhiteSpace(currentStatusCode) || !closableStates.Contains(currentStatusCode))
        {
            _logger.LogInformation(
                "Skipping auto-close for case {CaseNo}; status {Status} is not closable",
                caseRegister.CaseNo, currentStatusCode ?? "UNKNOWN");
            return;
        }

        var closedStatusId = await _context.CaseStatuses
            .Where(cs => cs.Code == "CLOSED")
            .Select(cs => cs.Id)
            .FirstOrDefaultAsync(ct);
        if (closedStatusId == Guid.Empty)
        {
            _logger.LogWarning("Cannot auto-close case {CaseNo}: CLOSED status not found", caseRegister.CaseNo);
            return;
        }

        var paidDispositionId = await _context.DispositionTypes
            .Where(dt => dt.Code == "PAID")
            .Select(dt => dt.Id)
            .FirstOrDefaultAsync(ct);
        if (paidDispositionId == Guid.Empty)
        {
            _logger.LogWarning("Cannot auto-close case {CaseNo}: PAID disposition not found", caseRegister.CaseNo);
            return;
        }

        caseRegister.CaseStatusId = closedStatusId;
        caseRegister.DispositionTypeId = paidDispositionId;
        caseRegister.ClosingReason =
            $"Case auto-closed after invoice payment reconciliation. Invoice: {invoice.InvoiceNo}; Receipt: {receiptNo}; " +
            $"Payment method: {request.PaymentMethod}; Amount: {request.AmountPaid:N2} {request.Currency}.";
        caseRegister.ClosedAt = DateTime.UtcNow;
        caseRegister.ClosedById = userId == Guid.Empty ? null : userId;
        caseRegister.UpdatedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Auto-closed case {CaseNo} after full payment for invoice {InvoiceNo}",
            caseRegister.CaseNo, invoice.InvoiceNo);
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

    public async Task<ReceiptStatisticsDto> GetStatisticsAsync(DateTime? dateFrom = null, DateTime? dateTo = null, Guid? stationId = null, CancellationToken ct = default)
    {
        var receipts = _context.Receipts.Where(r => r.DeletedAt == null);
        if (stationId.HasValue)
            receipts = receipts.Where(r => r.StationId == stationId.Value);
        if (dateFrom.HasValue)
            receipts = receipts.Where(r => r.PaymentDate >= DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc));
        if (dateTo.HasValue)
            receipts = receipts.Where(r => r.PaymentDate <= DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc));

        var total = await receipts.CountAsync(ct);

        var today = DateTime.UtcNow.Date;
        var todayReceipts = receipts.Where(r => r.PaymentDate >= today);
        var todayCount = await todayReceipts.CountAsync(ct);
        var todayAmount = await todayReceipts.SumAsync(r => r.AmountPaid, ct);
        var totalCollected = await receipts.SumAsync(r => r.AmountPaid, ct);

        // Per-currency breakdown
        var todayAmountKes = await todayReceipts.Where(r => r.Currency == "KES").SumAsync(r => r.AmountPaid, ct);
        var todayAmountUsd = await todayReceipts.Where(r => r.Currency == "USD").SumAsync(r => r.AmountPaid, ct);
        var totalCollectedKes = await receipts.Where(r => r.Currency == "KES").SumAsync(r => r.AmountPaid, ct);
        var totalCollectedUsd = await receipts.Where(r => r.Currency == "USD").SumAsync(r => r.AmountPaid, ct);

        var byMethod = await receipts
            .GroupBy(r => r.PaymentMethod)
            .Select(g => new PaymentMethodBreakdown
            {
                Method = g.Key,
                Count = g.Count(),
                Amount = g.Sum(r => r.AmountPaid)
            })
            .ToListAsync(ct);

        return new ReceiptStatisticsDto
        {
            Total = total,
            TodayCount = todayCount,
            TodayAmount = todayAmount,
            TotalCollected = totalCollected,
            TodayAmountKes = todayAmountKes,
            TodayAmountUsd = todayAmountUsd,
            TotalCollectedKes = totalCollectedKes,
            TotalCollectedUsd = totalCollectedUsd,
            ByPaymentMethod = byMethod
        };
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
            PaymentChannel = receipt.PaymentChannel,
            CreatedAt = receipt.CreatedAt,
            UpdatedAt = receipt.UpdatedAt
        };
    }
}
