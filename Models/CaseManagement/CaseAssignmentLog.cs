using TruLoad.Backend.Models.Common;
using TruLoad.Backend.Models.Identity;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Audit trail for case officer assignments and re-assignments.
/// Tracks history of who was assigned to cases and why.
/// </summary>
public class CaseAssignmentLog : BaseEntity
{
    /// <summary>
    /// Foreign key to case register
    /// </summary>
    public Guid CaseRegisterId { get; set; }

    /// <summary>
    /// Previous officer assigned (nullable for initial assignment)
    /// </summary>
    public Guid? PreviousOfficerId { get; set; }

    /// <summary>
    /// New officer being assigned
    /// </summary>
    public Guid NewOfficerId { get; set; }

    /// <summary>
    /// Supervisor who made the assignment
    /// </summary>
    public Guid AssignedById { get; set; }

    /// <summary>
    /// Assignment type: initial, re_assignment, transfer
    /// </summary>
    public string AssignmentType { get; set; } = "initial";

    /// <summary>
    /// Reason for assignment/change
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of assignment
    /// </summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public CaseRegister? CaseRegister { get; set; }
    public ApplicationUser? PreviousOfficer { get; set; }
    public ApplicationUser? NewOfficer { get; set; }
    public ApplicationUser? AssignedBy { get; set; }
}
