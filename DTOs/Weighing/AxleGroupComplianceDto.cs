namespace TruLoad.Backend.DTOs.Weighing;

/// <summary>
/// DTO representing axle group compliance calculation result.
/// Implements Kenya Traffic Act Cap 403 group-based compliance checking.
/// </summary>
public class AxleGroupResultDto
{
    /// <summary>
    /// Group label (A, B, C, D)
    /// </summary>
    public string GroupLabel { get; set; } = string.Empty;

    /// <summary>
    /// Axle type classification (Steering, SingleDrive, Tandem, Tridem, Quad)
    /// </summary>
    public string AxleType { get; set; } = string.Empty;

    /// <summary>
    /// Number of axles in this group
    /// </summary>
    public int AxleCount { get; set; }

    /// <summary>
    /// Total measured weight of all axles in group (kg)
    /// </summary>
    public int GroupWeightKg { get; set; }

    /// <summary>
    /// Total permissible weight of all axles in group (kg)
    /// </summary>
    public int GroupPermissibleKg { get; set; }

    /// <summary>
    /// Applied tolerance in kg
    /// 5% for single axles, 0% for groups (Tandem/Tridem)
    /// </summary>
    public int ToleranceKg { get; set; }

    /// <summary>
    /// Effective limit including tolerance (GroupPermissible + ToleranceKg)
    /// </summary>
    public int EffectiveLimitKg { get; set; }

    /// <summary>
    /// Overload amount (kg) - Max(0, GroupWeight - EffectiveLimit)
    /// </summary>
    public int OverloadKg { get; set; }

    /// <summary>
    /// Pavement Damage Factor - (Actual/Permissible)^4
    /// </summary>
    public decimal PavementDamageFactor { get; set; }

    /// <summary>
    /// Operational tolerance applied (kg)
    /// </summary>
    public int OperationalToleranceKg { get; set; }

    /// <summary>
    /// Compliance status: LEGAL, WARNING, OVERLOAD
    /// </summary>
    public string Status { get; set; } = "LEGAL";

    /// <summary>
    /// Fee calculated for this group in USD
    /// </summary>
    public decimal FeeUsd { get; set; }

    /// <summary>
    /// Demerit points for this group overload
    /// </summary>
    public int DemeritPoints { get; set; }

    /// <summary>
    /// Individual axle details within this group
    /// </summary>
    public List<AxleDetailDto> Axles { get; set; } = [];
}

/// <summary>
/// DTO for individual axle detail within a group
/// </summary>
public class AxleDetailDto
{
    public int AxleNumber { get; set; }
    public int MeasuredWeightKg { get; set; }
    public int PermissibleWeightKg { get; set; }
    public int OverloadKg { get; set; }
    public string? TyreType { get; set; }
    public decimal? SpacingMeters { get; set; }
}

/// <summary>
/// Complete compliance result for a weighing transaction
/// </summary>
public class WeighingComplianceResultDto
{
    /// <summary>
    /// Weighing transaction ID
    /// </summary>
    public Guid WeighingId { get; set; }

    /// <summary>
    /// Ticket number
    /// </summary>
    public string TicketNumber { get; set; } = string.Empty;

    /// <summary>
    /// Vehicle registration number
    /// </summary>
    public string? VehicleRegNumber { get; set; }

    /// <summary>
    /// Axle group compliance results
    /// </summary>
    public List<AxleGroupResultDto> GroupResults { get; set; } = [];

    /// <summary>
    /// GVW measured (kg)
    /// </summary>
    public int GvwMeasuredKg { get; set; }

    /// <summary>
    /// GVW permissible (kg)
    /// </summary>
    public int GvwPermissibleKg { get; set; }

    /// <summary>
    /// GVW overload (kg) - 0% tolerance
    /// </summary>
    public int GvwOverloadKg { get; set; }

    /// <summary>
    /// Total axle fees (USD)
    /// </summary>
    public decimal TotalAxleFeeUsd { get; set; }

    /// <summary>
    /// GVW overload fee (USD)
    /// </summary>
    public decimal GvwFeeUsd { get; set; }

    /// <summary>
    /// Total fee - MAX(GvwFee, TotalAxleFees) per EAC Act
    /// </summary>
    public decimal TotalFeeUsd { get; set; }

    /// <summary>
    /// Demerit points calculation result
    /// </summary>
    public DemeritPointsResultDto DemeritPoints { get; set; } = new();

    /// <summary>
    /// Overall compliance status: Compliant, Warning, Overloaded
    /// </summary>
    public string OverallStatus { get; set; } = "Compliant";

    /// <summary>
    /// Whether vehicle is compliant
    /// </summary>
    public bool IsCompliant { get; set; } = true;

    /// <summary>
    /// Whether vehicle should be sent to yard
    /// </summary>
    public bool ShouldSendToYard { get; set; }

    /// <summary>
    /// Operational tolerance applied for GVW (kg)
    /// </summary>
    public int OperationalToleranceKg { get; set; }

    /// <summary>
    /// Violation reasons (if any)
    /// </summary>
    public List<string> ViolationReasons { get; set; } = [];
}

/// <summary>
/// Demerit points calculation result
/// </summary>
public class DemeritPointsResultDto
{
    /// <summary>
    /// Total demerit points across all violations
    /// </summary>
    public int TotalPoints { get; set; }

    /// <summary>
    /// Breakdown of points by violation type
    /// </summary>
    public List<DemeritPointBreakdownDto> Breakdown { get; set; } = [];

    /// <summary>
    /// Applicable penalty based on total points
    /// </summary>
    public PenaltyDto? ApplicablePenalty { get; set; }

    /// <summary>
    /// Whether case requires court prosecution
    /// </summary>
    public bool RequiresCourt { get; set; }

    /// <summary>
    /// License suspension period in days (if applicable)
    /// </summary>
    public int? SuspensionDays { get; set; }
}

/// <summary>
/// Individual demerit point breakdown item
/// </summary>
public class DemeritPointBreakdownDto
{
    /// <summary>
    /// Violation type (STEERING, TANDEM, TRIDEM, GVW)
    /// </summary>
    public string ViolationType { get; set; } = string.Empty;

    /// <summary>
    /// Overload amount that triggered the points
    /// </summary>
    public int OverloadKg { get; set; }

    /// <summary>
    /// Points assigned for this violation
    /// </summary>
    public int Points { get; set; }
}

/// <summary>
/// Penalty information DTO
/// </summary>
public class PenaltyDto
{
    public string Description { get; set; } = string.Empty;
    public int? SuspensionDays { get; set; }
    public bool RequiresCourt { get; set; }
    public decimal AdditionalFineUsd { get; set; }
    public decimal AdditionalFineKes { get; set; }
}