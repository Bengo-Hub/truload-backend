using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using TruLoad.Backend.Common.Constants;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Reporting;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Services.Implementations.Infrastructure.PdfDocuments.Reports;
using TruLoad.Backend.Services.Implementations.Weighing;

namespace TruLoad.Backend.Services.Implementations.Reporting.Modules;

/// <summary>
/// Generates commercial weighing reports (two-pass weighing, cargo volumes, revenue, fleet utilization, etc.).
/// All queries filter by WeighingMode == "commercial" and respect tenant isolation via global query filters.
/// </summary>
public class CommercialReportGenerator : BaseReportGenerator
{
    private readonly TruLoadDbContext _context;
    private readonly ITenantContext _tenantContext;

    // =====================================================================
    // Structured custom-report-builder column catalogs. Each Key MUST match the literal header
    // text built in that report's generation method (see BaseReportGenerator.ApplyColumnSelection -
    // matches by header string). None of this file's headers are currency-interpolated, so every
    // static-text column gets a catalog entry.
    // =====================================================================

    private static readonly List<ReportColumnDefinition> CommercialDailySummaryColumns =
    [
        new() { Key = "Date", Label = "Date" },
        new() { Key = "Station", Label = "Station" },
        new() { Key = "Total Weighings", Label = "Total Weighings" },
        new() { Key = "Unique Vehicles", Label = "Unique Vehicles" },
        new() { Key = "Cargo Types", Label = "Cargo Types" },
        new() { Key = "Total Net Weight (kg)", Label = "Total Net Weight (kg)" },
        new() { Key = "Total Gross Weight (kg)", Label = "Total Gross Weight (kg)" },
        new() { Key = "Total Tare Weight (kg)", Label = "Total Tare Weight (kg)" }
    ];

    private static readonly List<ReportColumnDefinition> TransporterStatementColumns =
    [
        new() { Key = "Transporter", Label = "Transporter" },
        new() { Key = "Weighing Count", Label = "Weighing Count" },
        new() { Key = "Unique Vehicles", Label = "Unique Vehicles" },
        new() { Key = "Cargo Types", Label = "Cargo Types" },
        new() { Key = "Total Net Weight (kg)", Label = "Total Net Weight (kg)" },
        new() { Key = "Avg Net Weight (kg)", Label = "Average Net Weight (kg)" }
    ];

    private static readonly List<ReportColumnDefinition> CargoVolumeColumns =
    [
        new() { Key = "Cargo Type", Label = "Cargo Type" },
        new() { Key = "Trip Count", Label = "Trip Count" },
        new() { Key = "Total Net Weight (kg)", Label = "Total Net Weight (kg)" },
        new() { Key = "Avg Net Weight (kg)", Label = "Average Net Weight (kg)" },
        new() { Key = "Min Net Weight (kg)", Label = "Minimum Net Weight (kg)" },
        new() { Key = "Max Net Weight (kg)", Label = "Maximum Net Weight (kg)" }
    ];

    private static readonly List<ReportColumnDefinition> WeightDiscrepancyColumns =
    [
        new() { Key = "Ticket #", Label = "Ticket Number" },
        new() { Key = "Date/Time", Label = "Date/Time" },
        new() { Key = "Station", Label = "Station" },
        new() { Key = "County", Label = "County" },
        new() { Key = "Sub County", Label = "Sub County" },
        new() { Key = "Road", Label = "Road" },
        new() { Key = "Vehicle Reg", Label = "Vehicle Registration" },
        new() { Key = "Transporter", Label = "Transporter" },
        new() { Key = "Cargo", Label = "Cargo" },
        new() { Key = "Expected (kg)", Label = "Expected Weight (kg)" },
        new() { Key = "Actual (kg)", Label = "Actual Weight (kg)" },
        new() { Key = "Discrepancy (kg)", Label = "Discrepancy (kg)" },
        new() { Key = "Variance %", Label = "Variance %" }
    ];

    private static readonly List<ReportColumnDefinition> CommercialRevenueColumns =
    [
        new() { Key = "Date", Label = "Date" },
        new() { Key = "Currency", Label = "Currency" },
        new() { Key = "Status", Label = "Status" },
        new() { Key = "Invoice Count", Label = "Invoice Count" },
        new() { Key = "Total Amount", Label = "Total Amount" }
    ];

    private static readonly List<ReportColumnDefinition> ThroughputColumns =
    [
        new() { Key = "Date", Label = "Date" },
        new() { Key = "Station", Label = "Station" },
        new() { Key = "Vehicles Weighed", Label = "Vehicles Weighed" },
        new() { Key = "Two-Pass Transactions", Label = "Two-Pass Transactions" },
        new() { Key = "Avg Processing Time (min)", Label = "Average Processing Time (min)" },
        new() { Key = "Vehicles/Hour", Label = "Vehicles per Hour" }
    ];

    private static readonly List<ReportColumnDefinition> TareWeightAuditColumns =
    [
        new() { Key = "Vehicle Reg", Label = "Vehicle Registration" },
        new() { Key = "Date/Time", Label = "Date/Time" },
        new() { Key = "Station", Label = "Station" },
        new() { Key = "County", Label = "County" },
        new() { Key = "Sub County", Label = "Sub County" },
        new() { Key = "Road", Label = "Road" },
        new() { Key = "Tare Weight (kg)", Label = "Tare Weight (kg)" },
        new() { Key = "Previous Tare (kg)", Label = "Previous Tare (kg)" },
        new() { Key = "Drift %", Label = "Drift %" },
        new() { Key = "Source", Label = "Source" },
        new() { Key = "Status", Label = "Status" },
        new() { Key = "Notes", Label = "Notes" }
    ];

    private static readonly List<ReportColumnDefinition> FleetUtilizationColumns =
    [
        new() { Key = "Vehicle Reg", Label = "Vehicle Registration" },
        new() { Key = "Transporter", Label = "Transporter" },
        new() { Key = "Trip Count", Label = "Trip Count" },
        new() { Key = "Total Net Weight (kg)", Label = "Total Net Weight (kg)" },
        new() { Key = "Avg Payload (kg)", Label = "Average Payload (kg)" },
        new() { Key = "Min Payload (kg)", Label = "Minimum Payload (kg)" },
        new() { Key = "Max Payload (kg)", Label = "Maximum Payload (kg)" },
        new() { Key = "Avg Gross (kg)", Label = "Average Gross Weight (kg)" },
        new() { Key = "Avg Tare (kg)", Label = "Average Tare Weight (kg)" },
        new() { Key = "Payload Efficiency (%)", Label = "Payload Efficiency (%)" }
    ];

    private static readonly List<ReportColumnDefinition> DriverProductivityColumns =
    [
        new() { Key = "Driver", Label = "Driver" },
        new() { Key = "Trip Count", Label = "Trip Count" },
        new() { Key = "Total Net Weight (kg)", Label = "Total Net Weight (kg)" },
        new() { Key = "Avg Net Weight (kg)", Label = "Average Net Weight (kg)" },
        new() { Key = "Two-Pass Trips", Label = "Two-Pass Trips" },
        new() { Key = "Avg Turnaround (min)", Label = "Average Turnaround (min)" }
    ];

    private static readonly List<ReportColumnDefinition> QualityCommodityColumns =
    [
        new() { Key = "Cargo Type", Label = "Cargo Type" },
        new() { Key = "Transactions", Label = "Transactions" },
        new() { Key = "With Deductions", Label = "With Deductions" },
        new() { Key = "Total Deduction (kg)", Label = "Total Deduction (kg)" },
        new() { Key = "Avg Deduction (kg)", Label = "Average Deduction (kg)" },
        new() { Key = "Total Net Weight (kg)", Label = "Total Net Weight (kg)" },
        new() { Key = "Total Adjusted (kg)", Label = "Total Adjusted Weight (kg)" },
        new() { Key = "With Industry Metadata", Label = "With Industry Metadata" }
    ];

    private static readonly List<ReportColumnDefinition> ShiftPerformanceColumns =
    [
        new() { Key = "Date", Label = "Date" },
        new() { Key = "Shift", Label = "Shift" },
        new() { Key = "Station", Label = "Station" },
        new() { Key = "Total Transactions", Label = "Total Transactions" },
        new() { Key = "Total Net Weight (kg)", Label = "Total Net Weight (kg)" },
        new() { Key = "Avg Cycle Time (min)", Label = "Average Cycle Time (min)" },
        new() { Key = "Overloads", Label = "Overloads" },
        new() { Key = "Tolerance Exceptions", Label = "Tolerance Exceptions" },
        new() { Key = "Voided", Label = "Voided" }
    ];

    private static readonly List<ReportColumnDefinition> TransactionAuditLogColumns =
    [
        new() { Key = "Ticket #", Label = "Ticket Number" },
        new() { Key = "Vehicle Reg", Label = "Vehicle Registration" },
        new() { Key = "Transporter", Label = "Transporter" },
        new() { Key = "First Weight (kg)", Label = "First Weight (kg)" },
        new() { Key = "Second Weight (kg)", Label = "Second Weight (kg)" },
        new() { Key = "Net Weight (kg)", Label = "Net Weight (kg)" },
        new() { Key = "Quality Deduction (kg)", Label = "Quality Deduction (kg)" },
        new() { Key = "Adjusted Net (kg)", Label = "Adjusted Net Weight (kg)" },
        new() { Key = "Status", Label = "Status" },
        new() { Key = "Voided At", Label = "Voided At" },
        new() { Key = "Void Reason", Label = "Void Reason" },
        new() { Key = "Created By", Label = "Created By" },
        new() { Key = "Created At", Label = "Created At" }
    ];

