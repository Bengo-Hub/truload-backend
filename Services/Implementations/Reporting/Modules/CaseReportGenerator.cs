using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Reporting;
using TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports;

namespace TruLoad.Backend.Services.Implementations.Reporting.Modules;

/// <summary>
/// Generates case management reports: case register, repeat offenders, and case status summary.
/// </summary>
public class CaseReportGenerator : BaseReportGenerator
{
    private readonly TruLoadDbContext _context;

    // =====================================================================
    // Structured custom-report-builder column catalogs. Each Key MUST match the literal header
    // text built in that report's generation method (see BaseReportGenerator.ApplyColumnSelection
    // - matches by header string). None of these reports have currency-dependent headers.
    // =====================================================================

    private static readonly List<ReportColumnDefinition> CaseRegisterColumns =
    [
        new() { Key = "Case No", Label = "Case Number" },
        new() { Key = "County", Label = "County" },
        new() { Key = "Sub County", Label = "Sub County" },
        new() { Key = "Vehicle Reg", Label = "Vehicle Registration" },
        new() { Key = "Driver", Label = "Driver" },
        new() { Key = "Transporter", Label = "Transporter" },
        new() { Key = "Status", Label = "Status" },
        new() { Key = "Violation Type", Label = "Violation Type" },
        new() { Key = "Created", Label = "Created Date" },
        new() { Key = "Updated", Label = "Updated Date" },
        new() { Key = "Closed", Label = "Closed Date" }
    ];

    private static readonly List<ReportColumnDefinition> RepeatOffendersColumns =
    [
        new() { Key = "Vehicle Reg", Label = "Vehicle Registration" },
        new() { Key = "Total Cases", Label = "Total Cases" },
        new() { Key = "First Case No", Label = "First Case Number" },
        new() { Key = "Latest Case No", Label = "Latest Case Number" },
        new() { Key = "First Date", Label = "First Case Date" },
        new() { Key = "Latest Date", Label = "Latest Case Date" }
    ];

    private static readonly List<ReportColumnDefinition> CaseStatusSummaryColumns =
    [
        new() { Key = "Status", Label = "Status" },
        new() { Key = "Code", Label = "Status Code" },
        new() { Key = "Cases", Label = "Number of Cases" },
        new() { Key = "% of Total", Label = "% of Total" },
        new() { Key = "Escalated", Label = "Escalated Count" },
        new() { Key = "Closed", Label = "Closed Count" }
    ];

    // =====================================================================
    // Structured custom-report-builder filter catalog, shared across the Case module's report
    // types - lets the builder UI show only the filters a given report actually supports. Unlike
    // WeighingTransaction, CaseRegister has its OWN direct CountyId/SubcountyId FK properties (no
    // station join needed), so every report here that touches CaseRegister (directly or via a
    // joined entity) can filter on them the same way.
    // =====================================================================

    private static readonly List<ReportFilterDefinition> CaseGeoFilters =
    [
        new() { Key = "countyId", Label = "County", Kind = "county" },
        new() { Key = "subcountyId", Label = "Sub County", Kind = "subcounty" }
    ];

    public CaseReportGenerator(TruLoadDbContext context)
    {
        _context = context;
    }

    public override string Module => ReportModules.Cases;

    public override List<ReportDefinitionDto> GetDefinitions() =>
    [
        Def("case-register", "Case Register",
            "Full register of violation cases with vehicle, driver, transporter, and status details.",
            columns: CaseRegisterColumns, filters: CaseGeoFilters),
        Def("repeat-offenders", "Repeat Offenders",
            "Vehicles or transporters with multiple cases, indicating habitual violation patterns.",
            columns: RepeatOffendersColumns, filters: CaseGeoFilters),
        Def("case-status-summary", "Case Status Summary",
            "Aggregated breakdown of cases by status with counts and trends over the reporting period.",
            columns: CaseStatusSummaryColumns, filters: CaseGeoFilters)
    ];

