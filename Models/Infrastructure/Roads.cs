using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models;

/// <summary>
/// Road master data with district linkage
/// </summary>
public class Roads : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RoadClass { get; set; } = string.Empty; // A, B, C, D, E
    public Guid? DistrictId { get; set; }
    public decimal? TotalLengthKm { get; set; }

    // Navigation properties
    public Districts? District { get; set; }
}