    // Phase 8: true per-event audit trail (see WriteAuditLogAsync in CommercialWeighingService),
    // distinct from TransactionAuditLogColumns above (which is a per-transaction snapshot including
    // voided ones, not a per-action event log).
    private static readonly List<ReportColumnDefinition> TransactionEventLogColumns =
    [
        new() { Key = "Timestamp", Label = "Timestamp" },
        new() { Key = "Action", Label = "Action" },
        new() { Key = "Resource Type", Label = "Resource Type" },
        new() { Key = "Ticket #", Label = "Ticket Number" },
        new() { Key = "Vehicle Reg", Label = "Vehicle Registration" },
        new() { Key = "User", Label = "User" },
        new() { Key = "Old Values", Label = "Old Values" },
        new() { Key = "New Values", Label = "New Values" }
    ];

    private static readonly List<ReportFilterDefinition> TransactionEventLogFilters =
    [
        new() { Key = "userId", Label = "User", Kind = "text" },
        new() { Key = "ticketNumber", Label = "Ticket / Transaction Reference", Kind = "text" }
    ];

    private static readonly List<ReportColumnDefinition> PendingTransactionsColumns =
    [
        new() { Key = "Ticket #", Label = "Ticket Number" },
        new() { Key = "Vehicle Reg", Label = "Vehicle Registration" },
        new() { Key = "Transporter", Label = "Transporter" },
        new() { Key = "Station", Label = "Station" },
        new() { Key = "County", Label = "County" },
        new() { Key = "Sub County", Label = "Sub County" },
        new() { Key = "Road", Label = "Road" },
        new() { Key = "First Weight At", Label = "First Weight At" },
        new() { Key = "Hours Pending", Label = "Hours Pending" },
        new() { Key = "First Weight (kg)", Label = "First Weight (kg)" }
    ];

    private static readonly List<ReportColumnDefinition> MonthlyReconciliationColumns =
    [
        new() { Key = "Month", Label = "Month" },
        new() { Key = "Total Transactions", Label = "Total Transactions" },
        new() { Key = "Total Net Weight (kg)", Label = "Total Net Weight (kg)" },
        new() { Key = "Total Fee (KES)", Label = "Total Fee (KES)" },
        new() { Key = "Tolerance Exceptions", Label = "Tolerance Exceptions" },
        new() { Key = "Voided", Label = "Voided" },
        new() { Key = "Completed", Label = "Completed" },
        new() { Key = "Avg Net Weight (kg)", Label = "Average Net Weight (kg)" },
        new() { Key = "Prev Month Transactions", Label = "Previous Month Transactions" },
        new() { Key = "Prev Month Net Weight (kg)", Label = "Previous Month Net Weight (kg)" },
        new() { Key = "Prev Month Fee (KES)", Label = "Previous Month Fee (KES)" }
    ];

    private static readonly List<ReportColumnDefinition> TonnageByRouteColumns =
    [
        new() { Key = "Origin", Label = "Origin" },
        new() { Key = "Destination", Label = "Destination" },
        new() { Key = "Trip Count", Label = "Trip Count" },
        new() { Key = "Total Net Weight (kg)", Label = "Total Net Weight (kg)" },
        new() { Key = "Avg Net Weight (kg)", Label = "Average Net Weight (kg)" }
    ];

    private static readonly List<ReportColumnDefinition> TareVerificationColumns =
    [
        new() { Key = "Vehicle Reg", Label = "Vehicle Registration" },
        new() { Key = "Transporter", Label = "Transporter" },
        new() { Key = "Status", Label = "Status" },
        new() { Key = "Stored Tare (kg)", Label = "Stored Tare (kg)" },
        new() { Key = "Last Weighed At", Label = "Last Weighed At" },
        new() { Key = "Expiry Date", Label = "Expiry Date" },
        new() { Key = "Days Until Expiry", Label = "Days Until Expiry" },
        new() { Key = "Drift Alert", Label = "Drift Alert" }
    ];

    // =====================================================================
    // Structured custom-report-builder filter catalog, shared across the Commercial module's report
    // types - lets the builder UI show only the filters a given report actually supports (Key
    // matches a ReportFilterParams property; Kind tells the frontend which control to render).
    // Unlike Weighing, Commercial reports have no WeighingType/ControlStatus selector concept (a
    // couple of reports privately reuse those two ReportFilterParams fields as a discrepancy-kg
    // threshold override and a vehicle-reg substring filter respectively - that's pre-existing
    // behaviour, not a builder-exposed filter, so it's deliberately left out of this catalog), so
    // every report gets the same Station/County/Sub County drill-down.
    // =====================================================================
    private static readonly List<ReportFilterDefinition> CommercialGeoFilters =
    [
        new() { Key = "stationId", Label = "Station", Kind = "station" },
        new() { Key = "countyId", Label = "County", Kind = "county" },
        new() { Key = "subcountyId", Label = "Sub County", Kind = "subcounty" },
        new() { Key = "roadId", Label = "Road", Kind = "road" }
    ];

    /// <summary>
    /// This codebase's real seeded commercial roles (Data/Seeders/RoleSeeder.cs), mapped onto
    /// reports.md's "Access by Role" table (which uses the generic labels Operator/Supervisor/
    /// Finance/Admin). Auditor has no column in that doc table, but its seeded description ("Read-only
    /// access to commercial weighing records, financial data, and analytics for audit and compliance")
    /// makes it the one role that should see every report regardless of doc-table restrictions, so it
    /// is added to every non-null AllowedRoles list below rather than only where the doc mentions it.
    /// </summary>
    private static class Roles
    {
        public const string Admin = "Commercial Weighing Manager";
        public const string Supervisor = "Commercial Supervisor";
        public const string Operator = "Commercial Weighing Operator";
        public const string Finance = "Commercial Finance";
        public const string Auditor = "Commercial Auditor";
    }

