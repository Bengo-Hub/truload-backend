using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Common;
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

    // Navigation properties
    [ForeignKey("VehicleId")]
    public virtual Vehicle? Vehicle { get; set; }

    [ForeignKey("StationId")]
    public virtual Station? Station { get; set; }

    [ForeignKey("OrganizationId")]
    public virtual Organization? Organization { get; set; }
}
