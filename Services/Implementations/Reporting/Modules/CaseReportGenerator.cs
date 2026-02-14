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

    public CaseReportGenerator(TruLoadDbContext context)
    {
        _context = context;
    }

    public override string Module => ReportModules.Cases;

    public override List<ReportDefinitionDto> GetDefinitions() =>
    [
        Def("case-register", "Case Register",
            "Full register of violation cases with vehicle, driver, transporter, and status details."),
        Def("repeat-offenders", "Repeat Offenders",
            "Vehicles or transporters with multiple cases, indicating habitual violation patterns."),
        Def("case-status-summary", "Case Status Summary",
            "Aggregated breakdown of cases by status with counts and trends over the reporting period.")
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
            orderby c.CreatedAt descending
            select new
            {
                c.CaseNo,
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
            "Case No", "Vehicle Reg", "Driver", "Transporter",
            "Status", "Violation Type", "Created", "Updated", "Closed"
        ];
        var rows = cases.Select(c => new[]
        {
            c.CaseNo,
            c.VehicleRegNo,
            c.DriverName,
            c.TransporterName,
            c.Status,
            c.ViolationType,
            FormatDate(c.CreatedAt),
            FormatDate(c.UpdatedAt),
            FormatDate(c.ClosedAt)
        });

        if (format == "csv")
        {
            var csvData = GenerateCsv(headers, rows);
            return CsvResult(csvData, "case_register", from, to);
        }

        var doc = new CaseRegisterDocument
        {
            ReportTitle = "Case Register",
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = rows.ToList(),
            TotalCases = cases.Count
        };
        return PdfResult(doc.Generate(), "case_register", from, to);
    }

    // ──────────────────────────────────────────────────────────────────
    // repeat-offenders
    // ──────────────────────────────────────────────────────────────────

    private async Task<ReportResult> GenerateRepeatOffenders(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        // Group cases by vehicle to find repeats
        var vehicleCases = await _context.CaseRegisters
            .Where(c => c.DeletedAt == null)
            .Where(c => c.CreatedAt >= from && c.CreatedAt <= to)
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

        if (format == "csv")
        {
            var csvData = GenerateCsv(headers, rows);
            return CsvResult(csvData, "repeat_offenders", from, to);
        }

        var doc = new RepeatOffendersDocument
        {
            ReportTitle = "Repeat Offenders Report",
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = rows.ToList(),
            SummaryItems =
            [
                ("Repeat Vehicles", vehicleCases.Count.ToString()),
                ("Total Cases (Repeats)", vehicleCases.Sum(v => v.CaseCount).ToString()),
                ("Repeat Transporters", transporterRepeats.Count.ToString()),
                ("Max Cases (Single Vehicle)", vehicleCases.FirstOrDefault()?.CaseCount.ToString() ?? "0")
            ]
        };
        return PdfResult(doc.Generate(), "repeat_offenders", from, to);
    }

    // ──────────────────────────────────────────────────────────────────
    // case-status-summary
    // ──────────────────────────────────────────────────────────────────

    private async Task<ReportResult> GenerateCaseStatusSummary(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var statusGroups = await _context.CaseRegisters
            .Where(c => c.DeletedAt == null)
            .Where(c => c.CreatedAt >= from && c.CreatedAt <= to)
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

        if (format == "csv")
        {
            var csvData = GenerateCsv(headers, rows);
            return CsvResult(csvData, "case_status_summary", from, to);
        }

        var doc = new CaseStatusSummaryDocument
        {
            ReportTitle = "Case Status Summary",
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = rows.ToList(),
            SummaryItems =
            [
                ("Total Cases", totalCases.ToString()),
                ("Statuses", statusGroups.Count.ToString()),
                ("Total Escalated", statusGroups.Sum(s => s.EscalatedCount).ToString()),
                ("Total Closed", statusGroups.Sum(s => s.ClosedCount).ToString())
            ]
        };
        return PdfResult(doc.Generate(), "case_status_summary", from, to);
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

        protected override void ComposeContent(IContainer container)
        {
            container.Element(c => ComposeDataTable(c, Headers, Rows,
                summaryLabel: "Total Cases",
                summaryValue: TotalCases.ToString()));
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
