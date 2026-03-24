using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Reporting;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Models;
using TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports;

namespace TruLoad.Backend.Services.Implementations.Reporting.Modules;

/// <summary>
/// Generates all weighing module reports (PDF and CSV).
/// Covers daily summaries, weighbridge registers, compliance trends, axle overload analysis,
/// station performance, transporter statements, reweigh tracking, special releases, and scale tests.
/// </summary>
public class WeighingReportGenerator : BaseReportGenerator
{
    private readonly TruLoadDbContext _context;

    public WeighingReportGenerator(TruLoadDbContext context)
    {
        _context = context;
    }

    public override string Module => "weighing";

    /// <summary>
    /// Determine the charging currency from the default act (via settings).
    /// Traffic Act = KES, EAC = USD.
    /// </summary>
    private async Task<string> GetChargingCurrencyAsync(CancellationToken ct = default)
    {
        var defaultActCodeSetting = await _context.ApplicationSettings
            .Where(s => s.SettingKey == "compliance.default_act_code")
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(ct);

        var actCode = defaultActCodeSetting ?? "TRAFFIC_ACT";
        var defaultAct = await _context.ActDefinitions
            .Where(a => a.Code == actCode && a.IsActive)
            .FirstOrDefaultAsync(ct);
        return defaultAct?.ChargingCurrency ?? "KES";
    }

    /// <summary>
    /// Select the correct fee value based on charging currency.
    /// </summary>
    private static decimal GetFee(WeighingTransaction w, string currency)
    {
        return currency.Equals("KES", StringComparison.OrdinalIgnoreCase) && w.TotalFeeKes > 0
            ? w.TotalFeeKes
            : w.TotalFeeUsd;
    }

    public override List<ReportDefinitionDto> GetDefinitions() =>
    [
        Def("daily-summary", "Daily Weighing Summary",
            "Aggregated daily statistics including total vehicles weighed, compliance rate, and overload totals per station."),
        Def("weighbridge-register", "Weighbridge Register",
            "Detailed register of all weighing transactions with vehicle, driver, weight, and compliance data."),
        Def("compliance-trend", "Compliance Trend Analysis",
            "Daily compliance rates over the selected period, showing overload vs compliant vehicle counts."),
        Def("axle-overload", "Axle Overload Analysis",
            "Breakdown of overloaded axles by type and configuration, with pavement damage factor analysis."),
        Def("station-performance", "Station Performance Report",
            "Comparative performance metrics across weighbridge stations for the selected period."),
        Def("transporter-statement", "Transporter Statement",
            "Weighing history and compliance summary grouped by transporter company."),
        Def("overloaded-vehicles", "Overloaded Vehicles Register",
            "Filtered register showing only overloaded vehicles with overload amounts and violation details."),
        Def("reweigh-statement", "Reweigh Statement",
            "Tracks reweigh cycles for vehicles that underwent load redistribution or correction."),
        Def("special-release", "Special Release Register",
            "Register of all special releases issued, with release type, authorization, and compliance status."),
        Def("scale-test", "Scale Test Log",
            "Log of daily scale calibration tests per station and bound, with pass/fail results and deviations.")
    ];

    public override async Task<ReportResult> GenerateAsync(
        string reportType, ReportFilterParams filters, string format, CancellationToken ct = default)
    {
        return reportType switch
        {
            "daily-summary" => await GenerateDailySummaryAsync(filters, format, ct),
            "weighbridge-register" => await GenerateWeighbridgeRegisterAsync(filters, format, ct),
            "compliance-trend" => await GenerateComplianceTrendAsync(filters, format, ct),
            "axle-overload" => await GenerateAxleOverloadAsync(filters, format, ct),
            "station-performance" => await GenerateStationPerformanceAsync(filters, format, ct),
            "transporter-statement" => await GenerateTransporterStatementAsync(filters, format, ct),
            "overloaded-vehicles" => await GenerateOverloadedVehiclesAsync(filters, format, ct),
            "reweigh-statement" => await GenerateReweighStatementAsync(filters, format, ct),
            "special-release" => await GenerateSpecialReleaseAsync(filters, format, ct),
            "scale-test" => await GenerateScaleTestAsync(filters, format, ct),
            _ => throw new ArgumentException($"Unknown weighing report type: {reportType}")
        };
    }

    // =====================================================================
    // Daily Summary
    // =====================================================================

    private async Task<ReportResult> GenerateDailySummaryAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var currency = await GetChargingCurrencyAsync(ct);
        var (from, to) = GetDateRange(filters);

        var query = _context.WeighingTransactions
            .Where(w => w.DeletedAt == null)
            .Where(w => w.WeighedAt >= from && w.WeighedAt <= to)
            .Where(w => w.CaptureStatus == "captured");

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            query = query.Where(w => w.StationId == stationId);
        if (!string.IsNullOrEmpty(filters.WeighingType))
            query = query.Where(w => w.WeighingType == filters.WeighingType);
        if (!string.IsNullOrEmpty(filters.ControlStatus))
            query = ApplyControlStatusFilter(query, filters.ControlStatus);

        var rows = await query
            .Include(w => w.Station)
            .GroupBy(w => new { w.WeighedAt.Date, w.StationId, StationName = w.Station!.Name })
            .Select(g => new
            {
                Date = g.Key.Date,
                StationName = g.Key.StationName,
                TotalVehicles = g.Count(),
                Compliant = g.Count(x => x.IsCompliant),
                Overloaded = g.Count(x => !x.IsCompliant),
                TotalOverloadKg = g.Where(x => x.OverloadKg > 0).Sum(x => x.OverloadKg),
                AvgGvwKg = (int)g.Average(x => x.GvwMeasuredKg),
                TotalFeesUsd = g.Sum(x => x.TotalFeeUsd),
                TotalFeesKes = g.Sum(x => x.TotalFeeKes)
            })
            .OrderBy(r => r.Date)
            .ThenBy(r => r.StationName)
            .ToListAsync(ct);

