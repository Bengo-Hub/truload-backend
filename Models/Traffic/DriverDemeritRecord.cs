namespace TruLoad.Backend.Models;

/// <summary>
/// Individual demerit point record for driver violations.
/// Tracks violation history with automatic expiry (typically 36 months).
/// Linked to case registers and weighing transactions for audit trail.
/// </summary>
public class DriverDemeritRecord
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Driver who received the demerit points (foreign key)
    /// </summary>
    public Guid DriverId { get; set; }
    
    /// <summary>
    /// Related case register entry (if applicable)
    /// Links violation to formal case documentation
    /// </summary>
    public Guid? CaseRegisterId { get; set; }
    
    /// <summary>
    /// Related weighing transaction (if applicable)
    /// Direct link to weighing event that triggered violation
    /// </summary>
    public Guid? WeighingId { get; set; }
    
    /// <summary>
    /// Date when violation occurred
    /// Used as basis for expiry calculation (violation_date + 36 months)
    /// </summary>
    public DateTime ViolationDate { get; set; }
    
    /// <summary>
    /// Number of demerit points assigned for this violation
    /// From axle_fee_schedules.demerit_points based on overload severity
    /// Range: 3-20 points typical
    /// </summary>
    public int PointsAssigned { get; set; }
    
    /// <summary>
    /// Fee schedule that determined the points (foreign key)
    /// Links to specific tier in axle_fee_schedules
    /// </summary>
    public Guid? FeeScheduleId { get; set; }
    
    /// <summary>
    /// Legal framework under which violation was charged
    /// - EAC: East African Community regulations
    /// - TRAFFIC_ACT: Kenya Traffic Act
    /// </summary>
    public string LegalFramework { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of violation
    /// - GVW_OVERLOAD: Gross Vehicle Weight exceeded
    /// - AXLE_OVERLOAD: Individual axle weight exceeded
    /// - PERMIT_VIOLATION: Operating without valid permit
    /// - OTHER: Other traffic violations
    /// </summary>
    public string ViolationType { get; set; } = string.Empty;
    
    /// <summary>
    /// Overload amount in kg (if weight-related violation)
    /// NULL for non-weight violations
    /// </summary>
    public int? OverloadKg { get; set; }
    
    /// <summary>
    /// Penalty amount charged in USD
    /// Financial penalty associated with violation
    /// </summary>
    public decimal PenaltyAmountUsd { get; set; }
    
    /// <summary>
    /// Payment status of the penalty
    /// - pending: Not yet paid
    /// - paid: Penalty settled
    /// - waived: Penalty forgiven (e.g., successful appeal)
    /// </summary>
    public string PaymentStatus { get; set; } = "pending";
    
    /// <summary>
    /// Date when these points will expire
    /// Calculated as violation_date + 36 months (configurable)
    /// Points no longer count toward suspension after this date
    /// </summary>
    public DateTime PointsExpiryDate { get; set; }
    
    /// <summary>
    /// Has this record expired?
    /// TRUE if current date > points_expiry_date
    /// Updated by background job
    /// </summary>
    public bool IsExpired { get; set; } = false;
    
    /// <summary>
    /// Optional notes about the violation
    /// Officer comments, special circumstances, etc.
    /// </summary>
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public Driver Driver { get; set; } = null!;
    public AxleFeeSchedule? FeeSchedule { get; set; }
    // Future: public CaseRegister? CaseRegister { get; set; }
    // Future: public Weighing? Weighing { get; set; }
}
