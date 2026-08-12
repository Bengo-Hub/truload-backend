using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Common.Constants;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Reporting;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Models;
using TruLoad.Backend.Services.Implementations.Reporting;
using TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports;
using TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports.Weighing;

namespace TruLoad.Backend.Services.Implementations.Reporting.Modules;

/// <summary>
/// Generates all weighing module reports (PDF and CSV).
/// Covers daily summaries, weighbridge registers, compliance trends, axle overload analysis,
/// station performance, transporter statements, reweigh tracking, special releases, and scale tests.
/// </summary>
public class WeighingReportGenerator : BaseReportGenerator
{
    private readonly TruLoadDbContext _context;

    /// <summary>
    /// Structured custom-report-builder column catalog for `axle-load-analysis` - Key MUST match
    /// the literal header text built in <see cref="GenerateAxleLoadAnalysisAsync"/> (column
    /// selection matches by header string, see <c>BaseReportGenerator.ApplyColumnSelection</c>).
    /// </summary>
    private static readonly List<ReportColumnDefinition> AxleLoadAnalysisColumns =
    [
        new() { Key = "S/No.", Label = "Serial Number" },
        new() { Key = "Reg No.", Label = "Vehicle Registration" },
        new() { Key = "Station", Label = "Station" },
        new() { Key = "No. of Axle", Label = "Number of Axles" },
        new() { Key = "Legal", Label = "Legal (Permissible) Weight (kg)" },
        new() { Key = "Actual", Label = "Actual (Measured) Weight (kg)" },
        new() { Key = "Overload", Label = "Overload (kg)" },
        new() { Key = "% Gross Overload", Label = "% Gross Overload" },
        new() { Key = "Overloaded", Label = "Overloaded (Yes/No)" },
        new() { Key = "Status", Label = "Status" },
        new() { Key = "County", Label = "County" },
        new() { Key = "Sub County", Label = "Sub County" },
        new() { Key = "Road", Label = "Road" }
    ];

    private static readonly List<ReportChartOption> AxleLoadAnalysisChartOptions =
    [
        new() { Key = "proportion-bar", Label = "Vehicle-type proportion bar + Excel data bars (default)" },
        new() { Key = "none", Label = "Tables only, no visual emphasis" }
    ];

    // =====================================================================
    // Structured custom-report-builder filter catalogs, shared across the Weighing module's report
    // types - lets the builder UI show only the filters a given report actually supports (Key
    // matches a ReportFilterParams property; Kind tells the frontend which control to render).
    // =====================================================================

    private static readonly List<ReportFilterOption> WeighingTypeOptions =
    [
        new() { Value = "static", Label = "Static" },
        new() { Value = "multideck", Label = "Multideck" },
        new() { Value = "mobile", Label = "Mobile" }
    ];

    private static readonly List<ReportFilterOption> ControlStatusOptions =
    [
        new() { Value = "Compliant", Label = "Compliant" },
        new() { Value = "Warning", Label = "Warning" },
        new() { Value = "Overloaded", Label = "Overloaded" }
    ];

    /// <summary>Station/County/Sub County drill-down + weighing type/compliance status - the full set, for reports whose query applies all of them.</summary>
    private static readonly List<ReportFilterDefinition> StandardWeighingFilters =
    [
        new() { Key = "stationId", Label = "Station", Kind = "station" },
        new() { Key = "countyId", Label = "County", Kind = "county" },
        new() { Key = "subcountyId", Label = "Sub County", Kind = "subcounty" },
        new() { Key = "weighingType", Label = "Weighing Type", Kind = "select", Options = WeighingTypeOptions },
        new() { Key = "controlStatus", Label = "Compliance Status", Kind = "select", Options = ControlStatusOptions }
    ];

    /// <summary>Station/County/Sub County drill-down only, for reports whose query doesn't apply a weighing-type/compliance-status filter.</summary>
    private static readonly List<ReportFilterDefinition> StationGeoFilters =
    [
        new() { Key = "stationId", Label = "Station", Kind = "station" },
        new() { Key = "countyId", Label = "County", Kind = "county" },
        new() { Key = "subcountyId", Label = "Sub County", Kind = "subcounty" }
    ];

    // =====================================================================
    // Structured custom-report-builder column catalogs for the remaining report types.
    // Each Key MUST match the literal header text built in that report's generation method
    // (see BaseReportGenerator.ApplyColumnSelection - matches by header string). Columns whose
    // header text is currency-dependent (e.g. $"Fee ({currency})") are deliberately NOT included
    // here - the catalog is built once, synchronously, with no tenant DB access, so the runtime
    // currency isn't known yet; those columns stay always-included rather than offering a toggle
    // that could silently fail to match a KES tenant's actual "Fee (KES)" header text.
    // =====================================================================

    private static readonly List<ReportColumnDefinition> DailySummaryColumns =
    [
        new() { Key = "Date", Label = "Date" },
        new() { Key = "Station", Label = "Station" },
        new() { Key = "Total Vehicles", Label = "Total Vehicles" },
        new() { Key = "Compliant", Label = "Compliant" },
        new() { Key = "Overloaded", Label = "Overloaded" },
        new() { Key = "Compliance %", Label = "Compliance %" },
        new() { Key = "Total Overload (kg)", Label = "Total Overload (kg)" },
        new() { Key = "Avg GVW (kg)", Label = "Average GVW (kg)" }
    ];

    private static readonly List<ReportColumnDefinition> WeighbridgeRegisterColumns =
    [
        new() { Key = "Ticket #", Label = "Ticket Number" },
        new() { Key = "Date/Time", Label = "Date/Time" },
        new() { Key = "Station", Label = "Station" },
        new() { Key = "County", Label = "County" },
        new() { Key = "Sub County", Label = "Sub County" },
        new() { Key = "Road", Label = "Road" },
        new() { Key = "Bound", Label = "Bound" },
        new() { Key = "Vehicle Reg", Label = "Vehicle Registration" },
        new() { Key = "Axle Config", Label = "Axle Configuration" },
        new() { Key = "Driver", Label = "Driver" },
        new() { Key = "Transporter", Label = "Transporter" },
        new() { Key = "GVW (kg)", Label = "GVW (kg)" },
        new() { Key = "Permissible (kg)", Label = "Permissible (kg)" },
        new() { Key = "Overload (kg)", Label = "Overload (kg)" },
        new() { Key = "Status", Label = "Status" },
        new() { Key = "Type", Label = "Weighing Type" },
        new() { Key = "Reweigh #", Label = "Reweigh Cycle #" }
    ];

