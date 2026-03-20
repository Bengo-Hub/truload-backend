using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Reporting;
using TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports;

namespace TruLoad.Backend.Services.Implementations.Reporting.Modules;

/// <summary>
/// Generates prosecution-related reports: statistics, court calendar, daily charged,
/// payment list, court fines, and habitual offenders.
/// </summary>
public class ProsecutionReportGenerator : BaseReportGenerator
{
    private readonly TruLoadDbContext _context;

    public ProsecutionReportGenerator(TruLoadDbContext context)
    {
        _context = context;
    }

    public override string Module => ReportModules.Prosecution;

    public override List<ReportDefinitionDto> GetDefinitions() =>
    [
        Def("prosecution-statistics", "Prosecution Statistics",
            "Summary statistics of prosecution cases including charge basis breakdown and fees collected."),
        Def("court-calendar", "Court Calendar",
            "Upcoming and past court hearings with case references, dates, and presiding officers."),
        Def("daily-charged", "Daily Charged Vehicles",
            "List of vehicles charged per day with overload details, fees, and officer information."),
        Def("payment-list", "Prosecution Payment List",
            "Prosecution cases with associated invoice and payment status for revenue tracking."),
        Def("court-fines", "Court Fines Summary",
            "Summary of court-imposed fines aggregated by status and period."),
        Def("habitual-offenders", "Habitual Offenders",
            "Vehicles with multiple prosecution cases within 12 months flagged as repeat offenders.")
    ];

    public override async Task<ReportResult> GenerateAsync(
        string reportType, ReportFilterParams filters, string format, CancellationToken ct = default)
    {
        return reportType switch
        {
            "prosecution-statistics" => await GenerateProsecutionStatistics(filters, format, ct),
            "court-calendar" => await GenerateCourtCalendar(filters, format, ct),
            "daily-charged" => await GenerateDailyCharged(filters, format, ct),
            "payment-list" => await GeneratePaymentList(filters, format, ct),
            "court-fines" => await GenerateCourtFines(filters, format, ct),
            "habitual-offenders" => await GenerateHabitualOffenders(filters, format, ct),
            _ => throw new ArgumentException($"Unknown prosecution report type: {reportType}")
        };
    }

    // ──────────────────────────────────────────────────────────────────
    // prosecution-statistics
    // ──────────────────────────────────────────────────────────────────

    private async Task<ReportResult> GenerateProsecutionStatistics(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var query = _context.ProsecutionCases
            .Where(p => p.DeletedAt == null)
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to);

        if (!string.IsNullOrEmpty(filters.Status))
            query = query.Where(p => p.Status == filters.Status);

        var cases = await query
            .Select(p => new
            {
                p.Status,
                p.BestChargeBasis,
                p.TotalFeeKes,
                p.TotalFeeUsd,
                p.GvwOverloadKg,
                p.MaxAxleOverloadKg,
                p.OffenseCount,
                p.PenaltyMultiplier
            })
            .ToListAsync(ct);

        var totalCases = cases.Count;
        var totalKes = cases.Sum(c => c.TotalFeeKes);
        var totalUsd = cases.Sum(c => c.TotalFeeUsd);
        var gvwBased = cases.Count(c => c.BestChargeBasis == "gvw");
        var axleBased = cases.Count(c => c.BestChargeBasis == "axle");
        var avgOverloadKg = totalCases > 0
            ? cases.Average(c => (decimal)Math.Max(c.GvwOverloadKg, c.MaxAxleOverloadKg))
            : 0m;
        var repeatOffenders = cases.Count(c => c.OffenseCount > 1);

