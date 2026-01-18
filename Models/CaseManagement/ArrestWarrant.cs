using System.ComponentModel.DataAnnotations;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Arrest warrant tracking (part of Subfile G).
/// </summary>
public class ArrestWarrant : BaseEntity
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
    /// Drop reason
    /// </summary>
    public string? DroppedReason { get; set; }

    // Navigation properties
    public virtual CaseRegister CaseRegister { get; set; } = null!;
    public virtual WarrantStatus WarrantStatus { get; set; } = null!;
}
