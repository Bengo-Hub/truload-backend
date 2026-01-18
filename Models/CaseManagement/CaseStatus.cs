using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Case status lifecycle for overall case progression.
/// Tracks the current state of a case: open, pending, closed, escalated.
/// Used for case queue management and filtering.
/// </summary>
public class CaseStatus : BaseEntity
{
    /// <summary>
    /// Unique case status code (e.g., "OPEN", "PENDING", "CLOSED", "ESCALATED").
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Case status name for display (e.g., "Open", "Pending", "Closed", "Escalated").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Description of this status and allowed transitions.
    /// </summary>
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<CaseRegister> CaseRegisters { get; set; } = [];
}
