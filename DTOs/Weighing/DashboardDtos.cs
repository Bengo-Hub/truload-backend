namespace TruLoad.Backend.DTOs.Weighing;

/// <summary>
/// DTO for weighing statistics summary.
/// </summary>
public class WeighingStatisticsDto
{
    public int TotalWeighings { get; set; }
    public int LegalCount { get; set; }
    public int OverloadedCount { get; set; }
    public int WarningCount { get; set; }
    public decimal ComplianceRate { get; set; }
    public decimal TotalFeesKes { get; set; }
    public decimal TotalFeesUsd { get; set; }
    public decimal AvgOverloadKg { get; set; }
    public long TotalNetWeightKg { get; set; }
    public int UniqueTransporters { get; set; }

    /// <summary>
    /// Computed commercial tariff-engine revenue for the selected range — NOT the same as
    /// <see cref="TotalFeesKes"/> above (that field is summed from
    /// <c>WeighingTransaction.TotalFeeKes</c>, which is only ever written by the ENFORCEMENT
    /// compliance/overload fee calculation and is never touched by <c>CommercialWeighingService</c>,
    /// so it is always zero for a commercial-only tenant). This field instead sums the real
    /// <c>CommercialTariffRule</c>-resolved fee: <c>Invoice.AmountDue</c> for Immediate-billed
    /// weighings, plus <c>CommercialTariffAccrual.ComputedAmountKes</c> for weighings whose matched
    /// rule defers to Daily/Weekly/Monthly billing (money already earned by a weighing in this
    /// range, even if not yet rolled into an invoice) — see <c>WeighingController.GetStatistics</c>.
    /// </summary>
    public decimal TariffRevenueKes { get; set; }

    /// <summary>
    /// Count of weighings in the range whose <c>ControlStatus</c> is <c>ToleranceExceeded</c>
    /// (commercial weighing's discrepancy-between-declared-and-measured-weight flag) — paired with
    /// <see cref="TotalWeighings"/> on the client to derive a tolerance-exception rate.
    /// </summary>
    public int ToleranceExceededCount { get; set; }
}

/// <summary>
/// DTO for compliance trend data points.
/// </summary>
public class ComplianceTrendDto
{
    public string Name { get; set; } = string.Empty;
    public int Compliant { get; set; }
    public int Overloaded { get; set; }
    public int Warning { get; set; }
}

/// <summary>
/// DTO for tonnage-trend data points (commercial weighing) - one bucket per hour/day/week/month,
/// per <see cref="TruLoad.Backend.Common.TonnageTrendGranularity"/>. Built for the "aggregate tonnage
/// by hour/day/week/month" reporting need (quarry/waste-treatment tenants billed on periodic
/// tonnage), not just a UI chart - the same bucketed rows are reusable for a period-end statement.
/// </summary>
public class TonnageTrendDto
{
    public string Name { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public decimal TotalNetWeightKg { get; set; }
    public int TransactionCount { get; set; }
}

/// <summary>
/// DTO for tolerance-exception-rate trend data points (commercial weighing) - one bucket per EAT
/// calendar day, mirroring <see cref="ComplianceTrendDto"/>'s daily-GroupBy shape but for the
/// commercial-only <c>ToleranceExceeded</c> control status (a declared-vs-measured-weight
/// discrepancy) rather than the enforcement axle/overload statuses.
/// </summary>
public class ToleranceTrendDto
{
    public string Name { get; set; } = string.Empty;
    public int TotalWeighings { get; set; }
    public int ToleranceExceededCount { get; set; }
    public decimal ToleranceExceptionRate { get; set; }
}

/// <summary>
/// DTO for overload distribution by severity band.
/// </summary>
public class OverloadDistributionDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

/// <summary>
/// DTO for station performance metrics.
/// </summary>
public class StationPerformanceDto
{
    public Guid StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public int TotalWeighings { get; set; }
    public int OverloadedCount { get; set; }
    public decimal ComplianceRate { get; set; }
    public decimal RevenueKes { get; set; }
    public decimal RevenueUsd { get; set; }
    public decimal AvgProcessingTime { get; set; }
}

/// <summary>
/// DTO for revenue breakdown by station.
/// </summary>
public class RevenueByStationDto
{
    public Guid StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public decimal RevenueKes { get; set; }
    public decimal RevenueUsd { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

/// <summary>
/// DTO for monthly revenue trend.
/// </summary>
public class MonthlyRevenueDto
{
    public string Name { get; set; } = string.Empty; // "Jan 2024"
    public decimal RevenueKes { get; set; }
    public decimal RevenueUsd { get; set; }
    public int TransactionCount { get; set; }
}

/// <summary>
/// DTO for payment method distribution.
/// </summary>
public class PaymentMethodDistributionDto
{
    public string Name { get; set; } = string.Empty; // "Cash", "MPesa", etc.
    public decimal Amount { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

/// <summary>
/// DTO for users grouped by station.
/// </summary>
public class UsersByStationDto
{
    public Guid StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public int Count { get; set; }
}
