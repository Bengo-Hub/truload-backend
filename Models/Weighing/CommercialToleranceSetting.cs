using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.Weighing;

/// <summary>
/// Commercial weighing tolerance configuration per organization.
/// Defines acceptable weight variance thresholds for commercial/factory weighing.
/// Optionally scoped to specific cargo types.
/// </summary>
[Table("commercial_tolerance_settings")]
public class CommercialToleranceSetting : TenantAwareEntity
{
    /// <summary>
    /// Type of tolerance: "percentage" or "absolute" (kg).
    /// </summary>
    [Required]
    [MaxLength(20)]
    [Column("tolerance_type")]
    public string ToleranceType { get; set; } = "percentage";

    /// <summary>
    /// Tolerance value. If percentage, e.g. 0.5 means 0.5%. If absolute, value in kg.
    /// </summary>
    [Column("tolerance_value")]
    public decimal ToleranceValue { get; set; }

    /// <summary>
    /// Maximum tolerance cap in kg (applies when using percentage).
    /// Null means no cap.
    /// </summary>
    [Column("max_tolerance_kg")]
    public int? MaxToleranceKg { get; set; }

    /// <summary>
    /// Optional: scope this tolerance to a specific cargo type.
    /// Null means it applies to all cargo types for this org.
    /// </summary>
    [Column("cargo_type_id")]
    public Guid? CargoTypeId { get; set; }

    /// <summary>
    /// Description or label for this tolerance rule.
    /// </summary>
    [MaxLength(200)]
    [Column("description")]
    public string? Description { get; set; }

    // Navigation properties
    [ForeignKey("CargoTypeId")]
    public virtual CargoTypes? CargoType { get; set; }
}