        var headers = new[]
        {
            "Date", "Station", "Total Vehicles", "Compliant", "Overloaded",
            "Compliance %", "Total Overload (kg)", "Avg GVW (kg)", $"Total Fees ({currency})"
        };

        var csvRows = rows.Select(r => new[]
        {
            FormatDate(r.Date),
            r.StationName,
            r.TotalVehicles.ToString(),
            r.Compliant.ToString(),
            r.Overloaded.ToString(),
            r.TotalVehicles > 0
                ? $"{(decimal)r.Compliant / r.TotalVehicles * 100:F1}%"
                : "0.0%",
            FormatNumber(r.TotalOverloadKg),
            FormatNumber(r.AvgGvwKg),
            $"{(currency == "KES" && r.TotalFeesKes > 0 ? r.TotalFeesKes : r.TotalFeesUsd):N2}"
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, csvRows), "weighing_daily_summary", from, to);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Daily Weighing Summary", headers, csvRows, from, to), "weighing_daily_summary", from, to);

        var stationName = !string.IsNullOrEmpty(filters.StationId)
            ? rows.FirstOrDefault()?.StationName
            : null;

        var grandTotalVehicles = rows.Sum(r => r.TotalVehicles);
        var grandCompliant = rows.Sum(r => r.Compliant);
        var grandOverloaded = rows.Sum(r => r.Overloaded);

        var doc = new DailySummaryDocument
        {
            DateFrom = from,
            DateTo = to,
            StationName = stationName,
            Headers = headers,
            Rows = csvRows.ToArray(),
            SummaryItems =
            [
                ("Total Vehicles", FormatNumber(grandTotalVehicles)),
                ("Compliant", FormatNumber(grandCompliant)),
                ("Overloaded", FormatNumber(grandOverloaded)),
                ("Compliance Rate", grandTotalVehicles > 0
                    ? $"{(decimal)grandCompliant / grandTotalVehicles * 100:F1}%"
                    : "N/A")
            ]
        };

