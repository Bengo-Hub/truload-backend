using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Financial;
using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Models.Financial;
using TruLoad.Backend.Services.Interfaces.Financial;
using TruLoad.Backend.Services.Interfaces.Shared;
using TruLoad.Backend.Services.Interfaces.Infrastructure;
using TruLoad.Backend.Models.System;

namespace TruLoad.Backend.Services.Implementations.Financial;

/// <summary>
/// Service implementation for invoice management.
/// </summary>
public class InvoiceService : IInvoiceService
{
    private readonly TruLoadDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IBackgroundNotificationDispatcher _backgroundNotifications;
    private readonly ITenantContext _tenantContext;
    private readonly IDocumentNumberService _documentNumberService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InvoiceService> _logger;

    public InvoiceService(
        TruLoadDbContext context,
        INotificationService notificationService,
        IBackgroundNotificationDispatcher backgroundNotifications,
        ITenantContext tenantContext,
        IDocumentNumberService documentNumberService,
        IConfiguration configuration,
        ILogger<InvoiceService> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _backgroundNotifications = backgroundNotifications;
        _tenantContext = tenantContext;
        _documentNumberService = documentNumberService;
        _configuration = configuration;
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

    public async Task<InvoiceDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.CaseRegister)
            .Include(i => i.ProsecutionCase)
            .Include(i => i.Weighing)
            .Include(i => i.Receipts)
            .FirstOrDefaultAsync(i => i.Id == id && i.DeletedAt == null, ct);

