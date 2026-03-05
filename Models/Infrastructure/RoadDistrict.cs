namespace TruLoad.Backend.Models;

/// <summary>
/// Junction entity for many-to-many: a road can pass through multiple districts (subcounties).
/// </summary>
public class RoadDistrict
{
    public Guid RoadId { get; set; }
    public Guid DistrictId { get; set; }

    public Roads Road { get; set; } = null!;
    public Districts District { get; set; } = null!;
}
