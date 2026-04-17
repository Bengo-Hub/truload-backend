using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Reporting;
using TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports;

namespace TruLoad.Backend.Services.Implementations.Reporting.Modules;

/// <summary>
/// Generates commercial weighing reports (two-pass weighing, cargo volumes, revenue, fleet utilization, etc.).
/// All queries filter by WeighingMode == "commercial" and respect tenant isolation via global query filters.
/// </summary>
public class CommercialReportGenerator : BaseReportGenerator
{
    private readonly TruLoadDbContext _context;

    public CommercialReportGenerator(TruLoadDbContext context)
    {
        _context = context;
    }

    public override string Module => ReportModules.Commercial;

    public override List<ReportDefinitionDto> GetDefinitions() =>
    [
        Def("commercial-daily-summary", "Commercial Daily Summary",
            "Total weighings, total net weight, grouped by cargo type and station. Date range filter."),
        Def("transporter-statement", "Transporter Statement",
            "Per-transporter: weighing count, total net weight, average net weight, and cargo breakdown. Date range + transporter filter."),
        Def("cargo-volume", "Cargo Volume Report",
            "Volume trends by cargo type over time, grouped by day, week, or month. Date range filter."),
        Def("weight-discrepancy", "Weight Discrepancy Report",
            "Transactions where weight discrepancy exceeds a threshold. Shows expected vs actual and variance %. Date range + threshold filter."),
        Def("commercial-revenue", "Commercial Revenue Report",
            "Revenue from commercial weighing fees (from invoices). Date range filter."),
        Def("throughput", "Throughput Report",
            "Vehicles per hour, average processing time (SecondWeightAt - FirstWeightAt), by station. Date range filter."),
        Def("tare-weight-audit", "Tare Weight Audit Report",
            "Tare weight changes per vehicle from tare history. Flags anomalies (>5% drift). Date range + vehicle filter."),
        Def("fleet-utilization", "Fleet Utilization Report",
            "Per vehicle: trip count, total net weight, average payload, payload utilization. Date range + transporter filter."),
        Def("driver-productivity", "Driver Productivity Report",
            "Per driver: trip count, total net weight, average turnaround time. Date range filter."),
        Def("quality-commodity", "Quality & Commodity Report",
            "Quality deduction stats by cargo type for transactions with deductions or industry metadata. Date range filter.")
    ];

    public override async Task<ReportResult> GenerateAsync(
        string reportType, ReportFilterParams filters, string format, CancellationToken ct = default)
    {
        return reportType switch
        {
            "commercial-daily-summary" => await GenerateCommercialDailySummaryAsync(filters, format, ct),
            "transporter-statement" => await GenerateTransporterStatementAsync(filters, format, ct),
            "cargo-volume" => await GenerateCargoVolumeAsync(filters, format, ct),
            "weight-discrepancy" => await GenerateWeightDiscrepancyAsync(filters, format, ct),
            "commercial-revenue" => await GenerateCommercialRevenueAsync(filters, format, ct),
            "throughput" => await GenerateThroughputAsync(filters, format, ct),
            "tare-weight-audit" => await GenerateTareWeightAuditAsync(filters, format, ct),
            "fleet-utilization" => await GenerateFleetUtilizationAsync(filters, format, ct),
            "driver-productivity" => await GenerateDriverProductivityAsync(filters, format, ct),
            "quality-commodity" => await GenerateQualityCommodityAsync(filters, format, ct),
            _ => throw new ArgumentException($"Unknown commercial report type: {reportType}")
        };
    }

    /// <summary>
    /// Base query for commercial weighing transactions: soft-delete check, captured, commercial mode.
    /// </summary>
    private IQueryable<Models.Weighing.WeighingTransaction> CommercialBaseQuery()
    {
        return _context.WeighingTransactions
            .Where(w => w.DeletedAt == null)
            .Where(w => w.CaptureStatus == "captured")
            .Where(w => w.WeighingMode == "commercial");
    }

    // =====================================================================
    // Commercial Daily Summary
    // =====================================================================

    private async Task<ReportResult> GenerateCommercialDailySummaryAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var query = CommercialBaseQuery()
            .Where(w => w.WeighedAt >= from && w.WeighedAt <= to);

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            query = query.Where(w => w.StationId == stationId);