        if (invoice == null) return null;
        var checkoutMode = await ResolveCheckoutModeAsync(ct);
        return MapToDto(invoice, checkoutMode);
    }

    public async Task<IEnumerable<InvoiceDto>> GetByProsecutionIdAsync(Guid prosecutionCaseId, CancellationToken ct = default)
    {
        var invoices = await _context.Invoices
            .Include(i => i.CaseRegister)
            .Include(i => i.ProsecutionCase)
            .Include(i => i.Receipts)
            .Where(i => i.ProsecutionCaseId == prosecutionCaseId && i.DeletedAt == null)
            .OrderByDescending(i => i.GeneratedAt)
            .ToListAsync(ct);

        var checkoutMode = await ResolveCheckoutModeAsync(ct);
        return invoices.Select(i => MapToDto(i, checkoutMode));
    }

    public async Task<PagedResponse<InvoiceDto>> SearchAsync(InvoiceSearchCriteria criteria, CancellationToken ct = default)
    {
        var query = _context.Invoices
            .Include(i => i.CaseRegister)
            .Include(i => i.ProsecutionCase)
            .Include(i => i.Weighing)
            .Include(i => i.Receipts)
            .Where(i => i.DeletedAt == null)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(criteria.InvoiceNo))
            query = query.Where(i => i.InvoiceNo.Contains(criteria.InvoiceNo));

        if (!string.IsNullOrWhiteSpace(criteria.CaseNo))
            query = query.Where(i => i.CaseRegister != null && i.CaseRegister.CaseNo.Contains(criteria.CaseNo));

        if (!string.IsNullOrWhiteSpace(criteria.VehicleRegNumber))
            query = query.Where(i => i.Weighing != null && i.Weighing.VehicleRegNumber != null
                && i.Weighing.VehicleRegNumber.Contains(criteria.VehicleRegNumber));

        if (criteria.CaseRegisterId.HasValue)
            query = query.Where(i => i.CaseRegisterId == criteria.CaseRegisterId.Value);

        if (criteria.ProsecutionCaseId.HasValue)
            query = query.Where(i => i.ProsecutionCaseId == criteria.ProsecutionCaseId.Value);

        if (criteria.StationId.HasValue)
            query = query.Where(i => i.Weighing != null && i.Weighing.StationId == criteria.StationId.Value);

        if (!string.IsNullOrWhiteSpace(criteria.Status))
            query = query.Where(i => i.Status == criteria.Status);

        if (criteria.EffectiveFromDate.HasValue)
            query = query.Where(i => i.GeneratedAt >= criteria.EffectiveFromDate.Value);

        if (criteria.EffectiveToDate.HasValue)
            query = query.Where(i => i.GeneratedAt <= criteria.EffectiveToDate.Value);

        if (criteria.DueFrom.HasValue)
            query = query.Where(i => i.DueDate >= criteria.DueFrom.Value);

        if (criteria.DueTo.HasValue)
            query = query.Where(i => i.DueDate <= criteria.DueTo.Value);

        if (criteria.MinAmount.HasValue)
            query = query.Where(i => i.AmountDue >= criteria.MinAmount.Value);

        if (criteria.MaxAmount.HasValue)
            query = query.Where(i => i.AmountDue <= criteria.MaxAmount.Value);

        var totalCount = await query.CountAsync(ct);

        var invoices = await query
            .OrderByDescending(i => i.GeneratedAt)
            .Skip(criteria.Skip)
            .Take(criteria.PageSize)
            .ToListAsync(ct);

        var checkoutMode = await ResolveCheckoutModeAsync(ct);
        return PagedResponse<InvoiceDto>.Create(
            invoices.Select(i => MapToDto(i, checkoutMode)).ToList(),
            totalCount,
            criteria.PageNumber,
            criteria.PageSize);
    }

    public async Task<InvoiceDto> GenerateInvoiceAsync(Guid prosecutionCaseId, Guid userId, CancellationToken ct = default)
    {
        var prosecutionCase = await _context.ProsecutionCases
            .Include(p => p.Act)
            .Include(p => p.Weighing).ThenInclude(w => w!.Driver)
            .FirstOrDefaultAsync(p => p.Id == prosecutionCaseId && p.DeletedAt == null, ct)
            ?? throw new InvalidOperationException($"Prosecution case {prosecutionCaseId} not found");

        // Enforcement invoices require driver details for eCitizen invoice generation
        var driver = prosecutionCase.Weighing?.Driver;
        if (driver == null)
            throw new InvalidOperationException(
                "A driver must be linked to this weighing transaction before generating an invoice. Please capture driver details first.");

        if (string.IsNullOrWhiteSpace(driver.IdNumber))
            throw new InvalidOperationException(
                $"Driver '{driver.FullNames} {driver.Surname}' is missing a National ID number. " +
                "National ID is required for eCitizen invoice generation. Please update driver details first.");

        // Idempotency: if a live pending invoice already exists for this case, return it
        // instead of failing. Generating an invoice is naturally idempotent — a double-click,
        // a retried request, or two concurrent officers must never produce a second pending
        // invoice (the "double posting" symptom). The DB also enforces this via a unique
        // partial index (status='pending' AND deleted_at IS NULL); see the DbUpdateException
        // handler below for the lost-race path.
        // IgnoreQueryFilters so a platform SUPERUSER (tenant-context org != the record's org) still
        // finds the existing pending invoice rather than creating a duplicate.
        var existingInvoice = await _context.Invoices
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.ProsecutionCaseId == prosecutionCaseId
                && i.Status == "pending"
                && i.DeletedAt == null, ct);

        if (existingInvoice != null)
        {
            _logger.LogInformation(
                "GenerateInvoice is idempotent — returning existing pending invoice {InvoiceNo} for prosecution case {CaseId}",
                existingInvoice.InvoiceNo, prosecutionCaseId);
            return (await GetByIdAsync(existingInvoice.Id, ct))!;
        }

        var invoiceNo = await GenerateInvoiceNumberAsync(ct);

        // Determine currency from the Act definition
        // Traffic Act (Cap 403) charges in KES, EAC Act charges in USD
        var chargingCurrency = prosecutionCase.Act?.ChargingCurrency ?? "KES";
        var amountDue = chargingCurrency == "KES"
            ? prosecutionCase.TotalFeeKes
            : prosecutionCase.TotalFeeUsd;

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNo = invoiceNo,
            CaseRegisterId = prosecutionCase.CaseRegisterId,
            ProsecutionCaseId = prosecutionCaseId,
            WeighingId = prosecutionCase.WeighingId,
            AmountDue = amountDue,
            Currency = chargingCurrency,
            Status = "pending",
            GeneratedAt = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Invoices.Add(invoice);

        // Update prosecution case status
        prosecutionCase.Status = "invoiced";
        prosecutionCase.UpdatedAt = DateTime.UtcNow;

        try
        {
            // The check-then-insert above is not atomic on its own; wrap it in a transaction
            // so a concurrent request can't slip a second pending invoice past the read check.
            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Possibly lost the race against a concurrent generation that committed first and
            // tripped the unique partial index. Detach our doomed row and, if a pending invoice
            // now exists for this case, return it (idempotent). Otherwise rethrow the real error.
            _context.Entry(invoice).State = EntityState.Detached;
            var winner = await _context.Invoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.ProsecutionCaseId == prosecutionCaseId
                    && i.Status == "pending" && i.DeletedAt == null, ct);
            if (winner == null) throw;

            _logger.LogInformation(
                "GenerateInvoice lost a race — returning concurrently-created pending invoice {InvoiceNo} for prosecution case {CaseId}",
                winner.InvoiceNo, prosecutionCaseId);
            return (await GetByIdAsync(winner.Id, ct))!;
        }

        // NOTIFY: Invoice Generated
        _ = _notificationService.SendInternalNotificationAsync(
            userId,
            "Invoice Generated",
            $"Invoice {invoiceNo} has been generated for {chargingCurrency} {amountDue:N2}. Due in 30 days.",
            "info",
            $"/financial/invoices/{invoice.Id}");

        // Resolve recipient in-scope, then dispatch off-request with a fresh DI scope.
        var issuingOfficer = await _context.Users.AsNoTracking()
            .Where(u => u.Id == userId && !string.IsNullOrEmpty(u.Email))
            .Select(u => new { u.Email, u.FullName })
            .FirstOrDefaultAsync(ct);
        if (issuingOfficer != null)
        {
            var caseNo = prosecutionCase.CaseRegisterId == Guid.Empty ? null
                : await _context.CaseRegisters.AsNoTracking()
                    .Where(c => c.Id == prosecutionCase.CaseRegisterId).Select(c => c.CaseNo).FirstOrDefaultAsync(ct);
            var vehicleReg = prosecutionCase.Weighing?.VehicleRegNumber;

            var (data, subject) = TruLoad.Backend.Services.Implementations.Shared.TruLoadEmailData.InvoiceIssued(
                invoiceNo, caseNo, vehicleReg, amountDue, chargingCurrency,
                invoice.DueDate, invoice.GeneratedAt, prosecutionCase.CaseRegisterId, invoice.PesaflowPaymentLink);

            _backgroundNotifications.DispatchWorkflowEmail(
                _tenantContext.OrganizationCode?.ToLowerInvariant(),
                "invoiceIssued", "truload/invoice_issued",
                issuingOfficer.Email!, issuingOfficer.FullName ?? "Officer", data, subject);
        }

        return (await GetByIdAsync(invoice.Id, ct))!;
    }

    public async Task<InvoiceDto> UpdateStatusAsync(Guid id, string status, Guid userId, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices.FindAsync(new object[] { id }, ct)
            ?? throw new InvalidOperationException($"Invoice {id} not found");

        if (invoice.DeletedAt != null)
            throw new InvalidOperationException("Cannot update a deleted invoice");

        invoice.Status = status;
        invoice.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        // NOTIFY: Invoice Paid/Reconciled
        if (status is "paid" or "reconciled")
        {
            _ = _notificationService.SendInternalNotificationAsync(
                userId,
                "Invoice Payment Confirmed",
                $"Invoice {invoice.InvoiceNo} has been marked as {status}.",
                "success",
                $"/financial/invoices/{invoice.Id}");
        }

        return (await GetByIdAsync(id, ct))!;
    }

    public async Task<InvoiceDto> VoidInvoiceAsync(Guid id, string reason, Guid userId, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Receipts)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new InvalidOperationException($"Invoice {id} not found");

        if (invoice.DeletedAt != null)
            throw new InvalidOperationException("Invoice is already deleted");

        if (invoice.Receipts.Any(r => r.DeletedAt == null))
            throw new InvalidOperationException("Cannot void an invoice with payments. Void receipts first.");

        invoice.Status = "void";
        invoice.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        // NOTIFY: Invoice Voided
        _ = _notificationService.SendInternalNotificationAsync(
            userId,
            "Invoice Voided",
            $"Invoice {invoice.InvoiceNo} has been voided. Reason: {reason}",
            "warning",
            $"/financial/invoices/{invoice.Id}");

        return (await GetByIdAsync(id, ct))!;
    }

    public async Task<InvoiceStatisticsDto> GetStatisticsAsync(DateTime? dateFrom = null, DateTime? dateTo = null, Guid? stationId = null, CancellationToken ct = default)
    {
        var invoices = _context.Invoices.Where(i => i.DeletedAt == null);
        if (stationId.HasValue)
            invoices = invoices.Where(i => i.StationId == stationId.Value);
        if (dateFrom.HasValue)
        {
            var from = DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc);
            invoices = invoices.Where(i => i.GeneratedAt >= from);
        }
        if (dateTo.HasValue)
        {
            // Use exclusive upper bound for end-of-day so same-day records are included
            var to = DateTime.SpecifyKind(dateTo.Value.Date.AddDays(1), DateTimeKind.Utc);
            invoices = invoices.Where(i => i.GeneratedAt < to);
        }

        var total = await invoices.CountAsync(ct);
        var pending = await invoices.CountAsync(i => i.Status == "pending", ct);
        var paid = await invoices.CountAsync(i => i.Status == "paid", ct);
        var overdue = await invoices.CountAsync(i => i.Status == "pending" && i.DueDate < DateTime.UtcNow, ct);

        // Aggregate totals (legacy, mixed-currency)
        var totalAmountDue = await invoices
            .Where(i => i.Status == "pending")
            .SumAsync(i => i.AmountDue, ct);

        var receiptsBase = _context.Receipts.Where(r => r.DeletedAt == null);
        if (stationId.HasValue)
            receiptsBase = receiptsBase.Where(r => r.StationId == stationId.Value);
        if (dateFrom.HasValue)
            receiptsBase = receiptsBase.Where(r => r.PaymentDate >= DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc));
        if (dateTo.HasValue)
            receiptsBase = receiptsBase.Where(r => r.PaymentDate < DateTime.SpecifyKind(dateTo.Value.Date.AddDays(1), DateTimeKind.Utc));
        var totalAmountPaid = await receiptsBase.SumAsync(r => r.AmountPaid, ct);

        // Per-currency breakdown for pending invoices
        var pendingInvoices = invoices.Where(i => i.Status == "pending");
        var totalAmountDueKes = await pendingInvoices
            .Where(i => i.Currency == "KES")
            .SumAsync(i => i.AmountDue, ct);
        var totalAmountDueUsd = await pendingInvoices
            .Where(i => i.Currency == "USD")
            .SumAsync(i => i.AmountDue, ct);

        // Per-currency breakdown for receipts — scoped to pending invoices for balance calculation.
        // receiptsBase includes all receipts in range; using it causes negative balance when all invoices
        // are paid (pendingAmountDue = 0, allReceipts = 100K → balance = -100K).
        var receiptsOnPendingBase = _context.Receipts
            .Where(r => r.DeletedAt == null && r.Invoice != null && r.Invoice.Status == "pending");
        if (stationId.HasValue)
            receiptsOnPendingBase = receiptsOnPendingBase.Where(r => r.StationId == stationId.Value);
        if (dateFrom.HasValue)
            receiptsOnPendingBase = receiptsOnPendingBase.Where(r => r.PaymentDate >= DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc));
        if (dateTo.HasValue)
            receiptsOnPendingBase = receiptsOnPendingBase.Where(r => r.PaymentDate < DateTime.SpecifyKind(dateTo.Value.Date.AddDays(1), DateTimeKind.Utc));

        var totalAmountPaidKes = await receiptsOnPendingBase
            .Where(r => r.Currency == "KES")
            .SumAsync(r => r.AmountPaid, ct);
        var totalAmountPaidUsd = await receiptsOnPendingBase
            .Where(r => r.Currency == "USD")
            .SumAsync(r => r.AmountPaid, ct);

        return new InvoiceStatisticsDto
        {
            TotalInvoices = total,
            PendingInvoices = pending,
            PaidInvoices = paid,
            OverdueInvoices = overdue,
            TotalAmountDue = totalAmountDue,
            TotalAmountPaid = totalAmountPaid,
            TotalBalance = totalAmountDue - totalAmountPaid,
            TotalAmountDueKes = totalAmountDueKes,
            TotalAmountDueUsd = totalAmountDueUsd,
            TotalAmountPaidKes = totalAmountPaidKes,
            TotalAmountPaidUsd = totalAmountPaidUsd,
            TotalBalanceKes = totalAmountDueKes - totalAmountPaidKes,
            TotalBalanceUsd = totalAmountDueUsd - totalAmountPaidUsd
        };
    }

    public async Task<List<InvoiceAgingBucketDto>> GetAgingAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var pendingInvoices = await _context.Invoices
            .Where(i => i.DeletedAt == null && i.Status == "pending")
            .Select(i => new { i.GeneratedAt, i.AmountDue })
            .ToListAsync(ct);

        var buckets = new[]
        {
            new { Label = "Current (0-30d)", Min = 0, Max = 30 },
            new { Label = "31-60 days", Min = 31, Max = 60 },
            new { Label = "61-90 days", Min = 61, Max = 90 },
            new { Label = "90+ days", Min = 91, Max = int.MaxValue }
        };

        return buckets.Select(b =>
        {
            var matching = pendingInvoices.Where(i =>
            {
                var age = (int)(now - i.GeneratedAt).TotalDays;
                return age >= b.Min && age <= b.Max;
            }).ToList();

            return new InvoiceAgingBucketDto
            {
                Name = b.Label,
                Value = matching.Count,
                Amount = matching.Sum(i => i.AmountDue)
            };
        }).ToList();
    }

    public async Task<InvoiceDto> MarkAsPaidAsync(Guid id, decimal amountPaid, string channel, string? reference, string? notes, Guid userId, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Receipts)
            .FirstOrDefaultAsync(i => i.Id == id && i.DeletedAt == null, ct)
            ?? throw new InvalidOperationException($"Invoice {id} not found");

        if (invoice.Status == "paid")
            throw new InvalidOperationException("Invoice is already paid");

        if (invoice.Status == "void" || invoice.Status == "cancelled")
            throw new InvalidOperationException($"Cannot record payment for a {invoice.Status} invoice");

        // Receipt numbers follow the configured receipt convention/sequence (org-wide).
        var receiptOrgId = await ResolveOrganizationIdAsync(_tenantContext.StationId, ct);
        var receiptNo = await _documentNumberService.GenerateNumberAsync(
            receiptOrgId, null, DocumentTypes.Receipt);

        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            ReceiptNo = receiptNo,
            InvoiceId = id,
            AmountPaid = amountPaid,
            Currency = invoice.Currency,
            PaymentMethod = channel,
            TransactionReference = reference,
            Notes = notes,
            IdempotencyKey = Guid.NewGuid(),
            ReceivedById = userId == Guid.Empty ? null : userId,
            PaymentChannel = channel,
            PaymentDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Receipts.Add(receipt);

        invoice.Status = "paid";
        invoice.TreasuryIntentStatus = "succeeded";
        invoice.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return (await GetByIdAsync(id, ct))!;
    }

    public async Task<string> GenerateInvoiceNumberAsync(CancellationToken ct = default)
    {
        // Delegate to the centralized, atomic, monotonic document-numbering service
        // (backed by document_sequences). A COUNT-based scheme re-issues a number after an
        // invoice is deleted, which collides with eCitizen/Pesaflow (bill refs are retained
        // permanently) → "BillRefNumber already taken". The sequence only ever increments.
        var orgId = await ResolveOrganizationIdAsync(_tenantContext.StationId, ct);
        return await _documentNumberService.GenerateNumberAsync(orgId, null, DocumentTypes.Invoice);
    }

    private async Task<string> ResolveCheckoutModeAsync(CancellationToken ct)
    {
        var env = await _context.IntegrationConfigs
            .Where(c => c.ProviderName == "ecitizen_pesaflow" && c.IsActive)
            .Select(c => c.Environment)
            .FirstOrDefaultAsync(ct);
        return (env?.ToLowerInvariant() == "production" || env?.ToLowerInvariant() == "live")
            ? "redirect" : "iframe";
    }

    private InvoiceDto MapToDto(Invoice invoice, string checkoutMode = "iframe")
    {
        var amountPaid = invoice.Receipts?.Where(r => r.DeletedAt == null).Sum(r => r.AmountPaid) ?? 0;
        var paidAt = invoice.Receipts?
            .Where(r => r.DeletedAt == null)
            .OrderByDescending(r => r.PaymentDate)
            .Select(r => (DateTime?)r.PaymentDate)
            .FirstOrDefault();

        return new InvoiceDto
        {
            Id = invoice.Id,
            InvoiceNo = invoice.InvoiceNo,
            CaseRegisterId = invoice.CaseRegisterId,
            CaseNo = invoice.CaseRegister?.CaseNo,
            ProsecutionCaseId = invoice.ProsecutionCaseId,
            ProsecutionCertificateNo = invoice.ProsecutionCase?.CertificateNo,
            WeighingId = invoice.WeighingId,
            WeighingTicketNo = invoice.Weighing?.TicketNumber,
            AmountDue = invoice.AmountDue,
            AmountPaid = amountPaid,
            BalanceRemaining = invoice.AmountDue - amountPaid,
            Currency = invoice.Currency,
            Status = invoice.Status,
            GeneratedAt = invoice.GeneratedAt,
            DueDate = invoice.DueDate,
            PaidAt = paidAt,
            PesaflowInvoiceNumber = invoice.PesaflowInvoiceNumber,
            PesaflowPaymentReference = invoice.PesaflowPaymentReference,
            PesaflowPaymentLink = invoice.PesaflowPaymentLink,
            PesaflowGatewayFee = invoice.PesaflowGatewayFee,
            PesaflowAmountNet = invoice.PesaflowAmountNet,
            PesaflowTotalAmount = invoice.PesaflowTotalAmount,
            PesaflowSyncStatus = invoice.PesaflowSyncStatus,
            PesaflowCheckoutMode = checkoutMode,
            InvoiceType = invoice.InvoiceType ?? "enforcement_fine",
            TreasuryIntentId = invoice.TreasuryIntentId,
            TreasuryIntentStatus = invoice.TreasuryIntentStatus,
            TreasuryPaymentUrl = !string.IsNullOrWhiteSpace(invoice.TreasuryIntentId)
                ? $"{_configuration["Treasury:PayPortalBaseUrl"] ?? "https://books.codevertexitsolutions.com/pay"}?intent_id={invoice.TreasuryIntentId}"
                : null,
            CreatedAt = invoice.CreatedAt,
            UpdatedAt = invoice.UpdatedAt
        };
    }
}
