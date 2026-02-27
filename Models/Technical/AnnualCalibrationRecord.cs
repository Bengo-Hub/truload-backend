using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.Technical;

/// <summary>
/// Represents the statutory annual calibration record for a weighbridge station (e.g. Cap 513 Requirements).
/// This defines the baseline expected weight and permissible deviation which daily operational scale tests use to judge pass/fail.
/// </summary>
[Table("annual_calibration_records")]
public class AnnualCalibrationRecord : TenantAwareEntity
{
    [Required]
    [Column("station_id")]
    public new Guid StationId { get; set; }

    [Required]
    [StringLength(100)]
    [Column("certificate_no")]
    public string CertificateNo { get; set; } = null!;

    [Required]
    [Column("issue_date")]
    public DateTime IssueDate { get; set; }

    [Required]
    [Column("expiry_date")]
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// Target baseline weight metric established during the annual statutory calibration.
    /// Used by daily operational tests as the 'expected' benchmark.
    /// </summary>
    [Required]
    [Column("target_weight_kg")]
    public int TargetWeightKg { get; set; }

    /// <summary>
    /// Maximum permissible deviation (Kg) from the TargetWeightKg established during statutory checkout.
    /// Operational scale tests failing outside this parameter must fail.
    /// </summary>
    [Required]
    [Column("max_deviation_kg")]
    public int MaxDeviationKg { get; set; }

    /// <summary>
    /// Storage pointer to the uploaded/certified calibration document PDF.
    /// </summary>
    [StringLength(500)]
    [Column("certificate_file_url")]
    public string? CertificateFileUrl { get; set; }

    /// <summary>
    /// Lifecycle status: active, expired, revoked
    /// </summary>
    [Required]
    [StringLength(50)]
    [Column("status")]
    public string Status { get; set; } = "active";

    // Navigation property
    [ForeignKey("StationId")]
    public new virtual Station? Station { get; set; }
}
