using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.Infrastructure;

/// <summary>
/// Vehicle manufacturer/make taxonomy for weighing operations.
/// Examples: Mercedes-Benz, Volvo, Scania, Isuzu, MAN, Hino
/// </summary>
public class VehicleMake : BaseEntity
{
    /// <summary>
    /// Unique code for the make (e.g., "MERC", "VOLV", "SCAN")
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name (e.g., "Mercedes-Benz", "Volvo Trucks")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Country of origin (e.g., "Germany", "Sweden", "Japan")
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Additional description or notes
    /// </summary>
    public string? Description { get; set; }

    // Navigation properties
    public virtual ICollection<VehicleModel> Models { get; set; } = new List<VehicleModel>();
}