        var rows = await query
            .Include(w => w.Station)
            .Include(w => w.Cargo)
            .GroupBy(w => new { w.WeighedAt.Date, w.StationId, StationName = w.Station!.Name })
            .Select(g => new
            {
                Date = g.Key.Date,
                StationName = g.Key.StationName,
                TotalWeighings = g.Count(),
                TotalNetWeightKg = g.Sum(x => (long)(x.NetWeightKg ?? 0)),
                TotalGrossWeightKg = g.Sum(x => (long)(x.GrossWeightKg ?? 0)),
                TotalTareWeightKg = g.Sum(x => (long)(x.TareWeightKg ?? 0)),
                DistinctCargo = g.Select(x => x.CargoId).Distinct().Count(),
                DistinctVehicles = g.Select(x => x.VehicleId).Distinct().Count()
            })
            .OrderBy(r => r.Date)
            .ThenBy(r => r.StationName)
            .ToListAsync(ct);

        var headers = new[]
        {
            "Date", "Station", "Total Weighings", "Unique Vehicles", "Cargo Types",
            "Total Net Weight (kg)", "Total Gross Weight (kg)", "Total Tare Weight (kg)"
        };

        var csvRows = rows.Select(r => new[]
        {
            FormatDate(r.Date),
            r.StationName,
            r.TotalWeighings.ToString(),
            r.DistinctVehicles.ToString(),
            r.DistinctCargo.ToString(),
            FormatNumber(r.TotalNetWeightKg),
            FormatNumber(r.TotalGrossWeightKg),
            FormatNumber(r.TotalTareWeightKg)
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, csvRows), "commercial_daily_summary", from, to);
        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Commercial Daily Summary", headers, csvRows, from, to), "commercial_daily_summary", from, to);

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Commercial Daily Summary",
            ReportSubtitle = "Aggregated commercial weighing statistics by date and station",
            DateFrom = from,
            DateTo = to,
            StationName = !string.IsNullOrEmpty(filters.StationId) ? rows.FirstOrDefault()?.StationName : null,
            Headers = headers,
            Rows = csvRows.ToArray(),
            SummaryItems =
            [
                ("Total Weighings", FormatNumber(rows.Sum(r => r.TotalWeighings))),
                ("Total Net Weight", $"{FormatNumber(rows.Sum(r => r.TotalNetWeightKg))} kg"),
                ("Unique Vehicles", FormatNumber(rows.Sum(r => r.DistinctVehicles))),
                ("Days", rows.Select(r => r.Date).Distinct().Count().ToString())
            ]
        };

        return PdfResult(doc, filters, "commercial_daily_summary", from, to);
    }

    // =====================================================================
    // Transporter Statement
    // =====================================================================

    private async Task<ReportResult> GenerateTransporterStatementAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var query = CommercialBaseQuery()
            .Where(w => w.WeighedAt >= from && w.WeighedAt <= to)
            .Where(w => w.TransporterId != null);

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            query = query.Where(w => w.StationId == stationId);

        var transporterData = await query
            .Include(w => w.Transporter)
            .Include(w => w.Cargo)
            .GroupBy(w => new { w.TransporterId, TransporterName = w.Transporter!.Name })
            .Select(g => new
            {
                TransporterName = g.Key.TransporterName,
                WeighingCount = g.Count(),
                TotalNetWeightKg = g.Sum(x => (long)(x.NetWeightKg ?? 0)),
                AvgNetWeightKg = (int)g.Average(x => x.NetWeightKg ?? 0),
                UniqueVehicles = g.Select(x => x.VehicleId).Distinct().Count(),
                UniqueCargo = g.Select(x => x.CargoId).Distinct().Count()
            })
            .OrderByDescending(t => t.TotalNetWeightKg)
            .ToListAsync(ct);

        var headers = new[]
        {
            "Transporter", "Weighing Count", "Unique Vehicles", "Cargo Types",
            "Total Net Weight (kg)", "Avg Net Weight (kg)"
        };

        var csvRows = transporterData.Select(t => new[]
        {
            t.TransporterName,
            t.WeighingCount.ToString(),
            t.UniqueVehicles.ToString(),
            t.UniqueCargo.ToString(),
            FormatNumber(t.TotalNetWeightKg),
            FormatNumber(t.AvgNetWeightKg)
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, csvRows), "commercial_transporter_statement", from, to);
        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Transporter Statement", headers, csvRows, from, to), "commercial_transporter_statement", from, to);

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Transporter Statement",
            ReportSubtitle = "Commercial weighing summary by transporter",
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = csvRows.ToArray(),
            SummaryItems =
            [
                ("Transporters", transporterData.Count.ToString()),
                ("Total Weighings", FormatNumber(transporterData.Sum(t => t.WeighingCount))),
                ("Total Net Weight", $"{FormatNumber(transporterData.Sum(t => t.TotalNetWeightKg))} kg")
            ]
        };

        return PdfResult(doc, filters, "commercial_transporter_statement", from, to);
    }

    // =====================================================================
    // Cargo Volume Report
    // =====================================================================

    private async Task<ReportResult> GenerateCargoVolumeAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var query = CommercialBaseQuery()
            .Where(w => w.WeighedAt >= from && w.WeighedAt <= to)
            .Where(w => w.CargoId != null);

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            query = query.Where(w => w.StationId == stationId);

        var cargoData = await query
            .Include(w => w.Cargo)
            .GroupBy(w => new { w.CargoId, CargoName = w.Cargo!.Name })
            .Select(g => new
            {
                CargoName = g.Key.CargoName,
                TripCount = g.Count(),
                TotalNetWeightKg = g.Sum(x => (long)(x.NetWeightKg ?? 0)),
                AvgNetWeightKg = (int)g.Average(x => x.NetWeightKg ?? 0),
                MaxNetWeightKg = g.Max(x => x.NetWeightKg ?? 0),
                MinNetWeightKg = g.Min(x => x.NetWeightKg ?? 0)
            })
            .OrderByDescending(c => c.TotalNetWeightKg)
            .ToListAsync(ct);

        var headers = new[]
        {
            "Cargo Type", "Trip Count", "Total Net Weight (kg)",
            "Avg Net Weight (kg)", "Min Net Weight (kg)", "Max Net Weight (kg)"
        };

        var csvRows = cargoData.Select(c => new[]
        {
            c.CargoName,
            c.TripCount.ToString(),
            FormatNumber(c.TotalNetWeightKg),
            FormatNumber(c.AvgNetWeightKg),
            FormatNumber(c.MinNetWeightKg),
            FormatNumber(c.MaxNetWeightKg)
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, csvRows), "cargo_volume", from, to);
        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Cargo Volume Report", headers, csvRows, from, to), "cargo_volume", from, to);

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Cargo Volume Report",
            ReportSubtitle = "Volume trends by cargo type over the reporting period",
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = csvRows.ToArray(),
            SummaryItems =
            [
                ("Cargo Types", cargoData.Count.ToString()),
                ("Total Trips", FormatNumber(cargoData.Sum(c => c.TripCount))),
                ("Total Net Weight", $"{FormatNumber(cargoData.Sum(c => c.TotalNetWeightKg))} kg")
            ]
        };

        return PdfResult(doc, filters, "cargo_volume", from, to);
    }

    // =====================================================================
    // Weight Discrepancy Report
    // =====================================================================

    private async Task<ReportResult> GenerateWeightDiscrepancyAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        // Default threshold: 100 kg discrepancy (use ControlStatus field as threshold override)
        var threshold = 100;
        if (!string.IsNullOrEmpty(filters.ControlStatus) && int.TryParse(filters.ControlStatus, out var parsed))
            threshold = parsed;

        var query = CommercialBaseQuery()
            .Where(w => w.WeighedAt >= from && w.WeighedAt <= to)
            .Where(w => w.WeightDiscrepancyKg != null)
            .Where(w => Math.Abs(w.WeightDiscrepancyKg!.Value) > threshold);

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            query = query.Where(w => w.StationId == stationId);

        var discrepancies = await query
            .Include(w => w.Station)
            .Include(w => w.Cargo)
            .Include(w => w.Transporter)
            .OrderByDescending(w => Math.Abs(w.WeightDiscrepancyKg!.Value))
            .Take(filters.PageSize)
            .Select(w => new
            {
                w.TicketNumber,
                WeighedAt = w.WeighedAt,
                StationName = w.Station!.Name,
                VehicleReg = w.VehicleRegNumber,
                TransporterName = w.Transporter != null ? w.Transporter.Name : "-",
                CargoName = w.Cargo != null ? w.Cargo.Name : "-",
                ExpectedKg = w.ExpectedNetWeightKg ?? 0,
                ActualKg = w.NetWeightKg ?? 0,
                DiscrepancyKg = w.WeightDiscrepancyKg!.Value,
                VariancePct = w.ExpectedNetWeightKg.HasValue && w.ExpectedNetWeightKg.Value > 0
                    ? (decimal)w.WeightDiscrepancyKg.Value / w.ExpectedNetWeightKg.Value * 100
                    : 0m
            })
            .ToListAsync(ct);

        var headers = new[]
        {
            "Ticket #", "Date/Time", "Station", "Vehicle Reg", "Transporter", "Cargo",
            "Expected (kg)", "Actual (kg)", "Discrepancy (kg)", "Variance %"
        };

        var csvRows = discrepancies.Select(d => new[]
        {
            d.TicketNumber,
            d.WeighedAt.ToString("dd/MM/yyyy HH:mm"),
            d.StationName,
            d.VehicleReg,
            d.TransporterName,
            d.CargoName,
            FormatNumber(d.ExpectedKg),
            FormatNumber(d.ActualKg),
            FormatNumber(d.DiscrepancyKg),
            $"{d.VariancePct:F1}%"
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, csvRows), "weight_discrepancy", from, to);
        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Weight Discrepancy Report", headers, csvRows, from, to), "weight_discrepancy", from, to);

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Weight Discrepancy Report",
            ReportSubtitle = $"Transactions with discrepancy exceeding {FormatNumber(threshold)} kg",
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = csvRows.ToArray(),
            SummaryItems =
            [
                ("Discrepancies Found", discrepancies.Count.ToString()),
                ("Threshold", $"{FormatNumber(threshold)} kg"),
                ("Avg Discrepancy", discrepancies.Count > 0
                    ? $"{FormatNumber((int)discrepancies.Average(d => Math.Abs(d.DiscrepancyKg)))} kg"
                    : "N/A"),
                ("Max Discrepancy", discrepancies.Count > 0
                    ? $"{FormatNumber(discrepancies.Max(d => Math.Abs(d.DiscrepancyKg)))} kg"
                    : "N/A")
            ]
        };

        return PdfResult(doc, filters, "weight_discrepancy", from, to);
    }

    // =====================================================================
    // Commercial Revenue Report
    // =====================================================================

    private async Task<ReportResult> GenerateCommercialRevenueAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var invoiceData = await _context.Invoices
            .Where(i => i.DeletedAt == null)
            .Where(i => i.InvoiceType == "commercial_weighing_fee")
            .Where(i => i.GeneratedAt >= from && i.GeneratedAt <= to)
            .GroupBy(i => new { i.GeneratedAt.Date, i.Currency, i.Status })
            .Select(g => new
            {
                Date = g.Key.Date,
                Currency = g.Key.Currency,
                Status = g.Key.Status,
                Count = g.Count(),
                TotalAmount = g.Sum(x => x.AmountDue)
            })
            .OrderBy(r => r.Date)
            .ThenBy(r => r.Currency)
            .ToListAsync(ct);

        var headers = new[]
        {
            "Date", "Currency", "Status", "Invoice Count", "Total Amount"
        };

        var csvRows = invoiceData.Select(r => new[]
        {
            FormatDate(r.Date),
            r.Currency,
            r.Status,
            r.Count.ToString(),
            $"{r.TotalAmount:N2}"
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, csvRows), "commercial_revenue", from, to);
        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Commercial Revenue Report", headers, csvRows, from, to), "commercial_revenue", from, to);

        var totalRevenue = invoiceData.Where(r => r.Status == "paid").Sum(r => r.TotalAmount);
        var totalPending = invoiceData.Where(r => r.Status == "pending").Sum(r => r.TotalAmount);

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Commercial Revenue Report",
            ReportSubtitle = "Revenue from commercial weighing fees",
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = csvRows.ToArray(),
            SummaryItems =
            [
                ("Total Invoices", FormatNumber(invoiceData.Sum(r => r.Count))),
                ("Paid Revenue", $"{totalRevenue:N2}"),
                ("Pending Revenue", $"{totalPending:N2}"),
                ("Total Billed", $"{invoiceData.Sum(r => r.TotalAmount):N2}")
            ]
        };

        return PdfResult(doc, filters, "commercial_revenue", from, to);
    }

    // =====================================================================
    // Throughput Report
    // =====================================================================

    private async Task<ReportResult> GenerateThroughputAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var query = CommercialBaseQuery()
            .Where(w => w.WeighedAt >= from && w.WeighedAt <= to);

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            query = query.Where(w => w.StationId == stationId);

        var stationData = await query
            .Include(w => w.Station)
            .GroupBy(w => new { w.StationId, StationName = w.Station!.Name, w.WeighedAt.Date })
            .Select(g => new
            {
                StationName = g.Key.StationName,
                Date = g.Key.Date,
                VehicleCount = g.Count(),
                // Count transactions that have both first and second weight timestamps for processing time
                TwoPassCount = g.Count(x => x.FirstWeightAt != null && x.SecondWeightAt != null),
                // We'll compute avg processing time in-memory since EF can't do TimeSpan math
                FirstWeights = g.Where(x => x.FirstWeightAt != null && x.SecondWeightAt != null)
                    .Select(x => new { x.FirstWeightAt, x.SecondWeightAt })
                    .ToList()
            })
            .OrderBy(r => r.Date)
            .ThenBy(r => r.StationName)
            .ToListAsync(ct);

        // Calculate hours of operation per day (assume 24h or derive from first/last transaction)
        var headers = new[]
        {
            "Date", "Station", "Vehicles Weighed", "Two-Pass Transactions",
            "Avg Processing Time (min)", "Vehicles/Hour"
        };

        var csvRows = stationData.Select(s =>
        {
            var avgMinutes = s.FirstWeights.Count > 0
                ? s.FirstWeights.Average(x => (x.SecondWeightAt!.Value - x.FirstWeightAt!.Value).TotalMinutes)
                : 0;
            // Estimate vehicles per hour: count / 24 for simplicity (full day)
            var vph = s.VehicleCount / 24.0;
            return new[]
            {
                FormatDate(s.Date),
                s.StationName,
                s.VehicleCount.ToString(),
                s.TwoPassCount.ToString(),
                avgMinutes > 0 ? $"{avgMinutes:F1}" : "-",
                $"{vph:F1}"
            };
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, csvRows), "throughput", from, to);
        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Throughput Report", headers, csvRows, from, to), "throughput", from, to);

        var totalVehicles = stationData.Sum(s => s.VehicleCount);
        var totalTwoPass = stationData.Sum(s => s.TwoPassCount);
        var allProcessingTimes = stationData.SelectMany(s => s.FirstWeights).ToList();
        var overallAvgMin = allProcessingTimes.Count > 0
            ? allProcessingTimes.Average(x => (x.SecondWeightAt!.Value - x.FirstWeightAt!.Value).TotalMinutes)
            : 0;

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Throughput Report",
            ReportSubtitle = "Vehicle throughput and processing time by station",
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = csvRows.ToArray(),
            SummaryItems =
            [
                ("Total Vehicles", FormatNumber(totalVehicles)),
                ("Two-Pass Transactions", FormatNumber(totalTwoPass)),
                ("Avg Processing Time", overallAvgMin > 0 ? $"{overallAvgMin:F1} min" : "N/A"),
                ("Stations", stationData.Select(s => s.StationName).Distinct().Count().ToString())
            ]
        };

        return PdfResult(doc, filters, "throughput", from, to);
    }

    // =====================================================================
    // Tare Weight Audit Report
    // =====================================================================

    private async Task<ReportResult> GenerateTareWeightAuditAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var query = _context.VehicleTareHistory
            .Where(th => th.DeletedAt == null)
            .Where(th => th.WeighedAt >= from && th.WeighedAt <= to);

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            query = query.Where(th => th.StationId == stationId);

        // Use ControlStatus as vehicle reg filter if provided
        if (!string.IsNullOrEmpty(filters.WeighingType))
        {
            // WeighingType field reused as vehicle reg filter for this report
            query = query.Where(th => th.Vehicle != null && th.Vehicle.RegNo.Contains(filters.WeighingType));
        }

        var tareData = await query
            .Include(th => th.Vehicle)
            .Include(th => th.Station)
            .OrderBy(th => th.VehicleId)
            .ThenBy(th => th.WeighedAt)
            .Take(filters.PageSize)
            .Select(th => new
            {
                VehicleReg = th.Vehicle != null ? th.Vehicle.RegNo : "-",
                th.VehicleId,
                th.TareWeightKg,
                th.WeighedAt,
                StationName = th.Station != null ? th.Station.Name : "-",
                th.Source,
                DefaultTare = th.Vehicle != null ? th.Vehicle.DefaultTareWeightKg : null,
                th.Notes
            })
            .ToListAsync(ct);

        // Group by vehicle and detect anomalies (>5% drift from previous measurement)
        var processedRows = new List<string[]>();
        var anomalyCount = 0;

        var vehicleGroups = tareData.GroupBy(t => t.VehicleId);
        foreach (var group in vehicleGroups)
        {
            var entries = group.OrderBy(e => e.WeighedAt).ToList();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var previousTare = i > 0 ? entries[i - 1].TareWeightKg : (entry.DefaultTare ?? entry.TareWeightKg);
                var driftPct = previousTare > 0
                    ? Math.Abs((decimal)(entry.TareWeightKg - previousTare) / previousTare * 100)
                    : 0m;
                var isAnomaly = driftPct > 5;
                if (isAnomaly) anomalyCount++;

                processedRows.Add([
                    entry.VehicleReg,
                    entry.WeighedAt.ToString("dd/MM/yyyy HH:mm"),
                    entry.StationName,
                    FormatNumber(entry.TareWeightKg),
                    FormatNumber(previousTare),
                    $"{driftPct:F1}%",
                    entry.Source,
                    isAnomaly ? "ANOMALY" : "OK",
                    entry.Notes ?? "-"
                ]);
            }
        }

        var headers = new[]
        {
            "Vehicle Reg", "Date/Time", "Station", "Tare Weight (kg)", "Previous Tare (kg)",
            "Drift %", "Source", "Status", "Notes"
        };

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, processedRows), "tare_weight_audit", from, to);
        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Tare Weight Audit Report", headers, processedRows, from, to), "tare_weight_audit", from, to);

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Tare Weight Audit Report",
            ReportSubtitle = "Tare weight changes per vehicle with anomaly detection (>5% drift)",
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = processedRows.ToArray(),
            SummaryItems =
            [
                ("Tare Records", processedRows.Count.ToString()),
                ("Unique Vehicles", vehicleGroups.Count().ToString()),
                ("Anomalies Detected", anomalyCount.ToString()),
                ("Anomaly Rate", processedRows.Count > 0
                    ? $"{(decimal)anomalyCount / processedRows.Count * 100:F1}%"
                    : "N/A")
            ]
        };

        return PdfResult(doc, filters, "tare_weight_audit", from, to);
    }

    // =====================================================================
    // Fleet Utilization Report
    // =====================================================================

    private async Task<ReportResult> GenerateFleetUtilizationAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var query = CommercialBaseQuery()
            .Where(w => w.WeighedAt >= from && w.WeighedAt <= to);

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            query = query.Where(w => w.StationId == stationId);

        var vehicleData = await query
            .Include(w => w.Vehicle)
            .Include(w => w.Transporter)
            .GroupBy(w => new
            {
                w.VehicleId,
                VehicleReg = w.VehicleRegNumber,
                TransporterName = w.Transporter != null ? w.Transporter.Name : "-",
                MaxPayload = w.Vehicle != null ? w.Vehicle.DefaultTareWeightKg : null
            })
            .Select(g => new
            {
                VehicleReg = g.Key.VehicleReg,
                TransporterName = g.Key.TransporterName,
                TripCount = g.Count(),
                TotalNetWeightKg = g.Sum(x => (long)(x.NetWeightKg ?? 0)),
                AvgPayloadKg = (int)g.Average(x => x.NetWeightKg ?? 0),
                MaxPayloadKg = g.Max(x => x.NetWeightKg ?? 0),
                MinPayloadKg = g.Min(x => x.NetWeightKg ?? 0),
                AvgGrossKg = (int)g.Average(x => x.GrossWeightKg ?? 0),
                AvgTareKg = (int)g.Average(x => x.TareWeightKg ?? 0)
            })
            .OrderByDescending(v => v.TotalNetWeightKg)
            .ToListAsync(ct);

        var headers = new[]
        {
            "Vehicle Reg", "Transporter", "Trip Count", "Total Net Weight (kg)",
            "Avg Payload (kg)", "Min Payload (kg)", "Max Payload (kg)",
            "Avg Gross (kg)", "Avg Tare (kg)"
        };

        var csvRows = vehicleData.Select(v => new[]
        {
            v.VehicleReg,
            v.TransporterName,
            v.TripCount.ToString(),
            FormatNumber(v.TotalNetWeightKg),
            FormatNumber(v.AvgPayloadKg),
            FormatNumber(v.MinPayloadKg),
            FormatNumber(v.MaxPayloadKg),
            FormatNumber(v.AvgGrossKg),
            FormatNumber(v.AvgTareKg)
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, csvRows), "fleet_utilization", from, to);
        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Fleet Utilization Report", headers, csvRows, from, to), "fleet_utilization", from, to);

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Fleet Utilization Report",
            ReportSubtitle = "Per-vehicle trip count, payload, and utilization metrics",
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = csvRows.ToArray(),
            SummaryItems =
            [
                ("Vehicles", vehicleData.Count.ToString()),
                ("Total Trips", FormatNumber(vehicleData.Sum(v => v.TripCount))),
                ("Total Net Weight", $"{FormatNumber(vehicleData.Sum(v => v.TotalNetWeightKg))} kg"),
                ("Avg Payload", vehicleData.Count > 0
                    ? $"{FormatNumber((int)vehicleData.Average(v => v.AvgPayloadKg))} kg"
                    : "N/A")
            ]
        };

        return PdfResult(doc, filters, "fleet_utilization", from, to);
    }

    // =====================================================================
    // Driver Productivity Report
    // =====================================================================

    private async Task<ReportResult> GenerateDriverProductivityAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var query = CommercialBaseQuery()
            .Where(w => w.WeighedAt >= from && w.WeighedAt <= to)
            .Where(w => w.DriverId != null);

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            query = query.Where(w => w.StationId == stationId);

        var driverData = await query
            .Include(w => w.Driver)
            .GroupBy(w => new
            {
                w.DriverId,
                DriverName = w.Driver != null ? w.Driver.FullNames + " " + w.Driver.Surname : "-"
            })
            .Select(g => new
            {
                DriverName = g.Key.DriverName,
                TripCount = g.Count(),
                TotalNetWeightKg = g.Sum(x => (long)(x.NetWeightKg ?? 0)),
                AvgNetWeightKg = (int)g.Average(x => x.NetWeightKg ?? 0),
                // Count two-pass transactions for turnaround time calc
                TwoPassCount = g.Count(x => x.FirstWeightAt != null && x.SecondWeightAt != null),
                TurnaroundData = g.Where(x => x.FirstWeightAt != null && x.SecondWeightAt != null)
                    .Select(x => new { x.FirstWeightAt, x.SecondWeightAt })
                    .ToList()
            })
            .OrderByDescending(d => d.TripCount)
            .ToListAsync(ct);

        var headers = new[]
        {
            "Driver", "Trip Count", "Total Net Weight (kg)",
            "Avg Net Weight (kg)", "Two-Pass Trips", "Avg Turnaround (min)"
        };

        var csvRows = driverData.Select(d =>
        {
            var avgTurnaround = d.TurnaroundData.Count > 0
                ? d.TurnaroundData.Average(x => (x.SecondWeightAt!.Value - x.FirstWeightAt!.Value).TotalMinutes)
                : 0;
            return new[]
            {
                d.DriverName,
                d.TripCount.ToString(),
                FormatNumber(d.TotalNetWeightKg),
                FormatNumber(d.AvgNetWeightKg),
                d.TwoPassCount.ToString(),
                avgTurnaround > 0 ? $"{avgTurnaround:F1}" : "-"
            };
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, csvRows), "driver_productivity", from, to);
        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Driver Productivity Report", headers, csvRows, from, to), "driver_productivity", from, to);

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Driver Productivity Report",
            ReportSubtitle = "Per-driver trip count, net weight, and turnaround time",
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = csvRows.ToArray(),
            SummaryItems =
            [
                ("Drivers", driverData.Count.ToString()),
                ("Total Trips", FormatNumber(driverData.Sum(d => d.TripCount))),
                ("Total Net Weight", $"{FormatNumber(driverData.Sum(d => d.TotalNetWeightKg))} kg"),
                ("Avg Trips/Driver", driverData.Count > 0
                    ? $"{(decimal)driverData.Sum(d => d.TripCount) / driverData.Count:F1}"
                    : "N/A")
            ]
        };

        return PdfResult(doc, filters, "driver_productivity", from, to);
    }

    // =====================================================================
    // Quality & Commodity Report
    // =====================================================================

    private async Task<ReportResult> GenerateQualityCommodityAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var query = CommercialBaseQuery()
            .Where(w => w.WeighedAt >= from && w.WeighedAt <= to)
            .Where(w => w.QualityDeductionKg != null || w.IndustryMetadata != null);

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            query = query.Where(w => w.StationId == stationId);

        var qualityData = await query
            .Include(w => w.Cargo)
            .GroupBy(w => new { w.CargoId, CargoName = w.Cargo != null ? w.Cargo.Name : "Unknown" })
            .Select(g => new
            {
                CargoName = g.Key.CargoName,
                TransactionCount = g.Count(),
                WithDeductions = g.Count(x => x.QualityDeductionKg != null && x.QualityDeductionKg > 0),
                TotalDeductionKg = g.Sum(x => (long)(x.QualityDeductionKg ?? 0)),
                AvgDeductionKg = g.Where(x => x.QualityDeductionKg != null && x.QualityDeductionKg > 0).Any()
                    ? (int)g.Where(x => x.QualityDeductionKg != null && x.QualityDeductionKg > 0).Average(x => x.QualityDeductionKg!.Value)
                    : 0,
                TotalNetWeightKg = g.Sum(x => (long)(x.NetWeightKg ?? 0)),
                TotalAdjustedKg = g.Sum(x => (long)(x.AdjustedNetWeightKg ?? x.NetWeightKg ?? 0)),
                WithMetadata = g.Count(x => x.IndustryMetadata != null)
            })
            .OrderByDescending(q => q.TotalDeductionKg)
            .ToListAsync(ct);

        var headers = new[]
        {
            "Cargo Type", "Transactions", "With Deductions", "Total Deduction (kg)",
            "Avg Deduction (kg)", "Total Net Weight (kg)", "Total Adjusted (kg)",
            "With Industry Metadata"
        };

        var csvRows = qualityData.Select(q => new[]
        {
            q.CargoName,
            q.TransactionCount.ToString(),
            q.WithDeductions.ToString(),
            FormatNumber(q.TotalDeductionKg),
            FormatNumber(q.AvgDeductionKg),
            FormatNumber(q.TotalNetWeightKg),
            FormatNumber(q.TotalAdjustedKg),
            q.WithMetadata.ToString()
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, csvRows), "quality_commodity", from, to);
        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Quality & Commodity Report", headers, csvRows, from, to), "quality_commodity", from, to);

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Quality & Commodity Report",
            ReportSubtitle = "Quality deduction stats by cargo type",
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = csvRows.ToArray(),
            SummaryItems =
            [
                ("Cargo Types", qualityData.Count.ToString()),
                ("Total Transactions", FormatNumber(qualityData.Sum(q => q.TransactionCount))),
                ("Total Deductions", $"{FormatNumber(qualityData.Sum(q => q.TotalDeductionKg))} kg"),
                ("Transactions with Deductions", FormatNumber(qualityData.Sum(q => q.WithDeductions)))
            ]
        };

        return PdfResult(doc, filters, "quality_commodity", from, to);
    }

    // =====================================================================
    // Inner PDF Document Class
    // =====================================================================

    /// <summary>
    /// Reusable PDF document for commercial reports following the standard
    /// summary cards + data table pattern.
    /// </summary>
    private sealed class CommercialReportDocumentBase : BaseReportDocument
    {
        public string[] Headers { get; set; } = [];
        public string[][] Rows { get; set; } = [];
        public (string label, string value)[] SummaryItems { get; set; } = [];

        protected override void ComposeContent(IContainer container)
        {
            container.Column(col =>
            {
                col.Spacing(5);

                if (SummaryItems.Length > 0)
                    col.Item().Element(c => ComposeSummaryCards(c, SummaryItems));

                col.Item().Element(c => ComposeDataTable(c, Headers, Rows));
            });
        }
    }
}
