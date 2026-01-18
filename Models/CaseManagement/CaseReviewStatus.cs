using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Case review status lifecycle for closure checklist review process.
/// Tracks review progression: none, requested, approved, rejected.
/// Used in quality assurance and case finalization workflows.
/// </summary>
public class CaseReviewStatus : BaseEntity
{

    /// <summary>
    /// Unique review status code (e.g., "NONE", "REQUESTED", "APPROVED", "REJECTED").
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Review status name for display (e.g., "None", "Requested", "Approved", "Rejected").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Description of this review status and what it means for case progression.
    /// </summary>
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<CaseClosureChecklist> CaseClosureChecklists { get; set; } = [];
}
