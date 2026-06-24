using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Financial;
using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.Models.Financial;
using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Services.Interfaces.Financial;
using TruLoad.Backend.Services.Interfaces.Shared;
using TruLoad.Backend.Services.Implementations.Shared;
using TruLoad.Backend.Services.Interfaces.Infrastructure;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Middleware;

namespace TruLoad.Backend.Services.Implementations.Financial;

/// <summary>
/// Service implementation for receipt/payment management.
/// </summary>
public class ReceiptService : IReceiptService
{
    private readonly TruLoadDbContext _context;
    private readonly IBackgroundNotificationDispatcher _backgroundNotifications;
    private readonly IDocumentNumberService _documentNumberService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ReceiptService> _logger;

    public ReceiptService(
        TruLoadDbContext context,
        IBackgroundNotificationDispatcher backgroundNotifications,
        IDocumentNumberService documentNumberService,
        ITenantContext tenantContext,
        ILogger<ReceiptService> logger)
    {
        _context = context;
        _backgroundNotifications = backgroundNotifications;
        _documentNumberService = documentNumberService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the organization id for document numbering: prefer the owning station's
    /// organization, falling back to the current tenant context.
    /// </summary>
    private async Task<Guid> ResolveOrganizationIdAsync(Guid? stationId, CancellationToken ct)
    {
        if (stationId.HasValue && stationId.Value != Guid.Empty)
        {
            var orgId = await _context.Stations
                .Where(s => s.Id == stationId.Value)
                .Select(s => s.OrganizationId)
                .FirstOrDefaultAsync(ct);
            if (orgId != Guid.Empty) return orgId;
        }
        return _tenantContext.OrganizationId;
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
        // Include voided (soft-deleted) receipts here so the receipts page can render the
        // "voided" badge + void reason/timestamp (the void UI). Per-invoice paid totals and
        // statistics still scope to active receipts (DeletedAt == null) elsewhere.
        var query = _context.Receipts
            .Include(r => r.Invoice)
            .Include(r => r.ReceivedBy)
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
            .Include(i => i.Weighing)
            .Include(i => i.ProsecutionCase)
            .ThenInclude(p => p!.Weighing)
            .ThenInclude(w => w!.Station)
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

        // Receipt numbers are org-wide (the convention has no station code, so a per-station
        // sequence could collide across stations on the same day) — resolve the org, scope null.
        var receiptStationId = invoice.Weighing?.StationId
            ?? invoice.ProsecutionCase?.Weighing?.StationId
            ?? invoice.CaseRegister?.StationId;
        var receiptOrgId = await ResolveOrganizationIdAsync(receiptStationId, ct);
        var receiptNo = await _documentNumberService.GenerateNumberAsync(
            receiptOrgId, null, DocumentTypes.Receipt);

        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            ReceiptNo = receiptNo,
            InvoiceId = invoiceId,
            AmountPaid = request.AmountPaid,
            Currency = request.Currency,
            PaymentMethod = request.PaymentMethod,
            TransactionReference = request.TransactionReference,
            AlternateReference = request.AlternateReference,
            Notes = request.Notes,
            IdempotencyKey = request.IdempotencyKey,
            // System-driven flows (webhook/reconcile jobs) may not have an authenticated officer context.
            // Persist null instead of Guid.Empty to avoid FK violations on asp_net_users.
            ReceivedById = userId == Guid.Empty ? null : userId,
            // Populate StationId for revenue-by-station analytics
            StationId = invoice.Weighing?.StationId
                ?? invoice.ProsecutionCase?.Weighing?.StationId
                ?? invoice.CaseRegister?.StationId,
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
            LoadCorrectionMemo? memo = null;
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

                        var memoStationId = weighing?.StationId ?? invoice.CaseRegister?.StationId;
                        var memoOrgId = await ResolveOrganizationIdAsync(memoStationId, ct);
                        var memoNo = await _documentNumberService.GenerateNumberAsync(
                            memoOrgId, memoStationId, DocumentTypes.LoadCorrectionMemo);
                        memo = new LoadCorrectionMemo
                        {
                            MemoNo = memoNo,
                            CaseRegisterId = prosecution.CaseRegisterId,
                            WeighingId = weighingId.Value,
                            OverloadKg = weighing?.OverloadKg ?? 0,
                            RedistributionType = "redistribute",
                            // No acting user on system paths (online/webhook/reconcile) — leave null
                            // rather than Guid.Empty, which violates the FK to asp_net_users.
                            IssuedById = userId == Guid.Empty ? (Guid?)null : userId,
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
                // Don't throw — payment was already recorded successfully. Detach the failed memo so
                // it doesn't poison a subsequent SaveChanges on the shared DbContext (which previously
                // re-threw the FK violation up the reconcile/webhook call chain → HTTP 500).
                if (memo != null)
                    _context.Entry(memo).State = EntityState.Detached;
            }
        }

        // NOTIFY: Invoice paid / receipt generated (only on full payment).
        if (invoice.Status == "paid")
        {
            try
            {
                var caseRegister = invoice.CaseRegister ?? invoice.ProsecutionCase?.CaseRegister;
                var caseNo = caseRegister?.CaseNo;
                var caseId = caseRegister?.Id;
                var vehicleReg = invoice.Weighing?.VehicleRegNumber
                    ?? invoice.ProsecutionCase?.Weighing?.VehicleRegNumber;

                // Resolve tenant slug from the invoice's org (no request context on webhook/reconcile paths).
                var tenantSlug = await _context.Organizations.AsNoTracking()
                    .Where(o => o.Id == invoice.OrganizationId)
                    .Select(o => o.Code).FirstOrDefaultAsync(ct);
                tenantSlug = tenantSlug?.ToLowerInvariant();

                // Recipient: the case's creating officer if resolvable; otherwise the invoices pool carries it.
                string? officerEmail = null, officerName = null;
                if (caseRegister?.CreatedById is Guid creator && creator != Guid.Empty)
                {
                    var u = await _context.Users.AsNoTracking()
                        .Where(x => x.Id == creator && !string.IsNullOrEmpty(x.Email))
                        .Select(x => new { x.Email, x.FullName }).FirstOrDefaultAsync(ct);
                    officerEmail = u?.Email; officerName = u?.FullName;
                }

                var (data, subject) = TruLoadEmailData.InvoicePaid(
                    receiptNo, request.AmountPaid, request.Currency, invoice.InvoiceNo,
                    caseNo, vehicleReg, request.TransactionReference, receipt.PaymentDate, caseId);

                _backgroundNotifications.DispatchWorkflowEmail(
                    tenantSlug, "invoicePaid", "truload/invoice_paid",
                    officerEmail, officerName, data, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch invoice-paid email for invoice {InvoiceId}", invoiceId);
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
        receipt.VoidReason = reason;
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
            receipts = receipts.Where(r => r.PaymentDate < DateTime.SpecifyKind(dateTo.Value.Date.AddDays(1), DateTimeKind.Utc));

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
        // Receipt numbers follow the configured receipt convention/sequence (org-wide).
        var orgId = await ResolveOrganizationIdAsync(_tenantContext.StationId, ct);
        return await _documentNumberService.GenerateNumberAsync(
            orgId, null, DocumentTypes.Receipt);
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
            AlternateReference = receipt.AlternateReference,
            Notes = receipt.Notes,
            IdempotencyKey = receipt.IdempotencyKey,
            ReceivedById = receipt.ReceivedById,
            ReceivedByName = receipt.ReceivedBy?.FullName,
            PaymentDate = receipt.PaymentDate,
            PaymentChannel = receipt.PaymentChannel,
            Status = receipt.DeletedAt == null ? "completed" : "voided",
            VoidedAt = receipt.DeletedAt,
            VoidReason = receipt.VoidReason,
            CreatedAt = receipt.CreatedAt,
            UpdatedAt = receipt.UpdatedAt
        };
    }
}
