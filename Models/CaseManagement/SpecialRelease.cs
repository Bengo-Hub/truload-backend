using System.ComponentModel.DataAnnotations;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Special release records for compliant/redistribution cases (fast-path dispositions).
/// </summary>
public class SpecialRelease : BaseEntity
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

    // Navigation properties
    public virtual CaseRegister CaseRegister { get; set; } = null!;
    public virtual ReleaseType ReleaseType { get; set; } = null!;
}