    public override async Task<ReportResult> GenerateAsync(
        string reportType, ReportFilterParams filters, string format, CancellationToken ct = default)
    {
        return reportType switch
        {
            "case-register" => await GenerateCaseRegister(filters, format, ct),
            "repeat-offenders" => await GenerateRepeatOffenders(filters, format, ct),
            "case-status-summary" => await GenerateCaseStatusSummary(filters, format, ct),
            _ => throw new ArgumentException($"Unknown case report type: {reportType}")
        };
    }

    // ──────────────────────────────────────────────────────────────────
    // case-register
    // ──────────────────────────────────────────────────────────────────

    private async Task<ReportResult> GenerateCaseRegister(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var query = _context.CaseRegisters
            .Where(c => c.DeletedAt == null)
            .Where(c => c.CreatedAt >= from && c.CreatedAt <= to);

        if (!string.IsNullOrEmpty(filters.Status))
        {
            query = query.Where(c => c.CaseStatus.Code == filters.Status);
        }

        // CaseRegister carries its own direct CountyId/SubcountyId FKs (no station join needed,
        // unlike WeighingTransaction) - filter directly on those columns.
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(c => c.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(c => c.SubcountyId == subcountyId);

        var cases = await (
            from c in query
            join cs in _context.CaseStatuses on c.CaseStatusId equals cs.Id
            join vt in _context.ViolationTypes on c.ViolationTypeId equals vt.Id
            join v in _context.Vehicles on c.VehicleId equals v.Id into vj
            from v in vj.DefaultIfEmpty()
            join d in _context.Drivers on c.DriverId equals d.Id into dj
            from d in dj.DefaultIfEmpty()
            join wt in _context.WeighingTransactions on c.WeighingId equals wt.Id into wtj
            from wt in wtj.DefaultIfEmpty()
            join t in _context.Transporters on wt.TransporterId equals t.Id into tj
            from t in tj.DefaultIfEmpty()
            join cty in _context.Counties on c.CountyId equals cty.Id into ctyj
            from cty in ctyj.DefaultIfEmpty()
            join sct in _context.Subcounties on c.SubcountyId equals sct.Id into sctj
            from sct in sctj.DefaultIfEmpty()
            orderby c.CreatedAt descending
            select new
            {
                c.CaseNo,
                CountyName = cty != null ? cty.Name : "-",
                SubcountyName = sct != null ? sct.Name : "-",
                VehicleRegNo = v != null ? v.RegNo : "-",
                DriverName = d != null ? d.FullNames : "-",
                TransporterName = t != null ? t.Name : "-",
                Status = cs.Name,
                ViolationType = vt.Name,
                c.CreatedAt,
                c.UpdatedAt,
                c.ClosedAt
            })
            .ToListAsync(ct);

        string[] headers =
        [
            "Case No", "County", "Sub County", "Vehicle Reg", "Driver", "Transporter",
            "Status", "Violation Type", "Created", "Updated", "Closed"
        ];
        var rows = cases.Select(c => new[]
        {
            c.CaseNo,
            c.CountyName,
            c.SubcountyName,
            c.VehicleRegNo,
            c.DriverName,
            c.TransporterName,
            c.Status,
            c.ViolationType,
            FormatDate(c.CreatedAt),
            FormatDate(c.UpdatedAt),
            FormatDate(c.ClosedAt)
        });

        // Structured custom-report builder: UseDefaults=true (the default) reproduces today's
        // exact fixed output unchanged (plus the new County/Sub County columns added above).
        // Only when a caller explicitly opts out does column selection apply.
        var outputHeaders = headers;
        var outputRows = rows;
        int? effectiveStatusColumnIndex = 6; // "Status" now sits after the new County/Sub County columns

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, rows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
            var statusIdx = Array.IndexOf(outputHeaders, "Status");
            effectiveStatusColumnIndex = statusIdx >= 0 ? statusIdx : null;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "case_register", from, to);

        if (format == "xlsx")
        {
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Case Register", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                ConditionalStatusColumnIndex = effectiveStatusColumnIndex,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "case_register", from, to);
        }

        var doc = new CaseRegisterDocument
        {
            ReportTitle = "Case Register",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToList(),
            TotalCases = cases.Count,
            StatusColumnIndex = effectiveStatusColumnIndex
        };
        return PdfResult(doc, filters, "case_register", from, to);
    }