    public CommercialReportGenerator(TruLoadDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public override string Module => ReportModules.Commercial;

    public override List<ReportDefinitionDto> GetDefinitions() =>
    [
        // reports.md: "Daily Weighing Summary" - Operator, Supervisor, Finance, Admin all checked -
        // no restriction beyond the flat analytics.read permission.
        Def("commercial-daily-summary", "Commercial Daily Summary",
            "Total weighings, total net weight, grouped by cargo type and station. Date range filter.",
            columns: CommercialDailySummaryColumns, filters: CommercialGeoFilters),
        // reports.md: "Tonnage by Transporter" - Supervisor, Finance, Admin (not Operator).
        Def("transporter-statement", "Transporter Statement",
            "Per-transporter: weighing count, total net weight, average net weight, and cargo breakdown. Date range + transporter filter.",
            columns: TransporterStatementColumns, filters: CommercialGeoFilters,
            allowedRoles: [Roles.Supervisor, Roles.Finance, Roles.Admin, Roles.Auditor]),
        // reports.md: "Tonnage by Cargo Type" - Supervisor, Finance, Admin (not Operator).
        Def("cargo-volume", "Cargo Volume Report",
            "Volume trends by cargo type over time, grouped by day, week, or month. Date range filter.",
            columns: CargoVolumeColumns, filters: CommercialGeoFilters,
            allowedRoles: [Roles.Supervisor, Roles.Finance, Roles.Admin, Roles.Auditor]),
        // Not in reports.md's table - judgement call: an audit/quality-control style report, same
        // sensitivity tier as Vehicle Utilization/Shift Performance (Supervisor+Admin, not
        // Operator/Finance).
        Def("weight-discrepancy", "Weight Discrepancy Report",
            "Transactions where weight discrepancy exceeds a threshold. Shows expected vs actual and variance %. Date range + threshold filter.",
            columns: WeightDiscrepancyColumns, filters: CommercialGeoFilters,
            allowedRoles: [Roles.Supervisor, Roles.Admin, Roles.Auditor]),
        // Not in reports.md's table - judgement call: financial data, same tier as Monthly
        // Reconciliation (Finance+Admin only).
        Def("commercial-revenue", "Commercial Revenue Report",
            "Revenue from commercial weighing fees (from invoices). Date range filter.",
            columns: CommercialRevenueColumns,
            allowedRoles: [Roles.Finance, Roles.Admin, Roles.Auditor]),
        // Not in reports.md's table - judgement call: operational throughput metric, same tier as
        // Daily Weighing Summary - no restriction.
        Def("throughput", "Throughput Report",
            "Vehicles per hour, average processing time (SecondWeightAt - FirstWeightAt), by station. Date range filter.",
            columns: ThroughputColumns, filters: CommercialGeoFilters),
        // Not in reports.md's table - judgement call: same drift-log sensitivity as the new
        // Tare Verification report (Supervisor+Admin, not Operator/Finance).
        Def("tare-weight-audit", "Tare Weight Audit Report",
            "Tare weight changes per vehicle from tare history. Flags anomalies (>5% drift). Date range + vehicle filter.",
            columns: TareWeightAuditColumns, filters: CommercialGeoFilters,
            allowedRoles: [Roles.Supervisor, Roles.Admin, Roles.Auditor]),
        // reports.md: "Vehicle Utilization" - Supervisor, Admin only (not Operator, not Finance).
        Def("fleet-utilization", "Fleet Utilization Report",
            "Per vehicle: trip count, total net weight, average payload, payload utilization. Date range + transporter filter.",
            columns: FleetUtilizationColumns, filters: CommercialGeoFilters,
            allowedRoles: [Roles.Supervisor, Roles.Admin, Roles.Auditor]),
        // Not in reports.md's table - judgement call: per-driver performance data, same tier as
        // Vehicle Utilization (Supervisor+Admin only).
        Def("driver-productivity", "Driver Productivity Report",
            "Per driver: trip count, total net weight, average turnaround time. Date range filter.",
            columns: DriverProductivityColumns, filters: CommercialGeoFilters,
            allowedRoles: [Roles.Supervisor, Roles.Admin, Roles.Auditor]),
        // reports.md: "Quality Deductions" - Supervisor, Finance, Admin (not Operator).
        Def("quality-commodity", "Quality & Commodity Report",
            "Quality deduction stats by cargo type for transactions with deductions or industry metadata. Date range filter.",
            columns: QualityCommodityColumns, filters: CommercialGeoFilters,
            allowedRoles: [Roles.Supervisor, Roles.Finance, Roles.Admin, Roles.Auditor]),
        // reports.md: "Shift Performance" - Supervisor, Admin only (not Operator, not Finance).
        Def("shift-performance", "Shift Performance Report",
            "Group by shift: total transactions, total net weight (kg), average cycle time (minutes), overloads and tolerance exceptions. Date range + station filter.",
            columns: ShiftPerformanceColumns, filters: CommercialGeoFilters,
            allowedRoles: [Roles.Supervisor, Roles.Admin, Roles.Auditor]),
        // reports.md: "Transaction Audit Log" - Admin only. Per-transaction snapshot (see 8: this is
        // NOT the true per-event log - that's "transaction-event-log" immediately below).
        Def("transaction-audit-log", "Transaction Audit Log",
            "All commercial transactions including voided ones: ticket, vehicle, weights, quality deductions, status, void info, created by. Date range filter.",
            columns: TransactionAuditLogColumns, filters: CommercialGeoFilters,
            allowedRoles: [Roles.Admin, Roles.Auditor]),
        // Phase 8: true per-event action log (void, tolerance/tare-anomaly approve/reject/override,
        // first/second weight capture, stored-tare use, quality deduction) sourced from AuditLog rows
        // CommercialWeighingService writes explicitly - same Admin-only tier as transaction-audit-log.
        Def("transaction-event-log", "Transaction Event Log",
            "True per-event audit trail of actions taken on weighing transactions and tare records (captures, voids, approvals, overrides) with before/after values. Filtered by user, date range, or transaction reference.",
            columns: TransactionEventLogColumns, filters: TransactionEventLogFilters,
            allowedRoles: [Roles.Admin, Roles.Auditor]),
        // reports.md: "Pending Transactions" - Operator, Supervisor, Admin (not Finance).
        Def("pending-transactions", "Pending Transactions Report",
            "Transactions with first weight captured but no second weight and not voided. Shows hours pending. Date range + station filter.",
            columns: PendingTransactionsColumns, filters: CommercialGeoFilters,
            allowedRoles: [Roles.Operator, Roles.Supervisor, Roles.Admin, Roles.Auditor]),
        // reports.md: "Monthly Reconciliation" - Finance, Admin only.
        Def("monthly-reconciliation", "Monthly Reconciliation Report",
            "Monthly summary: total transactions, net weight, fees, tolerance exceptions, voided count, completed count, average net weight, revenue by cargo type, and comparison with the previous month. Date range filter.",
            columns: MonthlyReconciliationColumns, filters: CommercialGeoFilters,
            allowedRoles: [Roles.Finance, Roles.Admin, Roles.Auditor]),
        // 5b: new report - reuses OriginId/DestinationId, same sensitivity tier as Tonnage by
        // Cargo Type/Transporter (Supervisor, Finance, Admin - not Operator).
        Def("tonnage-by-route", "Tonnage by Route Report",
            "Net weight totals grouped by origin -> destination route over a selected period. Date range + station filter.",
            columns: TonnageByRouteColumns, filters: CommercialGeoFilters,
            allowedRoles: [Roles.Supervisor, Roles.Finance, Roles.Admin, Roles.Auditor]),
        // 5c: new report, separate from tare-weight-audit's drift log - reports.md: "Tare
        // Verification" - Supervisor, Admin only (not Operator, not Finance).
        Def("tare-verification", "Tare Verification Report",
            "Buckets vehicles by stored-tare status (valid / expiring within 14 days / expired) plus a drift-alert count. Date range not applicable - reflects current fleet state.",
            columns: TareVerificationColumns,
            allowedRoles: [Roles.Supervisor, Roles.Admin, Roles.Auditor])
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
            "shift-performance" => await GenerateShiftPerformanceAsync(filters, format, ct),
            "transaction-audit-log" => await GenerateTransactionAuditLogAsync(filters, format, ct),
            "transaction-event-log" => await GenerateTransactionEventLogAsync(filters, format, ct),
            "pending-transactions" => await GeneratePendingTransactionsAsync(filters, format, ct),
            "monthly-reconciliation" => await GenerateMonthlyReconciliationAsync(filters, format, ct),
            "tonnage-by-route" => await GenerateTonnageByRouteAsync(filters, format, ct),
            "tare-verification" => await GenerateTareVerificationAsync(filters, format, ct),
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
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(w => w.Station != null && w.Station.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(w =>
                (w.SubcountyId.HasValue && w.SubcountyId == subcountyId) ||
                (!w.SubcountyId.HasValue && w.Station != null && w.Station.SubcountyId == subcountyId));
        if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
            query = query.Where(w =>
                (w.RoadId.HasValue && w.RoadId == roadId) ||
                (!w.RoadId.HasValue && w.Station != null && w.Station.RoadId == roadId));

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

        // Structured custom-report builder: UseDefaults=true (the default) reproduces today's
        // exact fixed output unchanged. Only when a caller explicitly opts out does column
        // selection apply.
        var outputHeaders = headers;
        var outputRows = csvRows;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "commercial_daily_summary", from, to);
        if (format == "xlsx")
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Commercial Daily Summary", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "commercial_daily_summary", from, to);

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Commercial Daily Summary",
            ReportSubtitle = "Aggregated commercial weighing statistics by date and station",
            DateFrom = from,
            DateTo = to,
            StationName = !string.IsNullOrEmpty(filters.StationId) ? rows.FirstOrDefault()?.StationName : null,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
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
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(w => w.Station != null && w.Station.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(w =>
                (w.SubcountyId.HasValue && w.SubcountyId == subcountyId) ||
                (!w.SubcountyId.HasValue && w.Station != null && w.Station.SubcountyId == subcountyId));
        if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
            query = query.Where(w =>
                (w.RoadId.HasValue && w.RoadId == roadId) ||
                (!w.RoadId.HasValue && w.Station != null && w.Station.RoadId == roadId));

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

        // Structured custom-report builder: UseDefaults=true (the default) reproduces today's
        // exact fixed output unchanged. Only when a caller explicitly opts out does column
        // selection apply.
        var outputHeaders = headers;
        var outputRows = csvRows;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "commercial_transporter_statement", from, to);
        if (format == "xlsx")
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Transporter Statement", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "commercial_transporter_statement", from, to);

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Transporter Statement",
            ReportSubtitle = "Commercial weighing summary by transporter",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
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
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(w => w.Station != null && w.Station.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(w =>
                (w.SubcountyId.HasValue && w.SubcountyId == subcountyId) ||
                (!w.SubcountyId.HasValue && w.Station != null && w.Station.SubcountyId == subcountyId));
        if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
            query = query.Where(w =>
                (w.RoadId.HasValue && w.RoadId == roadId) ||
                (!w.RoadId.HasValue && w.Station != null && w.Station.RoadId == roadId));

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

        // Structured custom-report builder: UseDefaults=true (the default) reproduces today's
        // exact fixed output unchanged. Only when a caller explicitly opts out does column
        // selection apply.
        var outputHeaders = headers;
        var outputRows = csvRows;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "cargo_volume", from, to);
        if (format == "xlsx")
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Cargo Volume Report", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "cargo_volume", from, to);

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Cargo Volume Report",
            ReportSubtitle = "Volume trends by cargo type over the reporting period",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
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
    // Tonnage by Route Report (5b) - same pattern as Cargo Volume, grouped by
    // origin -> destination pair instead of cargo type.
    // =====================================================================

    private async Task<ReportResult> GenerateTonnageByRouteAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var query = CommercialBaseQuery()
            .Where(w => w.WeighedAt >= from && w.WeighedAt <= to)
            .Where(w => w.OriginId != null && w.DestinationId != null);

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            query = query.Where(w => w.StationId == stationId);
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(w => w.Station != null && w.Station.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(w =>
                (w.SubcountyId.HasValue && w.SubcountyId == subcountyId) ||
                (!w.SubcountyId.HasValue && w.Station != null && w.Station.SubcountyId == subcountyId));
        if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
            query = query.Where(w =>
                (w.RoadId.HasValue && w.RoadId == roadId) ||
                (!w.RoadId.HasValue && w.Station != null && w.Station.RoadId == roadId));

        var routeData = await query
            .Include(w => w.Origin)
            .Include(w => w.Destination)
            .GroupBy(w => new { w.OriginId, w.DestinationId, OriginName = w.Origin!.Name, DestinationName = w.Destination!.Name })
            .Select(g => new
            {
                OriginName = g.Key.OriginName,
                DestinationName = g.Key.DestinationName,
                TripCount = g.Count(),
                TotalNetWeightKg = g.Sum(x => (long)(x.NetWeightKg ?? 0)),
                AvgNetWeightKg = (int)g.Average(x => x.NetWeightKg ?? 0)
            })
            .OrderByDescending(r => r.TotalNetWeightKg)
            .ToListAsync(ct);

        var headers = new[] { "Origin", "Destination", "Trip Count", "Total Net Weight (kg)", "Avg Net Weight (kg)" };

        var csvRows = routeData.Select(r => new[]
        {
            r.OriginName,
            r.DestinationName,
            r.TripCount.ToString(),
            FormatNumber(r.TotalNetWeightKg),
            FormatNumber(r.AvgNetWeightKg)
        });

        var outputHeaders = headers;
        var outputRows = csvRows;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "tonnage_by_route", from, to);
        if (format == "xlsx")
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Tonnage by Route Report", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "tonnage_by_route", from, to);

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Tonnage by Route Report",
            ReportSubtitle = "Net weight totals by origin -> destination route over the reporting period",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
            SummaryItems =
            [
                ("Routes", routeData.Count.ToString()),
                ("Total Trips", FormatNumber(routeData.Sum(r => r.TripCount))),
                ("Total Net Weight", $"{FormatNumber(routeData.Sum(r => r.TotalNetWeightKg))} kg")
            ]
        };

        return PdfResult(doc, filters, "tonnage_by_route", from, to);
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
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(w => w.Station != null && w.Station.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(w =>
                (w.SubcountyId.HasValue && w.SubcountyId == subcountyId) ||
                (!w.SubcountyId.HasValue && w.Station != null && w.Station.SubcountyId == subcountyId));
        if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
            query = query.Where(w =>
                (w.RoadId.HasValue && w.RoadId == roadId) ||
                (!w.RoadId.HasValue && w.Station != null && w.Station.RoadId == roadId));

        var discrepancies = await query
            .Include(w => w.Station).ThenInclude(s => s!.County)
            .Include(w => w.Station).ThenInclude(s => s!.Subcounty)
            .Include(w => w.Station).ThenInclude(s => s!.Road)
            .Include(w => w.Cargo)
            .Include(w => w.Transporter)
            .Include(w => w.Road)
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
                    : 0m,
                CountyName = !string.IsNullOrWhiteSpace(w.LocationCounty) ? w.LocationCounty
                    : w.Station.County != null ? w.Station.County.Name : "-",
                SubcountyName = !string.IsNullOrWhiteSpace(w.LocationSubcounty) ? w.LocationSubcounty
                    : w.Station.Subcounty != null ? w.Station.Subcounty.Name : "-",
                RoadName = w.Road != null ? w.Road.Name : w.Station.Road != null ? w.Station.Road.Name : "-"
            })
            .ToListAsync(ct);

