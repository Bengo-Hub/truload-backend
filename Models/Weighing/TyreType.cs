namespace TruLoad.Backend.Models;

/// <summary>
/// Master reference for tyre type classifications.
/// 
/// Tyre types define the physical configuration of tyres on an axle,
/// which impacts permissible weight limits and fee calculations.
/// </summary>
public class TyreType
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Tyre type code (single character)
    /// - S = Single tyre (one tyre per axle end, 7500kg typical)
    /// - D = Dual/Twin tyres (two tyres per axle end, 10000kg typical)
    /// - W = Wide single super tyre (8000kg typical)
    /// </summary>
    public string Code { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name (e.g., "Single Tyre", "Dual Tyres", "Wide Single")
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Detailed description of tyre configuration
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Typical maximum permissible weight for this tyre type in kg
    /// Guide value for common configurations
    /// </summary>
    public int? TypicalMaxWeightKg { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ICollection<AxleWeightReference> AxleWeightReferences { get; set; } = new List<AxleWeightReference>();
    public ICollection<WeighingAxle> WeighingAxles { get; set; } = new List<WeighingAxle>();
}
