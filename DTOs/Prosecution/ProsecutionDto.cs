using TruLoad.Backend.DTOs.Shared;

namespace TruLoad.Backend.DTOs.Prosecution;

/// <summary>
/// Prosecution Case Data Transfer Object
/// </summary>
public class ProsecutionCaseDto
{
    public Guid Id { get; set; }
    public Guid CaseRegisterId { get; set; }
    public string CaseNo { get; set; } = string.Empty;
    public Guid? WeighingId { get; set; }
    public string? WeighingTicketNo { get; set; }
    public Guid ProsecutionOfficerId { get; set; }
    public string? ProsecutionOfficerName { get; set; }
    public Guid ActId { get; set; }
    public string? ActName { get; set; }
    public string ChargingCurrency { get; set; } = "KES";

    // Charge details
    public int GvwOverloadKg { get; set; }
    public decimal GvwFeeUsd { get; set; }
    public decimal GvwFeeKes { get; set; }
    public int MaxAxleOverloadKg { get; set; }
    public decimal MaxAxleFeeUsd { get; set; }
    public decimal MaxAxleFeeKes { get; set; }
    public string BestChargeBasis { get; set; } = "gvw";
    public decimal PenaltyMultiplier { get; set; }
    public int OffenseCount { get; set; }
    public int DemeritPoints { get; set; }
    public decimal TotalFeeUsd { get; set; }
    public decimal TotalFeeKes { get; set; }
    public decimal ForexRate { get; set; }

    // Status
    public string? CertificateNo { get; set; }
    public string? CaseNotes { get; set; }
    public string Status { get; set; } = "pending";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Charge calculation result
/// </summary>
public class ChargeCalculationResult
{
    public Guid WeighingId { get; set; }
    public string LegalFramework { get; set; } = string.Empty;

    // GVW-based charges
    public int GvwOverloadKg { get; set; }
    public decimal GvwFeeUsd { get; set; }
    public decimal GvwFeeKes { get; set; }

    // Axle-based charges
    public int MaxAxleOverloadKg { get; set; }
    public decimal MaxAxleFeeUsd { get; set; }
    public decimal MaxAxleFeeKes { get; set; }
    public List<AxleChargeBreakdown> AxleBreakdown { get; set; } = new();

    // Best charge determination
    public string BestChargeBasis { get; set; } = "gvw";
    public decimal TotalFeeUsd { get; set; }
    public decimal TotalFeeKes { get; set; }

    // Penalty info
    public decimal PenaltyMultiplier { get; set; } = 1.0m;
    public bool IsRepeatOffender { get; set; }
    public int PriorOffenseCount { get; set; }
    public int DemeritPoints { get; set; }

    // Forex & Currency
    public decimal ForexRate { get; set; }
    public string ChargingCurrency { get; set; } = "KES";
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Per-axle charge breakdown
/// </summary>
public class AxleChargeBreakdown
{
    public string AxleType { get; set; } = string.Empty;
    public int AxleNumber { get; set; }
    public int MeasuredKg { get; set; }
    public int PermissibleKg { get; set; }
    public int OverloadKg { get; set; }
    public decimal FeeUsd { get; set; }
    public decimal FeeKes { get; set; }
}

/// <summary>
/// Create prosecution case request
/// </summary>
public class CreateProsecutionRequest
{
    /// <summary>
    /// Applicable Act ID (EAC or Traffic Act)
    /// </summary>
    public Guid ActId { get; set; }

    /// <summary>
    /// Pre-calculated charge result (optional - will calculate if not provided)
    /// </summary>
    public ChargeCalculationResult? ChargeCalculation { get; set; }

    /// <summary>
    /// Additional notes
    /// </summary>
    public string? CaseNotes { get; set; }
}

/// <summary>
/// Update prosecution case request
/// </summary>
public class UpdateProsecutionRequest
{
    public Guid? ActId { get; set; }
    public string? CaseNotes { get; set; }
    public string? Status { get; set; }
}

/// <summary>
/// Search criteria for prosecution cases
/// </summary>
public class ProsecutionSearchCriteria : PagedRequest
{
    public string? CaseNo { get; set; }
    public Guid? CaseRegisterId { get; set; }
    public Guid? WeighingId { get; set; }
    public Guid? StationId { get; set; }
    public Guid? ActId { get; set; }
    public string? Status { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
    public decimal? MinTotalFee { get; set; }
    public decimal? MaxTotalFee { get; set; }

    public DateTime? EffectiveFromDate => DateFrom ?? CreatedFrom;
    public DateTime? EffectiveToDate => DateTo ?? CreatedTo;
}

/// <summary>
/// Prosecution statistics response DTO matching frontend ProsecutionStatistics type
/// </summary>
public class ProsecutionStatisticsDto
{
    public int TotalCases { get; set; }
    public int PendingCases { get; set; }
    public int InvoicedCases { get; set; }
    public int PaidCases { get; set; }
    public int CourtCases { get; set; }
    public decimal TotalFeesKes { get; set; }
    public decimal TotalFeesUsd { get; set; }
    public decimal CollectedFeesKes { get; set; }
    public decimal CollectedFeesUsd { get; set; }
}
