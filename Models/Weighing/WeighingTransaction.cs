using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Models.Common;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Models.Infrastructure;
using TruLoad.Backend.Models;

namespace TruLoad.Backend.Models.Weighing;

/// <summary>
/// Represents a central weighing transaction.
/// Records the entire lifecycle of a vehicle weighing event.
/// </summary>
[Table("weighing_transactions")]
public class WeighingTransaction : TenantAwareEntity
{

    /// <summary>
    /// Unique ticket number generated for this transaction.
    /// User-friendly identifier.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string TicketNumber { get; set; } = string.Empty;

    /// <summary>
    /// Foreign Key to the Vehicle being weighed.
    /// </summary>
    public Guid VehicleId { get; set; }
    
    /// <summary>
    /// Snapshot of vehicle registration number at time of weighing.
    /// </summary>
    [MaxLength(20)]
    public string VehicleRegNumber { get; set; } = string.Empty;

    /// <summary>
    /// Foreign Key to the Driver.
    /// </summary>
    public Guid? DriverId { get; set; }
    
    /// <summary>
    /// Foreign Key to the Transporter.
    /// </summary>
    public Guid? TransporterId { get; set; }


    /// <summary>
    /// Foreign Key to the User (Officer) who performed the weighing.
    /// </summary>
    public Guid WeighedByUserId { get; set; }

    /// <summary>
    /// Weighing type: static, wim (weigh-in-motion), axle
    /// </summary>
    [MaxLength(20)]
    public string WeighingType { get; set; } = "static";

    /// <summary>
    /// Applicable Act (EAC or Traffic Act)
    /// </summary>
    public Guid? ActId { get; set; }

    /// <summary>
    /// Direction: A or B (for bidirectional stations)
    /// </summary>
    [MaxLength(10)]
    public string? Bound { get; set; }

    /// <summary>
    /// Total Gross Vehicle Weight Measured (Sum of axle weights).
    /// </summary>
    public int GvwMeasuredKg { get; set; }

    /// <summary>
    /// Total Gross Vehicle Weight Permissible (Based on Axle Config + Tolerance).
    /// </summary>
    public int GvwPermissibleKg { get; set; }

    /// <summary>
    /// Calculated Overload Amount (Measured - Permissible).
    /// Negative means compliant.
    /// </summary>
    public int OverloadKg { get; set; }

    /// <summary>
    /// Current status of the transaction (e.g., "Compliant", "Overload", "Charged").
    /// </summary>
    [MaxLength(50)]
    public string ControlStatus { get; set; } = "Pending";

    /// <summary>
    /// Total fees calculated for this transaction (USD).
    /// For EAC Act: this is the primary fee. For Traffic Act: this is the reference conversion.
    /// </summary>
    public decimal TotalFeeUsd { get; set; }

    /// <summary>
    /// Total fees calculated for this transaction (KES).
    /// For Traffic Act: this is the primary fee (native KES, no conversion).
    /// For EAC Act: this is the reference conversion from USD.
    /// </summary>
    public decimal TotalFeeKes { get; set; }

    /// <summary>
    /// Timestamp when reading was captured.
    /// </summary>
    public DateTime WeighedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Indicates if this transaction is synced from an offline device.
    /// </summary>
    public bool IsSync { get; set; }

    /// <summary>
    /// True if the vehicle is compliant with weight limits.
    /// </summary>
    public bool IsCompliant { get; set; }

    /// <summary>
    /// True if the vehicle was prohibited and sent to the holding yard.
    /// </summary>
    public bool IsSentToYard { get; set; }

    /// <summary>
    /// Detailed reason for non-compliance.
    /// </summary>
    public string ViolationReason { get; set; } = string.Empty;

    /// <summary>
    /// Vector embedding for violation reason (semantic search)
    /// 384 dimensions for all-MiniLM-L12-v2 model
    /// NotMapped by default - explicitly configured for PostgreSQL only.
    /// </summary>
    [NotMapped]
    public Pgvector.Vector? ViolationReasonEmbedding { get; set; }

    /// <summary>
    /// Current reweigh cycle (0 = original weigh, 1+ = reweigh).
    /// </summary>
    public int ReweighCycleNo { get; set; } = 0;

