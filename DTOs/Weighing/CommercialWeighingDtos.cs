using System.ComponentModel.DataAnnotations;

namespace TruLoad.Backend.DTOs.Weighing;

// ── Request DTOs ──

/// <summary>
/// Request to initiate a commercial weighing transaction.
/// </summary>
public class InitiateCommercialWeighingRequest
{
    [Required]
    public Guid StationId { get; set; }

    /// <summary>
    /// Vehicle ID (if known). Either VehicleId or VehicleRegNo must be provided.
    /// </summary>
    public Guid? VehicleId { get; set; }

    /// <summary>
    /// Vehicle registration number. Auto-creates vehicle if not found.
    /// </summary>
    [MaxLength(20)]
    public string? VehicleRegNo { get; set; }

    /// <summary>
    /// Cargo type ID.
    /// </summary>
    public Guid? CargoId { get; set; }

    /// <summary>
    /// Transporter ID.
    /// </summary>
    public Guid? TransporterId { get; set; }

    /// <summary>
    /// Driver ID.
    /// </summary>
    public Guid? DriverId { get; set; }

    /// <summary>
    /// Origin location ID.
    /// </summary>
    public Guid? OriginId { get; set; }

    /// <summary>
    /// Destination location ID.
    /// </summary>
    public Guid? DestinationId { get; set; }

    /// <summary>
    /// Consignment or delivery note reference number.
    /// </summary>
    [MaxLength(100)]
    public string? ConsignmentNo { get; set; }

    /// <summary>
    /// Purchase order, sales order, or dispatch order reference.
    /// </summary>
    [MaxLength(100)]
    public string? OrderReference { get; set; }

    /// <summary>
    /// Expected net weight from the order/dispatch (kg).
    /// </summary>
    public int? ExpectedNetWeightKg { get; set; }

    /// <summary>
    /// Container or trailer seal numbers (comma-separated).
    /// </summary>
    [MaxLength(200)]
    public string? SealNumbers { get; set; }

    /// <summary>
    /// Trailer registration number (for articulated vehicles).
    /// </summary>
    [MaxLength(20)]
    public string? TrailerRegNo { get; set; }

    /// <summary>
    /// Operator notes or observations.
    /// </summary>
    public string? Remarks { get; set; }

    /// <summary>
    /// Industry-specific metadata as JSON string.
    /// </summary>
    public string? IndustryMetadata { get; set; }
}

/// <summary>
/// Request to capture the first weight (first pass on the scale).
/// </summary>
public class CaptureFirstWeightRequest
{
    /// <summary>
    /// Measured weight in kg.
    /// </summary>
    [Required]
    [Range(1, 200000)]
    public int WeightKg { get; set; }

    /// <summary>
    /// Type of weight: "tare" or "gross".
    /// </summary>
    [Required]
    [MaxLength(10)]
    [RegularExpression("^(tare|gross)$", ErrorMessage = "WeightType must be 'tare' or 'gross'.")]
    public string WeightType { get; set; } = "gross";
}

/// <summary>
/// Request to capture the second weight (second pass on the scale).
/// </summary>
public class CaptureSecondWeightRequest
{
    /// <summary>
    /// Measured weight in kg. The system auto-determines if this is tare or gross
    /// based on the first weight type.
    /// </summary>
    [Required]
    [Range(1, 200000)]
    public int WeightKg { get; set; }
}

/// <summary>
/// Request to use a stored/preset tare weight instead of measuring.
/// </summary>
public class UseStoredTareRequest
{
    /// <summary>
    /// Optional override tare weight in kg. If null, the vehicle's stored tare is used.
    /// </summary>
    [Range(1, 100000)]
    public int? OverrideTareWeightKg { get; set; }
}

/// <summary>
/// Request to update quality deduction on a completed commercial weighing.
/// </summary>
public class UpdateQualityDeductionRequest
{
    /// <summary>
    /// Quality deduction in kg (e.g., moisture, foreign matter).
    /// </summary>
    [Required]
    [Range(0, 100000)]
    public int QualityDeductionKg { get; set; }