    private static readonly List<ReportColumnDefinition> ComplianceTrendColumns =
    [
        new() { Key = "Date", Label = "Date" },
        new() { Key = "Total Weighed", Label = "Total Weighed" },
        new() { Key = "Compliant", Label = "Compliant" },
        new() { Key = "Overloaded", Label = "Overloaded" },
        new() { Key = "Sent to Yard", Label = "Sent to Yard" },
        new() { Key = "Compliance %", Label = "Compliance %" },
        new() { Key = "Avg Overload (kg)", Label = "Average Overload (kg)" },
        new() { Key = "Max Overload (kg)", Label = "Max Overload (kg)" }
    ];

    private static readonly List<ReportColumnDefinition> AxleOverloadColumns =
    [
        new() { Key = "Ticket #", Label = "Ticket Number" },
        new() { Key = "Date", Label = "Date" },
        new() { Key = "Station", Label = "Station" },
        new() { Key = "Vehicle Reg", Label = "Vehicle Registration" },
        new() { Key = "Axle #", Label = "Axle Number" },
        new() { Key = "Axle Type", Label = "Axle Type" },
        new() { Key = "Grouping", Label = "Axle Grouping" },
        new() { Key = "Config", Label = "Axle Configuration" },
        new() { Key = "Measured (kg)", Label = "Measured (kg)" },
        new() { Key = "Permissible (kg)", Label = "Permissible (kg)" },
        new() { Key = "Overload (kg)", Label = "Overload (kg)" },
        new() { Key = "Overload %", Label = "Overload %" },
        new() { Key = "Pavement Damage Factor", Label = "Pavement Damage Factor" }
    ];

    private static readonly List<ReportColumnDefinition> StationPerformanceColumns =
    [
        new() { Key = "Station", Label = "Station" },
        new() { Key = "Code", Label = "Station Code" },
        new() { Key = "Total Vehicles", Label = "Total Vehicles" },
        new() { Key = "Compliant", Label = "Compliant" },
        new() { Key = "Overloaded", Label = "Overloaded" },
        new() { Key = "Compliance %", Label = "Compliance %" },
        new() { Key = "Sent to Yard", Label = "Sent to Yard" },
        new() { Key = "Total Overload (kg)", Label = "Total Overload (kg)" },
        new() { Key = "Avg GVW (kg)", Label = "Average GVW (kg)" },
        new() { Key = "Max Overload (kg)", Label = "Max Overload (kg)" },
        new() { Key = "Reweighs", Label = "Reweighs" }
    ];

    private static readonly List<ReportColumnDefinition> TransporterStatementColumns =
    [
        new() { Key = "Transporter", Label = "Transporter" },
        new() { Key = "Code", Label = "Transporter Code" },
        new() { Key = "Total Weighings", Label = "Total Weighings" },
        new() { Key = "Unique Vehicles", Label = "Unique Vehicles" },
        new() { Key = "Compliant", Label = "Compliant" },
        new() { Key = "Overloaded", Label = "Overloaded" },
        new() { Key = "Compliance %", Label = "Compliance %" },
        new() { Key = "Total Overload (kg)", Label = "Total Overload (kg)" },
        new() { Key = "Avg Overload (kg)", Label = "Average Overload (kg)" },
        new() { Key = "Sent to Yard", Label = "Sent to Yard" }
    ];

    private static readonly List<ReportColumnDefinition> OverloadedVehiclesColumns =
    [
        new() { Key = "Ticket #", Label = "Ticket Number" },
        new() { Key = "Date/Time", Label = "Date/Time" },
        new() { Key = "Station", Label = "Station" },
        new() { Key = "County", Label = "County" },
        new() { Key = "Sub County", Label = "Sub County" },
        new() { Key = "Road", Label = "Road" },
        new() { Key = "Vehicle Reg", Label = "Vehicle Registration" },
        new() { Key = "Axle Config", Label = "Axle Configuration" },
        new() { Key = "Driver", Label = "Driver" },
        new() { Key = "Transporter", Label = "Transporter" },
        new() { Key = "GVW (kg)", Label = "GVW (kg)" },
        new() { Key = "Permissible (kg)", Label = "Permissible (kg)" },
        new() { Key = "Overload (kg)", Label = "Overload (kg)" },
        new() { Key = "Overload %", Label = "Overload %" },
        new() { Key = "Status", Label = "Status" },
        new() { Key = "Yard", Label = "Sent to Yard" }
    ];

    private static readonly List<ReportColumnDefinition> ReweighStatementColumns =
    [
        new() { Key = "Ticket #", Label = "Ticket Number" },
        new() { Key = "Date/Time", Label = "Date/Time" },
        new() { Key = "Station", Label = "Station" },
        new() { Key = "County", Label = "County" },
        new() { Key = "Sub County", Label = "Sub County" },
        new() { Key = "Road", Label = "Road" },
        new() { Key = "Vehicle Reg", Label = "Vehicle Registration" },
        new() { Key = "Cycle #", Label = "Reweigh Cycle #" },
        new() { Key = "Max Cycles", Label = "Max Cycles" },
        new() { Key = "Original Ticket", Label = "Original Ticket" },
        new() { Key = "Original GVW (kg)", Label = "Original GVW (kg)" },
        new() { Key = "Reweigh GVW (kg)", Label = "Reweigh GVW (kg)" },
        new() { Key = "Reduction (kg)", Label = "Weight Reduction (kg)" },
        new() { Key = "Overload (kg)", Label = "Overload (kg)" },
        new() { Key = "Status", Label = "Status" }
    ];

    private static readonly List<ReportColumnDefinition> SpecialReleaseColumns =
    [
        new() { Key = "Certificate #", Label = "Certificate Number" },
        new() { Key = "Issue Date", Label = "Issue Date" },
        new() { Key = "Case #", Label = "Case Number" },
        new() { Key = "Release Type", Label = "Release Type" },
        new() { Key = "Overload (kg)", Label = "Overload (kg)" },
        new() { Key = "Redistribution", Label = "Redistribution Allowed" },
        new() { Key = "Reweigh Required", Label = "Reweigh Required" },
        new() { Key = "Compliance Achieved", Label = "Compliance Achieved" },
        new() { Key = "Status", Label = "Status" },
        new() { Key = "Approval Date", Label = "Approval Date" },
        new() { Key = "Reason", Label = "Reason" }
    ];