    /// <summary>
    /// Reference to the parent transaction if this is a reweigh.
    /// </summary>
    public Guid? OriginalWeighingId { get; set; }

    /// <summary>
    /// Indicates if a valid permit was applied to this weighing.
    /// </summary>
    public bool HasPermit { get; set; }

    /// <summary>
    /// Origin location (foreign key)
    /// </summary>
    public Guid? OriginId { get; set; }

    /// <summary>
    /// Destination location (foreign key)
    /// </summary>
    public Guid? DestinationId { get; set; }

    /// <summary>
    /// Cargo type (foreign key)
    /// </summary>
    public Guid? CargoId { get; set; }

    /// <summary>
    /// Road/location where weighing took place (e.g. A109, Langata Road).
    /// </summary>
    public Guid? RoadId { get; set; }
    
    /// <summary>
    /// Subcounty where weighing took place (foreign key).
    /// </summary>
    public Guid? SubcountyId { get; set; }

    /// <summary>
    /// Town or city at weighing location (from geolocation or manual).
    /// </summary>
    [MaxLength(100)]
    public string? LocationTown { get; set; }

    /// <summary>
    /// County at weighing location.
    /// </summary>
    [MaxLength(100)]
    public string? LocationCounty { get; set; }
    
    /// <summary>
    /// Subcounty at weighing location.
    /// </summary>
    [MaxLength(100)]
    public string? LocationSubcounty { get; set; }

    /// <summary>
    /// Latitude at weighing location (from geolocation).
    /// </summary>
    public decimal? LocationLat { get; set; }

    /// <summary>
    /// Longitude at weighing location (from geolocation).
    /// </summary>
    public decimal? LocationLng { get; set; }

    /// <summary>
    /// Foreign Key to the Scale Test performed before this weighing session.
    /// Required per regulations - scale must be tested daily per station/bound before weighing.
    /// </summary>
    public Guid? ScaleTestId { get; set; }

    /// <summary>
    /// Whether tolerance was applied in compliance calculation
    /// </summary>
    public bool ToleranceApplied { get; set; } = false;
    
    /// <summary>
    /// Calculated GVW tolerance in Kg.
    /// </summary>
    public int GvwToleranceKg { get; set; }

    /// <summary>
    /// Formatted GVW tolerance for display (e.g. "5%", "2,000 kg").
    /// </summary>
    [MaxLength(50)]
    public string? GvwToleranceDisplay { get; set; }

    /// <summary>
    /// Formatted Axle tolerance for display.
    /// </summary>
    [MaxLength(50)]
    public string? AxleToleranceDisplay { get; set; }

    /// <summary>
    /// Operational allowance (additive) used in this transaction.
    /// </summary>
    public int OperationalAllowanceUsed { get; set; }

    /// <summary>
    /// Maximum allowed reweigh cycles (default 8 per regulations)
    /// </summary>
    public int ReweighLimit { get; set; } = 8;

    /// <summary>
    /// Client-generated UUID for offline idempotency
    /// </summary>
    public Guid? ClientLocalId { get; set; }

    /// <summary>
    /// Sync status: queued, synced, failed (for offline support)
    /// </summary>
    [MaxLength(20)]
    public string SyncStatus { get; set; } = "synced";

    /// <summary>
    /// Last sync timestamp
    /// </summary>
    public DateTime? SyncAt { get; set; }

    /// <summary>
    /// Capture source: auto (from middleware auto-weigh), manual (operator captured), frontend (from TruLoad app)
    /// </summary>
    [MaxLength(20)]
    public string CaptureSource { get; set; } = "manual";

    /// <summary>
    /// Capture status: auto (auto-weigh data only), captured (final weights submitted), not_weighed (vehicle left without capture)
    /// </summary>
    [MaxLength(50)]
    public string CaptureStatus { get; set; } = "captured";

    /// <summary>
    /// Auto-weigh GVW before final capture (for comparison)
    /// </summary>
    public int? AutoweighGvwKg { get; set; }

    /// <summary>
    /// Timestamp when auto-weigh captured the initial data
    /// </summary>
    public DateTime? AutoweighAt { get; set; }

    // ── Commercial Weighing Fields ──