        return PdfResult(doc, filters, "weighing_daily_summary", from, to);
    }

    // =====================================================================
    // Weighbridge Register
    // =====================================================================

    private async Task<ReportResult> GenerateWeighbridgeRegisterAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var currency = await GetChargingCurrencyAsync(ct);
        var (from, to) = GetDateRange(filters);

        var query = _context.WeighingTransactions
            .Where(w => w.DeletedAt == null)
            .Where(w => w.WeighedAt >= from && w.WeighedAt <= to)
            .Where(w => w.CaptureStatus == "captured");

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            query = query.Where(w => w.StationId == stationId);
        if (!string.IsNullOrEmpty(filters.WeighingType))
            query = query.Where(w => w.WeighingType == filters.WeighingType);
        if (!string.IsNullOrEmpty(filters.ControlStatus))
            query = ApplyControlStatusFilter(query, filters.ControlStatus);

        var transactions = await query
            .Include(w => w.Vehicle).ThenInclude(v => v!.AxleConfiguration)
            .Include(w => w.Station)
            .Include(w => w.Driver)
            .Include(w => w.Transporter)
            .OrderBy(w => w.WeighedAt)
            .Take(filters.PageSize)
            .Select(w => new
            {
                w.TicketNumber,
                WeighedAt = w.WeighedAt,
                StationName = w.Station!.Name,
                w.Bound,
                VehicleReg = w.VehicleRegNumber,
                AxleConfig = w.Vehicle != null && w.Vehicle.AxleConfiguration != null
                    ? w.Vehicle.AxleConfiguration.AxleCode : "-",
                DriverName = w.Driver != null
                    ? w.Driver.FullNames + " " + w.Driver.Surname : "-",
                TransporterName = w.Transporter != null ? w.Transporter.Name : "-",
                w.GvwMeasuredKg,
                w.GvwPermissibleKg,
                w.OverloadKg,
                w.IsCompliant,
                w.ControlStatus,
                w.WeighingType,
                w.ReweighCycleNo,
                w.TotalFeeUsd,
                w.TotalFeeKes
            })
            .ToListAsync(ct);

        var headers = new[]
        {
            "Ticket #", "Date/Time", "Station", "Bound", "Vehicle Reg", "Axle Config",
            "Driver", "Transporter", "GVW (kg)", "Permissible (kg)", "Overload (kg)",
            "Status", "Type", "Reweigh #", $"Fee ({currency})"
        };

        var csvRows = transactions.Select(t => new[]
        {
            t.TicketNumber,
            t.WeighedAt.ToString("dd/MM/yyyy HH:mm"),
            t.StationName,
            t.Bound ?? "-",
            t.VehicleReg,
            t.AxleConfig,
            t.DriverName,
            t.TransporterName,
            FormatNumber(t.GvwMeasuredKg),
            FormatNumber(t.GvwPermissibleKg),
            t.OverloadKg > 0 ? FormatNumber(t.OverloadKg) : "-",
            t.IsCompliant ? "Compliant" : t.ControlStatus,
            t.WeighingType,
            t.ReweighCycleNo > 0 ? t.ReweighCycleNo.ToString() : "-",
            $"{t.TotalFeeUsd:N2}"
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, csvRows), "weighbridge_register", from, to);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Weighbridge Register", headers, csvRows, from, to), "weighbridge_register", from, to);

        var stationName = !string.IsNullOrEmpty(filters.StationId)
            ? transactions.FirstOrDefault()?.StationName
            : null;

        var doc = new WeighbridgeRegisterDocument
        {
            DateFrom = from,
            DateTo = to,
            StationName = stationName,
            Headers = headers,
            Rows = csvRows.ToArray(),
            TotalRecords = transactions.Count
        };

        return PdfResult(doc, filters, "weighbridge_register", from, to);
    }

    // =====================================================================
    // Compliance Trend
    // =====================================================================

    private async Task<ReportResult> GenerateComplianceTrendAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var query = _context.WeighingTransactions
            .Where(w => w.DeletedAt == null)
            .Where(w => w.WeighedAt >= from && w.WeighedAt <= to)
            .Where(w => w.CaptureStatus == "captured");

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            query = query.Where(w => w.StationId == stationId);
        if (!string.IsNullOrEmpty(filters.WeighingType))
            query = query.Where(w => w.WeighingType == filters.WeighingType);
        if (!string.IsNullOrEmpty(filters.ControlStatus))
            query = ApplyControlStatusFilter(query, filters.ControlStatus);

        var dailyData = await query
            .GroupBy(w => w.WeighedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Total = g.Count(),
                Compliant = g.Count(x => x.IsCompliant),
                Overloaded = g.Count(x => !x.IsCompliant),
                SentToYard = g.Count(x => x.IsSentToYard),
                AvgOverloadKg = g.Where(x => x.OverloadKg > 0).Any()
                    ? (int)g.Where(x => x.OverloadKg > 0).Average(x => x.OverloadKg) : 0,
                MaxOverloadKg = g.Where(x => x.OverloadKg > 0).Any()
                    ? g.Where(x => x.OverloadKg > 0).Max(x => x.OverloadKg) : 0
            })
            .OrderBy(r => r.Date)
            .ToListAsync(ct);

        var headers = new[]
        {
            "Date", "Total Weighed", "Compliant", "Overloaded", "Sent to Yard",
            "Compliance %", "Avg Overload (kg)", "Max Overload (kg)"
        };

        var csvRows = dailyData.Select(d => new[]
        {
            FormatDate(d.Date),
            d.Total.ToString(),
            d.Compliant.ToString(),
            d.Overloaded.ToString(),
            d.SentToYard.ToString(),
            d.Total > 0 ? $"{(decimal)d.Compliant / d.Total * 100:F1}%" : "0.0%",
            FormatNumber(d.AvgOverloadKg),
            FormatNumber(d.MaxOverloadKg)
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, csvRows), "compliance_trend", from, to);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Compliance Trend Analysis", headers, csvRows, from, to), "compliance_trend", from, to);

        var grandTotal = dailyData.Sum(d => d.Total);
        var grandCompliant = dailyData.Sum(d => d.Compliant);

        var doc = new ComplianceTrendDocument
        {
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = csvRows.ToArray(),
            SummaryItems =
            [
                ("Period Total", FormatNumber(grandTotal)),
                ("Compliant", FormatNumber(grandCompliant)),
                ("Overloaded", FormatNumber(dailyData.Sum(d => d.Overloaded))),
                ("Overall Compliance", grandTotal > 0
                    ? $"{(decimal)grandCompliant / grandTotal * 100:F1}%"
                    : "N/A"),
                ("Days Analysed", dailyData.Count.ToString())
            ]
        };

        return PdfResult(doc, filters, "compliance_trend", from, to);
    }

    // =====================================================================
    // Axle Overload Analysis
    // =====================================================================

    private async Task<ReportResult> GenerateAxleOverloadAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var baseQuery = _context.WeighingTransactions
            .Where(w => w.DeletedAt == null)
            .Where(w => w.WeighedAt >= from && w.WeighedAt <= to)
            .Where(w => w.CaptureStatus == "captured")
            .Where(w => !w.IsCompliant);

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            baseQuery = baseQuery.Where(w => w.StationId == stationId);

        var overloadedTxIds = await baseQuery.Select(w => w.Id).ToListAsync(ct);

        var axleData = await _context.Set<WeighingAxle>()
            .Where(a => a.DeletedAt == null)
            .Where(a => overloadedTxIds.Contains(a.WeighingId))
            .Where(a => a.MeasuredWeightKg > a.PermissibleWeightKg)
            .Include(a => a.AxleConfiguration)
            .Include(a => a.WeighingTransaction).ThenInclude(w => w!.Station)
            .Include(a => a.WeighingTransaction).ThenInclude(w => w!.Vehicle)
            .OrderByDescending(a => a.MeasuredWeightKg - a.PermissibleWeightKg)
            .Take(filters.PageSize)
            .Select(a => new
            {
                TicketNumber = a.WeighingTransaction!.TicketNumber,
                WeighedAt = a.WeighingTransaction.WeighedAt,
                StationName = a.WeighingTransaction.Station!.Name,
                VehicleReg = a.WeighingTransaction.VehicleRegNumber,
                a.AxleNumber,
                a.AxleType,
                a.AxleGrouping,
                AxleConfig = a.AxleConfiguration != null ? a.AxleConfiguration.AxleCode : "-",
                a.MeasuredWeightKg,
                a.PermissibleWeightKg,
                OverloadKg = a.MeasuredWeightKg - a.PermissibleWeightKg,
                a.PavementDamageFactor
            })
            .ToListAsync(ct);

        var headers = new[]
        {
            "Ticket #", "Date", "Station", "Vehicle Reg", "Axle #", "Axle Type",
            "Grouping", "Config", "Measured (kg)", "Permissible (kg)",
            "Overload (kg)", "Overload %", "Pavement Damage Factor"
        };

        var csvRows = axleData.Select(a => new[]
        {
            a.TicketNumber,
            a.WeighedAt.ToString("dd/MM/yyyy HH:mm"),
            a.StationName,
            a.VehicleReg,
            a.AxleNumber.ToString(),
            a.AxleType,
            a.AxleGrouping,
            a.AxleConfig,
            FormatNumber(a.MeasuredWeightKg),
            FormatNumber(a.PermissibleWeightKg),
            FormatNumber(a.OverloadKg),
            a.PermissibleWeightKg > 0
                ? $"{(decimal)a.OverloadKg / a.PermissibleWeightKg * 100:F1}%"
                : "-",
            a.PavementDamageFactor.ToString("F4")
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, csvRows), "axle_overload_analysis", from, to);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Axle Overload Analysis", headers, csvRows, from, to), "axle_overload_analysis", from, to);

        // Aggregate by axle type for summary
        var axleTypeSummary = axleData
            .GroupBy(a => a.AxleType)
            .Select(g => (g.Key, $"{g.Count()} overloads, avg {FormatNumber((int)g.Average(x => x.OverloadKg))} kg"))
            .ToArray();

        var doc = new AxleOverloadDocument
        {
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = csvRows.ToArray(),
            SummaryItems = axleTypeSummary.Length > 0
                ? axleTypeSummary
                :
                [
                    ("Overloaded Axles", axleData.Count.ToString()),
                    ("No axle type breakdown", "-")
                ]
        };

        return PdfResult(doc, filters, "axle_overload_analysis", from, to);
    }

    // =====================================================================
    // Station Performance
    // =====================================================================

    private async Task<ReportResult> GenerateStationPerformanceAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var currency = await GetChargingCurrencyAsync(ct);
        var (from, to) = GetDateRange(filters);

        var query = _context.WeighingTransactions
            .Where(w => w.DeletedAt == null)
            .Where(w => w.WeighedAt >= from && w.WeighedAt <= to)
            .Where(w => w.CaptureStatus == "captured");

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            query = query.Where(w => w.StationId == stationId);

        var stationData = await query
            .Include(w => w.Station)
            .GroupBy(w => new { w.StationId, StationName = w.Station!.Name, StationCode = w.Station.Code })
            .Select(g => new
            {
                StationName = g.Key.StationName,
                StationCode = g.Key.StationCode,
                TotalVehicles = g.Count(),
                Compliant = g.Count(x => x.IsCompliant),
                Overloaded = g.Count(x => !x.IsCompliant),
                SentToYard = g.Count(x => x.IsSentToYard),
                TotalOverloadKg = g.Where(x => x.OverloadKg > 0).Sum(x => (long)x.OverloadKg),
                AvgGvwKg = (int)g.Average(x => x.GvwMeasuredKg),
                MaxOverloadKg = g.Where(x => x.OverloadKg > 0).Any()
                    ? g.Where(x => x.OverloadKg > 0).Max(x => x.OverloadKg) : 0,
                TotalFeesUsd = g.Sum(x => x.TotalFeeUsd),
                TotalFeesKes = g.Sum(x => x.TotalFeeKes),
                Reweighs = g.Count(x => x.ReweighCycleNo > 0),
                AutoWeighs = g.Count(x => x.CaptureSource == "auto"),
                ManualWeighs = g.Count(x => x.CaptureSource == "manual")
            })
            .OrderByDescending(s => s.TotalVehicles)
            .ToListAsync(ct);

        var headers = new[]
        {
            "Station", "Code", "Total Vehicles", "Compliant", "Overloaded",
            "Compliance %", "Sent to Yard", "Total Overload (kg)", "Avg GVW (kg)",
            "Max Overload (kg)", "Reweighs", $"Fees ({currency})"
        };

        var csvRows = stationData.Select(s => new[]
        {
            s.StationName,
            s.StationCode,
            s.TotalVehicles.ToString(),
            s.Compliant.ToString(),
            s.Overloaded.ToString(),
            s.TotalVehicles > 0
                ? $"{(decimal)s.Compliant / s.TotalVehicles * 100:F1}%"
                : "0.0%",
            s.SentToYard.ToString(),
            FormatNumber(s.TotalOverloadKg),
            FormatNumber(s.AvgGvwKg),
            FormatNumber(s.MaxOverloadKg),
            s.Reweighs.ToString(),
            $"{s.TotalFeesUsd:N2}"
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, csvRows), "station_performance", from, to);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Station Performance Report", headers, csvRows, from, to), "station_performance", from, to);

        var grandTotal = stationData.Sum(s => s.TotalVehicles);
        var grandCompliant = stationData.Sum(s => s.Compliant);

        var doc = new StationPerformanceDocument
        {
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = csvRows.ToArray(),
            SummaryItems =
            [
                ("Stations", stationData.Count.ToString()),
                ("Total Vehicles", FormatNumber(grandTotal)),
                ("Overall Compliance", grandTotal > 0
                    ? $"{(decimal)grandCompliant / grandTotal * 100:F1}%"
                    : "N/A"),
                ("Total Fees", $"{currency} {stationData.Sum(s => currency == "KES" ? s.TotalFeesKes : s.TotalFeesUsd):N2}")
            ]
        };

        return PdfResult(doc, filters, "station_performance", from, to);
    }

    // =====================================================================
    // Transporter Statement
    // =====================================================================

    private async Task<ReportResult> GenerateTransporterStatementAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var currency = await GetChargingCurrencyAsync(ct);
        var (from, to) = GetDateRange(filters);

        var query = _context.WeighingTransactions
            .Where(w => w.DeletedAt == null)
            .Where(w => w.WeighedAt >= from && w.WeighedAt <= to)
            .Where(w => w.CaptureStatus == "captured")
            .Where(w => w.TransporterId != null);

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            query = query.Where(w => w.StationId == stationId);

        var transporterData = await query
            .Include(w => w.Transporter)
            .GroupBy(w => new { w.TransporterId, TransporterName = w.Transporter!.Name, TransporterCode = w.Transporter.Code })
            .Select(g => new
            {
                TransporterName = g.Key.TransporterName,
                TransporterCode = g.Key.TransporterCode,
                TotalWeighings = g.Count(),
                Compliant = g.Count(x => x.IsCompliant),
                Overloaded = g.Count(x => !x.IsCompliant),
                TotalOverloadKg = g.Where(x => x.OverloadKg > 0).Sum(x => (long)x.OverloadKg),
                AvgOverloadKg = g.Where(x => x.OverloadKg > 0).Any()
                    ? (int)g.Where(x => x.OverloadKg > 0).Average(x => x.OverloadKg) : 0,
                SentToYard = g.Count(x => x.IsSentToYard),
                TotalFeesUsd = g.Sum(x => x.TotalFeeUsd),
                TotalFeesKes = g.Sum(x => x.TotalFeeKes),
                UniqueVehicles = g.Select(x => x.VehicleId).Distinct().Count()
            })
            .OrderByDescending(t => t.Overloaded)
            .ThenByDescending(t => t.TotalWeighings)
            .ToListAsync(ct);

        var headers = new[]
        {
            "Transporter", "Code", "Total Weighings", "Unique Vehicles", "Compliant",
            "Overloaded", "Compliance %", "Total Overload (kg)", "Avg Overload (kg)",
            "Sent to Yard", $"Fees ({currency})"
        };

        var csvRows = transporterData.Select(t => new[]
        {
            t.TransporterName,
            t.TransporterCode,
            t.TotalWeighings.ToString(),
            t.UniqueVehicles.ToString(),
            t.Compliant.ToString(),
            t.Overloaded.ToString(),
            t.TotalWeighings > 0
                ? $"{(decimal)t.Compliant / t.TotalWeighings * 100:F1}%"
                : "0.0%",
            FormatNumber(t.TotalOverloadKg),
            FormatNumber(t.AvgOverloadKg),
            t.SentToYard.ToString(),
            $"{t.TotalFeesUsd:N2}"
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, csvRows), "transporter_statement", from, to);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Transporter Statement", headers, csvRows, from, to), "transporter_statement", from, to);

        var doc = new TransporterStatementDocument
        {
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = csvRows.ToArray(),
            SummaryItems =
            [
                ("Transporters", transporterData.Count.ToString()),
                ("Total Weighings", FormatNumber(transporterData.Sum(t => t.TotalWeighings))),
                ("Total Overloaded", FormatNumber(transporterData.Sum(t => t.Overloaded))),
                ("Total Fees", $"{currency} {transporterData.Sum(t => currency == "KES" ? t.TotalFeesKes : t.TotalFeesUsd):N2}")
            ]
        };

        return PdfResult(doc, filters, "transporter_statement", from, to);
    }

    // =====================================================================
    // Overloaded Vehicles Register
    // =====================================================================

    private async Task<ReportResult> GenerateOverloadedVehiclesAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var currency = await GetChargingCurrencyAsync(ct);
        var (from, to) = GetDateRange(filters);

        var query = _context.WeighingTransactions
            .Where(w => w.DeletedAt == null)
            .Where(w => w.WeighedAt >= from && w.WeighedAt <= to)
            .Where(w => w.CaptureStatus == "captured")
            .Where(w => !w.IsCompliant && w.OverloadKg > 0);

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            query = query.Where(w => w.StationId == stationId);
        if (!string.IsNullOrEmpty(filters.WeighingType))
            query = query.Where(w => w.WeighingType == filters.WeighingType);
        if (!string.IsNullOrEmpty(filters.ControlStatus))
            query = ApplyControlStatusFilter(query, filters.ControlStatus);

        var overloaded = await query
            .Include(w => w.Vehicle).ThenInclude(v => v!.AxleConfiguration)
            .Include(w => w.Station)
            .Include(w => w.Driver)
            .Include(w => w.Transporter)
            .OrderByDescending(w => w.OverloadKg)
            .Take(filters.PageSize)
            .Select(w => new
            {
                w.TicketNumber,
                WeighedAt = w.WeighedAt,
                StationName = w.Station!.Name,
                VehicleReg = w.VehicleRegNumber,
                AxleConfig = w.Vehicle != null && w.Vehicle.AxleConfiguration != null
                    ? w.Vehicle.AxleConfiguration.AxleCode : "-",
                DriverName = w.Driver != null
                    ? w.Driver.FullNames + " " + w.Driver.Surname : "-",
                TransporterName = w.Transporter != null ? w.Transporter.Name : "-",
                w.GvwMeasuredKg,
                w.GvwPermissibleKg,
                w.OverloadKg,
                OverloadPct = w.GvwPermissibleKg > 0
                    ? (decimal)w.OverloadKg / w.GvwPermissibleKg * 100 : 0m,
                w.ControlStatus,
                w.IsSentToYard,
                w.ViolationReason,
                w.TotalFeeUsd,
                w.TotalFeeKes
            })
            .ToListAsync(ct);

        var headers = new[]
        {
            "Ticket #", "Date/Time", "Station", "Vehicle Reg", "Axle Config",
            "Driver", "Transporter", "GVW (kg)", "Permissible (kg)", "Overload (kg)",
            "Overload %", "Status", "Yard", $"Fee ({currency})"
        };

        var csvRows = overloaded.Select(o => new[]
        {
            o.TicketNumber,
            o.WeighedAt.ToString("dd/MM/yyyy HH:mm"),
            o.StationName,
            o.VehicleReg,
            o.AxleConfig,
            o.DriverName,
            o.TransporterName,
            FormatNumber(o.GvwMeasuredKg),
            FormatNumber(o.GvwPermissibleKg),
            FormatNumber(o.OverloadKg),
            $"{o.OverloadPct:F1}%",
            o.ControlStatus,
            o.IsSentToYard ? "Yes" : "No",
            $"{o.TotalFeeUsd:N2}"
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, csvRows), "overloaded_vehicles", from, to);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Overloaded Vehicles Register", headers, csvRows, from, to), "overloaded_vehicles", from, to);

        var doc = new OverloadedVehiclesDocument
        {
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = csvRows.ToArray(),
            SummaryItems =
            [
                ("Overloaded Vehicles", overloaded.Count.ToString()),
                ("Total Overload", $"{FormatNumber(overloaded.Sum(o => (long)o.OverloadKg))} kg"),
                ("Avg Overload", overloaded.Count > 0
                    ? $"{FormatNumber((int)overloaded.Average(o => o.OverloadKg))} kg"
                    : "N/A"),
                ("Max Overload", overloaded.Count > 0
                    ? $"{FormatNumber(overloaded.Max(o => o.OverloadKg))} kg"
                    : "N/A"),
                ("Total Fees", $"{currency} {overloaded.Sum(o => currency == "KES" ? o.TotalFeeKes : o.TotalFeeUsd):N2}")
            ]
        };

        return PdfResult(doc, filters, "overloaded_vehicles", from, to);
    }

    // =====================================================================
    // Reweigh Statement
    // =====================================================================

    private async Task<ReportResult> GenerateReweighStatementAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var query = _context.WeighingTransactions
            .Where(w => w.DeletedAt == null)
            .Where(w => w.WeighedAt >= from && w.WeighedAt <= to)
            .Where(w => w.CaptureStatus == "captured")
            .Where(w => w.ReweighCycleNo > 0);

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            query = query.Where(w => w.StationId == stationId);

        // Get reweigh transactions along with their original weighing data
        var reweighData = await query
            .Include(w => w.Station)
            .Include(w => w.Vehicle)
            .Include(w => w.OriginalWeighing)
            .OrderBy(w => w.VehicleRegNumber)
            .ThenBy(w => w.ReweighCycleNo)
            .Take(filters.PageSize)
            .Select(w => new
            {
                w.TicketNumber,
                WeighedAt = w.WeighedAt,
                StationName = w.Station!.Name,
                VehicleReg = w.VehicleRegNumber,
                w.ReweighCycleNo,
                w.ReweighLimit,
                w.GvwMeasuredKg,
                w.GvwPermissibleKg,
                w.OverloadKg,
                w.IsCompliant,
                w.ControlStatus,
                OriginalTicket = w.OriginalWeighing != null
                    ? w.OriginalWeighing.TicketNumber : "-",
                OriginalGvw = w.OriginalWeighing != null
                    ? w.OriginalWeighing.GvwMeasuredKg : 0,
                OriginalOverload = w.OriginalWeighing != null
                    ? w.OriginalWeighing.OverloadKg : 0,
                WeightReduction = w.OriginalWeighing != null
                    ? w.OriginalWeighing.GvwMeasuredKg - w.GvwMeasuredKg : 0
            })
            .ToListAsync(ct);

        var headers = new[]
        {
            "Ticket #", "Date/Time", "Station", "Vehicle Reg", "Cycle #", "Max Cycles",
            "Original Ticket", "Original GVW (kg)", "Reweigh GVW (kg)", "Reduction (kg)",
            "Overload (kg)", "Status"
        };

        var csvRows = reweighData.Select(r => new[]
        {
            r.TicketNumber,
            r.WeighedAt.ToString("dd/MM/yyyy HH:mm"),
            r.StationName,
            r.VehicleReg,
            r.ReweighCycleNo.ToString(),
            r.ReweighLimit.ToString(),
            r.OriginalTicket,
            FormatNumber(r.OriginalGvw),
            FormatNumber(r.GvwMeasuredKg),
            r.WeightReduction > 0 ? FormatNumber(r.WeightReduction) : "-",
            r.OverloadKg > 0 ? FormatNumber(r.OverloadKg) : "-",
            r.IsCompliant ? "Compliant" : r.ControlStatus
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, csvRows), "reweigh_statement", from, to);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Reweigh Statement", headers, csvRows, from, to), "reweigh_statement", from, to);

        var compliantAfterReweigh = reweighData.Count(r => r.IsCompliant);

        var doc = new ReweighStatementDocument
        {
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = csvRows.ToArray(),
            SummaryItems =
            [
                ("Total Reweighs", reweighData.Count.ToString()),
                ("Compliant After Reweigh", compliantAfterReweigh.ToString()),
                ("Still Overloaded", (reweighData.Count - compliantAfterReweigh).ToString()),
                ("Success Rate", reweighData.Count > 0
                    ? $"{(decimal)compliantAfterReweigh / reweighData.Count * 100:F1}%"
                    : "N/A"),
                ("Avg Weight Reduction", reweighData.Any(r => r.WeightReduction > 0)
                    ? $"{FormatNumber((int)reweighData.Where(r => r.WeightReduction > 0).Average(r => r.WeightReduction))} kg"
                    : "N/A")
            ]
        };

        return PdfResult(doc, filters, "reweigh_statement", from, to);
    }

    // =====================================================================
    // Special Release Register
    // =====================================================================

    private async Task<ReportResult> GenerateSpecialReleaseAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var query = _context.SpecialReleases
            .Where(sr => sr.DeletedAt == null)
            .Where(sr => sr.IssuedAt >= from && sr.IssuedAt <= to);

        if (!string.IsNullOrEmpty(filters.Status))
        {
            query = filters.Status.ToLowerInvariant() switch
            {
                "approved" => query.Where(sr => sr.IsApproved && !sr.IsRejected),
                "rejected" => query.Where(sr => sr.IsRejected),
                "pending" => query.Where(sr => !sr.IsApproved && !sr.IsRejected),
                _ => query
            };
        }

        var releases = await query
            .Include(sr => sr.CaseRegister)
            .Include(sr => sr.ReleaseType)
            .OrderByDescending(sr => sr.IssuedAt)
            .Take(filters.PageSize)
            .Select(sr => new
            {
                sr.CertificateNo,
                sr.IssuedAt,
                CaseNo = sr.CaseRegister.CaseNo,
                VehicleId = sr.CaseRegister.VehicleId,
                ReleaseTypeName = sr.ReleaseType.Name,
                sr.OverloadKg,
                sr.RedistributionAllowed,
                sr.ReweighRequired,
                sr.ComplianceAchieved,
                sr.Reason,
                sr.IsApproved,
                sr.IsRejected,
                sr.ApprovedAt,
                sr.RejectedAt,
                sr.RejectionReason
            })
            .ToListAsync(ct);

        var headers = new[]
        {
            "Certificate #", "Issue Date", "Case #", "Release Type", "Overload (kg)",
            "Redistribution", "Reweigh Required", "Compliance Achieved", "Status",
            "Approval Date", "Reason"
        };

        var csvRows = releases.Select(sr => new[]
        {
            sr.CertificateNo,
            sr.IssuedAt.ToString("dd/MM/yyyy HH:mm"),
            sr.CaseNo,
            sr.ReleaseTypeName,
            sr.OverloadKg.HasValue ? FormatNumber(sr.OverloadKg.Value) : "-",
            sr.RedistributionAllowed ? "Yes" : "No",
            sr.ReweighRequired ? "Yes" : "No",
            sr.ComplianceAchieved ? "Yes" : "No",
            sr.IsApproved ? "Approved" : sr.IsRejected ? "Rejected" : "Pending",
            sr.IsApproved ? FormatDate(sr.ApprovedAt) : sr.IsRejected ? FormatDate(sr.RejectedAt) : "-",
            sr.Reason.Length > 80 ? sr.Reason[..80] + "..." : sr.Reason
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, csvRows), "special_release_register", from, to);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Special Release Register", headers, csvRows, from, to), "special_release_register", from, to);

        var approved = releases.Count(sr => sr.IsApproved);
        var rejected = releases.Count(sr => sr.IsRejected);
        var pending = releases.Count(sr => !sr.IsApproved && !sr.IsRejected);

        var doc = new SpecialReleaseDocument
        {
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = csvRows.ToArray(),
            SummaryItems =
            [
                ("Total Releases", releases.Count.ToString()),
                ("Approved", approved.ToString()),
                ("Rejected", rejected.ToString()),
                ("Pending", pending.ToString()),
                ("Compliance Achieved", releases.Count(sr => sr.ComplianceAchieved).ToString())
            ]
        };

        return PdfResult(doc, filters, "special_release_register", from, to);
    }

    // =====================================================================
    // Scale Test Log
    // =====================================================================

    private async Task<ReportResult> GenerateScaleTestAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var query = _context.ScaleTests
            .Where(st => st.DeletedAt == null)
            .Where(st => st.CarriedAt >= from && st.CarriedAt <= to);

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            query = query.Where(st => st.StationId == stationId);

        var tests = await query
            .Include(st => st.Station)
            .Include(st => st.CarriedBy)
            .OrderBy(st => st.CarriedAt)
            .Take(filters.PageSize)
            .Select(st => new
            {
                st.CarriedAt,
                StationName = st.Station != null ? st.Station.Name : "-",
                st.Bound,
                st.TestType,
                st.WeighingMode,
                st.VehiclePlate,
                st.TestWeightKg,
                st.ActualWeightKg,
                st.DeviationKg,
                st.Result,
                st.Details,
                OfficerName = st.CarriedBy != null
                    ? st.CarriedBy.FullName : "-"
            })
            .ToListAsync(ct);

        var headers = new[]
        {
            "Date/Time", "Station", "Bound", "Test Type", "Weighing Mode", "Vehicle Plate",
            "Test Weight (kg)", "Actual Weight (kg)", "Deviation (kg)", "Result", "Officer"
        };

        var csvRows = tests.Select(t => new[]
        {
            t.CarriedAt.ToString("dd/MM/yyyy HH:mm"),
            t.StationName,
            t.Bound ?? "-",
            t.TestType,
            t.WeighingMode ?? "-",
            t.VehiclePlate ?? "-",
            t.TestWeightKg.HasValue ? FormatNumber(t.TestWeightKg.Value) : "-",
            t.ActualWeightKg.HasValue ? FormatNumber(t.ActualWeightKg.Value) : "-",
            t.DeviationKg.HasValue ? FormatNumber(t.DeviationKg.Value) : "-",
            t.Result.ToUpperInvariant(),
            t.OfficerName
        });

        if (format == "csv")
            return CsvResult(GenerateCsv(headers, csvRows), "scale_test_log", from, to);

        if (format == "xlsx")
            return ExcelResult(GenerateExcel("Scale Test Log", headers, csvRows, from, to), "scale_test_log", from, to);

        var passed = tests.Count(t => t.Result.Equals("pass", StringComparison.OrdinalIgnoreCase));
        var failed = tests.Count(t => t.Result.Equals("fail", StringComparison.OrdinalIgnoreCase));

        var doc = new ScaleTestDocument
        {
            DateFrom = from,
            DateTo = to,
            Headers = headers,
            Rows = csvRows.ToArray(),
            SummaryItems =
            [
                ("Total Tests", tests.Count.ToString()),
                ("Passed", passed.ToString()),
                ("Failed", failed.ToString()),
                ("Pass Rate", tests.Count > 0
                    ? $"{(decimal)passed / tests.Count * 100:F1}%"
                    : "N/A"),
                ("Avg Deviation", tests.Where(t => t.DeviationKg.HasValue).Any()
                    ? $"{FormatNumber((int)tests.Where(t => t.DeviationKg.HasValue).Average(t => Math.Abs(t.DeviationKg!.Value)))} kg"
                    : "N/A")
            ]
        };

        return PdfResult(doc, filters, "scale_test_log", from, to);
    }

    // =====================================================================
    // Inner PDF Document Classes
    // =====================================================================

    /// <summary>
    /// Base for all weighing report PDF documents that follow the standard
    /// summary cards + data table pattern.
    /// </summary>
    private abstract class WeighingReportDocumentBase : BaseReportDocument
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

    private sealed class DailySummaryDocument : WeighingReportDocumentBase
    {
        public DailySummaryDocument()
        {
            ReportTitle = "Daily Weighing Summary";
            ReportSubtitle = "Aggregated weighing statistics by date and station";
        }
    }

    private sealed class WeighbridgeRegisterDocument : WeighingReportDocumentBase
    {
        public int TotalRecords { get; set; }

        public WeighbridgeRegisterDocument()
        {
            ReportTitle = "Weighbridge Register";
            ReportSubtitle = "Detailed record of all weighing transactions";
        }

        protected override void ComposeContent(IContainer container)
        {
            container.Column(col =>
            {
                col.Spacing(5);

                col.Item().PaddingBottom(5).Text($"Total Records: {TotalRecords}")
                    .FontSize(9).SemiBold();

                col.Item().Element(c => ComposeDataTable(c, Headers, Rows));
            });
        }
    }

    private sealed class ComplianceTrendDocument : WeighingReportDocumentBase
    {
        public ComplianceTrendDocument()
        {
            ReportTitle = "Compliance Trend Analysis";
            ReportSubtitle = "Daily compliance rates over the reporting period";
        }
    }

    private sealed class AxleOverloadDocument : WeighingReportDocumentBase
    {
        public AxleOverloadDocument()
        {
            ReportTitle = "Axle Overload Analysis";
            ReportSubtitle = "Breakdown of overloaded axles by type and configuration";
        }
    }

    private sealed class StationPerformanceDocument : WeighingReportDocumentBase
    {
        public StationPerformanceDocument()
        {
            ReportTitle = "Station Performance Report";
            ReportSubtitle = "Comparative performance across weighbridge stations";
        }
    }

    private sealed class TransporterStatementDocument : WeighingReportDocumentBase
    {
        public TransporterStatementDocument()
        {
            ReportTitle = "Transporter Statement";
            ReportSubtitle = "Weighing history and compliance summary by transporter";
        }
    }

    private sealed class OverloadedVehiclesDocument : WeighingReportDocumentBase
    {
        public OverloadedVehiclesDocument()
        {
            ReportTitle = "Overloaded Vehicles Register";
            ReportSubtitle = "Vehicles exceeding permissible gross vehicle weight";
        }
    }

    private sealed class ReweighStatementDocument : WeighingReportDocumentBase
    {
        public ReweighStatementDocument()
        {
            ReportTitle = "Reweigh Statement";
            ReportSubtitle = "Load correction and reweigh cycle tracking";
        }
    }

    private sealed class SpecialReleaseDocument : WeighingReportDocumentBase
    {
        public SpecialReleaseDocument()
        {
            ReportTitle = "Special Release Register";
            ReportSubtitle = "Special release certificates issued for case dispositions";
        }
    }

    private sealed class ScaleTestDocument : WeighingReportDocumentBase
    {
        public ScaleTestDocument()
        {
            ReportTitle = "Scale Test Log";
            ReportSubtitle = "Daily scale calibration tests and results";
        }
    }

    private static IQueryable<WeighingTransaction> ApplyControlStatusFilter(
        IQueryable<WeighingTransaction> query, string controlStatus)
    {
        var normalized = controlStatus.Trim().ToUpperInvariant();
        return normalized switch
        {
            "LEGAL" or "COMPLIANT" => query.Where(w => w.ControlStatus == "Compliant" || w.ControlStatus == "LEGAL"),
            "OVERLOAD" or "OVERLOADED" => query.Where(w => w.ControlStatus == "Overloaded" || w.ControlStatus == "OVERLOAD"),
            "WARNING" => query.Where(w => w.ControlStatus == "Warning" || w.ControlStatus == "WARNING"),
            _ => query.Where(w => w.ControlStatus == controlStatus)
        };
    }
}
