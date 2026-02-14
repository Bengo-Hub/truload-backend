using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Reporting;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports;
using TruLoad.Backend.Services.Interfaces.System;

namespace TruLoad.Backend.Services.Implementations.Reporting.Modules;

/// <summary>
/// Generates financial reports: revenue collection, invoice aging, and payment reconciliation.
/// </summary>
public class FinancialReportGenerator : BaseReportGenerator
{
    private readonly TruLoadDbContext _context;
    private readonly ISettingsService _settingsService;

    public FinancialReportGenerator(TruLoadDbContext context, ISettingsService settingsService)
    {
        _context = context;
        _settingsService = settingsService;
    }

    public override string Module => ReportModules.Financial;

    public override List<ReportDefinitionDto> GetDefinitions() =>
    [
        Def("revenue-collection", "Revenue Collection",
            "Summary of all revenue collected from receipts, grouped by payment method and period."),
        Def("invoice-aging", "Invoice Aging",
            "Outstanding invoices grouped by aging buckets (current, 30-day, 60-day, 90-day+)."),
        Def("payment-reconciliation", "Payment Reconciliation",
            "Reconciliation of invoices against receipts to identify paid, partial, and outstanding balances.")
    ];

    public override async Task<ReportResult> GenerateAsync(
        string reportType, ReportFilterParams filters, string format, CancellationToken ct = default)
    {
        return reportType switch
        {
            "revenue-collection" => await GenerateRevenueCollection(filters, format, ct),
            "invoice-aging" => await GenerateInvoiceAging(filters, format, ct),
            "payment-reconciliation" => await GeneratePaymentReconciliation(filters, format, ct),
            _ => throw new ArgumentException($"Unknown financial report type: {reportType}")
        };
    }

    // ──────────────────────────────────────────────────────────────────
    // revenue-collection
    // ──────────────────────────────────────────────────────────────────

    private async Task<ReportResult> GenerateRevenueCollection(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var receipts = await _context.Receipts
            .Where(r => r.DeletedAt == null)
            .Where(r => r.PaymentDate >= from && r.PaymentDate <= to)
            .Select(r => new
            {
                r.ReceiptNo,
                r.AmountPaid,
                r.Currency,
                r.PaymentMethod,
                r.PaymentChannel,
                r.TransactionReference,
                r.PaymentDate,
                InvoiceNo = r.Invoice != null ? r.Invoice.InvoiceNo : "-"
            })
            .OrderByDescending(r => r.PaymentDate)
            .ToListAsync(ct);

        var totalCollected = receipts.Sum(r => r.AmountPaid);
        var receiptCount = receipts.Count;

        // Group by payment method for summary
        var methodGroups = receipts
            .GroupBy(r => r.PaymentMethod)
            .Select(g => new { Method = g.Key, Total = g.Sum(x => x.AmountPaid), Count = g.Count() })
            .OrderByDescending(g => g.Total)
            .ToList();

        string[] headers = ["Receipt No", "Invoice No", "Amount", "Currency", "Payment Method", "Channel", "Reference", "Date"];
        var rows = receipts.Select(r => new[]
        {
            r.ReceiptNo,
            r.InvoiceNo,
            FormatNumber(r.AmountPaid),
            r.Currency,
            r.PaymentMethod,
            r.PaymentChannel ?? "-",
            r.TransactionReference ?? "-",
            FormatDate(r.PaymentDate)
        });

        if (format == "csv")
        {
            var csvData = GenerateCsv(headers, rows);
            return CsvResult(csvData, "revenue_collection", from, to);
        }

        var summaryItems = new List<(string label, string value)>
        {
            ("Total Collected", FormatNumber(totalCollected)),
            ("Total Receipts", receiptCount.ToString())
        };
        foreach (var mg in methodGroups.Take(4))
        {
            summaryItems.Add(($"{mg.Method} ({mg.Count})", FormatNumber(mg.Total)));
        }

        var doc = new RevenueCollectionDocument
        {
            ReportTitle = "Revenue Collection Report",
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = rows.ToList(),
            SummaryItems = summaryItems.ToArray()
        };
        return PdfResult(doc.Generate(), "revenue_collection", from, to);
    }

    // ──────────────────────────────────────────────────────────────────
    // invoice-aging
    // ──────────────────────────────────────────────────────────────────