    /// <summary>
    /// Weighing mode: "enforcement" (axle-load) or "commercial" (factory/industry).
    /// Derived from Organization.TenantType at creation time.
    /// </summary>
    [MaxLength(20)]
    public string WeighingMode { get; set; } = "enforcement";

    /// <summary>
    /// First pass weight in kg (commercial two-pass weighing).
    /// </summary>
    public int? FirstWeightKg { get; set; }

    /// <summary>
    /// Type of first weight: "tare" or "gross".
    /// </summary>
    [MaxLength(10)]
    public string? FirstWeightType { get; set; }

    /// <summary>
    /// Timestamp of first weight capture.
    /// </summary>
    public DateTime? FirstWeightAt { get; set; }

    /// <summary>
    /// Second pass weight in kg (commercial two-pass weighing).
    /// </summary>
    public int? SecondWeightKg { get; set; }

    /// <summary>
    /// Type of second weight: "tare" or "gross".
    /// </summary>
    [MaxLength(10)]
    public string? SecondWeightType { get; set; }

    /// <summary>
    /// Timestamp of second weight capture.
    /// </summary>
    public DateTime? SecondWeightAt { get; set; }

    /// <summary>
    /// Resolved tare weight (from measurement, preset, or stored).
    /// </summary>
    public int? TareWeightKg { get; set; }

    /// <summary>
    /// Resolved gross weight.
    /// </summary>
    public int? GrossWeightKg { get; set; }

    /// <summary>
    /// Net weight = gross - tare (cargo weight).
    /// </summary>
    public int? NetWeightKg { get; set; }

    /// <summary>
    /// Source of the tare weight: "measured", "preset", "stored".
    /// </summary>
    [MaxLength(20)]
    public string? TareSource { get; set; }

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
    /// Expected net weight (from order/dispatch).
    /// Used for discrepancy detection.
    /// </summary>
    public int? ExpectedNetWeightKg { get; set; }

    /// <summary>
    /// Difference between actual and expected net weight.
    /// Positive = over expected, negative = under expected.
    /// </summary>
    public int? WeightDiscrepancyKg { get; set; }

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
    /// Operator notes or observations about this weighing.
    /// </summary>
    public string? Remarks { get; set; }

    /// <summary>
    /// Quality deduction in kg (e.g., moisture, foreign matter for agri commodities).
    /// </summary>
    public int? QualityDeductionKg { get; set; }

    /// <summary>
    /// Net weight after quality deductions: NetWeightKg - QualityDeductionKg.
    /// </summary>
    public int? AdjustedNetWeightKg { get; set; }

    /// <summary>
    /// Industry-specific metadata stored as JSON.
    /// E.g., mining: pit source, material grade. Agriculture: moisture %, grading.
    /// Logistics: container number, bill of lading. Waste: manifest, waste stream.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? IndustryMetadata { get; set; }

    /// <summary>
    /// Whether a supervisor has approved a tolerance exception for this transaction.
    /// Set when net weight discrepancy exceeds configured tolerance bands.
    /// </summary>
    public bool ToleranceExceptionApproved { get; set; } = false;

    /// <summary>
    /// User ID of the supervisor who approved the tolerance exception.
    /// </summary>
    public Guid? ToleranceExceptionApprovedBy { get; set; }

    /// <summary>
    /// Timestamp when the tolerance exception was approved.
    /// </summary>
    public DateTime? ToleranceExceptionApprovedAt { get; set; }

    // Navigation Properties
    public Vehicle? Vehicle { get; set; }
    public Driver? Driver { get; set; }
    public Transporter? Transporter { get; set; }
    public ApplicationUser? WeighedByUser { get; set; }
    public WeighingTransaction? OriginalWeighing { get; set; }
    public ActDefinition? Act { get; set; }
    public OriginsDestinations? Origin { get; set; }
    public OriginsDestinations? Destination { get; set; }
    public CargoTypes? Cargo { get; set; }
    public Roads? Road { get; set; }
    public Subcounty? Subcounty { get; set; }
    public ScaleTest? ScaleTest { get; set; }

    // One-to-Many relationship with Axle Weights
    public ICollection<WeighingAxle> WeighingAxles { get; set; } = new List<WeighingAxle>();
}
