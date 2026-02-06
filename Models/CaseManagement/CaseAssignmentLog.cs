using TruLoad.Backend.Models.Common;
using TruLoad.Backend.Models.Identity;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Audit trail for Investigating Officer (IO) assignments and re-assignments.
/// Tracks chain of custody - history of which IOs were assigned to cases.
/// Follows KenloadV2 CaseIOs pattern with IsCurrent flag for active IO tracking.
/// </summary>
public class CaseAssignmentLog : BaseEntity
{
    /// <summary>
    /// Foreign key to case register
    /// </summary>
    public Guid CaseRegisterId { get; set; }

    /// <summary>
    /// Previous IO assigned (nullable for initial assignment)
    /// </summary>
    public Guid? PreviousOfficerId { get; set; }

    /// <summary>
    /// New IO (Investigating Officer) being assigned
    /// </summary>
    public Guid NewOfficerId { get; set; }

    /// <summary>
    /// Supervisor who made the assignment (OCS/Dept OCS)
    /// </summary>
    public Guid AssignedById { get; set; }

    /// <summary>
    /// Assignment type: initial, re_assignment, transfer, handover
    /// </summary>
    public string AssignmentType { get; set; } = "initial";

    /// <summary>
    /// Reason for assignment/change (e.g., "Initial assignment", "Officer transferred", "Case complexity")
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of assignment / date IO took over
    /// </summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Indicates if this is the current/active IO for the case.
    /// Only one assignment per case should have IsCurrent = true.
    /// When reassigning, set previous assignment's IsCurrent to false.
    /// </summary>
    public bool IsCurrent { get; set; } = true;

    /// <summary>
    /// IO Rank (e.g., "Constable", "Corporal", "Sergeant", "Inspector", "OCS")
    /// </summary>
    public string? OfficerRank { get; set; }

    // Navigation properties
    public CaseRegister? CaseRegister { get; set; }
    public ApplicationUser? PreviousOfficer { get; set; }
    public ApplicationUser? NewOfficer { get; set; }
    public ApplicationUser? AssignedBy { get; set; }
}
