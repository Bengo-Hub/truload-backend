using System.ComponentModel.DataAnnotations;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Arrest warrant tracking (part of Subfile G).
/// </summary>
public class ArrestWarrant : TenantAwareEntity
{

    /// <summary>
    /// Case reference (required)
    /// </summary>
    [Required]
    public Guid CaseRegisterId { get; set; }

    /// <summary>
    /// Warrant number (unique, required)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string WarrantNo { get; set; } = string.Empty;

    /// <summary>
    /// Issuing authority (court/magistrate)
    /// </summary>
    [MaxLength(255)]
    public string? IssuedBy { get; set; }

    /// <summary>
    /// Accused person name (required)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string AccusedName { get; set; } = string.Empty;

    /// <summary>
    /// Accused ID/Passport number
    /// </summary>
    [MaxLength(50)]
    public string? AccusedIdNo { get; set; }

    /// <summary>
    /// Offence description
    /// </summary>
    public string? OffenceDescription { get; set; }

    /// <summary>
    /// Warrant status FK (issued, active, executed, dropped)
    /// </summary>
    [Required]
    public Guid WarrantStatusId { get; set; }

    /// <summary>
    /// Issue date (required)
    /// </summary>
    [Required]
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Execution date
    /// </summary>
    public DateTime? ExecutedAt { get; set; }

    /// <summary>
    /// Dropped date
    /// </summary>
    public DateTime? DroppedAt { get; set; }

    /// <summary>
    /// Execution notes
    /// </summary>
    public string? ExecutionDetails { get; set; }

    /// <summary>
    /// Drop/lift reason
    /// </summary>
    public string? DroppedReason { get; set; }

    /// <summary>
    /// Date the warrant was issued by the court (distinct from IssuedAt which is when recorded in system)
    /// </summary>
    [Required]
    public DateTime IssuedDate { get; set; }

    /// <summary>
    /// Date the warrant was physically executed (null if not yet executed)
    /// </summary>
    public DateTime? ExecutionDate { get; set; }

    /// <summary>
    /// URL to the uploaded warrant document file
    /// </summary>
    [MaxLength(500)]
    public string? WarrantFileUrl { get; set; }

    /// <summary>
    /// FK to the case party (defendant) this warrant is linked to
    /// </summary>
    public Guid? CasePartyId { get; set; }

    // Navigation properties
    public virtual CaseRegister CaseRegister { get; set; } = null!;
    public virtual WarrantStatus WarrantStatus { get; set; } = null!;
    public virtual CaseParty? CaseParty { get; set; }
}