        var statusGroups = cases
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), FeeKes = g.Sum(x => x.TotalFeeKes) })
            .OrderByDescending(g => g.Count)
            .ToList();

        string[] headers = ["Status", "Cases", "Total Fee (KES)", "% of Total"];
        var rows = statusGroups.Select(g => new[]
        {
            g.Status,
            g.Count.ToString(),
            FormatKes(g.FeeKes),
            totalCases > 0 ? $"{(decimal)g.Count / totalCases * 100:F1}%" : "0%"
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, rows), "prosecution_statistics", from, to);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Prosecution Statistics Report", headers, rows, from, to), "prosecution_statistics", from, to);

        var doc = new ProsecutionStatisticsDocument
        {
            ReportTitle = "Prosecution Statistics Report",
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = rows.ToList(),
            SummaryItems =
            [
                ("Total Cases", totalCases.ToString()),
                ("GVW-Based", gvwBased.ToString()),
                ("Axle-Based", axleBased.ToString()),
                ("Repeat Offenders", repeatOffenders.ToString()),
                ("Total Fees (KES)", FormatKes(totalKes)),
                ("Avg Overload (kg)", FormatNumber(avgOverloadKg))
            ]
        };
        return PdfResult(doc, filters, "prosecution_statistics", from, to);
    }

    // ──────────────────────────────────────────────────────────────────
    // court-calendar
    // ──────────────────────────────────────────────────────────────────

    private async Task<ReportResult> GenerateCourtCalendar(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var hearings = await _context.CourtHearings
            .Where(h => h.DeletedAt == null)
            .Where(h => h.HearingDate >= from && h.HearingDate <= to)
            .Include(h => h.CaseRegister)
            .Include(h => h.HearingType)
            .Include(h => h.HearingStatus)
            .Include(h => h.HearingOutcome)
            .OrderBy(h => h.HearingDate)
            .ThenBy(h => h.HearingTime)
            .Select(h => new
            {
                CaseNo = h.CaseRegister != null ? h.CaseRegister.CaseNo : "-",
                h.HearingDate,
                h.HearingTime,
                HearingType = h.HearingType != null ? h.HearingType.Name : "-",
                Status = h.HearingStatus != null ? h.HearingStatus.Name : "-",
                Outcome = h.HearingOutcome != null ? h.HearingOutcome.Name : "-",
                h.PresidingOfficer,
                h.NextHearingDate
            })
            .ToListAsync(ct);

        string[] headers = ["Case No", "Hearing Date", "Time", "Type", "Status", "Outcome", "Presiding Officer", "Next Hearing"];
        var rows = hearings.Select(h => new[]
        {
            h.CaseNo,
            FormatDate(h.HearingDate),
            h.HearingTime?.ToString(@"hh\:mm") ?? "-",
            h.HearingType,
            h.Status,
            h.Outcome,
            h.PresidingOfficer ?? "-",
            FormatDate(h.NextHearingDate)
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, rows), "court_calendar", from, to);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Court Calendar", headers, rows, from, to), "court_calendar", from, to);

        var doc = new SimpleTableDocument
        {
            ReportTitle = "Court Calendar",
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = rows.ToList(),
            SummaryLabel = "Total Hearings",
            SummaryValue = hearings.Count.ToString()
        };
        return PdfResult(doc, filters, "court_calendar", from, to);
    }

    // ──────────────────────────────────────────────────────────────────
    // daily-charged
    // ──────────────────────────────────────────────────────────────────

    private async Task<ReportResult> GenerateDailyCharged(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var cases = await _context.ProsecutionCases
            .Where(p => p.DeletedAt == null)
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
            .Include(p => p.CaseRegister)
            .Include(p => p.Weighing)
            .Include(p => p.ProsecutionOfficer)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                CaseNo = p.CaseRegister != null ? p.CaseRegister.CaseNo : "-",
                VehicleRegNumber = p.Weighing != null ? p.Weighing.VehicleRegNumber : "-",
                WeighingTicketNo = p.Weighing != null ? p.Weighing.TicketNumber : "-",
                p.GvwOverloadKg,
                p.MaxAxleOverloadKg,
                p.BestChargeBasis,
                p.TotalFeeKes,
                p.TotalFeeUsd,
                p.Status,
                ProsecutionOfficerName = p.ProsecutionOfficer != null ? p.ProsecutionOfficer.FullName : "-",
                p.CreatedAt
            })
            .ToListAsync(ct);

        string[] headers =
        [
            "Date", "Case No", "Vehicle Reg", "Ticket No", "GVW Overload (kg)",
            "Max Axle Overload (kg)", "Charge Basis", "Fee (KES)", "Status", "Officer"
        ];
        var rows = cases.Select(c => new[]
        {
            FormatDate(c.CreatedAt),
            c.CaseNo,
            c.VehicleRegNumber,
            c.WeighingTicketNo,
            FormatNumber(c.GvwOverloadKg),
            FormatNumber(c.MaxAxleOverloadKg),
            c.BestChargeBasis.ToUpperInvariant(),
            FormatKes(c.TotalFeeKes),
            c.Status,
            c.ProsecutionOfficerName
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, rows), "daily_charged", from, to);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Daily Charged Vehicles", headers, rows, from, to), "daily_charged", from, to);

        var doc = new SimpleTableDocument
        {
            ReportTitle = "Daily Charged Vehicles",
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = rows.ToList(),
            SummaryLabel = "Total Charged",
            SummaryValue = $"{cases.Count} vehicles | {FormatKes(cases.Sum(c => c.TotalFeeKes))}"
        };
        return PdfResult(doc, filters, "daily_charged", from, to);
    }

    // ──────────────────────────────────────────────────────────────────
    // payment-list
    // ──────────────────────────────────────────────────────────────────

    private async Task<ReportResult> GeneratePaymentList(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var data = await _context.ProsecutionCases
            .Where(p => p.DeletedAt == null)
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
            .Include(p => p.CaseRegister)
            .Include(p => p.Weighing)
            .Include(p => p.Invoices.Where(i => i.DeletedAt == null))
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                CaseNo = p.CaseRegister != null ? p.CaseRegister.CaseNo : "-",
                VehicleRegNumber = p.Weighing != null ? p.Weighing.VehicleRegNumber : "-",
                p.TotalFeeKes,
                p.Status,
                InvoiceNo = p.Invoices.OrderByDescending(i => i.CreatedAt).Select(i => i.InvoiceNo).FirstOrDefault() ?? "-",
                InvoiceStatus = p.Invoices.OrderByDescending(i => i.CreatedAt).Select(i => i.Status).FirstOrDefault() ?? "-",
                PesaflowLink = p.Invoices.OrderByDescending(i => i.CreatedAt).Select(i => i.PesaflowPaymentLink).FirstOrDefault() ?? "-",
                p.CreatedAt
            })
            .ToListAsync(ct);

        string[] headers = ["Date", "Case No", "Vehicle Reg", "Fee (KES)", "Case Status", "Invoice No", "Invoice Status", "Pesaflow Link"];
        var rows = data.Select(d => new[]
        {
            FormatDate(d.CreatedAt),
            d.CaseNo,
            d.VehicleRegNumber,
            FormatKes(d.TotalFeeKes),
            d.Status,
            d.InvoiceNo,
            d.InvoiceStatus,
            d.PesaflowLink
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, rows), "prosecution_payment_list", from, to);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Prosecution Payment List", headers, rows, from, to), "prosecution_payment_list", from, to);

        var totalFees = data.Sum(d => d.TotalFeeKes);
        var paidCount = data.Count(d => d.InvoiceStatus == "paid");
        var doc = new SimpleTableDocument
        {
            ReportTitle = "Prosecution Payment List",
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = rows.ToList(),
            SummaryLabel = "Total Fees",
            SummaryValue = $"{FormatKes(totalFees)} | {paidCount}/{data.Count} paid"
        };
        return PdfResult(doc, filters, "prosecution_payment_list", from, to);
    }

    // ──────────────────────────────────────────────────────────────────
    // court-fines
    // ──────────────────────────────────────────────────────────────────

    private async Task<ReportResult> GenerateCourtFines(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var invoices = await _context.Invoices
            .Where(i => i.DeletedAt == null)
            .Where(i => i.ProsecutionCaseId != null)
            .Where(i => i.GeneratedAt >= from && i.GeneratedAt <= to)
            .Include(i => i.ProsecutionCase)
                .ThenInclude(p => p!.CaseRegister)
            .Include(i => i.Receipts.Where(r => r.DeletedAt == null))
            .OrderByDescending(i => i.GeneratedAt)
            .Select(i => new
            {
                CaseNo = i.ProsecutionCase != null && i.ProsecutionCase.CaseRegister != null
                    ? i.ProsecutionCase.CaseRegister.CaseNo : "-",
                i.InvoiceNo,
                i.AmountDue,
                i.Currency,
                i.Status,
                i.GeneratedAt,
                i.DueDate,
                TotalPaid = i.Receipts.Sum(r => r.AmountPaid)
            })
            .ToListAsync(ct);

        string[] headers = ["Case No", "Invoice No", "Amount Due", "Currency", "Status", "Total Paid", "Generated", "Due Date"];
        var rows = invoices.Select(i => new[]
        {
            i.CaseNo,
            i.InvoiceNo,
            FormatNumber(i.AmountDue),
            i.Currency,
            i.Status,
            FormatNumber(i.TotalPaid),
            FormatDate(i.GeneratedAt),
            FormatDate(i.DueDate)
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, rows), "court_fines", from, to);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Court Fines Summary", headers, rows, from, to), "court_fines", from, to);

        var totalDue = invoices.Sum(i => i.AmountDue);
        var totalPaid = invoices.Sum(i => i.TotalPaid);
        var doc = new SimpleTableDocument
        {
            ReportTitle = "Court Fines Summary",
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = rows.ToList(),
            SummaryLabel = "Totals",
            SummaryValue = $"Due: {FormatNumber(totalDue)} | Paid: {FormatNumber(totalPaid)}"
        };
        return PdfResult(doc, filters, "court_fines", from, to);
    }

    // ──────────────────────────────────────────────────────────────────
    // habitual-offenders
    // ──────────────────────────────────────────────────────────────────

    private async Task<ReportResult> GenerateHabitualOffenders(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        // Find vehicles with multiple prosecution cases (OffenseCount > 1 or PenaltyMultiplier > 1)
        var offenders = await _context.ProsecutionCases
            .Where(p => p.DeletedAt == null)
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
            .Where(p => p.OffenseCount > 1)
            .Include(p => p.Weighing)
            .Include(p => p.CaseRegister)
            .OrderByDescending(p => p.OffenseCount)
            .ThenByDescending(p => p.TotalFeeKes)
            .Select(p => new
            {
                VehicleRegNumber = p.Weighing != null ? p.Weighing.VehicleRegNumber : "-",
                CaseNo = p.CaseRegister != null ? p.CaseRegister.CaseNo : "-",
                p.OffenseCount,
                p.PenaltyMultiplier,
                p.TotalFeeKes,
                p.GvwOverloadKg,
                p.MaxAxleOverloadKg,
                p.BestChargeBasis,
                p.DemeritPoints,
                p.CreatedAt
            })
            .ToListAsync(ct);

        string[] headers =
        [
            "Vehicle Reg", "Case No", "Offenses (12mo)", "Multiplier", "Fee (KES)",
            "GVW Overload (kg)", "Max Axle Overload (kg)", "Charge Basis", "Demerit Pts", "Date"
        ];
        var rows = offenders.Select(o => new[]
        {
            o.VehicleRegNumber,
            o.CaseNo,
            o.OffenseCount.ToString(),
            $"{o.PenaltyMultiplier:F1}x",
            FormatKes(o.TotalFeeKes),
            FormatNumber(o.GvwOverloadKg),
            FormatNumber(o.MaxAxleOverloadKg),
            o.BestChargeBasis.ToUpperInvariant(),
            o.DemeritPoints.ToString(),
            FormatDate(o.CreatedAt)
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, rows), "habitual_offenders", from, to);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Habitual Offenders Report", headers, rows, from, to), "habitual_offenders", from, to);

        var doc = new SimpleTableDocument
        {
            ReportTitle = "Habitual Offenders Report",
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = rows.ToList(),
            SummaryLabel = "Total Habitual Offenders",
            SummaryValue = offenders.Count.ToString()
        };
        return PdfResult(doc, filters, "habitual_offenders", from, to);
    }

    // ══════════════════════════════════════════════════════════════════
    // Inner PDF document classes
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// PDF document for prosecution statistics with summary cards and breakdown table.
    /// </summary>
    private sealed class ProsecutionStatisticsDocument : BaseReportDocument
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
                    summaryLabel: "Total Statuses",
                    summaryValue: Rows.Count.ToString()));
            });
        }
    }

    /// <summary>
    /// Generic table-based PDF document used by most prosecution reports.
    /// </summary>
    private sealed class SimpleTableDocument : BaseReportDocument
    {
        public required string[] Headers { get; init; }
        public required List<string[]> Rows { get; init; }
        public string? SummaryLabel { get; init; }
        public string? SummaryValue { get; init; }

        protected override void ComposeContent(IContainer container)
        {
            container.Element(c => ComposeDataTable(c, Headers, Rows,
                summaryLabel: SummaryLabel,
                summaryValue: SummaryValue));
        }
    }
}
