using System.ComponentModel.DataAnnotations;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Special release records for compliant/redistribution cases (fast-path dispositions).
/// </summary>
public class SpecialRelease : TenantAwareEntity
{

    /// <summary>
    /// Case reference (required, unique per release)
    /// </summary>
    [Required]
    public Guid CaseRegisterId { get; set; }

    /// <summary>
    /// Certificate number (unique)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string CertificateNo { get; set; } = string.Empty;

    /// <summary>
    /// Release type FK (redistribution, tolerance, permit_valid, admin_discretion)
    /// </summary>
    [Required]
    public Guid ReleaseTypeId { get; set; }

    /// <summary>
    /// Original overload amount in kg
    /// </summary>
    public int? OverloadKg { get; set; }

    /// <summary>
    /// Whether redistribution allowed
    /// </summary>
    public bool RedistributionAllowed { get; set; } = false;

    /// <summary>
    /// Whether reweigh required
    /// </summary>
    public bool ReweighRequired { get; set; } = false;

    /// <summary>
    /// Reweigh reference (nullable)
    /// </summary>
    public Guid? ReweighWeighingId { get; set; }

    /// <summary>
    /// Whether reweigh compliant
    /// </summary>
    public bool ComplianceAchieved { get; set; } = false;

    /// <summary>
    /// Release reason/justification (required)
    /// </summary>
    [Required]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Authorizing supervisor (required)
    /// </summary>
    [Required]
    public Guid AuthorizedById { get; set; }

    /// <summary>
    /// Issue timestamp
    /// </summary>
    [Required]
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether release is approved
    /// </summary>
    public bool IsApproved { get; set; } = false;

    /// <summary>
    /// User who approved the release
    /// </summary>
    public Guid? ApprovedById { get; set; }

    /// <summary>
    /// Approval timestamp
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Whether release was rejected
    /// </summary>
    public bool IsRejected { get; set; } = false;

    /// <summary>
    /// User who rejected the release
    /// </summary>
    public Guid? RejectedById { get; set; }

    /// <summary>
    /// Rejection timestamp
    /// </summary>
    public DateTime? RejectedAt { get; set; }

    /// <summary>
    /// Reason for rejection
    /// </summary>
    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    /// <summary>
    /// Load correction memo reference
    /// </summary>
    public Guid? LoadCorrectionMemoId { get; set; }

    /// <summary>
    /// Compliance certificate reference
    /// </summary>
    public Guid? ComplianceCertificateId { get; set; }

    /// <summary>
    /// User who created/requested the release
    /// </summary>
    public Guid CreatedById { get; set; }

    // Navigation properties
    public virtual CaseRegister CaseRegister { get; set; } = null!;
    public virtual ReleaseType ReleaseType { get; set; } = null!;
}
