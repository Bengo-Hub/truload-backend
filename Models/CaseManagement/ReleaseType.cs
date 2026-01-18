using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Special release type taxonomy for compliant/redistribution cases.
/// Defines fast-path dispositions: redistribution, tolerance, permit_valid, admin_discretion.
/// Enables flexible case handling for non-standard situations.
/// </summary>
public class ReleaseType : BaseEntity
{

    /// <summary>
    /// Unique release type code (e.g., "REDISTRIBUTION", "TOLERANCE", "PERMIT_VALID", "ADMIN_DISCRETION").
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Release type name for display (e.g., "Redistribution", "Tolerance Release", "Permit Valid", "Administrative Discretion").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Description of when this release type is applicable and conditions required.
    /// </summary>
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<SpecialRelease> SpecialReleases { get; set; } = [];
}