    private static readonly List<ReportColumnDefinition> ScaleTestColumns =
    [
        new() { Key = "Date/Time", Label = "Date/Time" },
        new() { Key = "Station", Label = "Station" },
        new() { Key = "Bound", Label = "Bound" },
        new() { Key = "Test Type", Label = "Test Type" },
        new() { Key = "Weighing Mode", Label = "Weighing Mode" },
        new() { Key = "Vehicle Plate", Label = "Vehicle Plate" },
        new() { Key = "Test Weight (kg)", Label = "Test Weight (kg)" },
        new() { Key = "Actual Weight (kg)", Label = "Actual Weight (kg)" },
        new() { Key = "Deviation (kg)", Label = "Deviation (kg)" },
        new() { Key = "Result", Label = "Result" },
        new() { Key = "Officer", Label = "Officer" }
    ];

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

    /// <summary>
    /// Shared station/date/status filter chain used by the per-transaction weighing reports
    /// (weighbridge register, overloaded vehicles, axle load analysis) so new report types don't
    /// re-duplicate this exact chain.
    /// </summary>
    private IQueryable<WeighingTransaction> BuildWeighingBaseQuery(
        ReportFilterParams filters, DateTime from, DateTime to)
    {
        var query = _context.WeighingTransactions
            .Where(w => w.DeletedAt == null)
            .Where(w => w.WeighedAt >= from && w.WeighedAt <= to)
            .Where(w => w.CaptureStatus == "captured");

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            query = query.Where(w => w.StationId == stationId);
        // WeighingTransaction has no direct CountyId FK (only SubcountyId, for mobile-unit capture) -
        // county filtering goes through the weighing station's own CountyId, the reliable FK-based
        // source for both static/multideck and mobile-unit transactions alike.
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(w => w.Station != null && w.Station.CountyId == countyId);
        // Sub-county: prefer the transaction's own SubcountyId when set (mobile-unit capture
        // location), fall back to its station's SubcountyId otherwise - matches the same
        // transaction-first/station-fallback precedence used for display elsewhere in this report set.
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(w =>
                (w.SubcountyId.HasValue && w.SubcountyId == subcountyId) ||
                (!w.SubcountyId.HasValue && w.Station != null && w.Station.SubcountyId == subcountyId));
        if (!string.IsNullOrEmpty(filters.WeighingType))
            query = query.Where(w => w.WeighingType == filters.WeighingType);
        if (!string.IsNullOrEmpty(filters.ControlStatus))
            query = ApplyControlStatusFilter(query, filters.ControlStatus);

        return query;
    }

    public override List<ReportDefinitionDto> GetDefinitions() =>
    [
        Def("daily-summary", "Daily Weighing Summary",
            "Aggregated daily statistics including total vehicles weighed, compliance rate, and overload totals per station.",
            columns: DailySummaryColumns, filters: StandardWeighingFilters),
        Def("weighbridge-register", "Weighbridge Register",
            "Detailed register of all weighing transactions with vehicle, driver, weight, and compliance data.",
            columns: WeighbridgeRegisterColumns, filters: StandardWeighingFilters),
        Def("axle-load-analysis", "Axle Load Data Analysis",
            "Per-vehicle axle load analysis with vehicle-type/axle-count and overload-by-GVW summary " +
            "breakdowns, colour-coded status and legend. Covers all stations by default, or a single " +
            "station when filtered.",
            columns: AxleLoadAnalysisColumns, filters: StandardWeighingFilters, chartOptions: AxleLoadAnalysisChartOptions),
        Def("compliance-trend", "Compliance Trend Analysis",
            "Daily compliance rates over the selected period, showing overload vs compliant vehicle counts.",
            columns: ComplianceTrendColumns, filters: StandardWeighingFilters),
        Def("axle-overload", "Axle Overload Analysis",
            "Breakdown of overloaded axles by type and configuration, with pavement damage factor analysis.",
            columns: AxleOverloadColumns, filters: StationGeoFilters),
        Def("station-performance", "Station Performance Report",
            "Comparative performance metrics across weighbridge stations for the selected period.",
            columns: StationPerformanceColumns, filters: StationGeoFilters),
        Def("transporter-statement", "Transporter Statement",
            "Weighing history and compliance summary grouped by transporter company.",
            columns: TransporterStatementColumns, filters: StationGeoFilters),
        Def("overloaded-vehicles", "Overloaded Vehicles Register",
            "Filtered register showing only overloaded vehicles with overload amounts and violation details.",
            columns: OverloadedVehiclesColumns, filters: StandardWeighingFilters),
        Def("reweigh-statement", "Reweigh Statement",
            "Tracks reweigh cycles for vehicles that underwent load redistribution or correction.",
            columns: ReweighStatementColumns, filters: StationGeoFilters),
        Def("special-release", "Special Release Register",
            "Register of all special releases issued, with release type, authorization, and compliance status.",
            columns: SpecialReleaseColumns),
        Def("scale-test", "Scale Test Log",
            "Log of daily scale calibration tests per station and bound, with pass/fail results and deviations.",
            columns: ScaleTestColumns, filters: StationGeoFilters)
    ];

