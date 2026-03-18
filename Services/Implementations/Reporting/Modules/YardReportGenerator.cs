using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Reporting;
using TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports;

namespace TruLoad.Backend.Services.Implementations.Reporting.Modules;

/// <summary>
/// Generates yard management reports: yard occupancy and vehicle entries/exits.
/// </summary>
public class YardReportGenerator : BaseReportGenerator
{
    private readonly TruLoadDbContext _context;

    public YardReportGenerator(TruLoadDbContext context)
    {
        _context = context;
    }

    public override string Module => ReportModules.Yard;

    public override List<ReportDefinitionDto> GetDefinitions() =>
    [
        Def("yard-occupancy", "Yard Occupancy",
            "Current and historical yard occupancy showing vehicles in the holding yard by station."),
        Def("vehicle-entries-exits", "Vehicle Entries & Exits",
            "Detailed log of all vehicle entries and exits from the holding yard with durations.")
    ];

    public override async Task<ReportResult> GenerateAsync(
        string reportType, ReportFilterParams filters, string format, CancellationToken ct = default)
    {
        return reportType switch
        {
            "yard-occupancy" => await GenerateYardOccupancy(filters, format, ct),
            "vehicle-entries-exits" => await GenerateVehicleEntriesExits(filters, format, ct),
            _ => throw new ArgumentException($"Unknown yard report type: {reportType}")
        };
    }

    // ──────────────────────────────────────────────────────────────────
    // yard-occupancy
    // ──────────────────────────────────────────────────────────────────

    private async Task<ReportResult> GenerateYardOccupancy(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var query = _context.YardEntries
            .Where(y => y.DeletedAt == null);

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
        {
            query = query.Where(y => y.StationId == stationId);
        }

        if (!string.IsNullOrEmpty(filters.Status))
        {
            query = query.Where(y => y.Status == filters.Status);
        }

        var entries = await query
            .Include(y => y.Weighing)
            .Include(y => y.Station)
            .OrderByDescending(y => y.EnteredAt)
            .Select(y => new
            {
                VehicleRegNo = y.Weighing != null ? y.Weighing.VehicleRegNumber : "-",
                Bound = y.Weighing != null ? y.Weighing.Bound ?? "-" : "-",
                StationName = y.Station != null ? y.Station.Name : "-",
                y.StationId,
                y.Reason,
                y.Status,
                y.EnteredAt,
                y.ReleasedAt,
                IsOccupied = y.ReleasedAt == null && y.Status != "released"
            })
            .ToListAsync(ct);

        // Occupancy by station
        var stationGroups = entries
            .GroupBy(e => new { e.StationId, e.StationName })
            .Select(g => new
            {
                g.Key.StationName,
                Total = g.Count(),
                CurrentlyOccupied = g.Count(e => e.IsOccupied),
                Released = g.Count(e => !e.IsOccupied)
            })
            .OrderByDescending(g => g.CurrentlyOccupied)
            .ToList();

        var totalOccupied = entries.Count(e => e.IsOccupied);
        var totalReleased = entries.Count(e => !e.IsOccupied);

        string[] headers = ["Vehicle Reg", "Station", "Bound", "Reason", "Status", "Entry Time", "Exit Time", "Duration"];
        var rows = entries.Select(e =>
        {
            var duration = e.ReleasedAt.HasValue
                ? (e.ReleasedAt.Value - e.EnteredAt).TotalHours
                : (DateTime.UtcNow - e.EnteredAt).TotalHours;
            var durationStr = duration < 24
                ? $"{duration:F1} hrs"
                : $"{duration / 24:F1} days";
            return new[]
            {
                e.VehicleRegNo,
                e.StationName,
                e.Bound,
                e.Reason,
                e.Status,
                FormatDate(e.EnteredAt),
                e.ReleasedAt.HasValue ? FormatDate(e.ReleasedAt) : "Still in yard",
                durationStr
            };
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, rows), "yard_occupancy", null, null);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Yard Occupancy Report", headers, rows, null, null), "yard_occupancy", null, null);

        var summaryItems = new List<(string label, string value)>
        {
            ("Currently Occupied", totalOccupied.ToString()),
            ("Released", totalReleased.ToString()),
            ("Total Entries", entries.Count.ToString()),
            ("Stations", stationGroups.Count.ToString())
        };
        foreach (var sg in stationGroups.Take(3))
        {
            summaryItems.Add(($"{sg.StationName}", $"{sg.CurrentlyOccupied} occupied / {sg.Total} total"));
        }

        var doc = new YardOccupancyDocument
        {
            ReportTitle = "Yard Occupancy Report",
            Headers = headers,
            Rows = rows.ToList(),
            SummaryItems = summaryItems.ToArray()
        };
        return PdfResult(doc.Generate(), "yard_occupancy", null, null);
    }

