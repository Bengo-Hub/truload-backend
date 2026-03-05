namespace TruLoad.Backend.Models;

/// <summary>
/// Junction entity for many-to-many: a road can pass through multiple counties.
/// </summary>
public class RoadCounty
{
    public Guid RoadId { get; set; }
    public Guid CountyId { get; set; }

    public Roads Road { get; set; } = null!;
    public Counties County { get; set; } = null!;
}
