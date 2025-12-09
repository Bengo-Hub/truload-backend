namespace TruLoad.Backend.Models;

/// <summary>
/// Individual axle weight specification within an axle configuration.
/// Defines legal weights and axle group classifications for each axle position.
/// Maps to axleweightxreff table in legacy system.
/// </summary>
public class AxleWeightReference
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Parent axle configuration ID (foreign key)
    /// </summary>
    public Guid AxleConfigurationId { get; set; }
    
    /// <summary>
    /// Axle position/sequence number within the configuration (1, 2, 3, ...)
    /// Identifies which physical axle this specification applies to
    /// </summary>
    public int AxlePosition { get; set; }
    
    /// <summary>
    /// Legal/permissible weight for this specific axle in kg
    /// Range: typically 4750-10000 kg depending on axle group and tyre type
    /// </summary>
    public int AxleLegalWeightKg { get; set; }
    
    /// <summary>
    /// Axle group classification (foreign key to axle_groups table)
    /// Examples: "S1" (4750kg), "SA4" (10000kg), "SA6" (6000kg), "TAG8" (9000kg),
    /// "TAG8B" (7000kg), "TAG12" (8000kg), "QAG16" (8000kg), "WWW" (7500kg), "SSS" (6000kg), "S4" (6000kg)
    /// </summary>
    public Guid AxleGroupId { get; set; }
    
    /// <summary>
    /// Axle grouping/cluster identifier (A, B, C, D)
    /// A = Front axle, B = Trailer coupling axle, C = Mid-trailer axle, D = Rear trailer axle
    /// Used for combined weight calculations and positioning
    /// </summary>
    public string AxleGrouping { get; set; } = string.Empty;
    
    /// <summary>
    /// Tyre type for this axle (foreign key to tyre_types table)
    /// S = Single tyre (7500kg), D = Dual/Twin tyres (10000kg), W = Wide single (8000kg)
    /// NULL if not specified
    /// </summary>
    public Guid? TyreTypeId { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public AxleConfiguration AxleConfiguration { get; set; } = null!;
    public AxleGroup? AxleGroup { get; set; }
    public TyreType? TyreType { get; set; }
}