        var headers = new[]
        {
            "Ticket #", "Date/Time", "Station", "County", "Sub County", "Road", "Vehicle Reg", "Transporter", "Cargo",
            "Expected (kg)", "Actual (kg)", "Discrepancy (kg)", "Variance %"
        };

        var csvRows = discrepancies.Select(d => new[]
        {
            d.TicketNumber,
            d.WeighedAt.ToString("dd/MM/yyyy HH:mm"),
            d.StationName,
            d.CountyName,
            d.SubcountyName,
            d.RoadName,
            d.VehicleReg,
            d.TransporterName,
            d.CargoName,
            FormatNumber(d.ExpectedKg),
            FormatNumber(d.ActualKg),
            FormatNumber(d.DiscrepancyKg),
            $"{d.VariancePct:F1}%"
        });

        // Structured custom-report builder: UseDefaults=true (the default) reproduces today's
        // exact fixed output unchanged. Only when a caller explicitly opts out does column
        // selection apply.
        var outputHeaders = headers;
        var outputRows = csvRows;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "weight_discrepancy", from, to);
        if (format == "xlsx")
        {
            var variancePctIdx = Array.IndexOf(outputHeaders, "Variance %");
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Weight Discrepancy Report", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                PercentageDataBarColumnIndexes = variancePctIdx >= 0 ? new[] { variancePctIdx } : null,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "weight_discrepancy", from, to);
        }

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Weight Discrepancy Report",
            ReportSubtitle = $"Transactions with discrepancy exceeding {FormatNumber(threshold)} kg",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
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

        const int statusColumnIndex = 2;
        var legend = new[]
        {
            ("#C6EFCE", "Paid"),
            ("#FEF3C7", "Pending")
        };

        // Structured custom-report builder: UseDefaults=true (the default) reproduces today's
        // exact fixed output unchanged. Only when a caller explicitly opts out does column
        // selection apply.
        var outputHeaders = headers;
        var outputRows = csvRows;
        int? effectiveStatusColumnIndex = statusColumnIndex;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
            var statusIdx = Array.IndexOf(outputHeaders, "Status");
            effectiveStatusColumnIndex = statusIdx >= 0 ? statusIdx : null;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "commercial_revenue", from, to);
        if (format == "xlsx")
        {
            var xlsxRequest = new ExcelReportRequest
            {
                ReportTitle = "Commercial Revenue Report", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                ConditionalStatusColumnIndex = effectiveStatusColumnIndex, Legend = legend,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            };
            return ExcelResult(GenerateExcel(xlsxRequest), "commercial_revenue", from, to);
        }

        var totalRevenue = invoiceData.Where(r => r.Status == "paid").Sum(r => r.TotalAmount);
        var totalPending = invoiceData.Where(r => r.Status == "pending").Sum(r => r.TotalAmount);

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Commercial Revenue Report",
            ReportSubtitle = "Revenue from commercial weighing fees",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
            StatusColumnIndex = effectiveStatusColumnIndex,
            Legend = legend,
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
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(w => w.Station != null && w.Station.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(w =>
                (w.SubcountyId.HasValue && w.SubcountyId == subcountyId) ||
                (!w.SubcountyId.HasValue && w.Station != null && w.Station.SubcountyId == subcountyId));
        if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
            query = query.Where(w =>
                (w.RoadId.HasValue && w.RoadId == roadId) ||
                (!w.RoadId.HasValue && w.Station != null && w.Station.RoadId == roadId));

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

        // Structured custom-report builder: UseDefaults=true (the default) reproduces today's
        // exact fixed output unchanged. Only when a caller explicitly opts out does column
        // selection apply.
        var outputHeaders = headers;
        var outputRows = csvRows;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "throughput", from, to);
        if (format == "xlsx")
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Throughput Report", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "throughput", from, to);

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
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
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
        // VehicleTareHistory has no own SubcountyId field (unlike WeighingTransaction) - county/
        // subcounty filtering goes through the tare check's station, same as Weighing's Scale Test Log.
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(th => th.Station != null && th.Station.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(th => th.Station != null && th.Station.SubcountyId == subcountyId);
        if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
            query = query.Where(th => th.Station != null && th.Station.RoadId == roadId);

        // Use ControlStatus as vehicle reg filter if provided
        if (!string.IsNullOrEmpty(filters.WeighingType))
        {
            // WeighingType field reused as vehicle reg filter for this report
            query = query.Where(th => th.Vehicle != null && th.Vehicle.RegNo.Contains(filters.WeighingType));
        }

        var tareData = await query
            .Include(th => th.Vehicle)
            .Include(th => th.Station).ThenInclude(s => s!.County)
            .Include(th => th.Station).ThenInclude(s => s!.Subcounty)
            .Include(th => th.Station).ThenInclude(s => s!.Road)
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
                CountyName = th.Station != null && th.Station.County != null ? th.Station.County.Name : "-",
                SubcountyName = th.Station != null && th.Station.Subcounty != null ? th.Station.Subcounty.Name : "-",
                RoadName = th.Station != null && th.Station.Road != null ? th.Station.Road.Name : "-",
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
                var driftPct = TareDriftHelper.ComputeTareDriftPercent(entry.TareWeightKg, previousTare);
                var isAnomaly = driftPct > 5;
                if (isAnomaly) anomalyCount++;

                processedRows.Add([
                    entry.VehicleReg,
                    entry.WeighedAt.ToString("dd/MM/yyyy HH:mm"),
                    entry.StationName,
                    entry.CountyName,
                    entry.SubcountyName,
                    entry.RoadName,
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
            "Vehicle Reg", "Date/Time", "Station", "County", "Sub County", "Road", "Tare Weight (kg)",
            "Previous Tare (kg)", "Drift %", "Source", "Status", "Notes"
        };

        // Header layout shifted +3 (County/Sub County/Road inserted after Station) - "Status" is
        // now at index 10, was 7.
        const int statusColumnIndex = 10;
        var legend = new[] { (BrandingConstants.Colors.SampleTemplateRed, "Anomaly (>5% drift)") };

        // Structured custom-report builder: UseDefaults=true (the default) reproduces today's
        // exact fixed output unchanged. Only when a caller explicitly opts out does column
        // selection apply.
        var outputHeaders = headers;
        var outputRows = processedRows;
        int? effectiveStatusColumnIndex = statusColumnIndex;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, processedRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
            var statusIdx = Array.IndexOf(outputHeaders, "Status");
            effectiveStatusColumnIndex = statusIdx >= 0 ? statusIdx : null;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "tare_weight_audit", from, to);
        if (format == "xlsx")
        {
            var driftPctIdx = Array.IndexOf(outputHeaders, "Drift %");
            var xlsxRequest = new ExcelReportRequest
            {
                ReportTitle = "Tare Weight Audit Report", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                ConditionalStatusColumnIndex = effectiveStatusColumnIndex, Legend = legend,
                PercentageDataBarColumnIndexes = driftPctIdx >= 0 ? new[] { driftPctIdx } : null,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            };
            return ExcelResult(GenerateExcel(xlsxRequest), "tare_weight_audit", from, to);
        }

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Tare Weight Audit Report",
            ReportSubtitle = "Tare weight changes per vehicle with anomaly detection (>5% drift)",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
            StatusColumnIndex = effectiveStatusColumnIndex,
            Legend = legend,
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
    // Tare Verification Report (5c) - separate from Tare Weight Audit's drift log. Buckets the
    // fleet by stored-tare status (valid / expiring within 14 days / expired / no tare) and flags
    // vehicles whose latest tare measurement drifted >5% (reusing TareDriftHelper.ComputeTareDriftPercent,
    // shared - Stage C - with CommercialWeighingService's live tare-anomaly detection).
    // =====================================================================

    private async Task<ReportResult> GenerateTareVerificationAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        // Vehicle carries no OrganizationId of its own (vehicles are shared across a transporter's
        // orgs) - scope the fleet the same way Fleet Utilization/Driver Productivity do, via the
        // tenant-filtered WeighingTransactions this org has actually captured. Not date-bounded -
        // this is a current fleet-state snapshot, not a transactional report - but still supports
        // narrowing to a single station.
        var vehicleIdsQuery = CommercialBaseQuery();
        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            vehicleIdsQuery = vehicleIdsQuery.Where(w => w.StationId == stationId);

        var vehicleIds = await vehicleIdsQuery.Select(w => w.VehicleId).Distinct().ToListAsync(ct);

        var vehicles = await _context.Vehicles
            .Where(v => vehicleIds.Contains(v.Id))
            .Include(v => v.Transporter)
            .OrderBy(v => v.RegNo)
            .Take(filters.PageSize)
            .ToListAsync(ct);

        var tareHistory = await _context.VehicleTareHistory
            .Where(th => th.DeletedAt == null && vehicleIds.Contains(th.VehicleId))
            .OrderBy(th => th.VehicleId).ThenBy(th => th.WeighedAt)
            .Select(th => new { th.VehicleId, th.TareWeightKg, th.WeighedAt })
            .ToListAsync(ct);
        var historyByVehicle = tareHistory.GroupBy(h => h.VehicleId).ToDictionary(g => g.Key, g => g.ToList());

        var now = DateTime.UtcNow;
        var processedRows = new List<string[]>();
        var statusCounts = new Dictionary<string, int> { ["Valid"] = 0, ["Expiring Soon"] = 0, ["Expired"] = 0, ["No Tare"] = 0 };
        var driftAlertCount = 0;

        foreach (var vehicle in vehicles)
        {
            string status;
            DateTime? expiryDate = null;
            int? daysUntilExpiry = null;

            if (!vehicle.LastTareWeighedAt.HasValue)
            {
                status = "No Tare";
            }
            else
            {
                // Same expiry resolution as CommercialWeighingService.UseStoredTareAsync (vehicle
                // override, falling back to the 90-day default; org grace period is a stored-tare
                // usability extension, not part of the underlying expiry date shown here).
                var expiryDays = vehicle.TareExpiryDays ?? 90;
                expiryDate = vehicle.LastTareWeighedAt.Value.AddDays(expiryDays);
                daysUntilExpiry = (int)Math.Ceiling((expiryDate.Value - now).TotalDays);

                status = daysUntilExpiry < 0 ? "Expired"
                    : daysUntilExpiry <= 14 ? "Expiring Soon"
                    : "Valid";
            }
            statusCounts[status]++;

            var driftAlert = false;
            if (historyByVehicle.TryGetValue(vehicle.Id, out var history) && history.Count >= 2)
            {
                var last = history[^1];
                var previous = history[^2];
                driftAlert = TareDriftHelper.ComputeTareDriftPercent(last.TareWeightKg, previous.TareWeightKg) > 5;
            }
            if (driftAlert) driftAlertCount++;

            processedRows.Add([
                vehicle.RegNo,
                vehicle.Transporter?.Name ?? "-",
                status,
                vehicle.LastTareWeightKg.HasValue ? FormatNumber(vehicle.LastTareWeightKg.Value) : "-",
                vehicle.LastTareWeighedAt?.ToString("dd/MM/yyyy HH:mm") ?? "-",
                expiryDate?.ToString("dd/MM/yyyy") ?? "-",
                daysUntilExpiry?.ToString() ?? "-",
                driftAlert ? "DRIFT ALERT" : "-"
            ]);
        }

        var headers = new[]
        {
            "Vehicle Reg", "Transporter", "Status", "Stored Tare (kg)", "Last Weighed At",
            "Expiry Date", "Days Until Expiry", "Drift Alert"
        };

        // Plain table, not the ConditionalStatusColumnIndex/Legend mechanism - that mechanism's
        // vocabulary (ReportStatusColors.Classify) is oriented at compliance states (Overloaded/
        // Warning/Compliant), not tare-expiry buckets, so hijacking it would misclassify these rows
        // rather than highlight them meaningfully. The bucket counts below carry the signal instead.
        var outputHeaders = headers;
        var outputRows = processedRows;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, processedRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "tare_verification", from, to);
        if (format == "xlsx")
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Tare Verification Report", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "tare_verification", from, to);

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Tare Verification Report",
            ReportSubtitle = "Fleet stored-tare status: valid, expiring within 14 days, or expired",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
            SummaryItems =
            [
                ("Total Vehicles", vehicles.Count.ToString()),
                ("Valid", statusCounts["Valid"].ToString()),
                ("Expiring Soon", statusCounts["Expiring Soon"].ToString()),
                ("Expired", statusCounts["Expired"].ToString()),
                ("No Tare", statusCounts["No Tare"].ToString()),
                ("Drift Alerts", driftAlertCount.ToString())
            ]
        };

        return PdfResult(doc, filters, "tare_verification", from, to);
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
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(w => w.Station != null && w.Station.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(w =>
                (w.SubcountyId.HasValue && w.SubcountyId == subcountyId) ||
                (!w.SubcountyId.HasValue && w.Station != null && w.Station.SubcountyId == subcountyId));
        if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
            query = query.Where(w =>
                (w.RoadId.HasValue && w.RoadId == roadId) ||
                (!w.RoadId.HasValue && w.Station != null && w.Station.RoadId == roadId));

        var vehicleData = await query
            .Include(w => w.Vehicle)
            .Include(w => w.Transporter)
            .GroupBy(w => new
            {
                w.VehicleId,
                VehicleReg = w.VehicleRegNumber,
                TransporterName = w.Transporter != null ? w.Transporter.Name : "-",
                RatedCapacityKg = w.Vehicle != null ? w.Vehicle.RatedCapacityKg : null
            })
            .Select(g => new
            {
                VehicleReg = g.Key.VehicleReg,
                TransporterName = g.Key.TransporterName,
                RatedCapacityKg = g.Key.RatedCapacityKg,
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

        // PayloadEfficiencyPercent = avg actual payload / rated capacity, computed here (not in the
        // SQL projection above) since it depends on AvgPayloadKg, itself an aggregate of the same
        // group. Null/omitted (not estimated) when the vehicle has no RatedCapacityKg configured -
        // replaces the old MaxPayload = Vehicle.DefaultTareWeightKg artifact, which used the empty-
        // vehicle tare weight as a stand-in for capacity (a bug: tare weight is not payload capacity).
        var efficiencyByVehicle = vehicleData.ToDictionary(
            v => v.VehicleReg,
            v => v.RatedCapacityKg.HasValue && v.RatedCapacityKg.Value > 0
                ? Math.Round(v.AvgPayloadKg / v.RatedCapacityKg.Value * 100, 1)
                : (decimal?)null);

        var headers = new[]
        {
            "Vehicle Reg", "Transporter", "Trip Count", "Total Net Weight (kg)",
            "Avg Payload (kg)", "Min Payload (kg)", "Max Payload (kg)",
            "Avg Gross (kg)", "Avg Tare (kg)", "Payload Efficiency (%)"
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
            FormatNumber(v.AvgTareKg),
            efficiencyByVehicle[v.VehicleReg].HasValue ? $"{efficiencyByVehicle[v.VehicleReg]:F1}%" : "-"
        });

        // Structured custom-report builder: UseDefaults=true (the default) reproduces today's
        // exact fixed output unchanged. Only when a caller explicitly opts out does column
        // selection apply.
        var outputHeaders = headers;
        var outputRows = csvRows;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "fleet_utilization", from, to);
        if (format == "xlsx")
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Fleet Utilization Report", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "fleet_utilization", from, to);

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Fleet Utilization Report",
            ReportSubtitle = "Per-vehicle trip count, payload, and utilization metrics",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
            SummaryItems =
            [
                ("Vehicles", vehicleData.Count.ToString()),
                ("Total Trips", FormatNumber(vehicleData.Sum(v => v.TripCount))),
                ("Total Net Weight", $"{FormatNumber(vehicleData.Sum(v => v.TotalNetWeightKg))} kg"),
                ("Avg Payload", vehicleData.Count > 0
                    ? $"{FormatNumber((int)vehicleData.Average(v => v.AvgPayloadKg))} kg"
                    : "N/A"),
                ("Avg Payload Efficiency", efficiencyByVehicle.Values.Any(v => v.HasValue)
                    ? $"{efficiencyByVehicle.Values.Where(v => v.HasValue).Average(v => v!.Value):F1}%"
                    : "N/A (no vehicle capacities configured)")
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
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(w => w.Station != null && w.Station.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(w =>
                (w.SubcountyId.HasValue && w.SubcountyId == subcountyId) ||
                (!w.SubcountyId.HasValue && w.Station != null && w.Station.SubcountyId == subcountyId));
        if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
            query = query.Where(w =>
                (w.RoadId.HasValue && w.RoadId == roadId) ||
                (!w.RoadId.HasValue && w.Station != null && w.Station.RoadId == roadId));

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

        // Structured custom-report builder: UseDefaults=true (the default) reproduces today's
        // exact fixed output unchanged. Only when a caller explicitly opts out does column
        // selection apply.
        var outputHeaders = headers;
        var outputRows = csvRows;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "driver_productivity", from, to);
        if (format == "xlsx")
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Driver Productivity Report", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "driver_productivity", from, to);

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Driver Productivity Report",
            ReportSubtitle = "Per-driver trip count, net weight, and turnaround time",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
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
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(w => w.Station != null && w.Station.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(w =>
                (w.SubcountyId.HasValue && w.SubcountyId == subcountyId) ||
                (!w.SubcountyId.HasValue && w.Station != null && w.Station.SubcountyId == subcountyId));
        if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
            query = query.Where(w =>
                (w.RoadId.HasValue && w.RoadId == roadId) ||
                (!w.RoadId.HasValue && w.Station != null && w.Station.RoadId == roadId));

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

        // Structured custom-report builder: UseDefaults=true (the default) reproduces today's
        // exact fixed output unchanged. Only when a caller explicitly opts out does column
        // selection apply.
        var outputHeaders = headers;
        var outputRows = csvRows;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "quality_commodity", from, to);
        if (format == "xlsx")
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Quality & Commodity Report", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "quality_commodity", from, to);

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Quality & Commodity Report",
            ReportSubtitle = "Quality deduction stats by cargo type",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
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
    // Shift Performance Report
    // =====================================================================

    private async Task<ReportResult> GenerateShiftPerformanceAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        // Base: all commercial transactions (include pending/voided for full shift picture)
        var query = _context.WeighingTransactions
            .Where(w => w.DeletedAt == null)
            .Where(w => w.WeighingMode == "commercial")
            .Where(w => w.WeighedAt >= from && w.WeighedAt <= to);

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            query = query.Where(w => w.StationId == stationId);
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(w => w.Station != null && w.Station.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(w =>
                (w.SubcountyId.HasValue && w.SubcountyId == subcountyId) ||
                (!w.SubcountyId.HasValue && w.Station != null && w.Station.SubcountyId == subcountyId));
        if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
            query = query.Where(w =>
                (w.RoadId.HasValue && w.RoadId == roadId) ||
                (!w.RoadId.HasValue && w.Station != null && w.Station.RoadId == roadId));

        // Join with the operator's active UserShift on the date of the transaction
        // to group by shift. We do this client-side by correlating WeighedByUserId + date
        // with user_shifts. Fetch raw transaction data first, then correlate in-memory.
        var txns = await query
            .Include(w => w.Station)
            .Select(w => new
            {
                w.Id,
                w.WeighedAt,
                w.WeighedByUserId,
                StationName = w.Station != null ? w.Station.Name : "-",
                w.FirstWeightAt,
                w.SecondWeightAt,
                w.NetWeightKg,
                w.ToleranceExceeded,
                w.CaptureStatus,
                w.VoidedAt,
                w.OverloadKg
            })
            .ToListAsync(ct);

        // Pull all user-shifts relevant to the date range to correlate in-memory
        var userIds = txns.Select(t => t.WeighedByUserId).Distinct().ToList();
        var fromDate = DateOnly.FromDateTime(from);
        var toDate = DateOnly.FromDateTime(to);

        var userShifts = await _context.UserShifts
            .Where(us => userIds.Contains(us.UserId))
            .Where(us => us.StartsOn <= toDate && (us.EndsOn == null || us.EndsOn >= fromDate))
            .Include(us => us.WorkShift)
            .ToListAsync(ct);

        // Group by shift name + date
        var grouped = txns
            .GroupBy(t =>
            {
                var txDate = DateOnly.FromDateTime(t.WeighedAt);
                var shift = userShifts
                    .Where(us => us.UserId == t.WeighedByUserId
                                 && us.StartsOn <= txDate
                                 && (us.EndsOn == null || us.EndsOn >= txDate))
                    .Select(us => us.WorkShift?.Name)
                    .FirstOrDefault() ?? "Unassigned";
                return new { Date = txDate, ShiftName = shift, StationName = t.StationName };
            })
            .Select(g =>
            {
                var twoPassData = g
                    .Where(t => t.FirstWeightAt != null && t.SecondWeightAt != null)
                    .ToList();
                var avgCycleMin = twoPassData.Count > 0
                    ? twoPassData.Average(t => (t.SecondWeightAt!.Value - t.FirstWeightAt!.Value).TotalMinutes)
                    : 0;
                return new
                {
                    g.Key.Date,
                    g.Key.ShiftName,
                    g.Key.StationName,
                    TotalTransactions = g.Count(),
                    TotalNetWeightKg = g.Sum(t => (long)(t.NetWeightKg ?? 0)),
                    AvgCycleMin = avgCycleMin,
                    Overloads = g.Count(t => t.OverloadKg > 0),
                    ToleranceExceptions = g.Count(t => t.ToleranceExceeded),
                    Voided = g.Count(t => t.VoidedAt != null)
                };
            })
            .OrderBy(r => r.Date).ThenBy(r => r.ShiftName)
            .ToList();

        var headers = new[]
        {
            "Date", "Shift", "Station", "Total Transactions", "Total Net Weight (kg)",
            "Avg Cycle Time (min)", "Overloads", "Tolerance Exceptions", "Voided"
        };

        var csvRows = grouped.Select(r => new[]
        {
            r.Date.ToString("dd/MM/yyyy"),
            r.ShiftName,
            r.StationName,
            r.TotalTransactions.ToString(),
            FormatNumber(r.TotalNetWeightKg),
            r.AvgCycleMin > 0 ? $"{r.AvgCycleMin:F1}" : "-",
            r.Overloads.ToString(),
            r.ToleranceExceptions.ToString(),
            r.Voided.ToString()
        });

        // Structured custom-report builder: UseDefaults=true (the default) reproduces today's
        // exact fixed output unchanged. Only when a caller explicitly opts out does column
        // selection apply.
        var outputHeaders = headers;
        var outputRows = csvRows;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "shift_performance", from, to);
        if (format == "xlsx")
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Shift Performance Report", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "shift_performance", from, to);

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Shift Performance Report",
            ReportSubtitle = "Commercial weighing activity grouped by shift and date",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
            SummaryItems =
            [
                ("Total Transactions", FormatNumber(grouped.Sum(r => r.TotalTransactions))),
                ("Total Net Weight", $"{FormatNumber(grouped.Sum(r => r.TotalNetWeightKg))} kg"),
                ("Tolerance Exceptions", FormatNumber(grouped.Sum(r => r.ToleranceExceptions))),
                ("Voided", FormatNumber(grouped.Sum(r => r.Voided)))
            ]
        };

        return PdfResult(doc, filters, "shift_performance", from, to);
    }

    // =====================================================================
    // Transaction Audit Log
    // =====================================================================

    private async Task<ReportResult> GenerateTransactionAuditLogAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        // Include ALL commercial transactions regardless of CaptureStatus or void, for audit purposes
        var query = _context.WeighingTransactions
            .Where(w => w.DeletedAt == null)
            .Where(w => w.WeighingMode == "commercial")
            .Where(w => w.WeighedAt >= from && w.WeighedAt <= to);

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            query = query.Where(w => w.StationId == stationId);
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(w => w.Station != null && w.Station.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(w =>
                (w.SubcountyId.HasValue && w.SubcountyId == subcountyId) ||
                (!w.SubcountyId.HasValue && w.Station != null && w.Station.SubcountyId == subcountyId));
        if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
            query = query.Where(w =>
                (w.RoadId.HasValue && w.RoadId == roadId) ||
                (!w.RoadId.HasValue && w.Station != null && w.Station.RoadId == roadId));

        var rows = await query
            .Include(w => w.Transporter)
            .Include(w => w.WeighedByUser)
            .OrderBy(w => w.WeighedAt)
            .Take(filters.PageSize)
            .Select(w => new
            {
                w.TicketNumber,
                VehicleReg = w.VehicleRegNumber,
                TransporterName = w.Transporter != null ? w.Transporter.Name : "-",
                w.FirstWeightKg,
                w.SecondWeightKg,
                w.NetWeightKg,
                w.QualityDeductionKg,
                w.AdjustedNetWeightKg,
                w.CaptureStatus,
                w.VoidedAt,
                w.VoidReason,
                CreatedBy = w.WeighedByUser != null ? w.WeighedByUser.FullName : "-",
                CreatedAt = w.WeighedAt
            })
            .ToListAsync(ct);

        var headers = new[]
        {
            "Ticket #", "Vehicle Reg", "Transporter",
            "First Weight (kg)", "Second Weight (kg)", "Net Weight (kg)",
            "Quality Deduction (kg)", "Adjusted Net (kg)",
            "Status", "Voided At", "Void Reason", "Created By", "Created At"
        };

        var csvRows = rows.Select(r => new[]
        {
            r.TicketNumber,
            r.VehicleReg,
            r.TransporterName,
            r.FirstWeightKg.HasValue ? FormatNumber(r.FirstWeightKg.Value) : "-",
            r.SecondWeightKg.HasValue ? FormatNumber(r.SecondWeightKg.Value) : "-",
            r.NetWeightKg.HasValue ? FormatNumber(r.NetWeightKg.Value) : "-",
            r.QualityDeductionKg.HasValue ? FormatNumber(r.QualityDeductionKg.Value) : "-",
            r.AdjustedNetWeightKg.HasValue ? FormatNumber(r.AdjustedNetWeightKg.Value) : "-",
            r.CaptureStatus,
            r.VoidedAt.HasValue ? r.VoidedAt.Value.ToString("dd/MM/yyyy HH:mm") : "-",
            r.VoidReason ?? "-",
            r.CreatedBy,
            r.CreatedAt.ToString("dd/MM/yyyy HH:mm")
        });

        // Structured custom-report builder: UseDefaults=true (the default) reproduces today's
        // exact fixed output unchanged. Only when a caller explicitly opts out does column
        // selection apply.
        var outputHeaders = headers;
        var outputRows = csvRows;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "transaction_audit_log", from, to);
        if (format == "xlsx")
        {
            // Not status-colour-coded: the "Status" column here is CaptureStatus ("captured"/
            // "pending"), while the actual exception worth flagging (voided) lives in a separate
            // "Voided At" timestamp column - colouring by CaptureStatus would highlight the wrong
            // thing, so this stays a plain (but now branded) export.
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Transaction Audit Log", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "transaction_audit_log", from, to);
        }

        var voidedCount = rows.Count(r => r.VoidedAt.HasValue);
        var completedCount = rows.Count(r => r.CaptureStatus == "captured");

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Transaction Audit Log",
            ReportSubtitle = "Full commercial transaction audit trail including voided records",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
            SummaryItems =
            [
                ("Total Records", FormatNumber(rows.Count)),
                ("Completed", FormatNumber(completedCount)),
                ("Voided", FormatNumber(voidedCount)),
                ("Total Net Weight", $"{FormatNumber(rows.Where(r => r.NetWeightKg.HasValue).Sum(r => (long)r.NetWeightKg!.Value))} kg")
            ]
        };

        return PdfResult(doc, filters, "transaction_audit_log", from, to);
    }

    // =====================================================================
    // Transaction Event Log (Phase 8: true per-event audit trail)
    // =====================================================================

    /// <summary>
    /// True per-event log, sourced from AuditLog rows CommercialWeighingService's mutating methods
    /// write explicitly (see WriteAuditLogAsync there) - distinct from GenerateTransactionAuditLogAsync
    /// above, which is a per-transaction snapshot. Scoped to WeighingTransaction/VehicleTareHistory
    /// resource types and the current organization (AuditLog is not a globally query-filtered entity -
    /// same manual org-scoping convention CommercialWeighingService itself uses for VehicleTareHistory).
    /// Filterable by date range (shared DateFrom/DateTo), user (UserId), and transaction reference
    /// (TicketNumber, matched against the resolved WeighingTransaction's ticket number) per
    /// reports.md's "Filtered by user, date, or transaction reference" requirement.
    /// </summary>
    private async Task<ReportResult> GenerateTransactionEventLogAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);
        var orgId = _tenantContext.OrganizationId;

        var resourceTypes = new[] { "WeighingTransaction", "VehicleTareHistory" };

        // Resolve the ticket-number/transaction-reference filter to a set of WeighingTransaction ids
        // up front so it can be applied at the AuditLog query level (before paging), rather than
        // filtering the already-paged in-memory result.
        List<Guid>? ticketFilterTxIds = null;
        if (!string.IsNullOrWhiteSpace(filters.TicketNumber))
        {
            var refFilter = filters.TicketNumber.Trim();
            ticketFilterTxIds = await _context.WeighingTransactions
                .AsNoTracking()
                .Where(t => t.WeighingMode == "commercial" && t.TicketNumber != null && t.TicketNumber.Contains(refFilter))
                .Select(t => t.Id)
                .ToListAsync(ct);
        }

        var query = _context.AuditLogs
            .AsNoTracking()
            .Include(a => a.User)
            .Where(a => resourceTypes.Contains(a.ResourceType))
            .Where(a => a.CreatedAt >= from && a.CreatedAt <= to)
            .Where(a => orgId == Guid.Empty || a.OrganizationId == orgId);

        if (!string.IsNullOrWhiteSpace(filters.UserId) && Guid.TryParse(filters.UserId, out var userIdFilter))
            query = query.Where(a => a.UserId == userIdFilter);

        if (ticketFilterTxIds != null)
            query = query.Where(a => a.ResourceType == "WeighingTransaction" && a.ResourceId.HasValue && ticketFilterTxIds.Contains(a.ResourceId.Value));

        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(filters.PageSize)
            .ToListAsync(ct);

        // Batch-resolve ticket numbers / vehicle registrations for the referenced resources (2 lookup
        // queries total, regardless of row count) rather than a per-row query.
        var txIds = logs.Where(l => l.ResourceType == "WeighingTransaction" && l.ResourceId.HasValue)
            .Select(l => l.ResourceId!.Value).Distinct().ToList();
        var historyIds = logs.Where(l => l.ResourceType == "VehicleTareHistory" && l.ResourceId.HasValue)
            .Select(l => l.ResourceId!.Value).Distinct().ToList();

        var txLookup = await _context.WeighingTransactions
            .AsNoTracking()
            .Where(t => txIds.Contains(t.Id))
            .Select(t => new { t.Id, t.TicketNumber, t.VehicleRegNumber })
            .ToDictionaryAsync(t => t.Id, ct);

        var historyLookup = await _context.VehicleTareHistory
            .AsNoTracking()
            .Where(h => historyIds.Contains(h.Id))
            .Select(h => new { h.Id, VehicleReg = h.Vehicle != null ? h.Vehicle.RegNo : null })
            .ToDictionaryAsync(h => h.Id, ct);

        var csvRows = logs.Select(l =>
        {
            string? ticketNumber = null;
            string? vehicleReg = null;

            if (l.ResourceType == "WeighingTransaction" && l.ResourceId.HasValue && txLookup.TryGetValue(l.ResourceId.Value, out var tx))
            {
                ticketNumber = tx.TicketNumber;
                vehicleReg = tx.VehicleRegNumber;
            }
            else if (l.ResourceType == "VehicleTareHistory" && l.ResourceId.HasValue && historyLookup.TryGetValue(l.ResourceId.Value, out var h))
            {
                vehicleReg = h.VehicleReg;
            }

            return new[]
            {
                l.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss"),
                l.Action,
                l.ResourceType,
                ticketNumber ?? "-",
                vehicleReg ?? "-",
                l.User?.FullName ?? "-",
                l.OldValues ?? "-",
                l.NewValues ?? "-"
            };
        }).ToList();

        var headers = new[] { "Timestamp", "Action", "Resource Type", "Ticket #", "Vehicle Reg", "User", "Old Values", "New Values" };

        var outputHeaders = headers;
        var outputRows = csvRows;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "transaction_event_log", from, to);
        if (format == "xlsx")
        {
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Transaction Event Log", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "transaction_event_log", from, to);
        }

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Transaction Event Log",
            ReportSubtitle = "Per-event action log for weighing transactions and tare records (captures, voids, approvals, overrides)",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
            SummaryItems =
            [
                ("Total Events", FormatNumber(logs.Count)),
                ("Unique Users", FormatNumber(logs.Select(l => l.UserId).Distinct().Count())),
                ("Distinct Actions", FormatNumber(logs.Select(l => l.Action).Distinct().Count()))
            ]
        };

        return PdfResult(doc, filters, "transaction_event_log", from, to);
    }

    // =====================================================================
    // Pending Transactions Report
    // =====================================================================

    private async Task<ReportResult> GeneratePendingTransactionsAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        var query = _context.WeighingTransactions
            .Where(w => w.DeletedAt == null)
            .Where(w => w.WeighingMode == "commercial")
            .Where(w => w.CaptureStatus == "first_weight_captured")
            .Where(w => w.VoidedAt == null)
            .Where(w => w.FirstWeightAt >= from && w.FirstWeightAt <= to);

        if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
            query = query.Where(w => w.StationId == stationId);
        if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
            query = query.Where(w => w.Station != null && w.Station.CountyId == countyId);
        if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
            query = query.Where(w =>
                (w.SubcountyId.HasValue && w.SubcountyId == subcountyId) ||
                (!w.SubcountyId.HasValue && w.Station != null && w.Station.SubcountyId == subcountyId));
        if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
            query = query.Where(w =>
                (w.RoadId.HasValue && w.RoadId == roadId) ||
                (!w.RoadId.HasValue && w.Station != null && w.Station.RoadId == roadId));

        var now = DateTime.UtcNow;
        var rows = await query
            .Include(w => w.Station).ThenInclude(s => s!.County)
            .Include(w => w.Station).ThenInclude(s => s!.Subcounty)
            .Include(w => w.Station).ThenInclude(s => s!.Road)
            .Include(w => w.Transporter)
            .Include(w => w.Road)
            .OrderBy(w => w.FirstWeightAt)
            .Take(filters.PageSize)
            .Select(w => new
            {
                w.TicketNumber,
                VehicleReg = w.VehicleRegNumber,
                TransporterName = w.Transporter != null ? w.Transporter.Name : "-",
                StationName = w.Station != null ? w.Station.Name : "-",
                w.FirstWeightAt,
                w.FirstWeightKg,
                CountyName = !string.IsNullOrWhiteSpace(w.LocationCounty) ? w.LocationCounty
                    : w.Station != null && w.Station.County != null ? w.Station.County.Name : "-",
                SubcountyName = !string.IsNullOrWhiteSpace(w.LocationSubcounty) ? w.LocationSubcounty
                    : w.Station != null && w.Station.Subcounty != null ? w.Station.Subcounty.Name : "-",
                RoadName = w.Road != null ? w.Road.Name
                    : w.Station != null && w.Station.Road != null ? w.Station.Road.Name : "-"
            })
            .ToListAsync(ct);

        var headers = new[]
        {
            "Ticket #", "Vehicle Reg", "Transporter", "Station", "County", "Sub County", "Road",
            "First Weight At", "Hours Pending", "First Weight (kg)"
        };

        var csvRows = rows.Select(r => new[]
        {
            r.TicketNumber,
            r.VehicleReg,
            r.TransporterName,
            r.StationName,
            r.CountyName,
            r.SubcountyName,
            r.RoadName,
            r.FirstWeightAt.HasValue ? r.FirstWeightAt.Value.ToString("dd/MM/yyyy HH:mm") : "-",
            r.FirstWeightAt.HasValue ? $"{(now - r.FirstWeightAt.Value).TotalHours:F1}" : "-",
            r.FirstWeightKg.HasValue ? FormatNumber(r.FirstWeightKg.Value) : "-"
        });

        // Structured custom-report builder: UseDefaults=true (the default) reproduces today's
        // exact fixed output unchanged. Only when a caller explicitly opts out does column
        // selection apply.
        var outputHeaders = headers;
        var outputRows = csvRows;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
        }

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "pending_transactions", from, to);
        if (format == "xlsx")
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Pending Transactions Report", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "pending_transactions", from, to);

        var avgHoursPending = rows.Count > 0 && rows.Any(r => r.FirstWeightAt.HasValue)
            ? rows.Where(r => r.FirstWeightAt.HasValue).Average(r => (now - r.FirstWeightAt!.Value).TotalHours)
            : 0;

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Pending Transactions Report",
            ReportSubtitle = "Transactions with first weight captured awaiting second weight",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
            SummaryItems =
            [
                ("Pending Count", FormatNumber(rows.Count)),
                ("Avg Hours Pending", avgHoursPending > 0 ? $"{avgHoursPending:F1}" : "N/A"),
                ("Oldest Pending", rows.Count > 0 && rows[0].FirstWeightAt.HasValue
                    ? rows[0].FirstWeightAt!.Value.ToString("dd/MM/yyyy HH:mm")
                    : "N/A")
            ]
        };

        return PdfResult(doc, filters, "pending_transactions", from, to);
    }

    // =====================================================================
    // Monthly Reconciliation Report
    // =====================================================================

    private async Task<ReportResult> GenerateMonthlyReconciliationAsync(
        ReportFilterParams filters, string format, CancellationToken ct)
    {
        var (from, to) = GetDateRange(filters);

        // Shared geo filters (station/county/subcounty/road), applied identically to the monthly
        // aggregate query below and the cargo-type revenue breakdown so both reconcile to the same
        // population of transactions.
        IQueryable<Models.Weighing.WeighingTransaction> ApplyGeoFilters(IQueryable<Models.Weighing.WeighingTransaction> q)
        {
            if (!string.IsNullOrEmpty(filters.StationId) && Guid.TryParse(filters.StationId, out var stationId))
                q = q.Where(w => w.StationId == stationId);
            if (!string.IsNullOrEmpty(filters.CountyId) && Guid.TryParse(filters.CountyId, out var countyId))
                q = q.Where(w => w.Station != null && w.Station.CountyId == countyId);
            if (!string.IsNullOrEmpty(filters.SubcountyId) && Guid.TryParse(filters.SubcountyId, out var subcountyId))
                q = q.Where(w =>
                    (w.SubcountyId.HasValue && w.SubcountyId == subcountyId) ||
                    (!w.SubcountyId.HasValue && w.Station != null && w.Station.SubcountyId == subcountyId));
            if (!string.IsNullOrEmpty(filters.RoadId) && Guid.TryParse(filters.RoadId, out var roadId))
                q = q.Where(w =>
                    (w.RoadId.HasValue && w.RoadId == roadId) ||
                    (!w.RoadId.HasValue && w.Station != null && w.Station.RoadId == roadId));
            return q;
        }

        // Extend the query window one calendar month earlier than `from` so the first displayed
        // month still has a previous-month baseline for the "comparison with previous month"
        // enrichment - that lead-in month is used only to build previousMonthLookup below, never
        // shown as its own row.
        var rangeStartMonth = new DateTime(from.Year, from.Month, 1);
        var extendedFrom = rangeStartMonth.AddMonths(-1);

        var extendedQuery = ApplyGeoFilters(_context.WeighingTransactions
            .Where(w => w.DeletedAt == null)
            .Where(w => w.WeighingMode == "commercial")
            .Where(w => w.WeighedAt >= extendedFrom && w.WeighedAt <= to));

        var extendedMonthlyData = await extendedQuery
            .GroupBy(w => new { w.WeighedAt.Year, w.WeighedAt.Month })
            .Select(g => new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                TotalTransactions = g.Count(),
                TotalNetWeightKg = g.Sum(x => (long)(x.NetWeightKg ?? 0)),
                TotalFeeKes = g.Sum(x => x.TotalFeeKes),
                ToleranceExceptions = g.Count(x => x.ToleranceExceeded),
                VoidedCount = g.Count(x => x.VoidedAt != null),
                CompletedCount = g.Count(x => x.CaptureStatus == "captured" && x.VoidedAt == null),
                AvgNetWeightKg = g.Where(x => x.NetWeightKg != null && x.NetWeightKg > 0).Any()
                    ? (int)g.Where(x => x.NetWeightKg != null && x.NetWeightKg > 0).Average(x => x.NetWeightKg!.Value)
                    : 0
            })
            .OrderBy(r => r.Year).ThenBy(r => r.Month)
            .ToListAsync(ct);

        var previousMonthLookup = extendedMonthlyData.ToDictionary(r => (r.Year, r.Month));
        var monthlyData = extendedMonthlyData.Where(r => new DateTime(r.Year, r.Month, 1) >= rangeStartMonth).ToList();

        // Revenue by cargo type (5d) - same population/filters as the monthly totals above (so
        // summing this breakdown across cargo types reconciles with "Total Fees"), just within the
        // originally requested [from, to] rather than the extended lead-in window.
        var revenueByCargoType = await ApplyGeoFilters(_context.WeighingTransactions
                .Where(w => w.DeletedAt == null)
                .Where(w => w.WeighingMode == "commercial")
                .Where(w => w.WeighedAt >= from && w.WeighedAt <= to)
                .Where(w => w.CargoId != null))
            .Include(w => w.Cargo)
            .GroupBy(w => new { w.CargoId, CargoName = w.Cargo!.Name })
            .Select(g => new
            {
                CargoName = g.Key.CargoName,
                TransactionCount = g.Count(),
                TotalRevenueKes = g.Sum(x => x.TotalFeeKes)
            })
            .OrderByDescending(c => c.TotalRevenueKes)
            .ToListAsync(ct);

        var headers = new[]
        {
            "Month", "Total Transactions", "Total Net Weight (kg)",
            "Total Fee (KES)", "Tolerance Exceptions", "Voided", "Completed", "Avg Net Weight (kg)",
            "Prev Month Transactions", "Prev Month Net Weight (kg)", "Prev Month Fee (KES)"
        };

        var csvRows = monthlyData.Select(r =>
        {
            var prevMonthDate = new DateTime(r.Year, r.Month, 1).AddMonths(-1);
            previousMonthLookup.TryGetValue((prevMonthDate.Year, prevMonthDate.Month), out var prev);
            return new[]
            {
                new DateTime(r.Year, r.Month, 1).ToString("MMMM yyyy"),
                r.TotalTransactions.ToString(),
                FormatNumber(r.TotalNetWeightKg),
                $"{r.TotalFeeKes:N2}",
                r.ToleranceExceptions.ToString(),
                r.VoidedCount.ToString(),
                r.CompletedCount.ToString(),
                FormatNumber(r.AvgNetWeightKg),
                prev?.TotalTransactions.ToString() ?? "N/A",
                prev != null ? FormatNumber(prev.TotalNetWeightKg) : "N/A",
                prev != null ? $"{prev.TotalFeeKes:N2}" : "N/A"
            };
        });

        // Structured custom-report builder: UseDefaults=true (the default) reproduces today's
        // exact fixed output unchanged. Only when a caller explicitly opts out does column
        // selection apply.
        var outputHeaders = headers;
        var outputRows = csvRows;

        if (!filters.UseDefaults)
        {
            var (selectedHeaders, selectedRows) = ApplyColumnSelection(headers, csvRows, filters.Columns);
            outputHeaders = selectedHeaders;
            outputRows = selectedRows;
        }

        var cargoRevenueHeaders = new[] { "Cargo Type", "Transactions", "Total Revenue (KES)" };
        var cargoRevenueRows = revenueByCargoType.Select(c => new[]
        {
            c.CargoName,
            c.TransactionCount.ToString(),
            $"{c.TotalRevenueKes:N2}"
        }).ToList();

        if (format == "csv")
            return CsvResult(GenerateCsv(outputHeaders, outputRows), "monthly_reconciliation", from, to);
        if (format == "xlsx")
            return ExcelResult(GenerateExcel(new ExcelReportRequest
            {
                ReportTitle = "Monthly Reconciliation Report", Headers = outputHeaders, Rows = outputRows, DateFrom = from, DateTo = to,
                SummaryTables = [new ExcelSummaryTable { Title = "Revenue by Cargo Type", Headers = cargoRevenueHeaders, Rows = cargoRevenueRows }],
                OrgName = filters.OrganizationName, OrgLogoFile = filters.OrgLogoFile
            }), "monthly_reconciliation", from, to);

        var doc = new CommercialReportDocumentBase
        {
            ReportTitle = "Monthly Reconciliation Report",
            ReportSubtitle = "Monthly summary of commercial weighing activity",
            DateFrom = from,
            DateTo = to,
            Headers = outputHeaders,
            Rows = outputRows.ToArray(),
            SummaryTables = [("Revenue by Cargo Type", cargoRevenueHeaders, cargoRevenueRows.ToArray())],
            SummaryItems =
            [
                ("Months", monthlyData.Count.ToString()),
                ("Total Transactions", FormatNumber(monthlyData.Sum(r => r.TotalTransactions))),
                ("Total Net Weight", $"{FormatNumber(monthlyData.Sum(r => r.TotalNetWeightKg))} kg"),
                ("Total Fees", $"{monthlyData.Sum(r => r.TotalFeeKes):N2} KES")
            ]
        };

        return PdfResult(doc, filters, "monthly_reconciliation", from, to);
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

        /// <summary>Optional - set by report types that have a genuine per-row status column.</summary>
        public int? StatusColumnIndex { get; set; }
        public (string colorHex, string label)[] Legend { get; set; } = [];

        /// <summary>
        /// Optional titled breakdown tables rendered beneath the main table (e.g. Monthly
        /// Reconciliation's "Revenue by Cargo Type") - a different grain than the main per-row
        /// table, so it can't just be extra columns. Mirrors AxleLoadAnalysisDocument's SummaryTables.
        /// </summary>
        public (string title, string[] headers, string[][] rows)[] SummaryTables { get; set; } = [];

        protected override void ComposeContent(IContainer container)
        {
            container.Column(col =>
            {
                col.Spacing(5);

                if (SummaryItems.Length > 0)
                    col.Item().Element(c => ComposeSummaryCards(c, SummaryItems));

                col.Item().Element(c => ComposeDataTable(c, Headers, Rows, conditionalStatusColumnIndex: StatusColumnIndex));
                col.Item().Element(c => ComposeLegend(c, Legend));

                foreach (var table in SummaryTables)
                    col.Item().Element(c => ComposeTitledTable(c, table.title, table.headers, table.rows));
            });
        }
    }
}