    /// <summary>
    /// Reason for the quality deduction.
    /// </summary>
    [MaxLength(500)]
    public string? Reason { get; set; }
}

// ── Response DTOs ──

/// <summary>
/// Full result DTO for a commercial weighing transaction.
/// </summary>
public class CommercialWeighingResultDto
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string ControlStatus { get; set; } = string.Empty;
    public string WeighingMode { get; set; } = "commercial";

    // Vehicle info
    public Guid VehicleId { get; set; }
    public string VehicleRegNumber { get; set; } = string.Empty;
    public string? VehicleMake { get; set; }
    public string? VehicleModel { get; set; }
    public string? TrailerRegNo { get; set; }

    // People
    public Guid? DriverId { get; set; }
    public string? DriverName { get; set; }
    public Guid? TransporterId { get; set; }
    public string? TransporterName { get; set; }
    public string? WeighedByUserName { get; set; }

    // Station
    public Guid StationId { get; set; }
    public string? StationName { get; set; }

    // Weight fields
    public int? FirstWeightKg { get; set; }
    public string? FirstWeightType { get; set; }
    public DateTime? FirstWeightAt { get; set; }

    public int? SecondWeightKg { get; set; }
    public string? SecondWeightType { get; set; }
    public DateTime? SecondWeightAt { get; set; }

    public int? TareWeightKg { get; set; }
    public int? GrossWeightKg { get; set; }
    public int? NetWeightKg { get; set; }
    public string? TareSource { get; set; }

    // Quality and adjustments
    public int? QualityDeductionKg { get; set; }
    public int? AdjustedNetWeightKg { get; set; }

    // Order/consignment
    public string? ConsignmentNo { get; set; }
    public string? OrderReference { get; set; }
    public int? ExpectedNetWeightKg { get; set; }
    public int? WeightDiscrepancyKg { get; set; }
    public string? SealNumbers { get; set; }
    public string? Remarks { get; set; }

    // Route & Cargo
    public Guid? OriginId { get; set; }
    public string? SourceLocation { get; set; }
    public Guid? DestinationId { get; set; }
    public string? DestinationLocation { get; set; }
    public Guid? CargoId { get; set; }
    public string? CargoType { get; set; }

    // Tolerance
    public bool ToleranceExceeded { get; set; }
    public string? ToleranceDisplay { get; set; }

    // Metadata
    public string? IndustryMetadata { get; set; }
    public DateTime WeighedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// CRUD DTO for commercial tolerance settings.
/// </summary>
public class CommercialToleranceSettingDto
{
    public Guid? Id { get; set; }

    /// <summary>
    /// Type of tolerance: "percentage" or "absolute".
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string ToleranceType { get; set; } = "percentage";

    /// <summary>
    /// Tolerance value. If percentage, e.g. 0.5 means 0.5%. If absolute, value in kg.
    /// </summary>
    [Required]
    public decimal ToleranceValue { get; set; }

    /// <summary>
    /// Maximum tolerance cap in kg (applies when using percentage). Null means no cap.
    /// </summary>
    public int? MaxToleranceKg { get; set; }

    /// <summary>
    /// Optional: scope to a specific cargo type.
    /// </summary>
    public Guid? CargoTypeId { get; set; }
    public string? CargoTypeName { get; set; }

    /// <summary>
    /// Description or label for this tolerance rule.
    /// </summary>
    [MaxLength(200)]
    public string? Description { get; set; }
}

/// <summary>
/// Vehicle tare weight history entry.
/// </summary>
public class VehicleTareHistoryDto
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public string? VehicleRegNo { get; set; }
    public int TareWeightKg { get; set; }
    public DateTime WeighedAt { get; set; }
    public Guid? StationId { get; set; }
    public string? StationName { get; set; }
    public string Source { get; set; } = "measured";
    public string? Notes { get; set; }
}
