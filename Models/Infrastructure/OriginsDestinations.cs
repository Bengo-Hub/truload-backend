using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models;

/// <summary>
/// Origin and destination master data for cargo routes
/// </summary>
public class OriginsDestinations : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LocationType { get; set; } = "city"; // city, town, port, border, warehouse
    public string Country { get; set; } = "Kenya";
}