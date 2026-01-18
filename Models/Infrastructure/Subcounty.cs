using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.Infrastructure;

/// <summary>
/// Subcounty master data within districts.
/// Administrative subdivision for geographic organization.
/// </summary>
public class Subcounty : BaseEntity
{
    /// <summary>
    /// Parent district ID (foreign key)
    /// </summary>
    public Guid DistrictId { get; set; }

    /// <summary>
    /// Unique subcounty code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Subcounty name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    // Navigation properties
    public Districts? District { get; set; }
}
