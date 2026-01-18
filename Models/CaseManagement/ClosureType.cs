using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Case closure type taxonomy for final case disposition.
/// Defines how a case is finally closed: withdrawn, discharged, paid, jailed.
/// Each type has specific requirements (CPC sections, evidence checklists, etc.)
/// </summary>
public class ClosureType : BaseEntity
{

    /// <summary>
    /// Unique closure type code (e.g., "WITHDRAWN", "DISCHARGED", "PAID", "JAILED").
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Closure type name for display (e.g., "Withdrawn", "Discharged", "Paid", "Convicted & Jailed").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Description of when this closure type is used and its legal implications.
    /// </summary>
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<CaseClosureChecklist> CaseClosureChecklists { get; set; } = [];
}
