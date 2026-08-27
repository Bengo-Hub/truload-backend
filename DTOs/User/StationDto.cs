namespace TruLoad.Backend.DTOs.User;

public class StationDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public string StationType { get; set; } = string.Empty;
    public string? Location { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool SupportsBidirectional { get; set; }
    public string? BoundACode { get; set; }
    public string? BoundBCode { get; set; }
    public bool IsActive { get; set; }
    /// <summary>True when this station is the organisation HQ (users assigned here can access all stations).</summary>
    public bool IsHq { get; set; }

    /// <summary>Start of this station's operating hours/shift boundary (EAT local clock).</summary>
    public TimeSpan? OperatingHoursStart { get; set; }

    /// <summary>End of this station's operating hours/shift boundary (EAT local clock).</summary>
    public TimeSpan? OperatingHoursEnd { get; set; }

    /// <summary>Printer configuration JSON (metadata only - not wired to a real print pipeline yet).</summary>
    public string? PrinterConfiguration { get; set; }

    /// <summary>Selected weight-ticket layout/template name for this station.</summary>
    public string? TicketTemplate { get; set; }

    /// <summary>
    /// Advisory/informational default weighing mode ("Enforcement"/"Commercial"). Display only -
    /// does not gate actual routing, which is derived from Organization.TenantType.
    /// </summary>
    public string? DefaultWeighingMode { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateStationRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public string StationType { get; set; } = "weigh_bridge";
    public string? Location { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool SupportsBidirectional { get; set; }
    public string? BoundACode { get; set; }
    public string? BoundBCode { get; set; }
    public TimeSpan? OperatingHoursStart { get; set; }
    public TimeSpan? OperatingHoursEnd { get; set; }
    public string? PrinterConfiguration { get; set; }
    public string? TicketTemplate { get; set; }
    public string? DefaultWeighingMode { get; set; }
}

public class UpdateStationRequest
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? StationType { get; set; }
    public string? Location { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool? SupportsBidirectional { get; set; }
    public string? BoundACode { get; set; }
    public string? BoundBCode { get; set; }
    public bool? IsActive { get; set; }
    public TimeSpan? OperatingHoursStart { get; set; }
    public TimeSpan? OperatingHoursEnd { get; set; }
    public string? PrinterConfiguration { get; set; }
    public string? TicketTemplate { get; set; }
    public string? DefaultWeighingMode { get; set; }
}
