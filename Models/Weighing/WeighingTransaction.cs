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
    [MaxLength(20)]
    public string CaptureStatus { get; set; } = "captured";

    /// <summary>
    /// Auto-weigh GVW before final capture (for comparison)
    /// </summary>
    public int? AutoweighGvwKg { get; set; }

    /// <summary>
    /// Timestamp when auto-weigh captured the initial data
    /// </summary>
    public DateTime? AutoweighAt { get; set; }

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
