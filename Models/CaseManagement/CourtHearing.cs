using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Court hearing schedule and minutes (part of Subfile J).
/// </summary>
public class CourtHearing : TenantAwareEntity
{

    /// <summary>
    /// Case reference (required)
    /// </summary>
    [Required]
    public Guid CaseRegisterId { get; set; }

    /// <summary>
    /// Court ID
    /// </summary>
    public Guid? CourtId { get; set; }

    /// <summary>
    /// Scheduled date (required)
    /// </summary>
    [Required]
    public DateTime HearingDate { get; set; }

    /// <summary>
    /// Scheduled time
    /// </summary>
    public TimeSpan? HearingTime { get; set; }

    /// <summary>
    /// Hearing type FK (mention, hearing, judgment, ruling, bail, etc.)
    /// </summary>
    public Guid? HearingTypeId { get; set; }

    /// <summary>
    /// Status FK (scheduled, held, adjourned, cancelled)
    /// </summary>
    public Guid? HearingStatusId { get; set; }

    /// <summary>
    /// Outcome FK (adjourned, ruling, convicted, acquitted, etc.)
    /// </summary>
    public Guid? HearingOutcomeId { get; set; }

    /// <summary>
    /// Minute sheet notes
    /// </summary>
    public string? MinuteNotes { get; set; }

    /// <summary>
    /// Vector embedding for minute notes (semantic search).
    /// 384 dimensions for all-MiniLM-L12-v2 model.
    /// NotMapped by default - explicitly configured for PostgreSQL only.
    /// </summary>
    [NotMapped]
    public Pgvector.Vector? MinuteNotesEmbedding { get; set; }

    /// <summary>
    /// Next hearing date
    /// </summary>
    public DateTime? NextHearingDate { get; set; }

    /// <summary>
    /// Adjournment reason
    /// </summary>
    public string? AdjournmentReason { get; set; }

    /// <summary>
    /// Magistrate/Judge name
    /// </summary>
    [MaxLength(255)]
    public string? PresidingOfficer { get; set; }

    // Navigation properties
    public virtual CaseRegister CaseRegister { get; set; } = null!;
    public virtual HearingType? HearingType { get; set; }
    public virtual HearingStatus? HearingStatus { get; set; }
    public virtual HearingOutcome? HearingOutcome { get; set; }
}