    public override async Task<ReportResult> GenerateAsync(
        string reportType, ReportFilterParams filters, string format, CancellationToken ct = default)
    {
        return reportType switch
        {
            "daily-summary" => await GenerateDailySummaryAsync(filters, format, ct),
            "weighbridge-register" => await GenerateWeighbridgeRegisterAsync(filters, format, ct),
            "axle-load-analysis" => await GenerateAxleLoadAnalysisAsync(filters, format, ct),
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
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(w => w.Station != null && w.Station.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(w =>
                (w.SubcountyId.HasValue && w.SubcountyId == subcountyId) ||
                (!w.SubcountyId.HasValue && w.Station != null && w.Station.SubcountyId == subcountyId));
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

        var outputHeaders = headers;
        List<string[]> outputRows = csvRows.ToList();
        int[]? pctIdx = [5]; // "Compliance %"

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
            var idx = Array.IndexOf(outputHeaders, "Compliance %");
            pctIdx = idx >= 0 ? [idx] : null;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "weighing_daily_summary", from, to);

        if (format == "xlsx")
        {
            var request = new ExcelReportRequest
            {
                ReportTitle = "Daily Weighing Summary",
                Headers = outputHeaders,
                Rows = outputRows,
                DateFrom = from,
                DateTo = to,
                PercentageDataBarColumnIndexes = pctIdx,
                OrgName = filters.OrganizationName,
                OrgLogoFile = filters.OrgLogoFile
            };
            return ExcelResult(GenerateExcel(request), "weighing_daily_summary", from, to);
        }

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
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
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
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(w => w.Station != null && w.Station.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(w =>
                (w.SubcountyId.HasValue && w.SubcountyId == subcountyId) ||
                (!w.SubcountyId.HasValue && w.Station != null && w.Station.SubcountyId == subcountyId));
        if (!string.IsNullOrEmpty(filters.WeighingType))
            query = query.Where(w => w.WeighingType == filters.WeighingType);
        if (!string.IsNullOrEmpty(filters.ControlStatus))
            query = ApplyControlStatusFilter(query, filters.ControlStatus);

        var transactions = await query
            .Include(w => w.Vehicle).ThenInclude(v => v!.AxleConfiguration)
            .Include(w => w.Station).ThenInclude(s => s!.County)
            .Include(w => w.Station).ThenInclude(s => s!.Subcounty)
            .Include(w => w.Station).ThenInclude(s => s!.Road)
            .Include(w => w.Driver)
            .Include(w => w.Transporter)
            .Include(w => w.Road)
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
                w.TotalFeeKes,
                CountyName = !string.IsNullOrWhiteSpace(w.LocationCounty) ? w.LocationCounty
                    : w.Station.County != null ? w.Station.County.Name : "-",
                SubcountyName = !string.IsNullOrWhiteSpace(w.LocationSubcounty) ? w.LocationSubcounty
                    : w.Station.Subcounty != null ? w.Station.Subcounty.Name : "-",
                RoadName = w.Road != null ? w.Road.Name : w.Station.Road != null ? w.Station.Road.Name : "-"
            })
            .ToListAsync(ct);

        var headers = new[]
        {
            "Ticket #", "Date/Time", "Station", "County", "Sub County", "Road", "Bound", "Vehicle Reg",
            "Axle Config", "Driver", "Transporter", "GVW (kg)", "Permissible (kg)", "Overload (kg)",
            "Status", "Type", "Reweigh #", $"Fee ({currency})"
        };

        var csvRows = transactions.Select(t => new[]
        {
            t.TicketNumber,
            t.WeighedAt.ToString("dd/MM/yyyy HH:mm"),
            t.StationName,
            t.CountyName,
            t.SubcountyName,
            t.RoadName,
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
            $"{(currency.Equals("KES", StringComparison.OrdinalIgnoreCase) && t.TotalFeeKes > 0 ? t.TotalFeeKes : t.TotalFeeUsd):N2}"
        });

        const int statusColumnIndex = 14;
        // Legend swatches must mirror the actual row colour, which is driven by the raw
        // ControlStatus text ("Warning"/"Overloaded") via ReportStatusColors.Resolve - not the
        // "Within Permissible Tolerance" bucket (blue), which only applies to the sample-template
        // vocabulary used by the Axle Load Data Analysis report.
        var legend = new[]
        {
            (ReportStatusColors.Resolve("Warning").ExcelFillHex, "Warning (axle overload)"),
            (BrandingConstants.Colors.SampleTemplateRed, "Overloaded (GVW)")
        };

        var outputHeaders = headers;
        List<string[]> outputRows = csvRows.ToList();
        int? effectiveStatusColumnIndex = statusColumnIndex;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
            var idx = Array.IndexOf(outputHeaders, "Status");
            effectiveStatusColumnIndex = idx >= 0 ? idx : null;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "weighbridge_register", from, to);

        if (format == "xlsx")
        {
            var request = new ExcelReportRequest
            {
                ReportTitle = "Weighbridge Register",
                Headers = outputHeaders,
                Rows = outputRows,
                DateFrom = from,
                DateTo = to,
                ConditionalStatusColumnIndex = effectiveStatusColumnIndex,
                Legend = legend,
                OrgName = filters.OrganizationName,
                OrgLogoFile = filters.OrgLogoFile
            };
            return ExcelResult(GenerateExcel(request), "weighbridge_register", from, to);
        }

        var stationName = !string.IsNullOrEmpty(filters.StationId)
            ? transactions.FirstOrDefault()?.StationName
            : null;

        var doc = new WeighbridgeRegisterDocument
        {
            DateFrom = from,
            DateTo = to,
            StationName = stationName,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
            TotalRecords = transactions.Count,
            StatusColumnIndex = effectiveStatusColumnIndex,
            Legend = legend
        };

