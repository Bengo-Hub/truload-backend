namespace TruLoad.Backend.Models.System;

/// <summary>
/// Fee calculation tiers for overload penalties per legal framework
/// Unified table for EAC and Traffic Act fee bands (GVW and AXLE types)
/// </summary>
public class AxleFeeSchedule
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Legal framework: EAC or TRAFFIC_ACT
    /// </summary>
    public string LegalFramework { get; set; } = string.Empty;
    
    /// <summary>
    /// Fee type: GVW or AXLE
    /// </summary>
    public string FeeType { get; set; } = string.Empty;
    
    /// <summary>
    /// Minimum overload in kg (inclusive)
    /// </summary>
    public int OverloadMinKg { get; set; }
    
    /// <summary>
    /// Maximum overload in kg (inclusive, NULL = no upper limit)
    /// </summary>
    public int? OverloadMaxKg { get; set; }
    
    /// <summary>
    /// Fee per kg in USD (used when act ChargingCurrency = USD, e.g. EAC Act)
    /// </summary>
    public decimal FeePerKgUsd { get; set; }

    /// <summary>
    /// Flat fee component in USD (default 0)
    /// </summary>
    public decimal FlatFeeUsd { get; set; } = 0;

    /// <summary>
    /// Fee per kg in KES (used when act ChargingCurrency = KES, e.g. Traffic Act Cap 403).
    /// When non-zero, this is the native KES rate — no USD→KES conversion needed.
    /// </summary>
    public decimal FeePerKgKes { get; set; }

    /// <summary>
    /// Flat fee component in KES (default 0)
    /// </summary>
    public decimal FlatFeeKes { get; set; } = 0;
    
    /// <summary>
    /// Demerit points assigned for this overload band
    /// </summary>
    public int DemeritPoints { get; set; } = 0;
    
    /// <summary>
    /// Description of penalty/band
    /// </summary>
    public string PenaltyDescription { get; set; } = string.Empty;
    
    /// <summary>
    /// Effective start date for this fee schedule
    /// </summary>
    public DateTime EffectiveFrom { get; set; }
    
    /// <summary>
    /// Effective end date (NULL = currently active)
    /// </summary>
    public DateTime? EffectiveTo { get; set; }
    
    /// <summary>
    /// Conviction number (1 = first conviction, 2 = second conviction).
    /// Used by Traffic Act for differentiated fine schedules per Rule 41(2).
    /// EAC Act uses multiplier-based repeat offender logic instead.
    /// </summary>
    public int ConvictionNumber { get; set; } = 1;

    /// <summary>
    /// Active status
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
