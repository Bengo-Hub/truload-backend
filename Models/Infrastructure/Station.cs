using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models;

/// <summary>
/// Station entity - Weighbridge/Mobile unit/Yard locations
/// </summary>
public class Station : BaseEntity
{
    /// <summary>
    /// Unique station identifier code (e.g., "NRB-MOBILE-01")
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the station
    /// </summary>
    public string Name { get; set; } = string.Empty;

    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Station type: weigh_bridge, mobile_unit, yard
    /// </summary>
    public string StationType { get; set; } = "weigh_bridge";

    public string? Location { get; set; } // Address/location description

    /// <summary>
    /// Road where this station is located (foreign key)
    /// </summary>
    public Guid? RoadId { get; set; }

    /// <summary>
    /// County where this station is located (foreign key)
    /// </summary>
    public Guid? CountyId { get; set; }

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool SupportsBidirectional { get; set; } = false;

    /// <summary>
    /// Virtual station code for Bound A (for bidirectional stations)
    /// </summary>
    public string? BoundACode { get; set; }

    /// <summary>
    /// Virtual station code for Bound B (for bidirectional stations)
    /// </summary>
    public string? BoundBCode { get; set; }

    // Navigation properties
    public Organization Organization { get; set; } = null!;
    public Roads? Road { get; set; }
    public Counties? County { get; set; }
}
