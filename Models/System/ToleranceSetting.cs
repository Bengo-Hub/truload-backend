namespace TruLoad.Backend.Models;

/// <summary>
/// Tolerance settings for different legal frameworks (EAC vs Traffic Act)
/// Defines acceptable weight variance before enforcement action
/// </summary>
public class ToleranceSetting
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Setting code (e.g., "EAC_TOLERANCE", "TRAFFIC_ACT_TOLERANCE")
    /// </summary>
    public string Code { get; set; } = string.Empty;
    
    /// <summary>
    /// Setting name
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Applicable legal framework (EAC, TRAFFIC_ACT, BOTH)
    /// </summary>
    public string LegalFramework { get; set; } = string.Empty;
    
    /// <summary>
    /// Tolerance percentage (e.g., 5.0 for 5%)
    /// Applied to permissible weight to calculate tolerance threshold
    /// </summary>
    public decimal TolerancePercentage { get; set; }
    
    /// <summary>
    /// Absolute tolerance in kg (alternative to percentage)
    /// </summary>
    public int? ToleranceKg { get; set; }
    
    /// <summary>
    /// Applies to: GVW, AXLE, or BOTH
    /// </summary>
    public string AppliesTo { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of tolerance rules
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Effective date for this tolerance setting
    /// </summary>
    public DateTime EffectiveFrom { get; set; }
    
    /// <summary>
    /// Expiry date (null if current)
    /// </summary>
    public DateTime? EffectiveTo { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