    // ──────────────────────────────────────────────────────────────────
    // repeat-offenders
    // ──────────────────────────────────────────────────────────────────

    private async Task<ReportResult> GenerateRepeatOffenders(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        Guid? countyId = null;
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var parsedCountyId))
            countyId = parsedCountyId;
        Guid? subcountyId = null;
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var parsedSubcountyId))
            subcountyId = parsedSubcountyId;

        // Group cases by vehicle to find repeats. CaseRegister carries its own direct
        // CountyId/SubcountyId FKs (no station join needed) - filter directly on those columns.
        var vehicleCases = await _context.CaseRegisters
            .Where(c => c.DeletedAt == null)
            .Where(c => c.CreatedAt >= from && c.CreatedAt <= to)
            .Where(c => countyId == null || c.CountyId == countyId)
            .Where(c => subcountyId == null || c.SubcountyId == subcountyId)
            .Include(c => c.CaseStatus)
            .GroupBy(c => c.VehicleId)
            .Where(g => g.Count() > 1)
            .Select(g => new
            {
                VehicleId = g.Key,
                CaseCount = g.Count(),
                FirstCase = g.OrderBy(c => c.CreatedAt).Select(c => c.CaseNo).First(),
                LatestCase = g.OrderByDescending(c => c.CreatedAt).Select(c => c.CaseNo).First(),
                FirstDate = g.Min(c => c.CreatedAt),
                LatestDate = g.Max(c => c.CreatedAt)
            })
            .OrderByDescending(g => g.CaseCount)
            .ToListAsync(ct);

        // Resolve vehicle registration numbers
        var vehicleIds = vehicleCases.Select(v => v.VehicleId).ToList();
        var vehicles = await _context.Vehicles
            .Where(v => vehicleIds.Contains(v.Id))
            .Select(v => new { v.Id, v.RegNo })
            .ToDictionaryAsync(v => v.Id, v => v.RegNo, ct);

        // Also look for transporter repeats via CaseParties
        var transporterRepeats = await _context.CaseParties
            .Where(cp => cp.DeletedAt == null)
            .Where(cp => cp.TransporterId != null)
            .Where(cp => cp.CaseRegister != null && cp.CaseRegister.DeletedAt == null)
            .Where(cp => cp.CaseRegister!.CreatedAt >= from && cp.CaseRegister!.CreatedAt <= to)
            .Where(cp => countyId == null || cp.CaseRegister!.CountyId == countyId)
            .Where(cp => subcountyId == null || cp.CaseRegister!.SubcountyId == subcountyId)
            .GroupBy(cp => cp.TransporterId)
            .Where(g => g.Count() > 1)
            .Select(g => new
            {
                TransporterId = g.Key,
                CaseCount = g.Count()
            })
            .ToListAsync(ct);

        string[] headers = ["Vehicle Reg", "Total Cases", "First Case No", "Latest Case No", "First Date", "Latest Date"];
        var rows = vehicleCases.Select(v => new[]
        {
            vehicles.GetValueOrDefault(v.VehicleId, "-"),
            v.CaseCount.ToString(),
            v.FirstCase,
            v.LatestCase,
            FormatDate(v.FirstDate),
            FormatDate(v.LatestDate)
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
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "repeat_offenders", from, to);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Repeat Offenders Report", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "repeat_offenders", from, to);

        var doc = new RepeatOffendersDocument
        {
            ReportTitle = "Repeat Offenders Report",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToList(),
            SummaryItems =
            [
                ("Repeat Vehicles", vehicleCases.Count.ToString()),
                ("Total Cases (Repeats)", vehicleCases.Sum(v => v.CaseCount).ToString()),
                ("Repeat Transporters", transporterRepeats.Count.ToString()),
                ("Max Cases (Single Vehicle)", vehicleCases.FirstOrDefault()?.CaseCount.ToString() ?? "0")
            ]
        };
        return PdfResult(doc, filters, "repeat_offenders", from, to);
    }

    // ──────────────────────────────────────────────────────────────────
    // case-status-summary
    // ──────────────────────────────────────────────────────────────────

    private async Task<ReportResult> GenerateCaseStatusSummary(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        // CaseRegister carries its own direct CountyId/SubcountyId FKs (no station join needed) -
        // filter directly on those columns.
        var statusGroupsQuery = _context.CaseRegisters
            .Where(c => c.DeletedAt == null)
            .Where(c => c.CreatedAt >= from && c.CreatedAt <= to);

        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            statusGroupsQuery = statusGroupsQuery.Where(c => c.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            statusGroupsQuery = statusGroupsQuery.Where(c => c.SubcountyId == subcountyId);

        var statusGroups = await statusGroupsQuery
            .Include(c => c.CaseStatus)
            .GroupBy(c => new { c.CaseStatus.Code, c.CaseStatus.Name })
            .Select(g => new
            {
                StatusCode = g.Key.Code,
                StatusName = g.Key.Name,
                Count = g.Count(),
                EscalatedCount = g.Count(c => c.EscalatedToCaseManager),
                ClosedCount = g.Count(c => c.ClosedAt != null)
            })
            .OrderByDescending(g => g.Count)
            .ToListAsync(ct);

        var totalCases = statusGroups.Sum(s => s.Count);

        string[] headers = ["Status", "Code", "Cases", "% of Total", "Escalated", "Closed"];
        var rows = statusGroups.Select(s => new[]
        {
            s.StatusName,
            s.StatusCode,
            s.Count.ToString(),
            totalCases > 0 ? $"{(decimal)s.Count / totalCases * 100:F1}%" : "0%",
            s.EscalatedCount.ToString(),
            s.ClosedCount.ToString()
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
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "case_status_summary", from, to);

        if (format == "xlsx")
        {
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Case Status Summary", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                ConditionalStatusColumnIndex = effectiveStatusColumnIndex, PercentageDataBarColumnIndexes = percentageDataBarColumnIndexes,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "case_status_summary", from, to);
        }

        var doc = new CaseStatusSummaryDocument
        {
            ReportTitle = "Case Status Summary",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToList(),
            StatusColumnIndex = effectiveStatusColumnIndex,
            SummaryItems =
            [
                ("Total Cases", totalCases.ToString()),
                ("Statuses", statusGroups.Count.ToString()),
                ("Total Escalated", statusGroups.Sum(s => s.EscalatedCount).ToString()),
                ("Total Closed", statusGroups.Sum(s => s.ClosedCount).ToString())
            ]
        };
        return PdfResult(doc, filters, "case_status_summary", from, to);
    }

    // ══════════════════════════════════════════════════════════════════
    // Inner PDF document classes
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// PDF document for the full case register listing.
    /// </summary>
    private sealed class CaseRegisterDocument : BaseReportDocument
    {
        public required string[] Headers { get; init; }
        public required List<string[]> Rows { get; init; }
        public int TotalCases { get; init; }
        public int? StatusColumnIndex { get; init; }

        protected override void ComposeContent(IContainer container)
        {
            container.Element(c => ComposeDataTable(c, Headers, Rows,
                summaryLabel: "Total Cases",
                summaryValue: TotalCases.ToString(),
                conditionalStatusColumnIndex: StatusColumnIndex));
        }
    }

    /// <summary>
    /// PDF document for repeat offenders with summary statistics.
    /// </summary>
    private sealed class RepeatOffendersDocument : BaseReportDocument
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
    /// PDF document for the case status summary with aggregates.
    /// </summary>
    private sealed class CaseStatusSummaryDocument : BaseReportDocument
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
