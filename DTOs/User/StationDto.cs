namespace TruLoad.Backend.DTOs.User;

public class StationDto
{
    public Guid Id { get; set; }
    public string StationCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // Alias for consistency
    public string Name { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public string StationType { get; set; } = string.Empty;
    public string? Location { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool SupportsBidirectional { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateStationRequest
{
    public string StationCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // Alias for consistency
    public string Name { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public string StationType { get; set; } = "weigh_bridge";
    public string? Location { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool SupportsBidirectional { get; set; }
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
    public bool? IsActive { get; set; }
}
