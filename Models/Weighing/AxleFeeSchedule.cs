namespace TruLoad.Backend.Models;

/// <summary>
/// Fee calculation tiers for overload penalties per legal framework.
/// 
/// Defines how overload charges and demerit points are calculated based on:
/// - Legal framework (EAC or Traffic Act)
/// - Fee type (GVW-based or per-axle)
/// - Overload amount (kg)
/// </summary>
public class AxleFeeSchedule
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Legal framework this schedule applies to
    /// - EAC = EAC Vehicle Load Control Act (2016) - 5% tolerance
    /// - TRAFFIC_ACT = Kenya Traffic Act (Cap 403) - zero tolerance
    /// </summary>
    public string LegalFramework { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of fee calculation
    /// - GVW = Gross Vehicle Weight overload fee
    /// - AXLE = Per-axle overload fee
    /// Fee calculation uses max(GVW_fee, sum(axle_fees))
    /// </summary>
    public string FeeType { get; set; } = string.Empty;
    
    /// <summary>
    /// Minimum overload in kg (inclusive)
    /// Fee applies when overload_kg >= overload_min_kg
    /// </summary>
    public int OverloadMinKg { get; set; }
    
    /// <summary>
    /// Maximum overload in kg (inclusive), NULL = no upper limit
    /// Fee applies when overload_kg <= overload_max_kg (if set)
    /// </summary>
    public int? OverloadMaxKg { get; set; }
    
    /// <summary>
    /// Fee per kilogram of overload in USD
    /// Examples: 0.5 USD/kg, 1.0 USD/kg, etc.
    /// </summary>
    public decimal FeePerKgUsd { get; set; }
    
    /// <summary>
    /// Flat/minimum fee component in USD
    /// Total fee = flat_fee + (overload_kg * fee_per_kg)
    /// </summary>
    public decimal FlatFeeUsd { get; set; } = 0m;
    
    /// <summary>
    /// Demerit points assigned for this overload category
    /// EAC uses demerit system for compliance scoring
    /// Traffic Act may use different escalation
    /// </summary>
    public int DemeritPoints { get; set; } = 0;
    
    /// <summary>
    /// Description of penalty or violation
    /// Examples: "Overweight GVW", "Overweight Axle", "Dangerous Load"
    /// </summary>
    public string? PenaltyDescription { get; set; }
    
    /// <summary>
    /// Effective start date for this fee schedule
    /// </summary>
    public DateOnly EffectiveFrom { get; set; }
    
    /// <summary>
    /// Effective end date, NULL = currently active
    /// Allows historical tracking of fee changes
    /// </summary>
    public DateOnly? EffectiveTo { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; } // Soft delete support
}
