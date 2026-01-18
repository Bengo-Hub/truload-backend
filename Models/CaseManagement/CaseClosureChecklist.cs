using System.ComponentModel.DataAnnotations;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Closure requirement tracking by disposition type (validates Subfiles A-J completeness).
/// </summary>
public class CaseClosureChecklist : BaseEntity
{

    /// <summary>
    /// Case register reference (unique, required)
    /// </summary>
    [Required]
    public Guid CaseRegisterId { get; set; }

    /// <summary>
    /// Closure type FK (withdrawn, discharged, paid, jailed)
    /// </summary>
    public Guid? ClosureTypeId { get; set; }

    /// <summary>
    /// Legal section reference (consolidated CPC/PC sections)
    /// </summary>
    public Guid? LegalSectionId { get; set; }

    /// <summary>
    /// Subfile A complete (Case details)
    /// </summary>
    public bool SubfileAComplete { get; set; } = false;

    /// <summary>
    /// Subfile B complete (Document Evidence)
    /// </summary>
    public bool SubfileBComplete { get; set; } = false;

    /// <summary>
    /// Subfile C complete (Expert Reports)
    /// </summary>
    public bool SubfileCComplete { get; set; } = false;

    /// <summary>
    /// Subfile D complete (Witness Statements)
    /// </summary>
    public bool SubfileDComplete { get; set; } = false;

    /// <summary>
    /// Subfile E complete (Accused Statements)
    /// </summary>
    public bool SubfileEComplete { get; set; } = false;

    /// <summary>
    /// Subfile F complete (Investigation Diary)
    /// </summary>
    public bool SubfileFComplete { get; set; } = false;

    /// <summary>
    /// Subfile G complete (Charge Sheets)
    /// </summary>
    public bool SubfileGComplete { get; set; } = false;

    /// <summary>
    /// Subfile H complete (Accused Records)
    /// </summary>
    public bool SubfileHComplete { get; set; } = false;

    /// <summary>
    /// Subfile I complete (Covering Report)
    /// </summary>
    public bool SubfileIComplete { get; set; } = false;

    /// <summary>
    /// Subfile J complete (Minute Sheets)
    /// </summary>
    public bool SubfileJComplete { get; set; } = false;

    /// <summary>
    /// All subfiles verified
    /// </summary>
    public bool AllSubfilesVerified { get; set; } = false;

    /// <summary>
    /// Review status: none, requested, approved, rejected
    /// </summary>
    public Guid? ReviewStatusId { get; set; }

    /// <summary>
    /// Review request timestamp
    /// </summary>
    public DateTime? ReviewRequestedAt { get; set; }

    /// <summary>
    /// Officer requesting review
    /// </summary>
    public Guid? ReviewRequestedById { get; set; }

    /// <summary>
    /// Review notes from reviewer
    /// </summary>
    public string? ReviewNotes { get; set; }

    /// <summary>
    /// Approving officer (Supervisor)
    /// </summary>
    public Guid? ApprovedById { get; set; }

    /// <summary>
    /// Approval timestamp
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Verified by user
    /// </summary>
    public Guid? VerifiedById { get; set; }

    /// <summary>
    /// Verification timestamp
    /// </summary>
    public DateTime? VerifiedAt { get; set; }

    // Navigation properties
    public virtual CaseRegister CaseRegister { get; set; } = null!;
    public virtual ClosureType? ClosureType { get; set; }
    public virtual LegalSection? LegalSection { get; set; }
    public virtual CaseReviewStatus? ReviewStatus { get; set; }
}
