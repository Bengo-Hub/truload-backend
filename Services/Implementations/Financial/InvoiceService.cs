using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Financial;
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

    public async Task<IEnumerable<InvoiceDto>> SearchAsync(InvoiceSearchCriteria criteria, CancellationToken ct = default)
    {
        var query = _context.Invoices
            .Include(i => i.CaseRegister)
            .Include(i => i.ProsecutionCase)
            .Include(i => i.Receipts)
            .Where(i => i.DeletedAt == null)
            .AsQueryable();

        if (criteria.CaseRegisterId.HasValue)
            query = query.Where(i => i.CaseRegisterId == criteria.CaseRegisterId.Value);

        if (criteria.ProsecutionCaseId.HasValue)
            query = query.Where(i => i.ProsecutionCaseId == criteria.ProsecutionCaseId.Value);

        if (!string.IsNullOrWhiteSpace(criteria.Status))
            query = query.Where(i => i.Status == criteria.Status);

        if (criteria.GeneratedFrom.HasValue)
            query = query.Where(i => i.GeneratedAt >= criteria.GeneratedFrom.Value);

        if (criteria.GeneratedTo.HasValue)
            query = query.Where(i => i.GeneratedAt <= criteria.GeneratedTo.Value);

        if (criteria.DueFrom.HasValue)
            query = query.Where(i => i.DueDate >= criteria.DueFrom.Value);

        if (criteria.DueTo.HasValue)
            query = query.Where(i => i.DueDate <= criteria.DueTo.Value);

        var invoices = await query
            .OrderByDescending(i => i.GeneratedAt)
            .Skip(criteria.Skip)
            .Take(criteria.PageSize)
            .ToListAsync(ct);

        return invoices.Select(MapToDto);
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

    public async Task<Dictionary<string, object>> GetStatisticsAsync(CancellationToken ct = default)
    {
        var stats = new Dictionary<string, object>();

        var total = await _context.Invoices.CountAsync(i => i.DeletedAt == null, ct);
        stats["total"] = total;

        var pending = await _context.Invoices.CountAsync(i => i.Status == "pending" && i.DeletedAt == null, ct);
        stats["pending"] = pending;

        var paid = await _context.Invoices.CountAsync(i => i.Status == "paid" && i.DeletedAt == null, ct);
        stats["paid"] = paid;

        var overdue = await _context.Invoices.CountAsync(i => i.Status == "pending"
            && i.DueDate < DateTime.UtcNow
            && i.DeletedAt == null, ct);
        stats["overdue"] = overdue;

        var totalAmountDue = await _context.Invoices
            .Where(i => i.Status == "pending" && i.DeletedAt == null)
            .SumAsync(i => i.AmountDue, ct);
        stats["totalAmountDue"] = totalAmountDue;

        return stats;
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
            PesaflowInvoiceNumber = invoice.PesaflowInvoiceNumber,
            PesaflowPaymentReference = invoice.PesaflowPaymentReference,
            PesaflowCheckoutUrl = invoice.PesaflowCheckoutUrl,
            CreatedAt = invoice.CreatedAt,
            UpdatedAt = invoice.UpdatedAt
        };
    }
}