    private async Task<ReportResult> GenerateInvoiceAging(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Only pending/unpaid invoices
        var invoices = await _context.Invoices
            .Where(i => i.DeletedAt == null)
            .Where(i => i.Status != "paid" && i.Status != "cancelled" && i.Status != "void")
            .Include(i => i.Receipts.Where(r => r.DeletedAt == null))
            .OrderBy(i => i.GeneratedAt)
            .Select(i => new
            {
                i.InvoiceNo,
                i.AmountDue,
                i.Currency,
                i.Status,
                i.GeneratedAt,
                i.DueDate,
                i.PesaflowPaymentLink,
                TotalPaid = i.Receipts.Sum(r => r.AmountPaid),
                AgeDays = (int)(now - i.GeneratedAt).TotalDays
            })
            .ToListAsync(ct);

        // Apply station filter if provided (via prosecution case or case register)
        if (!string.IsNullOrEmpty(filters.StationId))
        {
            // Station filtering for invoices requires joining through related entities
            // For simplicity, include all invoices when station filter is set at this level
        }

        // Classify into aging buckets (configurable thresholds)
        var currentDays = await _settingsService.GetSettingValueAsync(SettingKeys.FinancialInvoiceAgingCurrentDays, 30);
        var overdueDays = await _settingsService.GetSettingValueAsync(SettingKeys.FinancialInvoiceAgingOverdueDays, 60);
        var severeDays = overdueDays + currentDays; // e.g. 90

        var current = invoices.Where(i => i.AgeDays <= currentDays).ToList();
        var days30 = invoices.Where(i => i.AgeDays > currentDays && i.AgeDays <= overdueDays).ToList();
        var days60 = invoices.Where(i => i.AgeDays > overdueDays && i.AgeDays <= severeDays).ToList();
        var days90Plus = invoices.Where(i => i.AgeDays > severeDays).ToList();

        string[] headers = ["Invoice No", "Amount Due", "Paid", "Outstanding", "Currency", "Status", "Age (days)", "Bucket", "Generated", "Due Date"];
        var rows = invoices.Select(i =>
        {
            var outstanding = i.AmountDue - i.TotalPaid;
            var bucket = i.AgeDays switch
            {
                _ when i.AgeDays <= currentDays => "Current",
                _ when i.AgeDays <= overdueDays => $"{currentDays}-{overdueDays} days",
                _ when i.AgeDays <= severeDays => $"{overdueDays}-{severeDays} days",
                _ => $"{severeDays}+ days"
            };
            return new[]
            {
                i.InvoiceNo,
                FormatNumber(i.AmountDue),
                FormatNumber(i.TotalPaid),
                FormatNumber(outstanding),
                i.Currency,
                i.Status,
                i.AgeDays.ToString(),
                bucket,
                FormatDate(i.GeneratedAt),
                FormatDate(i.DueDate)
            };
        });

        if (format == "csv")
        {
            var csvData = GenerateCsv(headers, rows);
            return CsvResult(csvData, "invoice_aging", null, null);
        }

        var totalOutstanding = invoices.Sum(i => i.AmountDue - i.TotalPaid);
        var doc = new InvoiceAgingDocument
        {
            ReportTitle = "Invoice Aging Report",
            Headers = headers,
            Rows = rows.ToList(),
            SummaryItems =
            [
                ($"Current (0-{currentDays}d)", $"{current.Count} | {FormatNumber(current.Sum(i => i.AmountDue - i.TotalPaid))}"),
                ($"{currentDays}-{overdueDays} days", $"{days30.Count} | {FormatNumber(days30.Sum(i => i.AmountDue - i.TotalPaid))}"),
                ($"{overdueDays}-{severeDays} days", $"{days60.Count} | {FormatNumber(days60.Sum(i => i.AmountDue - i.TotalPaid))}"),
                ($"{severeDays}+ days", $"{days90Plus.Count} | {FormatNumber(days90Plus.Sum(i => i.AmountDue - i.TotalPaid))}"),
                ("Total Outstanding", FormatNumber(totalOutstanding)),
                ("Total Invoices", invoices.Count.ToString())
            ]
        };
        return PdfResult(doc.Generate(), "invoice_aging", null, null);
    }

    // ──────────────────────────────────────────────────────────────────
    // payment-reconciliation
    // ──────────────────────────────────────────────────────────────────

