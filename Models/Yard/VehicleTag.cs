using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Common;
using TruLoad.Backend.Models.Identity;

namespace TruLoad.Backend.Models.Yard;

/// <summary>
/// Violation tags for vehicles (automatic or manual flagging).
/// Used for cross-station enforcement and watchlist tracking.
/// </summary>
public class VehicleTag : TenantAwareEntity
{
    /// <summary>
    /// Vehicle registration number
    /// </summary>
    public string RegNo { get; set; } = string.Empty;

    /// <summary>
    /// Tag type: automatic (system-generated), manual (officer-created)
    /// </summary>
    public string TagType { get; set; } = "automatic";

    /// <summary>
    /// Foreign key to tag category taxonomy
    /// </summary>
    public Guid TagCategoryId { get; set; }

    /// <summary>
    /// Reason for tagging (detailed description)
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Vector embedding for reason (semantic search).
    /// 384 dimensions for all-MiniLM-L12-v2 model.
    /// NotMapped by default - explicitly configured for PostgreSQL only.
    /// </summary>
    [NotMapped]
    public Pgvector.Vector? ReasonEmbedding { get; set; }

    /// <summary>
    /// Station code where tag was created
    /// </summary>
    public string StationCode { get; set; } = string.Empty;

    /// <summary>
    /// Tag status: open, closed
    /// </summary>
    public string Status { get; set; } = "open";

    /// <summary>
    /// Photo path for evidence
    /// </summary>
    public string? TagPhotoPath { get; set; }

    /// <summary>
    /// Duration the tag is active (e.g., 30 days)
    /// </summary>
    public TimeSpan? EffectiveTimePeriod { get; set; }

    /// <summary>
    /// User who created the tag
    /// </summary>
    public Guid CreatedById { get; set; }

    /// <summary>
    /// User who closed the tag
    /// </summary>
    public Guid? ClosedById { get; set; }

    /// <summary>
    /// Reason for closing the tag
    /// </summary>
    public string? ClosedReason { get; set; }

    /// <summary>
    /// Timestamp when tag was opened
    /// </summary>
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when tag was closed
    /// </summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>
    /// Whether tag has been exported to external systems
    /// </summary>
    public bool Exported { get; set; } = false;

    /// <summary>
    /// Case register reference (required for manual tags, optional for automatic)
    /// Links tag violations to the case management workflow for prosecution tracking
    /// </summary>
    public Guid? CaseRegisterId { get; set; }

    // Navigation properties
    public TagCategory? TagCategory { get; set; }
    public ApplicationUser? CreatedBy { get; set; }
    public ApplicationUser? ClosedBy { get; set; }
    public CaseManagement.CaseRegister? CaseRegister { get; set; }
}