        return PdfResult(doc, filters, "weighbridge_register", from, to);
    }

    // =====================================================================
    // Axle Load Data Analysis (all stations by default; station is an optional drill-down filter)
    // =====================================================================

    private async Task<ReportResult> GenerateAxleLoadAnalysisAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        // Station is an optional drill-down filter, not a requirement - this report covers all
        // stations by default, same as every other report in the fleet.
        Guid? stationId = null;
        if (!string.IsNullOrEmpty(filters.StationId))
        {
            if (!Guid.TryParse(filters.StationId, out var parsedStationId))
                throw new ArgumentException("Invalid stationId.");
            stationId = parsedStationId;
        }

        var (from, to) = GetDateRange(filters);

        var transactions = await BuildWeighingBaseQuery(filters, from, to)
            .Include(w => w.Vehicle).ThenInclude(v => v!.AxleConfiguration)
            .Include(w => w.Road)
            .Include(w => w.Station).ThenInclude(s => s!.County)
            .Include(w => w.Station).ThenInclude(s => s!.Subcounty)
            .Include(w => w.Station).ThenInclude(s => s!.Road)
            .OrderBy(w => w.WeighedAt)
            .Take(filters.PageSize)
            .Select(w => new
            {
                w.VehicleRegNumber,
                AxleCount = w.Vehicle != null && w.Vehicle.AxleConfiguration != null
                    ? w.Vehicle.AxleConfiguration.AxleNumber : 0,
                w.GvwPermissibleKg,
                w.GvwMeasuredKg,
                w.OverloadKg,
                w.ControlStatus,
                StationName = w.Station != null ? w.Station.Name : "-",
                StationCountyName = w.Station != null && w.Station.County != null ? w.Station.County.Name : "-",
                StationSubcountyName = w.Station != null && w.Station.Subcounty != null ? w.Station.Subcounty.Name : "-",
                StationRoadName = w.Station != null && w.Station.Road != null ? w.Station.Road.Name : "-",
                w.LocationCounty,
                w.LocationSubcounty,
                TransactionRoadName = w.Road != null ? w.Road.Name : null
            })
            .ToListAsync(ct);

        var headers = new[]
        {
            "S/No.", "Reg No.", "Station", "No. of Axle", "Legal", "Actual", "Overload",
            "% Gross Overload", "Overloaded", "Status", "County", "Sub County", "Road"
        };
        const int statusColumnIndex = 9;

        var rows = new List<string[]>();
        var bucketWeighed = new SortedDictionary<int, int>();
        var bucketOverloaded = new SortedDictionary<int, int>();
        var serial = 1;

        foreach (var t in transactions)
        {
            var pctGrossOverload = t.GvwPermissibleKg > 0
                ? (decimal)t.OverloadKg / t.GvwPermissibleKg * 100
                : 0m;
            var isOverloaded = t.OverloadKg > 0;
            var sampleStatus = ReportStatusColors.ToSampleTemplateStatus(t.ControlStatus);

            rows.Add(new[]
            {
                serial.ToString(),
                t.VehicleRegNumber,
                t.StationName,
                t.AxleCount.ToString(),
                FormatNumber(t.GvwPermissibleKg),
                FormatNumber(t.GvwMeasuredKg),
                t.OverloadKg > 0 ? FormatNumber(t.OverloadKg) : "0",
                $"{pctGrossOverload:F2}%",
                isOverloaded ? "Yes" : "No",
                sampleStatus,
                !string.IsNullOrWhiteSpace(t.LocationCounty) ? t.LocationCounty : t.StationCountyName,
                !string.IsNullOrWhiteSpace(t.LocationSubcounty) ? t.LocationSubcounty : t.StationSubcountyName,
                t.TransactionRoadName ?? t.StationRoadName
            });

            // Bucket by axle count (2-6 exact, 7+ grouped) to mirror the sample's
            // "N Axle Truck" vehicle-type breakdown; 0/1 (unclassified/no axle config) is its own bucket.
            var bucket = t.AxleCount switch { >= 2 and <= 6 => t.AxleCount, > 6 => 7, _ => 0 };
            bucketWeighed[bucket] = bucketWeighed.GetValueOrDefault(bucket) + 1;
            if (isOverloaded)
                bucketOverloaded[bucket] = bucketOverloaded.GetValueOrDefault(bucket) + 1;

            serial++;
        }

        static string BucketLabel(int bucket) => bucket switch
        {
            0 => "Unclassified",
            7 => "7+ Axle Truck",
            _ => $"{bucket} Axle Truck"
        };

        var totalWeighed = transactions.Count;

        var vehicleTypeHeaders = new[] { "No.", "Type of Vehicle", "No. of Vehicles Weighed", "% OF TOTAL" };
        var vehicleTypeRows = new List<string[]>();
        var rowNo = 1;
        foreach (var bucket in bucketWeighed.Keys)
        {
            var count = bucketWeighed[bucket];
            vehicleTypeRows.Add(new[]
            {
                rowNo.ToString(), BucketLabel(bucket), count.ToString(),
                totalWeighed > 0 ? $"{(decimal)count / totalWeighed * 100:F2}%" : "0.00%"
            });
            rowNo++;
        }
        vehicleTypeRows.Add(new[] { "", "Total", totalWeighed.ToString(), totalWeighed > 0 ? "100.00%" : "0.00%" });

        var overloadHeaders = new[]
        {
            "Type of Vehicle", "No. of Vehicles Weighed", "No. of Vehicles Overloaded by GVW", "% Overloaded by GVW"
        };
        var overloadRows = new List<string[]>();
        var totalOverloaded = 0;
        foreach (var bucket in bucketWeighed.Keys)
        {
            var count = bucketWeighed[bucket];
            var overloadedCount = bucketOverloaded.GetValueOrDefault(bucket);
            totalOverloaded += overloadedCount;
            overloadRows.Add(new[]
            {
                BucketLabel(bucket), count.ToString(), overloadedCount.ToString(),
                count > 0 ? $"{(decimal)overloadedCount / count * 100:F2}%" : "0.00%"
            });
        }
        overloadRows.Add(new[]
        {
            "Total", totalWeighed.ToString(), totalOverloaded.ToString(),
            totalWeighed > 0 ? $"{(decimal)totalOverloaded / totalWeighed * 100:F2}%" : "0.00%"
        });

        var legend = new[]
        {
            (BrandingConstants.Colors.ToleranceBlue, "Within Permissible Tolerance"),
            (BrandingConstants.Colors.SampleTemplateRed, "Overloaded and charged")
        };

        // Structured custom-report builder: UseDefaults=true (the default) reproduces today's
        // exact fixed output unchanged. Only when a caller explicitly opts out does column
        // selection / the chart-visual toggle apply.
        var outputHeaders = headers;
        List<string[]> outputRows = rows;
        int? effectiveStatusColumnIndex = statusColumnIndex;
        var includeChartVisuals = true;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, rows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
            var statusIdx = Array.IndexOf(outputHeaders, "Status");
            effectiveStatusColumnIndex = statusIdx >= 0 ? statusIdx : null;
            includeChartVisuals = filters.ChartType != "none";
        }

        var palette = new[] { "#3B82F6", "#10B981", "#F59E0B", "#8B5CF6", "#06B6D4", "#EC4899", "#84CC16" };
        var vehicleTypeProportions = bucketWeighed.Keys
            .Select((bucket, i) => (BucketLabel(bucket), (decimal)bucketWeighed[bucket], palette[i % palette.Length]))
            .ToArray();

        // Title/subtitle read sensibly whether scoped to one station or covering all of them.
        var first = transactions.FirstOrDefault();
        string reportTitle;
        string reportSubtitle;
        if (stationId.HasValue)
        {
            var scopedStationName = first?.StationName;
            if (string.IsNullOrWhiteSpace(scopedStationName) || scopedStationName == "-")
                scopedStationName = await _context.Stations.AsNoTracking()
                    .Where(s => s.Id == stationId.Value).Select(s => s.Name).FirstOrDefaultAsync(ct) ?? "Station";
            reportTitle = $"AXLE LOAD DATA ANALYSIS FOR {scopedStationName.ToUpperInvariant()}";
            reportSubtitle = first != null
                ? $"{scopedStationName} - {first.StationRoadName}, {first.StationSubcountyName}, {first.StationCountyName}"
                : scopedStationName;
        }
        else
        {
            reportTitle = "AXLE LOAD DATA ANALYSIS - ALL STATIONS";
            reportSubtitle = "All Stations";
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "axle_load_data_analysis", from, to);

        if (format == "xlsx")
        {
            var request = new ExcelReportRequest
            {
                ReportTitle = reportTitle,
                Headers = outputHeaders,
                Rows = outputRows,
                DateFrom = from,
                DateTo = to,
                ConditionalStatusColumnIndex = effectiveStatusColumnIndex,
                Legend = legend,
                SummaryTables =
                [
                    new ExcelSummaryTable
                    {
                        Title = "Vehicle Type Breakdown (by Axle Count)",
                        Headers = vehicleTypeHeaders, Rows = vehicleTypeRows
                    },
                    new ExcelSummaryTable
                    {
                        Title = "Overload by GVW (by Vehicle Type)",
                        Headers = overloadHeaders, Rows = overloadRows
                    }
                ],
                OrgName = filters.OrganizationName,
                OrgLogoFile = filters.OrgLogoFile,
                IncludeChartVisuals = includeChartVisuals
            };
            return ExcelResult(GenerateExcel(request), "axle_load_data_analysis", from, to);
        }

        var doc = new AxleLoadAnalysisDocument
        {
            ReportSubtitle = reportSubtitle,
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
            StatusColumnIndex = effectiveStatusColumnIndex,
            Legend = legend,
            VehicleTypeProportions = vehicleTypeProportions,
            IncludeChartVisuals = includeChartVisuals,
            SummaryTables =
            [
                ("Vehicle Type Breakdown (by Axle Count)", vehicleTypeHeaders, vehicleTypeRows.ToArray()),
                ("Overload by GVW (by Vehicle Type)", overloadHeaders, overloadRows.ToArray())
            ]
        };

        return PdfResult(doc, filters, "axle_load_data_analysis", from, to);
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
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(w => w.Station != null && w.Station.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(w =>
                (w.SubcountyId.HasValue && w.SubcountyId == subcountyId) ||
                (!w.SubcountyId.HasValue && w.Station != null && w.Station.SubcountyId == subcountyId));
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

        var outputHeaders = headers;
        List<string[]> outputRows = csvRows.ToList();
        int[]? pctIdx = [5]; // "Compliance %"

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
            var idx = Array.IndexOf(outputHeaders, "Compliance %");
            pctIdx = idx >= 0 ? [idx] : null;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "compliance_trend", from, to);

        if (format == "xlsx")
        {
            var request = new ExcelReportRequest
            {
                ReportTitle = "Compliance Trend Analysis",
                Headers = outputHeaders,
                Rows = outputRows,
                DateFrom = from,
                DateTo = to,
                PercentageDataBarColumnIndexes = pctIdx,
                OrgName = filters.OrganizationName,
                OrgLogoFile = filters.OrgLogoFile
            };
            return ExcelResult(GenerateExcel(request), "compliance_trend", from, to);
        }

        var grandTotal = dailyData.Sum(d => d.Total);
        var grandCompliant = dailyData.Sum(d => d.Compliant);

        var doc = new ComplianceTrendDocument
        {
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
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
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            baseQuery = baseQuery.Where(w => w.Station != null && w.Station.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            baseQuery = baseQuery.Where(w =>
                (w.SubcountyId.HasValue && w.SubcountyId == subcountyId) ||
                (!w.SubcountyId.HasValue && w.Station != null && w.Station.SubcountyId == subcountyId));

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

        var outputHeaders = headers;
        List<string[]> outputRows = csvRows.ToList();
        int[]? pctIdx = [11]; // "Overload %"

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
            var idx = Array.IndexOf(outputHeaders, "Overload %");
            pctIdx = idx >= 0 ? [idx] : null;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "axle_overload_analysis", from, to);

        if (format == "xlsx")
        {
            var request = new ExcelReportRequest
            {
                ReportTitle = "Axle Overload Analysis",
                Headers = outputHeaders,
                Rows = outputRows,
                DateFrom = from,
                DateTo = to,
                PercentageDataBarColumnIndexes = pctIdx,
                OrgName = filters.OrganizationName,
                OrgLogoFile = filters.OrgLogoFile
            };
            return ExcelResult(GenerateExcel(request), "axle_overload_analysis", from, to);
        }

        // Aggregate by axle type for summary
        var axleTypeSummary = axleData
            .GroupBy(a => a.AxleType)
            .Select(g => (g.Key, $"{g.Count()} overloads, avg {FormatNumber((int)g.Average(x => x.OverloadKg))} kg"))
            .ToArray();

        var doc = new AxleOverloadDocument
        {
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
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
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(w => w.Station != null && w.Station.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(w =>
                (w.SubcountyId.HasValue && w.SubcountyId == subcountyId) ||
                (!w.SubcountyId.HasValue && w.Station != null && w.Station.SubcountyId == subcountyId));

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

        var outputHeaders = headers;
        List<string[]> outputRows = csvRows.ToList();
        int[]? pctIdx = [5]; // "Compliance %"

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
            var idx = Array.IndexOf(outputHeaders, "Compliance %");
            pctIdx = idx >= 0 ? [idx] : null;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "station_performance", from, to);

        if (format == "xlsx")
        {
            var request = new ExcelReportRequest
            {
                ReportTitle = "Station Performance Report",
                Headers = outputHeaders,
                Rows = outputRows,
                DateFrom = from,
                DateTo = to,
                PercentageDataBarColumnIndexes = pctIdx,
                OrgName = filters.OrganizationName,
                OrgLogoFile = filters.OrgLogoFile
            };
            return ExcelResult(GenerateExcel(request), "station_performance", from, to);
        }

        var grandTotal = stationData.Sum(s => s.TotalVehicles);
        var grandCompliant = stationData.Sum(s => s.Compliant);

        var doc = new StationPerformanceDocument
        {
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
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
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(w => w.Station != null && w.Station.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(w =>
                (w.SubcountyId.HasValue && w.SubcountyId == subcountyId) ||
                (!w.SubcountyId.HasValue && w.Station != null && w.Station.SubcountyId == subcountyId));

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

        var outputHeaders = headers;
        List<string[]> outputRows = csvRows.ToList();
        int[]? pctIdx = [6]; // "Compliance %"

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
            var idx = Array.IndexOf(outputHeaders, "Compliance %");
            pctIdx = idx >= 0 ? [idx] : null;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "transporter_statement", from, to);

        if (format == "xlsx")
        {
            var request = new ExcelReportRequest
            {
                ReportTitle = "Transporter Statement",
                Headers = outputHeaders,
                Rows = outputRows,
                DateFrom = from,
                DateTo = to,
                PercentageDataBarColumnIndexes = pctIdx,
                OrgName = filters.OrganizationName,
                OrgLogoFile = filters.OrgLogoFile
            };
            return ExcelResult(GenerateExcel(request), "transporter_statement", from, to);
        }

        var doc = new TransporterStatementDocument
        {
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
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
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(w => w.Station != null && w.Station.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(w =>
                (w.SubcountyId.HasValue && w.SubcountyId == subcountyId) ||
                (!w.SubcountyId.HasValue && w.Station != null && w.Station.SubcountyId == subcountyId));
        if (!string.IsNullOrEmpty(filters.WeighingType))
            query = query.Where(w => w.WeighingType == filters.WeighingType);
        if (!string.IsNullOrEmpty(filters.ControlStatus))
            query = ApplyControlStatusFilter(query, filters.ControlStatus);

        var overloaded = await query
            .Include(w => w.Vehicle).ThenInclude(v => v!.AxleConfiguration)
            .Include(w => w.Station).ThenInclude(s => s!.County)
            .Include(w => w.Station).ThenInclude(s => s!.Subcounty)
            .Include(w => w.Station).ThenInclude(s => s!.Road)
            .Include(w => w.Driver)
            .Include(w => w.Transporter)
            .Include(w => w.Road)
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
                w.TotalFeeKes,
                CountyName = !string.IsNullOrWhiteSpace(w.LocationCounty) ? w.LocationCounty
                    : w.Station.County != null ? w.Station.County.Name : "-",
                SubcountyName = !string.IsNullOrWhiteSpace(w.LocationSubcounty) ? w.LocationSubcounty
                    : w.Station.Subcounty != null ? w.Station.Subcounty.Name : "-",
                RoadName = w.Road != null ? w.Road.Name : w.Station.Road != null ? w.Station.Road.Name : "-"
            })
            .ToListAsync(ct);

        var headers = new[]
        {
            "Ticket #", "Date/Time", "Station", "County", "Sub County", "Road", "Vehicle Reg",
            "Axle Config", "Driver", "Transporter", "GVW (kg)", "Permissible (kg)", "Overload (kg)",
            "Overload %", "Status", "Yard", $"Fee ({currency})"
        };

        var csvRows = overloaded.Select(o => new[]
        {
            o.TicketNumber,
            o.WeighedAt.ToString("dd/MM/yyyy HH:mm"),
            o.StationName,
            o.CountyName,
            o.SubcountyName,
            o.RoadName,
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
            $"{(currency.Equals("KES", StringComparison.OrdinalIgnoreCase) && o.TotalFeeKes > 0 ? o.TotalFeeKes : o.TotalFeeUsd):N2}"
        });

        const int statusColumnIndex = 14;
        var legend = new[]
        {
            (BrandingConstants.Colors.SampleTemplateRed, "Overloaded (GVW)")
        };

        var outputHeaders = headers;
        List<string[]> outputRows = csvRows.ToList();
        int? effectiveStatusColumnIndex = statusColumnIndex;
        int[]? pctIdx = [13]; // "Overload %"

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
            var idx = Array.IndexOf(outputHeaders, "Status");
            effectiveStatusColumnIndex = idx >= 0 ? idx : null;
            var pIdx = Array.IndexOf(outputHeaders, "Overload %");
            pctIdx = pIdx >= 0 ? [pIdx] : null;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "overloaded_vehicles", from, to);

        if (format == "xlsx")
        {
            var request = new ExcelReportRequest
            {
                ReportTitle = "Overloaded Vehicles Register",
                Headers = outputHeaders,
                Rows = outputRows,
                DateFrom = from,
                DateTo = to,
                ConditionalStatusColumnIndex = effectiveStatusColumnIndex,
                Legend = legend,
                PercentageDataBarColumnIndexes = pctIdx,
                OrgName = filters.OrganizationName,
                OrgLogoFile = filters.OrgLogoFile
            };
            return ExcelResult(GenerateExcel(request), "overloaded_vehicles", from, to);
        }

        var doc = new OverloadedVehiclesDocument
        {
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
            StatusColumnIndex = effectiveStatusColumnIndex,
            Legend = legend,
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
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(w => w.Station != null && w.Station.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(w =>
                (w.SubcountyId.HasValue && w.SubcountyId == subcountyId) ||
                (!w.SubcountyId.HasValue && w.Station != null && w.Station.SubcountyId == subcountyId));

        // Get reweigh transactions along with their original weighing data
        var reweighData = await query
            .Include(w => w.Station).ThenInclude(s => s!.County)
            .Include(w => w.Station).ThenInclude(s => s!.Subcounty)
            .Include(w => w.Station).ThenInclude(s => s!.Road)
            .Include(w => w.Vehicle)
            .Include(w => w.OriginalWeighing)
            .Include(w => w.Road)
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
                    ? w.OriginalWeighing.GvwMeasuredKg - w.GvwMeasuredKg : 0,
                CountyName = !string.IsNullOrWhiteSpace(w.LocationCounty) ? w.LocationCounty
                    : w.Station.County != null ? w.Station.County.Name : "-",
                SubcountyName = !string.IsNullOrWhiteSpace(w.LocationSubcounty) ? w.LocationSubcounty
                    : w.Station.Subcounty != null ? w.Station.Subcounty.Name : "-",
                RoadName = w.Road != null ? w.Road.Name : w.Station.Road != null ? w.Station.Road.Name : "-"
            })
            .ToListAsync(ct);

        var headers = new[]
        {
            "Ticket #", "Date/Time", "Station", "County", "Sub County", "Road", "Vehicle Reg",
            "Cycle #", "Max Cycles", "Original Ticket", "Original GVW (kg)", "Reweigh GVW (kg)",
            "Reduction (kg)", "Overload (kg)", "Status"
        };

        var csvRows = reweighData.Select(r => new[]
        {
            r.TicketNumber,
            r.WeighedAt.ToString("dd/MM/yyyy HH:mm"),
            r.StationName,
            r.CountyName,
            r.SubcountyName,
            r.RoadName,
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

        const int statusColumnIndex = 14;
        var legend = new[]
        {
            (ReportStatusColors.Resolve("Warning").ExcelFillHex, "Warning (axle overload)"),
            (BrandingConstants.Colors.SampleTemplateRed, "Overloaded (GVW)")
        };

        var outputHeaders = headers;
        List<string[]> outputRows = csvRows.ToList();
        int? effectiveStatusColumnIndex = statusColumnIndex;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
            var idx = Array.IndexOf(outputHeaders, "Status");
            effectiveStatusColumnIndex = idx >= 0 ? idx : null;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "reweigh_statement", from, to);

        if (format == "xlsx")
        {
            var request = new ExcelReportRequest
            {
                ReportTitle = "Reweigh Statement",
                Headers = outputHeaders,
                Rows = outputRows,
                DateFrom = from,
                DateTo = to,
                ConditionalStatusColumnIndex = effectiveStatusColumnIndex,
                Legend = legend,
                OrgName = filters.OrganizationName,
                OrgLogoFile = filters.OrgLogoFile
            };
            return ExcelResult(GenerateExcel(request), "reweigh_statement", from, to);
        }

        var compliantAfterReweigh = reweighData.Count(r => r.IsCompliant);

        var doc = new ReweighStatementDocument
        {
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
            StatusColumnIndex = effectiveStatusColumnIndex,
            Legend = legend,
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

        const int statusColumnIndex = 8;
        var legend = new[]
        {
            (BrandingConstants.Colors.SampleTemplateRed, "Rejected"),
            ("#FEF3C7", "Pending")
        };

        var outputHeaders = headers;
        List<string[]> outputRows = csvRows.ToList();
        int? effectiveStatusColumnIndex = statusColumnIndex;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
            var idx = Array.IndexOf(outputHeaders, "Status");
            effectiveStatusColumnIndex = idx >= 0 ? idx : null;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "special_release_register", from, to);

        if (format == "xlsx")
        {
            var request = new ExcelReportRequest
            {
                ReportTitle = "Special Release Register",
                Headers = outputHeaders,
                Rows = outputRows,
                DateFrom = from,
                DateTo = to,
                ConditionalStatusColumnIndex = effectiveStatusColumnIndex,
                Legend = legend,
                OrgName = filters.OrganizationName,
                OrgLogoFile = filters.OrgLogoFile
            };
            return ExcelResult(GenerateExcel(request), "special_release_register", from, to);
        }

        var approved = releases.Count(sr => sr.IsApproved);
        var rejected = releases.Count(sr => sr.IsRejected);
        var pending = releases.Count(sr => !sr.IsApproved && !sr.IsRejected);

        var doc = new SpecialReleaseDocument
        {
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
            StatusColumnIndex = effectiveStatusColumnIndex,
            Legend = legend,
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
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(st => st.Station != null && st.Station.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(st => st.Station != null && st.Station.SubcountyId == subcountyId);

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

        const int statusColumnIndex = 9;
        var legend = new[]
        {
            (BrandingConstants.Colors.SampleTemplateRed, "Fail")
        };

        var outputHeaders = headers;
        List<string[]> outputRows = csvRows.ToList();
        int? effectiveStatusColumnIndex = statusColumnIndex;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
            var idx = Array.IndexOf(outputHeaders, "Result");
            effectiveStatusColumnIndex = idx >= 0 ? idx : null;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "scale_test_log", from, to);

        if (format == "xlsx")
        {
            var request = new ExcelReportRequest
            {
                ReportTitle = "Scale Test Log",
                Headers = outputHeaders,
                Rows = outputRows,
                DateFrom = from,
                DateTo = to,
                ConditionalStatusColumnIndex = effectiveStatusColumnIndex,
                Legend = legend,
                OrgName = filters.OrganizationName,
                OrgLogoFile = filters.OrgLogoFile
            };
            return ExcelResult(GenerateExcel(request), "scale_test_log", from, to);
        }

        var passed = tests.Count(t => t.Result.Equals("pass", StringComparison.OrdinalIgnoreCase));
        var failed = tests.Count(t => t.Result.Equals("fail", StringComparison.OrdinalIgnoreCase));

        var doc = new ScaleTestDocument
        {
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
            StatusColumnIndex = effectiveStatusColumnIndex,
            Legend = legend,
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

    // Inner PDF document classes for this module's reports live in
    // Services/Implementations/Infrastructure/PdfDocuments/Reports/Weighing/
    // (extracted so this file stays within the project's file-length guideline).

    /// <summary>
    /// Delegates to the shared <see cref="WeighingQueryHelpers.ApplyControlStatusFilter"/> so this
    /// module and the dashboard/stats endpoints in <c>WeighingController</c> can't drift into two
    /// different status-alias mappings again (they already had - see the 2026-08-12 audit).
    /// </summary>
    private static IQueryable<WeighingTransaction> ApplyControlStatusFilter(
        IQueryable<WeighingTransaction> query, string controlStatus)
        => TruLoad.Backend.Common.WeighingQueryHelpers.ApplyControlStatusFilter(query, controlStatus);
}