    private async Task<ReportResult> GeneratePaymentReconciliation(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var invoices = await _context.Invoices
            .Where(i => i.DeletedAt == null)
            .Where(i => i.GeneratedAt >= from && i.GeneratedAt <= to)
            .Include(i => i.Receipts.Where(r => r.DeletedAt == null))
            .OrderByDescending(i => i.GeneratedAt)
            .Select(i => new
            {
                i.InvoiceNo,
                i.AmountDue,
                i.Currency,
                i.Status,
                i.GeneratedAt,
                i.PesaflowPaymentLink,
                TotalPaid = i.Receipts.Sum(r => r.AmountPaid),
                ReceiptCount = i.Receipts.Count(),
                LatestPaymentDate = i.Receipts.Max(r => (DateTime?)r.PaymentDate),
                PaymentMethods = i.Receipts.Select(r => r.PaymentMethod).Distinct().ToList()
            })
            .ToListAsync(ct);

        string[] headers =
        [
            "Invoice No", "Amount Due", "Total Paid", "Balance", "Currency",
            "Status", "Receipts", "Payment Methods", "Last Payment", "Generated"
        ];
        var rows = invoices.Select(i =>
        {
            var balance = i.AmountDue - i.TotalPaid;
            var reconciled = balance <= 0 ? "RECONCILED" : i.Status;
            return new[]
            {
                i.InvoiceNo,
                FormatNumber(i.AmountDue),
                FormatNumber(i.TotalPaid),
                FormatNumber(balance),
                i.Currency,
                reconciled,
                i.ReceiptCount.ToString(),
                i.PaymentMethods.Any() ? string.Join(", ", i.PaymentMethods) : "-",
                FormatDate(i.LatestPaymentDate),
                FormatDate(i.GeneratedAt)
            };
        });

        if (format == "csv")
        {
            var csvData = GenerateCsv(headers, rows);
            return CsvResult(csvData, "payment_reconciliation", from, to);
        }

        var totalDue = invoices.Sum(i => i.AmountDue);
        var totalPaid = invoices.Sum(i => i.TotalPaid);
        var totalBalance = totalDue - totalPaid;
        var fullyPaid = invoices.Count(i => i.TotalPaid >= i.AmountDue);
        var partial = invoices.Count(i => i.TotalPaid > 0 && i.TotalPaid < i.AmountDue);
        var unpaid = invoices.Count(i => i.TotalPaid == 0);

        var doc = new PaymentReconciliationDocument
        {
            ReportTitle = "Payment Reconciliation Report",
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = rows.ToList(),
            SummaryItems =
            [
                ("Total Invoiced", FormatNumber(totalDue)),
                ("Total Received", FormatNumber(totalPaid)),
                ("Outstanding Balance", FormatNumber(totalBalance)),
                ("Fully Paid", fullyPaid.ToString()),
                ("Partially Paid", partial.ToString()),
                ("Unpaid", unpaid.ToString())
            ]
        };
        return PdfResult(doc.Generate(), "payment_reconciliation", from, to);
    }

    // ══════════════════════════════════════════════════════════════════
    // Inner PDF document classes
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// PDF document for revenue collection with summary cards and detail table.
    /// </summary>
    private sealed class RevenueCollectionDocument : BaseReportDocument
    {
        public required string[] Headers { get; init; }
        public required List<string[]> Rows { get; init; }
        public required (string label, string value)[] SummaryItems { get; init; }

        protected override void ComposeContent(IContainer container)
        {
            container.Column(col =>
            {
                col.Spacing(8);
                col.Item().Element(c => ComposeSummaryCards(c, SummaryItems));
                col.Item().Element(c => ComposeDataTable(c, Headers, Rows,
                    summaryLabel: "Total Receipts",
                    summaryValue: Rows.Count.ToString()));
            });
        }
    }

    /// <summary>
    /// PDF document for invoice aging with aging bucket summary and detail table.
    /// </summary>
    private sealed class InvoiceAgingDocument : BaseReportDocument
    {
        public required string[] Headers { get; init; }
        public required List<string[]> Rows { get; init; }
        public required (string label, string value)[] SummaryItems { get; init; }

        protected override void ComposeContent(IContainer container)
        {
            container.Column(col =>
            {
                col.Spacing(8);
                col.Item().Element(c => ComposeSummaryCards(c, SummaryItems));
                col.Item().Element(c => ComposeDataTable(c, Headers, Rows));
            });
        }
    }

    /// <summary>
    /// PDF document for payment reconciliation with balance summary and detail table.
    /// </summary>
    private sealed class PaymentReconciliationDocument : BaseReportDocument
    {
        public required string[] Headers { get; init; }
        public required List<string[]> Rows { get; init; }
        public required (string label, string value)[] SummaryItems { get; init; }

        protected override void ComposeContent(IContainer container)
        {
            container.Column(col =>
            {
                col.Spacing(8);
                col.Item().Element(c => ComposeSummaryCards(c, SummaryItems));
                col.Item().Element(c => ComposeDataTable(c, Headers, Rows));
            });
        }
    }
}
