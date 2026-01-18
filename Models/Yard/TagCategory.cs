using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.Yard;

/// <summary>
/// Tag category taxonomy for vehicle tags.
/// Defines standard categories for violation flagging (e.g., habitual offender, stolen vehicle, etc.).
/// </summary>
public class TagCategory : BaseEntity
{
    /// <summary>
    /// Unique category code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Category display name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Category description
    /// </summary>
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<VehicleTag> VehicleTags { get; set; } = new List<VehicleTag>();
}
