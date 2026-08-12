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

    // =====================================================================
    // Structured custom-report-builder column catalogs. Each Key MUST match the literal header
    // text built in that report's generation method (see BaseReportGenerator.ApplyColumnSelection
    // - matches by header string). The "(KES)" columns below are hardcoded literals, not
    // $"...({currency})" runtime interpolations, so they're safe to catalog like any other column.
    // =====================================================================

    private static readonly List<ReportColumnDefinition> ProsecutionStatisticsColumns =
    [
        new() { Key = "Status", Label = "Status" },
        new() { Key = "Cases", Label = "Number of Cases" },
        new() { Key = "Total Fee (KES)", Label = "Total Fee (KES)" },
        new() { Key = "% of Total", Label = "% of Total" }
    ];

    private static readonly List<ReportColumnDefinition> CourtCalendarColumns =
    [
        new() { Key = "Case No", Label = "Case Number" },
        new() { Key = "Hearing Date", Label = "Hearing Date" },
        new() { Key = "Time", Label = "Hearing Time" },
        new() { Key = "Type", Label = "Hearing Type" },
        new() { Key = "Status", Label = "Hearing Status" },
        new() { Key = "Outcome", Label = "Hearing Outcome" },
        new() { Key = "Presiding Officer", Label = "Presiding Officer" },
        new() { Key = "Next Hearing", Label = "Next Hearing Date" }
    ];

    private static readonly List<ReportColumnDefinition> DailyChargedColumns =
    [
        new() { Key = "Date", Label = "Date" },
        new() { Key = "Case No", Label = "Case Number" },
        new() { Key = "County", Label = "County" },
        new() { Key = "Sub County", Label = "Sub County" },
        new() { Key = "Vehicle Reg", Label = "Vehicle Registration" },
        new() { Key = "Ticket No", Label = "Weighing Ticket Number" },
        new() { Key = "GVW Overload (kg)", Label = "GVW Overload (kg)" },
        new() { Key = "Max Axle Overload (kg)", Label = "Max Axle Overload (kg)" },
        new() { Key = "Charge Basis", Label = "Charge Basis" },
        new() { Key = "Fee (KES)", Label = "Fee (KES)" },
        new() { Key = "Status", Label = "Status" },
        new() { Key = "Officer", Label = "Prosecution Officer" }
    ];

    private static readonly List<ReportColumnDefinition> PaymentListColumns =
    [
        new() { Key = "Date", Label = "Date" },
        new() { Key = "Case No", Label = "Case Number" },
        new() { Key = "Vehicle Reg", Label = "Vehicle Registration" },
        new() { Key = "Fee (KES)", Label = "Fee (KES)" },
        new() { Key = "Case Status", Label = "Case Status" },
        new() { Key = "Invoice No", Label = "Invoice Number" },
        new() { Key = "Invoice Status", Label = "Invoice Status" },
        new() { Key = "Pesaflow Link", Label = "Pesaflow Payment Link" }
    ];

    private static readonly List<ReportColumnDefinition> CourtFinesColumns =
    [
        new() { Key = "Case No", Label = "Case Number" },
        new() { Key = "Invoice No", Label = "Invoice Number" },
        new() { Key = "Amount Due", Label = "Amount Due" },
        new() { Key = "Currency", Label = "Currency" },
        new() { Key = "Status", Label = "Status" },
        new() { Key = "Total Paid", Label = "Total Paid" },
        new() { Key = "Generated", Label = "Generated Date" },
        new() { Key = "Due Date", Label = "Due Date" }
    ];

    private static readonly List<ReportColumnDefinition> HabitualOffendersColumns =
    [
        new() { Key = "Vehicle Reg", Label = "Vehicle Registration" },
        new() { Key = "Case No", Label = "Case Number" },
        new() { Key = "Offenses (12mo)", Label = "Offenses (12 Months)" },
        new() { Key = "Multiplier", Label = "Penalty Multiplier" },
        new() { Key = "Fee (KES)", Label = "Fee (KES)" },
        new() { Key = "GVW Overload (kg)", Label = "GVW Overload (kg)" },
        new() { Key = "Max Axle Overload (kg)", Label = "Max Axle Overload (kg)" },
        new() { Key = "Charge Basis", Label = "Charge Basis" },
        new() { Key = "Demerit Pts", Label = "Demerit Points" },
        new() { Key = "Date", Label = "Date" }
    ];

    // =====================================================================
    // Structured custom-report-builder filter catalog, shared across the Prosecution module's
    // report types - lets the builder UI show only the filters a given report actually supports.
    // ProsecutionCase itself has no direct CountyId/SubcountyId FK - every report here reaches
    // CaseRegister (which does) either via the ProsecutionCase.CaseRegister navigation, or, for
    // court-calendar/court-fines, via CourtHearing.CaseRegister / Invoice.ProsecutionCase.CaseRegister.
    // =====================================================================

    private static readonly List<ReportFilterDefinition> ProsecutionGeoFilters =
    [
        new() { Key = "countyId", Label = "County", Kind = "county" },
        new() { Key = "subcountyId", Label = "Sub County", Kind = "subcounty" },
        new() { Key = "roadId", Label = "Road", Kind = "road" }
    ];

    public ProsecutionReportGenerator(TruLoadDbContext context)
    {
        _context = context;
    }

    public override string Module => ReportModules.Prosecution;

    public override List<ReportDefinitionDto> GetDefinitions() =>
    [
        Def("prosecution-statistics", "Prosecution Statistics",
            "Summary statistics of prosecution cases including charge basis breakdown and fees collected.",
            columns: ProsecutionStatisticsColumns, filters: ProsecutionGeoFilters),
        Def("court-calendar", "Court Calendar",
            "Upcoming and past court hearings with case references, dates, and presiding officers.",
            columns: CourtCalendarColumns, filters: ProsecutionGeoFilters),
        Def("daily-charged", "Daily Charged Vehicles",
            "List of vehicles charged per day with overload details, fees, and officer information.",
            columns: DailyChargedColumns, filters: ProsecutionGeoFilters),
        Def("payment-list", "Prosecution Payment List",
            "Prosecution cases with associated invoice and payment status for revenue tracking.",
            columns: PaymentListColumns, filters: ProsecutionGeoFilters),
        Def("court-fines", "Court Fines Summary",
            "Summary of court-imposed fines aggregated by status and period.",
            columns: CourtFinesColumns, filters: ProsecutionGeoFilters),
        Def("habitual-offenders", "Habitual Offenders",
            "Vehicles with multiple prosecution cases within 12 months flagged as repeat offenders.",
            columns: HabitualOffendersColumns, filters: ProsecutionGeoFilters)
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

        // ProsecutionCase has no direct CountyId/SubcountyId FK - reach it via the joined
        // CaseRegister (CaseRegisterId is required, but the nav is still checked defensively to
        // match the existing null-safe style used for CaseRegister/Weighing elsewhere in this file).
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(p => p.CaseRegister != null && p.CaseRegister.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(p => p.CaseRegister != null && p.CaseRegister.SubcountyId == subcountyId);
        if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
            query = query.Where(p => p.CaseRegister != null && p.CaseRegister.RoadId == roadId);

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

        // Structured custom-report builder: UseDefaults=true (the default) reproduces today's
        // exact fixed output unchanged. Only when a caller explicitly opts out does column
        // selection apply.
        var outputHeaders = headers;
        var outputRows = rows;
        int? effectiveStatusColumnIndex = 0;
        int[]? percentageDataBarColumnIndexes = [3];

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, rows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
            var statusIdx = Array.IndexOf(outputHeaders, "Status");
            effectiveStatusColumnIndex = statusIdx >= 0 ? statusIdx : null;
            var pctIdx = Array.IndexOf(outputHeaders, "% of Total");
            percentageDataBarColumnIndexes = pctIdx >= 0 ? new[] { pctIdx } : null;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "prosecution_statistics", from, to);

        if (format == "xlsx")
        {
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Prosecution Statistics Report", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                ConditionalStatusColumnIndex = effectiveStatusColumnIndex, PercentageDataBarColumnIndexes = percentageDataBarColumnIndexes,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "prosecution_statistics", from, to);
        }

        var doc = new ProsecutionStatisticsDocument
        {
            ReportTitle = "Prosecution Statistics Report",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToList(),
            StatusColumnIndex = effectiveStatusColumnIndex,
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

        var hearingsQuery = _context.CourtHearings
            .Where(h => h.DeletedAt == null)
            .Where(h => h.HearingDate >= from && h.HearingDate <= to);

        // CourtHearing has no direct CountyId/SubcountyId FK - reach it via the joined CaseRegister.
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            hearingsQuery = hearingsQuery.Where(h => h.CaseRegister != null && h.CaseRegister.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            hearingsQuery = hearingsQuery.Where(h => h.CaseRegister != null && h.CaseRegister.SubcountyId == subcountyId);
        if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
            hearingsQuery = hearingsQuery.Where(h => h.CaseRegister != null && h.CaseRegister.RoadId == roadId);

        var hearings = await hearingsQuery
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

        // Structured custom-report builder: UseDefaults=true (the default) reproduces today's
        // exact fixed output unchanged. Only when a caller explicitly opts out does column
        // selection apply.
        var outputHeaders = headers;
        var outputRows = rows;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, rows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "court_calendar", from, to);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Court Calendar", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "court_calendar", from, to);

        var doc = new SimpleTableDocument
        {
            ReportTitle = "Court Calendar",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToList(),
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

        var query = _context.ProsecutionCases
            .Where(p => p.DeletedAt == null)
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to);

        // ProsecutionCase has no direct CountyId/SubcountyId FK - reach it via the joined CaseRegister.
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(p => p.CaseRegister != null && p.CaseRegister.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(p => p.CaseRegister != null && p.CaseRegister.SubcountyId == subcountyId);
        if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
            query = query.Where(p => p.CaseRegister != null && p.CaseRegister.RoadId == roadId);

        var cases = await query
            .Include(p => p.CaseRegister)
            .Include(p => p.Weighing)
            .Include(p => p.ProsecutionOfficer)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                CaseNo = p.CaseRegister != null ? p.CaseRegister.CaseNo : "-",
                CountyId = p.CaseRegister != null ? p.CaseRegister.CountyId : null,
                SubcountyId = p.CaseRegister != null ? p.CaseRegister.SubcountyId : null,
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

        // Resolve County/Sub County names via lookup dictionaries (CaseRegister has no County/
        // Subcounty navigation property, only the raw FKs) - same "resolve via dictionary" pattern
        // used for vehicle registration numbers in the Case module's repeat-offenders report.
        var countyIds = cases.Where(c => c.CountyId.HasValue).Select(c => c.CountyId!.Value).Distinct().ToList();
        var subcountyIds = cases.Where(c => c.SubcountyId.HasValue).Select(c => c.SubcountyId!.Value).Distinct().ToList();
        var countyNames = await _context.Counties
            .Where(c => countyIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var subcountyNames = await _context.Subcounties
            .Where(s => subcountyIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Name })
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        string[] headers =
        [
            "Date", "Case No", "County", "Sub County", "Vehicle Reg", "Ticket No", "GVW Overload (kg)",
            "Max Axle Overload (kg)", "Charge Basis", "Fee (KES)", "Status", "Officer"
        ];
        var rows = cases.Select(c => new[]
        {
            FormatDate(c.CreatedAt),
            c.CaseNo,
            c.CountyId.HasValue && countyNames.TryGetValue(c.CountyId.Value, out var countyName) ? countyName : "-",
            c.SubcountyId.HasValue && subcountyNames.TryGetValue(c.SubcountyId.Value, out var subcountyName) ? subcountyName : "-",
            c.VehicleRegNumber,
            c.WeighingTicketNo,
            FormatNumber(c.GvwOverloadKg),
            FormatNumber(c.MaxAxleOverloadKg),
            c.BestChargeBasis.ToUpperInvariant(),
            FormatKes(c.TotalFeeKes),
            c.Status,
            c.ProsecutionOfficerName
        });

        // Structured custom-report builder: UseDefaults=true (the default) reproduces today's
        // exact fixed output unchanged (plus the new County/Sub County columns added above).
        // Only when a caller explicitly opts out does column selection apply.
        var outputHeaders = headers;
        var outputRows = rows;
        int? effectiveStatusColumnIndex = 10; // "Status" now sits after the new County/Sub County columns

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, rows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
            var statusIdx = Array.IndexOf(outputHeaders, "Status");
            effectiveStatusColumnIndex = statusIdx >= 0 ? statusIdx : null;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "daily_charged", from, to);

        if (format == "xlsx")
        {
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Daily Charged Vehicles", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                ConditionalStatusColumnIndex = effectiveStatusColumnIndex,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "daily_charged", from, to);
        }

        var doc = new SimpleTableDocument
        {
            ReportTitle = "Daily Charged Vehicles",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToList(),
            SummaryLabel = "Total Charged",
            SummaryValue = $"{cases.Count} vehicles | {FormatKes(cases.Sum(c => c.TotalFeeKes))}",
            StatusColumnIndex = effectiveStatusColumnIndex
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

        var paymentListQuery = _context.ProsecutionCases
            .Where(p => p.DeletedAt == null)
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to);

        // ProsecutionCase has no direct CountyId/SubcountyId FK - reach it via the joined CaseRegister.
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            paymentListQuery = paymentListQuery.Where(p => p.CaseRegister != null && p.CaseRegister.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            paymentListQuery = paymentListQuery.Where(p => p.CaseRegister != null && p.CaseRegister.SubcountyId == subcountyId);
        if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
            paymentListQuery = paymentListQuery.Where(p => p.CaseRegister != null && p.CaseRegister.RoadId == roadId);

        var data = await paymentListQuery
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

        // Structured custom-report builder: UseDefaults=true (the default) reproduces today's
        // exact fixed output unchanged. Only when a caller explicitly opts out does column
        // selection apply.
        var outputHeaders = headers;
        var outputRows = rows;
        int? effectiveStatusColumnIndex = 6; // "Invoice Status"

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, rows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
            var statusIdx = Array.IndexOf(outputHeaders, "Invoice Status");
            effectiveStatusColumnIndex = statusIdx >= 0 ? statusIdx : null;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "prosecution_payment_list", from, to);

        if (format == "xlsx")
        {
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Prosecution Payment List", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                ConditionalStatusColumnIndex = effectiveStatusColumnIndex, // "Invoice Status"
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "prosecution_payment_list", from, to);
        }

        var totalFees = data.Sum(d => d.TotalFeeKes);
        var paidCount = data.Count(d => d.InvoiceStatus == "paid");
        var doc = new SimpleTableDocument
        {
            ReportTitle = "Prosecution Payment List",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToList(),
            SummaryLabel = "Total Fees",
            SummaryValue = $"{FormatKes(totalFees)} | {paidCount}/{data.Count} paid",
            StatusColumnIndex = effectiveStatusColumnIndex
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

        var invoicesQuery = _context.Invoices
            .Where(i => i.DeletedAt == null)
            .Where(i => i.ProsecutionCaseId != null)
            .Where(i => i.GeneratedAt >= from && i.GeneratedAt <= to);

        // Invoice has no direct CountyId/SubcountyId FK - reach it via ProsecutionCase.CaseRegister.
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            invoicesQuery = invoicesQuery.Where(i =>
                i.ProsecutionCase != null && i.ProsecutionCase.CaseRegister != null &&
                i.ProsecutionCase.CaseRegister.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            invoicesQuery = invoicesQuery.Where(i =>
                i.ProsecutionCase != null && i.ProsecutionCase.CaseRegister != null &&
                i.ProsecutionCase.CaseRegister.SubcountyId == subcountyId);
        if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
            invoicesQuery = invoicesQuery.Where(i =>
                i.ProsecutionCase != null && i.ProsecutionCase.CaseRegister != null &&
                i.ProsecutionCase.CaseRegister.RoadId == roadId);

        var invoices = await invoicesQuery
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

        // Structured custom-report builder: UseDefaults=true (the default) reproduces today's
        // exact fixed output unchanged. Only when a caller explicitly opts out does column
        // selection apply.
        var outputHeaders = headers;
        var outputRows = rows;
        int? effectiveStatusColumnIndex = 4;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, rows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
            var statusIdx = Array.IndexOf(outputHeaders, "Status");
            effectiveStatusColumnIndex = statusIdx >= 0 ? statusIdx : null;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "court_fines", from, to);

        if (format == "xlsx")
        {
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Court Fines Summary", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                ConditionalStatusColumnIndex = effectiveStatusColumnIndex,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "court_fines", from, to);
        }

        var totalDue = invoices.Sum(i => i.AmountDue);
        var totalPaid = invoices.Sum(i => i.TotalPaid);
        var doc = new SimpleTableDocument
        {
            ReportTitle = "Court Fines Summary",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToList(),
            SummaryLabel = "Totals",
            SummaryValue = $"Due: {FormatNumber(totalDue)} | Paid: {FormatNumber(totalPaid)}",
            StatusColumnIndex = effectiveStatusColumnIndex
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
        var offendersQuery = _context.ProsecutionCases
            .Where(p => p.DeletedAt == null)
            .Where(p => p.CreatedAt >= from && p.CreatedAt <= to)
            .Where(p => p.OffenseCount > 1);

        // ProsecutionCase has no direct CountyId/SubcountyId FK - reach it via the joined CaseRegister.
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            offendersQuery = offendersQuery.Where(p => p.CaseRegister != null && p.CaseRegister.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            offendersQuery = offendersQuery.Where(p => p.CaseRegister != null && p.CaseRegister.SubcountyId == subcountyId);
        if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
            offendersQuery = offendersQuery.Where(p => p.CaseRegister != null && p.CaseRegister.RoadId == roadId);

        var offenders = await offendersQuery
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

        // Structured custom-report builder: UseDefaults=true (the default) reproduces today's
        // exact fixed output unchanged. Only when a caller explicitly opts out does column
        // selection apply.
        var outputHeaders = headers;
        var outputRows = rows;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, rows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "habitual_offenders", from, to);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Habitual Offenders Report", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "habitual_offenders", from, to);

        var doc = new SimpleTableDocument
        {
            ReportTitle = "Habitual Offenders Report",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToList(),
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
        public int? StatusColumnIndex { get; init; }

        protected override void ComposeContent(IContainer container)
        {
            container.Column(col =>
            {
                col.Spacing(8);
                col.Item().Element(c => ComposeSummaryCards(c, SummaryItems));
                col.Item().Element(c => ComposeDataTable(c, Headers, Rows,
                    summaryLabel: "Total Statuses",
                    summaryValue: Rows.Count.ToString(),
                    conditionalStatusColumnIndex: StatusColumnIndex));
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
        public int? StatusColumnIndex { get; init; }
        public (string colorHex, string label)[] Legend { get; init; } = [];

        protected override void ComposeContent(IContainer container)
        {
            container.Column(col =>
            {
                col.Spacing(5);
                col.Item().Element(c => ComposeDataTable(c, Headers, Rows,
                    summaryLabel: SummaryLabel,
                    summaryValue: SummaryValue,
                    conditionalStatusColumnIndex: StatusColumnIndex));
                col.Item().Element(c => ComposeLegend(c, Legend));
            });
        }
    }
}
