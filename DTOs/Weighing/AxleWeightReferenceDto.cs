using TruLoad.Backend.DTOs.Shared;

namespace TruLoad.Backend.DTOs.Weighing;

/// <summary>
/// DTO for axle weight reference data
/// </summary>
public class AxleWeightReferenceDto
{
    public Guid Id { get; set; }
    public Guid AxleConfigurationId { get; set; }
    public int AxlePosition { get; set; }
    public int AxleLegalWeightKg { get; set; }
    public Guid AxleGroupId { get; set; }
    public string AxleGrouping { get; set; } = string.Empty;
    public Guid? TyreTypeId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Search criteria for paginated weight reference queries
/// </summary>
public class SearchAxleWeightReferencesRequest : PagedRequest
{
    public Guid? ConfigurationId { get; set; }
    public string? AxleGrouping { get; set; }
    public bool IncludeInactive { get; set; } = false;
}