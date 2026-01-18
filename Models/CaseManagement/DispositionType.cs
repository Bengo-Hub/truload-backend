using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Case disposition type taxonomy for initial case resolution paths.
/// Defines how a case can be resolved: special_release, paid, court, pending.
/// Critical for case routing and workflow determination.
/// </summary>
public class DispositionType : BaseEntity
{

    /// <summary>
    /// Unique disposition code (e.g., "SPECIAL_RELEASE", "PAID", "COURT", "PENDING").
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Disposition name for display (e.g., "Special Release", "Paid", "Court Escalation", "Pending").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Description of when this disposition is applicable and its workflow implications.
    /// </summary>
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<CaseRegister> CaseRegisters { get; set; } = [];
}
