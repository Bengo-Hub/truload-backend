namespace TruLoad.Backend.Models;

/// <summary>
/// Station entity - Weighbridge/Mobile unit/Yard locations
/// </summary>
public class Station
{
    public Guid Id { get; set; }
    public string StationCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // Alias for StationCode for consistency
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Alias for Name - for compatibility with seeders and legacy code
    /// </summary>
    public string? StationName { get; set; }
    
    /// <summary>
    /// Status/Status string for compatibility with seeders
    /// </summary>
    public string? Status { get; set; }
    
    public Guid OrganizationId { get; set; }
    
    /// <summary>
    /// Station type: weigh_bridge, mobile_unit, yard
    /// </summary>
    public string StationType { get; set; } = "weigh_bridge";
    
    public string? Location { get; set; } // Address/location description
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool SupportsBidirectional { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
    
    // Navigation properties
    public Organization Organization { get; set; } = null!;
}
