using TruLoad.Backend.Models.Common;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Models.Infrastructure;

/// <summary>
/// Vehicle model taxonomy linked to make.
/// Examples: Actros (Mercedes), FH16 (Volvo), R-Series (Scania)
/// </summary>
public class VehicleModel : BaseEntity
{
    /// <summary>
    /// Unique code for the model (e.g., "ACTR", "FH16")
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name (e.g., "Actros", "FH16")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to VehicleMake
    /// </summary>
    public Guid MakeId { get; set; }

    /// <summary>
    /// Vehicle category classification
    /// </summary>
    public string VehicleCategory { get; set; } = "Truck"; // Truck, Trailer, Bus, Van, Other

    /// <summary>
    /// Default axle configuration for this model (optional)
    /// </summary>
    public Guid? AxleConfigurationId { get; set; }

    /// <summary>
    /// Additional description or notes
    /// </summary>
    public string? Description { get; set; }

    // Navigation properties
    public virtual VehicleMake? Make { get; set; }
    public virtual AxleConfiguration? AxleConfiguration { get; set; }
}
