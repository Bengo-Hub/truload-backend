namespace TruLoad.Backend.Models;

/// <summary>
/// Individual axle measurement within a weighing transaction.
/// Records actual weights, configurations, and fees for each axle during a weighing event.
/// Maps to weighing_axles table in database.
/// </summary>
public class WeighingAxle
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Parent weighing transaction ID (foreign key)
    /// </summary>
    public Guid WeighingId { get; set; }
    
    /// <summary>
    /// Axle position/sequence number (1, 2, 3, ...)
    /// Identifies which physical axle was measured in order
    /// </summary>
    public int AxleNumber { get; set; }
    
    /// <summary>
    /// Measured weight for this axle in kg
    /// Actual reading from scale platform
    /// </summary>
    public int MeasuredWeightKg { get; set; }
    
    /// <summary>
    /// Permissible/legal weight for this axle in kg
    /// From axle_weight_references based on configuration
    /// </summary>
    public int PermissibleWeightKg { get; set; }
    
    /// <summary>
    /// Overload amount in kg (generated/calculated)
    /// measured_weight - permissible_weight
    /// Negative values indicate underload
    /// </summary>
    public int OverloadKg => MeasuredWeightKg - PermissibleWeightKg;
    
    /// <summary>
    /// Axle configuration used for this weighing (foreign key)
    /// Links to the template configuration to understand expected weight distribution
    /// </summary>
    public Guid AxleConfigurationId { get; set; }
    
    /// <summary>
    /// Axle weight reference specification (foreign key)
    /// Specific position within the configuration with expected limits
    /// </summary>
    public Guid? AxleWeightReferenceId { get; set; }
    
    /// <summary>
    /// Axle group classification (foreign key)
    /// S1, SA4, TAG8, etc. - for categorization and fee lookup
    /// </summary>
    public Guid AxleGroupId { get; set; }
    
    /// <summary>
    /// Axle grouping/cluster (A=Front, B=Trailer coupling, C=Mid-trailer, D=Rear trailer)
    /// Identifies axle position within vehicle structure
    /// </summary>
    public string AxleGrouping { get; set; } = string.Empty;
    
    /// <summary>
    /// Tyre type used on this axle (foreign key)
    /// S (Single), D (Dual), W (Wide) - affects weight limits
    /// NULL if not determined
    /// </summary>
    public Guid? TyreTypeId { get; set; }
    
    /// <summary>
    /// Fee calculated for this axle in USD
    /// Based on overload and applicable fee schedule
    /// </summary>
    public decimal FeeUsd { get; set; } = 0m;
    
    /// <summary>
    /// Timestamp when this axle was captured/measured
    /// </summary>
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    
    // Composite unique constraint: one entry per weighing per axle number
    // UNIQUE (weighing_id, axle_number)
    
    // Navigation properties
    // public Weighing? Weighing { get; set; } // Will be configured when Weighing model is created
    public AxleConfiguration? AxleConfiguration { get; set; }
    public AxleWeightReference? AxleWeightReference { get; set; }
    public AxleGroup? AxleGroup { get; set; }
    public TyreType? TyreType { get; set; }
}
