using System.Text.Json.Serialization;

namespace TruLoad.Backend.Models;

/// <summary>
/// Junction entity for many-to-many: a road can pass through multiple counties.
/// </summary>
public class RoadCounty
{
    public Guid RoadId { get; set; }
    public Guid CountyId { get; set; }

    [JsonIgnore]
    public Roads? Road { get; set; }
    public Counties? County { get; set; }
}
