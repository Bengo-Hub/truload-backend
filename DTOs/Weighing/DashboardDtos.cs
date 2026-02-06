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
    public decimal AvgOverloadKg { get; set; }
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
    public decimal Revenue { get; set; }
    public decimal AvgProcessingTime { get; set; }
}

/// <summary>
/// DTO for revenue breakdown by station.
/// </summary>
public class RevenueByStationDto
{
    public Guid StationId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

/// <summary>
/// DTO for monthly revenue trend.
/// </summary>
public class MonthlyRevenueDto
{
    public string Name { get; set; } = string.Empty; // "Jan 2024"
    public decimal Revenue { get; set; }
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
