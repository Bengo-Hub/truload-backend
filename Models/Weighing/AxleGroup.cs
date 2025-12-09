namespace TruLoad.Backend.Models;

/// <summary>
/// Master reference for axle group classifications with spacing rules.
/// 
/// Axle groups define the category of an axle based on its position and configuration.
/// Each group has specific spacing rules and weight limits affecting vehicle compliance.
/// </summary>
public class AxleGroup
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Axle group code (e.g., "S1", "SA4", "SA6", "TAG8", "TAG8B", "TAG12", "QAG16", "WWW", "SSS", "S4")
    /// Unique identifier for axle group classification
    /// </summary>
    public string Code { get; set; } = string.Empty;
    
    /// <summary>
    /// Full descriptive name of the axle group
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Detailed description of axle group characteristics
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Typical permissible weight for this axle group in kg
    /// Range: 6000-10000 kg typical
    /// </summary>
    public int TypicalWeightKg { get; set; }
    
    /// <summary>
    /// Minimum axle spacing in feet (if applicable)
    /// NULL if spacing rules don't apply
    /// </summary>
    public decimal? MinSpacingFeet { get; set; }
    
    /// <summary>
    /// Maximum axle spacing in feet (if applicable)
    /// NULL if spacing rules don't apply
    /// </summary>
    public decimal? MaxSpacingFeet { get; set; }
    
    /// <summary>
    /// Number of axles within this group configuration
    /// Typically 1, but can be 2+ for multi-axle groups
    /// </summary>
    public int AxleCountInGroup { get; set; } = 1;
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ICollection<AxleWeightReference> AxleWeightReferences { get; set; } = new List<AxleWeightReference>();
    public ICollection<WeighingAxle> WeighingAxles { get; set; } = new List<WeighingAxle>();
}
