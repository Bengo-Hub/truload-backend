using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.Common;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Central register for all violation cases (Subfile A).
/// Auto-created from weighing or manually created by officer.
/// </summary>
public class CaseRegister : TenantAwareEntity
{

    /// <summary>
    /// Unique case number (auto-generated or manual)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string CaseNo { get; set; } = string.Empty;

    /// <summary>
    /// Related weighing ID (nullable for manual entries)
    /// </summary>
    public Guid? WeighingId { get; set; }

    /// <summary>
    /// Related yard entry ID
    /// </summary>
    public Guid? YardEntryId { get; set; }

    /// <summary>
    /// Prohibition order reference
    /// </summary>
    public Guid? ProhibitionOrderId { get; set; }

    /// <summary>
    /// Vehicle ID (required)
    /// </summary>
    [Required]
    public Guid VehicleId { get; set; }

    /// <summary>
    /// Driver ID
    /// </summary>
    public Guid? DriverId { get; set; }

    /// <summary>
    /// Violation type ID (FK to taxonomy)
    /// </summary>
    [Required]
    public Guid ViolationTypeId { get; set; }

    /// <summary>
    /// Road where violation occurred
    /// </summary>
    public Guid? RoadId { get; set; }

    /// <summary>
    /// County
    /// </summary>
    public Guid? CountyId { get; set; }

    /// <summary>
    /// Subcounty
    /// </summary>
    public Guid? SubcountyId { get; set; }

    /// <summary>
    /// Detailed violation description
    /// </summary>
    public string? ViolationDetails { get; set; }

    /// <summary>
    /// Vector embedding for violation details (semantic search).
    /// 384 dimensions for all-MiniLM-L12-v2 model.
    /// NotMapped by default - explicitly configured for PostgreSQL only.
    /// </summary>
    [NotMapped]
    public Pgvector.Vector? ViolationDetailsEmbedding { get; set; }

    /// <summary>
    /// Applicable Act (EAC or Traffic)
    /// </summary>
    public Guid? ActId { get; set; }

    /// <summary>
    /// NTAC number served to Driver for this case. Case-specific; one driver can have many NTACs across cases (one-to-many).
    /// Mandatory only when escalating to case manager; optional for prosecution/court cases.
    /// </summary>
    [MaxLength(50)]
    public string? DriverNtacNo { get; set; }

    /// <summary>
    /// NTAC number served to Transporter/Owner for this case. Case-specific; one owner can have many NTACs across cases (one-to-many).
    /// Mandatory only when escalating to case manager; optional for prosecution/court cases.
    /// </summary>
    [MaxLength(50)]
    public string? TransporterNtacNo { get; set; }

    /// <summary>
    /// Occurrence Book number
    /// </summary>
    [MaxLength(50)]
    public string? ObNo { get; set; }

    /// <summary>
    /// Assigned court
    /// </summary>
    public Guid? CourtId { get; set; }

    /// <summary>
    /// Disposition type FK (special_release, paid, court, pending)
    /// </summary>
    public Guid? DispositionTypeId { get; set; }

    /// <summary>
    /// Status FK (open, pending, closed, escalated)
    /// </summary>
    [Required]
    public Guid CaseStatusId { get; set; }

    /// <summary>
    /// Whether escalated to formal case management
    /// </summary>
    public bool EscalatedToCaseManager { get; set; } = false;

    /// <summary>
    /// Assigned case manager (Prosecutor/Legal Liaison)
    /// </summary>
    public Guid? CaseManagerId { get; set; }

    /// <summary>
    /// Assigned prosecutor (if different from case manager)
    /// </summary>
    public Guid? ProsecutorId { get; set; }

    /// <summary>
    /// Complainant officer (Witness 1) – relevant for court/prosecution cases.
    /// </summary>
    public Guid? ComplainantOfficerId { get; set; }

    /// <summary>
    /// Station where the vehicle is detained (may differ from creating station).
    /// Used for court and prosecution cases.
    /// </summary>
    public Guid? DetentionStationId { get; set; }

    /// <summary>
    /// Investigating officer (Required ONLY for Court Escalation)
    /// </summary>
    public Guid? InvestigatingOfficerId { get; set; }

    /// <summary>
    /// Supervisor who assigned IO (Court cases only)
    /// </summary>
    public Guid? InvestigatingOfficerAssignedById { get; set; }

    /// <summary>
    /// Assignment timestamp (Court cases only)
    /// </summary>
    public DateTime? InvestigatingOfficerAssignedAt { get; set; }

    /// <summary>
    /// Officer who created the case
    /// </summary>
    public Guid? CreatedById { get; set; }

    /// <summary>
    /// Closure timestamp
    /// </summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>
    /// Officer who closed
    /// </summary>
    public Guid? ClosedById { get; set; }

    /// <summary>
    /// Closure reason/notes
    /// </summary>
    public string? ClosingReason { get; set; }

    // Navigation properties
    public virtual WeighingTransaction? Weighing { get; set; }
    // public virtual Vehicle Vehicle { get; set; } = null!;
    // public virtual Driver? Driver { get; set; }
    public virtual ViolationType ViolationType { get; set; } = null!;
    public virtual ActDefinition? ActDefinition { get; set; }
    public virtual DispositionType? DispositionType { get; set; }
    public virtual CaseStatus CaseStatus { get; set; } = null!;
    public virtual CaseManager? CaseManager { get; set; }
    /// <summary>Complainant officer (for court/prosecution cases).</summary>
    public virtual ApplicationUser? ComplainantOfficer { get; set; }
    /// <summary>Station where vehicle is detained.</summary>
    public virtual Station? DetentionStation { get; set; }
    public virtual ICollection<CaseSubfile> Subfiles { get; set; } = new List<CaseSubfile>();
    public virtual ICollection<SpecialRelease> SpecialReleases { get; set; } = new List<SpecialRelease>();
    public virtual ICollection<ArrestWarrant> ArrestWarrants { get; set; } = new List<ArrestWarrant>();
    public virtual ICollection<CourtHearing> CourtHearings { get; set; } = new List<CourtHearing>();
    public virtual CaseClosureChecklist? ClosureChecklist { get; set; }
}
