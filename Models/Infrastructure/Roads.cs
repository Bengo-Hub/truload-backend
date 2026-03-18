using TruLoad.Backend.Models.Common;
using TruLoad.Backend.Models.Infrastructure;

namespace TruLoad.Backend.Models;

/// <summary>
/// Road master data. A road can pass through multiple counties and subcounties (many-to-many).
/// </summary>
public class Roads : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RoadClass { get; set; } = string.Empty; // A, B, C, D, E, S
    public decimal? TotalLengthKm { get; set; }

    public ICollection<RoadCounty> RoadCounties { get; set; } = new List<RoadCounty>();
    public ICollection<RoadSubcounty> RoadSubcounties { get; set; } = new List<RoadSubcounty>();
}