using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models;

/// <summary>
/// Individual axle measurement within a weighing transaction.
/// Records actual weights, configurations, and fees for each axle during a weighing event.
/// Maps to weighing_axles table in database.
/// </summary>
public class WeighingAxle : BaseEntity
{
    
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
    /// Axle type classification for regulatory compliance
    /// Values: Steering, SingleDrive, Tandem, Tridem, Tag
    /// CRITICAL: Different weight limits per EAC Act 2016 (Steering: 7k, Drive: 10k, Tandem: 16k, Tridem: 24k)
    /// Kenya Traffic Act Cap 403 Schedule 2 requires this for fee calculation
    /// </summary>
    public string AxleType { get; set; } = string.Empty;

    /// <summary>
    /// Distance to next axle in meters (NULL for last axle)
    /// REGULATORY: Kenya Traffic Act Cap 403 Schedule 2 requires specific spacing:
    /// - Tandem axles: 1.2m-1.8m apart for 16-tonne grouping
    /// - Tridem axles: spacing requirements for 24-tonne grouping
    /// </summary>
    public decimal? AxleSpacingMeters { get; set; }

    /// <summary>
    /// Pavement Damage Factor (Fourth Power Law calculation)
    /// Formula: (ActualWeight / PermissibleWeight) ^ 4
    /// REGULATORY: EAC Vehicle Load Control Act 2016 Section 15
    /// Infrastructure damage surcharge based on damage severity
    /// Values > 1.0 indicate damage to road infrastructure
    /// </summary>
    public decimal PavementDamageFactor { get; set; } = 0.0000m;

    /// <summary>
    /// Cached total weight of all axles in the same axle_grouping (A/B/C/D)
    /// Performance optimization for group compliance checks
    /// REGULATORY: Kenya Traffic Act requires 5% tolerance on GROUP weight, not individual axles
    /// </summary>
    public int? GroupAggregateWeightKg { get; set; }

    /// <summary>
    /// Cached permissible weight for the axle group
    /// Performance optimization for tolerance calculations
    /// REGULATORY: Used to apply 5% tolerance per group (Traffic Act Cap 403)
    /// </summary>
    public int? GroupPermissibleWeightKg { get; set; }

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
    public WeighingTransaction? WeighingTransaction { get; set; }
    public AxleConfiguration? AxleConfiguration { get; set; }
    public AxleWeightReference? AxleWeightReference { get; set; }
    public AxleGroup? AxleGroup { get; set; }
    public TyreType? TyreType { get; set; }
}
