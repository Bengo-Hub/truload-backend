using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Common;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Models.Infrastructure;

namespace TruLoad.Backend.Models.Weighing;

/// <summary>
/// Tracks historical tare weight measurements for a vehicle.
/// Used for tare weight drift detection, anomaly alerts, and audit compliance.
/// </summary>
[Table("vehicle_tare_history")]
public class VehicleTareHistory : BaseEntity
{
    /// <summary>
    /// Vehicle this tare measurement belongs to.
    /// </summary>
    [Required]
    [Column("vehicle_id")]
    public Guid VehicleId { get; set; }

    /// <summary>
    /// Tare weight in kilograms.
    /// </summary>
    [Column("tare_weight_kg")]
    public int TareWeightKg { get; set; }

    /// <summary>
    /// When the tare weight was measured.
    /// </summary>
    [Column("weighed_at")]
    public DateTime WeighedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Station where tare was measured (optional).
    /// </summary>
    [Column("station_id")]
    public Guid? StationId { get; set; }

    /// <summary>
    /// Organization that performed the measurement.
    /// </summary>
    [Column("organization_id")]
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Source of the tare weight: "measured" (from scale), "manual" (operator input).
    /// </summary>
    [Required]
    [MaxLength(20)]
    [Column("source")]
    public string Source { get; set; } = "measured";

    /// <summary>
    /// Optional notes about this measurement (e.g., "Post-maintenance tare update").
    /// </summary>
    [MaxLength(500)]
    [Column("notes")]
    public string? Notes { get; set; }

    /// <summary>
    /// User who recorded this tare measurement (nullable — some historical rows and
    /// system-generated entries have no operator attached).
    /// </summary>
    [Column("recorded_by_user_id")]
    public Guid? RecordedByUserId { get; set; }

    /// <summary>
    /// Denormalized display name of the user who recorded this measurement, captured at
    /// write time so history entries remain readable without a join even if the user
    /// is later deactivated/renamed.
    /// </summary>
    [MaxLength(255)]
    [Column("recorded_by_name")]
    public string? RecordedByName { get; set; }

    // ── Tare Anomaly Detection (Phase 7 MVP) ──
    // Anchor for drift-anomaly flags raised via RecordTareHistoryEntryAsync (the standalone "Tare
    // Register > Record Tare" dialog), which - unlike CaptureSecondWeightAsync/UseStoredTareAsync -
    // isn't tied to any live WeighingTransaction. Same field shape as WeighingTransaction's
    // TareAnomaly* fields for consistency; see CommercialWeighingService for the shared detection
    // logic and the report for why this entity was chosen as the fallback anchor.

    /// <summary>Timestamp when a tare drift anomaly was flagged for this history entry. Null when none.</summary>
    [Column("tare_anomaly_flagged_at")]
    public DateTime? TareAnomalyFlaggedAt { get; set; }

    /// <summary>Human-readable reason the anomaly was flagged.</summary>
    [MaxLength(500)]
    [Column("tare_anomaly_reason")]
    public string? TareAnomalyReason { get; set; }

    /// <summary>User ID of the supervisor who resolved the flagged anomaly.</summary>
    [Column("tare_anomaly_resolved_by_user_id")]
    public Guid? TareAnomalyResolvedByUserId { get; set; }

    /// <summary>Timestamp when the anomaly was resolved. Null while still pending review.</summary>
    [Column("tare_anomaly_resolved_at")]
    public DateTime? TareAnomalyResolvedAt { get; set; }

    /// <summary>Resolution outcome text (see WeighingTransaction.TareAnomalyResolution for format).</summary>
    [MaxLength(1000)]
    [Column("tare_anomaly_resolution")]
    public string? TareAnomalyResolution { get; set; }

    // Navigation properties
    [ForeignKey("VehicleId")]
    public virtual Vehicle? Vehicle { get; set; }

    [ForeignKey("StationId")]
    public virtual Station? Station { get; set; }

    [ForeignKey("OrganizationId")]
    public virtual Organization? Organization { get; set; }

    [ForeignKey("RecordedByUserId")]
    public virtual ApplicationUser? RecordedByUser { get; set; }
}