    // ──────────────────────────────────────────────────────────────────
    // vehicle-entries-exits
    // ──────────────────────────────────────────────────────────────────

    private async Task<ReportResult> GenerateVehicleEntriesExits(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var query = _context.YardEntries
            .Where(y => y.DeletedAt == null)
            .Where(y => y.EnteredAt >= from && y.EnteredAt <= to);

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
        {
            query = query.Where(y => y.StationId == stationId);
        }

        var entries = await query
            .Include(y => y.Weighing)
            .Include(y => y.Station)
            .OrderByDescending(y => y.EnteredAt)
            .Select(y => new
            {
                VehicleRegNo = y.Weighing != null ? y.Weighing.VehicleRegNumber : "-",
                Bound = y.Weighing != null ? y.Weighing.Bound ?? "-" : "-",
                TicketNo = y.Weighing != null ? y.Weighing.TicketNumber : "-",
                StationName = y.Station != null ? y.Station.Name : "-",
                y.Reason,
                y.Status,
                y.EnteredAt,
                y.ReleasedAt
            })
            .ToListAsync(ct);

        string[] headers =
        [
            "Vehicle Reg", "Ticket No", "Station", "Bound", "Reason",
            "Status", "Entry", "Exit", "Duration (hrs)"
        ];
        var rows = entries.Select(e =>
        {
            var duration = e.ReleasedAt.HasValue
                ? (e.ReleasedAt.Value - e.EnteredAt).TotalHours
                : (DateTime.UtcNow - e.EnteredAt).TotalHours;
            return new[]
            {
                e.VehicleRegNo,
                e.TicketNo,
                e.StationName,
                e.Bound,
                e.Reason,
                e.Status,
                FormatDate(e.EnteredAt),
                e.ReleasedAt.HasValue ? FormatDate(e.ReleasedAt) : "-",
                $"{duration:F1}"
            };
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, rows), "vehicle_entries_exits", from, to);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Vehicle Entries & Exits", headers, rows, from, to), "vehicle_entries_exits", from, to);

        var totalEntries = entries.Count;
        var totalExits = entries.Count(e => e.ReleasedAt.HasValue);
        var avgDuration = entries.Where(e => e.ReleasedAt.HasValue).Select(e => (e.ReleasedAt!.Value - e.EnteredAt).TotalHours).DefaultIfEmpty(0).Average();

        var doc = new VehicleEntriesExitsDocument
        {
            ReportTitle = "Vehicle Entries & Exits",
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = rows.ToList(),
            SummaryItems =
            [
                ("Total Entries", totalEntries.ToString()),
                ("Total Exits", totalExits.ToString()),
                ("Still in Yard", (totalEntries - totalExits).ToString()),
                ("Avg Duration (hrs)", $"{avgDuration:F1}")
            ]
        };
        return PdfResult(doc.Generate(), "vehicle_entries_exits", from, to);
    }

    // ══════════════════════════════════════════════════════════════════
    // Inner PDF document classes
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// PDF document for yard occupancy with station-level summary and detail table.
    /// </summary>
    private sealed class YardOccupancyDocument : BaseReportDocument
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
    /// PDF document for vehicle entries and exits with summary statistics.
    /// </summary>
    private sealed class VehicleEntriesExitsDocument : BaseReportDocument
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
                    summaryLabel: "Total Records",
                    summaryValue: Rows.Count.ToString()));
            });
        }
    }
}
