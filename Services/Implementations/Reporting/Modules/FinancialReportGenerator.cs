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

    // =====================================================================
    // Structured custom-report-builder column catalogs. Each Key MUST match the literal header
    // text built in that report's generation method (see BaseReportGenerator.ApplyColumnSelection
    // - matches by header string). None of these reports build a header via string interpolation
    // with a runtime value, so all columns are included in the catalog.
    // =====================================================================

    private static readonly List<ReportColumnDefinition> RevenueCollectionColumns =
    [
        new() { Key = "Receipt No", Label = "Receipt Number" },
        new() { Key = "Invoice No", Label = "Invoice Number" },
        new() { Key = "Amount", Label = "Amount" },
        new() { Key = "Currency", Label = "Currency" },
        new() { Key = "Payment Method", Label = "Payment Method" },
        new() { Key = "Channel", Label = "Payment Channel" },
        new() { Key = "Reference", Label = "Transaction Reference" },
        new() { Key = "Date", Label = "Payment Date" }
    ];

    private static readonly List<ReportFilterDefinition> FinancialGeoFilters =
    [
        new() { Key = "countyId", Label = "County", Kind = "county" },
        new() { Key = "subcountyId", Label = "Sub County", Kind = "subcounty" },
        new() { Key = "roadId", Label = "Road", Kind = "road" }
    ];

    private static readonly List<ReportColumnDefinition> InvoiceAgingColumns =
    [
        new() { Key = "Invoice No", Label = "Invoice Number" },
        new() { Key = "Amount Due", Label = "Amount Due" },
        new() { Key = "Paid", Label = "Amount Paid" },
        new() { Key = "Outstanding", Label = "Outstanding Balance" },
        new() { Key = "Currency", Label = "Currency" },
        new() { Key = "Status", Label = "Status" },
        new() { Key = "Age (days)", Label = "Age (Days)" },
        new() { Key = "Bucket", Label = "Aging Bucket" },
        new() { Key = "Generated", Label = "Generated Date" },
        new() { Key = "Due Date", Label = "Due Date" }
    ];

    private static readonly List<ReportColumnDefinition> PaymentReconciliationColumns =
    [
        new() { Key = "Invoice No", Label = "Invoice Number" },
        new() { Key = "Amount Due", Label = "Amount Due" },
        new() { Key = "Total Paid", Label = "Total Paid" },
        new() { Key = "Balance", Label = "Balance" },
        new() { Key = "Currency", Label = "Currency" },
        new() { Key = "Status", Label = "Reconciliation Status" },
        new() { Key = "Receipts", Label = "Receipt Count" },
        new() { Key = "Payment Methods", Label = "Payment Methods" },
        new() { Key = "Last Payment", Label = "Last Payment Date" },
        new() { Key = "Generated", Label = "Generated Date" }
    ];

    public FinancialReportGenerator(TruLoadDbContext context, ISettingsService settingsService)
    {
        _context = context;
        _settingsService = settingsService;
    }

    public override string Module => ReportModules.Financial;

    public override List<ReportDefinitionDto> GetDefinitions() =>
    [
        Def("revenue-collection", "Revenue Collection",
            "Summary of all revenue collected from receipts, grouped by payment method and period.",
            columns: RevenueCollectionColumns, filters: FinancialGeoFilters),
        Def("invoice-aging", "Invoice Aging",
            "Outstanding invoices grouped by aging buckets (current, 30-day, 60-day, 90-day+).",
            columns: InvoiceAgingColumns, filters: FinancialGeoFilters),
        Def("payment-reconciliation", "Payment Reconciliation",
            "Reconciliation of invoices against receipts to identify paid, partial, and outstanding balances.",
            columns: PaymentReconciliationColumns, filters: FinancialGeoFilters)
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

        var receiptQuery = _context.Receipts
            .Where(r => r.DeletedAt == null)
            .Where(r => r.PaymentDate >= from && r.PaymentDate <= to);

        // County/Sub County via Receipt -> Invoice -> CaseRegister (which carries its own direct
        // CountyId/SubcountyId FKs) - same pattern as invoice-aging.
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            receiptQuery = receiptQuery.Where(r => r.Invoice != null && r.Invoice.CaseRegister != null && r.Invoice.CaseRegister.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            receiptQuery = receiptQuery.Where(r => r.Invoice != null && r.Invoice.CaseRegister != null && r.Invoice.CaseRegister.SubcountyId == subcountyId);
        if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
            receiptQuery = receiptQuery.Where(r => r.Invoice != null && r.Invoice.CaseRegister != null && r.Invoice.CaseRegister.RoadId == roadId);

        var receipts = await receiptQuery
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

        var outputHeaders = headers;
        List<string[]> outputRows = rows.ToList();

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, rows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "revenue_collection", from, to);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Revenue Collection Report", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "revenue_collection", from, to);

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
            Headers = outputHeaders,
            Rows = outputRows,
            SummaryItems = summaryItems.ToArray()
        };
        return PdfResult(doc, filters, "revenue_collection", from, to);
    }

    // ──────────────────────────────────────────────────────────────────
    // invoice-aging
    // ──────────────────────────────────────────────────────────────────

    private async Task<ReportResult> GenerateInvoiceAging(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Only pending/unpaid invoices
        var invoiceQuery = _context.Invoices
            .Where(i => i.DeletedAt == null)
            .Where(i => i.Status != "paid" && i.Status != "cancelled" && i.Status != "void");

        // County/Sub County via the linked CaseRegister's own direct CountyId/SubcountyId FKs (no
        // station join needed - CaseRegister already carries these). Station filtering is a
        // separate, pre-existing gap (would need a further join through WeighingTransaction) -
        // not attempted here.
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            invoiceQuery = invoiceQuery.Where(i => i.CaseRegister != null && i.CaseRegister.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            invoiceQuery = invoiceQuery.Where(i => i.CaseRegister != null && i.CaseRegister.SubcountyId == subcountyId);
        if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
            invoiceQuery = invoiceQuery.Where(i => i.CaseRegister != null && i.CaseRegister.RoadId == roadId);

        var invoices = await invoiceQuery
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

        var outputHeaders = headers;
        List<string[]> outputRows = rows.ToList();
        int? effectiveStatusColumnIndex = 5;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, rows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
            var idx = Array.IndexOf(outputHeaders, "Status");
            effectiveStatusColumnIndex = idx >= 0 ? idx : null;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "invoice_aging", null, null);

        if (format == "xlsx")
        {
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Invoice Aging Report", Headers = outputHeaders, Rows = outputRows,
                ConditionalStatusColumnIndex = effectiveStatusColumnIndex,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "invoice_aging", null, null);
        }

        var totalOutstanding = invoices.Sum(i => i.AmountDue - i.TotalPaid);
        var doc = new InvoiceAgingDocument
        {
            ReportTitle = "Invoice Aging Report",
            Headers = outputHeaders,
            Rows = outputRows,
            StatusColumnIndex = effectiveStatusColumnIndex,
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
        return PdfResult(doc, filters, "invoice_aging", null, null);
    }

    // ──────────────────────────────────────────────────────────────────
    // payment-reconciliation
    // ──────────────────────────────────────────────────────────────────

    private async Task<ReportResult> GeneratePaymentReconciliation(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var reconciliationQuery = _context.Invoices
            .Where(i => i.DeletedAt == null)
            .Where(i => i.GeneratedAt >= from && i.GeneratedAt <= to);

        // County/Sub County via the invoice's linked CaseRegister - same pattern as invoice-aging.
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            reconciliationQuery = reconciliationQuery.Where(i => i.CaseRegister != null && i.CaseRegister.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            reconciliationQuery = reconciliationQuery.Where(i => i.CaseRegister != null && i.CaseRegister.SubcountyId == subcountyId);
        if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
            reconciliationQuery = reconciliationQuery.Where(i => i.CaseRegister != null && i.CaseRegister.RoadId == roadId);

        var invoices = await reconciliationQuery
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

        var outputHeaders = headers;
        List<string[]> outputRows = rows.ToList();
        int? effectiveStatusColumnIndex = 5;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, rows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
            var idx = Array.IndexOf(outputHeaders, "Status");
            effectiveStatusColumnIndex = idx >= 0 ? idx : null;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "payment_reconciliation", from, to);

        if (format == "xlsx")
        {
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Payment Reconciliation Report", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                ConditionalStatusColumnIndex = effectiveStatusColumnIndex,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "payment_reconciliation", from, to);
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
            Headers = outputHeaders,
            Rows = outputRows,
            StatusColumnIndex = effectiveStatusColumnIndex,
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
        return PdfResult(doc, filters, "payment_reconciliation", from, to);
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
        public int? StatusColumnIndex { get; init; }

        protected override void ComposeContent(IContainer container)
        {
            container.Column(col =>
            {
                col.Spacing(8);
                col.Item().Element(c => ComposeSummaryCards(c, SummaryItems));
                col.Item().Element(c => ComposeDataTable(c, Headers, Rows, conditionalStatusColumnIndex: StatusColumnIndex));
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
        public int? StatusColumnIndex { get; init; }

        protected override void ComposeContent(IContainer container)
        {
            container.Column(col =>
            {
                col.Spacing(8);
                col.Item().Element(c => ComposeSummaryCards(c, SummaryItems));
                col.Item().Element(c => ComposeDataTable(c, Headers, Rows, conditionalStatusColumnIndex: StatusColumnIndex));
            });
        }
    }
}
