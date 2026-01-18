using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Case manager/prosecutor role assignment and tracking.
/// Links users to case management functions with role-based specialization.
/// Supports Prosecutor, Case Manager, and Investigator role types.
/// </summary>
public class CaseManager : BaseEntity
{

    /// <summary>
    /// Reference to user account (system user managing cases).
    /// </summary>
    public required Guid UserId { get; set; }

    /// <summary>
    /// Role type: case_manager, prosecutor, investigator.
    /// Determines access permissions and case assignment eligibility.
    /// </summary>
    public required string RoleType { get; set; } // case_manager, prosecutor, investigator

    /// <summary>
    /// Specialization area (e.g., "Traffic Violations", "Heavy Vehicle Enforcement").
    /// Helps assign cases to appropriate personnel.
    /// </summary>
    public string? Specialization { get; set; }

    // Navigation properties
    public ICollection<CaseRegister> CaseRegisters { get; set; } = [];
}
