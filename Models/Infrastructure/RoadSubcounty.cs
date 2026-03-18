using System.Text.Json.Serialization;
using TruLoad.Backend.Models;

namespace TruLoad.Backend.Models.Infrastructure;

/// <summary>
/// Junction entity for many-to-many: a road can pass through multiple subcounties.
/// </summary>
public class RoadSubcounty
{
    public Guid RoadId { get; set; }
    public Guid SubcountyId { get; set; }

    [JsonIgnore]
    public Roads? Road { get; set; }
    public Subcounty? Subcounty { get; set; }
}
