using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Financial;
using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.Models.Financial;
using TruLoad.Backend.Services.Interfaces.Financial;

namespace TruLoad.Backend.Services.Implementations.Financial;

/// <summary>
/// Service implementation for invoice management.
/// </summary>
public class InvoiceService : IInvoiceService
{
    private readonly TruLoadDbContext _context;

    public InvoiceService(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<InvoiceDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await _context.Invoices
            .Include(i => i.CaseRegister)
            .Include(i => i.ProsecutionCase)
            .Include(i => i.Weighing)
            .Include(i => i.Receipts)
            .FirstOrDefaultAsync(i => i.Id == id && i.DeletedAt == null, ct);

        return invoice == null ? null : MapToDto(invoice);
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

        return invoices.Select(MapToDto);
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

        return PagedResponse<InvoiceDto>.Create(
            invoices.Select(MapToDto).ToList(),
            totalCount,
            criteria.PageNumber,
            criteria.PageSize);
    }

    public async Task<InvoiceDto> GenerateInvoiceAsync(Guid prosecutionCaseId, Guid userId, CancellationToken ct = default)
    {
        var prosecutionCase = await _context.ProsecutionCases
            .Include(p => p.Act)
            .FirstOrDefaultAsync(p => p.Id == prosecutionCaseId && p.DeletedAt == null, ct)
            ?? throw new InvalidOperationException($"Prosecution case {prosecutionCaseId} not found");

        // Check for existing pending invoice
        var existingInvoice = await _context.Invoices
            .FirstOrDefaultAsync(i => i.ProsecutionCaseId == prosecutionCaseId
                && i.Status == "pending"
                && i.DeletedAt == null, ct);

        if (existingInvoice != null)
            throw new InvalidOperationException($"Pending invoice {existingInvoice.InvoiceNo} already exists for this prosecution case");

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

        await _context.SaveChangesAsync(ct);

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
            var to = DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc);
            invoices = invoices.Where(i => i.GeneratedAt <= to);
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
            receiptsBase = receiptsBase.Where(r => r.PaymentDate <= DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc));
        var totalAmountPaid = await receiptsBase.SumAsync(r => r.AmountPaid, ct);

        // Per-currency breakdown for pending invoices
        var pendingInvoices = invoices.Where(i => i.Status == "pending");
        var totalAmountDueKes = await pendingInvoices
            .Where(i => i.Currency == "KES")
            .SumAsync(i => i.AmountDue, ct);
        var totalAmountDueUsd = await pendingInvoices
            .Where(i => i.Currency == "USD")
            .SumAsync(i => i.AmountDue, ct);

        // Per-currency breakdown for receipts
        var receipts = receiptsBase;
        var totalAmountPaidKes = await receipts
            .Where(r => r.Currency == "KES")
            .SumAsync(r => r.AmountPaid, ct);
        var totalAmountPaidUsd = await receipts
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

    public async Task<string> GenerateInvoiceNumberAsync(CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var count = await _context.Invoices
            .CountAsync(i => i.CreatedAt.Year == year, ct);

        return $"INV-{year}-{(count + 1):D6}";
    }

    private InvoiceDto MapToDto(Invoice invoice)
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
            InvoiceType = invoice.InvoiceType ?? "enforcement_fine",
            TreasuryIntentId = invoice.TreasuryIntentId,
            TreasuryIntentStatus = invoice.TreasuryIntentStatus,
            // Generate treasury pay URL for commercial invoices with a payment intent
            TreasuryPaymentUrl = !string.IsNullOrWhiteSpace(invoice.TreasuryIntentId)
                ? $"https://books.codevertexitsolutions.com/pay?intent_id={invoice.TreasuryIntentId}"
                : null,
            CreatedAt = invoice.CreatedAt,
            UpdatedAt = invoice.UpdatedAt
        };
    }
}
