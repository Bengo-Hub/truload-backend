using TruLoad.Backend.DTOs.Shared;

namespace TruLoad.Backend.DTOs.CaseManagement;

/// <summary>
/// Court Data Transfer Object
/// </summary>
public class CourtDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string CourtType { get; set; } = "magistrate";
    public Guid? CountyId { get; set; }
    public Guid? DistrictId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Create Court Request
/// </summary>
public class CreateCourtRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string CourtType { get; set; } = "magistrate";
    public Guid? CountyId { get; set; }
    public Guid? DistrictId { get; set; }
}

/// <summary>
/// Update Court Request
/// </summary>
public class UpdateCourtRequest
{
    public string? Name { get; set; }
    public string? Location { get; set; }
    public string? CourtType { get; set; }
    public Guid? CountyId { get; set; }
    public Guid? DistrictId { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>
/// Court Search Criteria
/// </summary>
public class CourtSearchCriteria : PagedRequest
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? CourtType { get; set; }
    public Guid? CountyId { get; set; }
    public Guid? DistrictId { get; set; }
    public bool? IsActive { get; set; }
}
