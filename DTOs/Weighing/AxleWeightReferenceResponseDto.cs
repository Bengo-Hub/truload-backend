namespace TruLoad.Backend.DTOs.Weighing;

/// <summary>
/// DTO for returning axle weight reference data in API responses
/// </summary>
public class AxleWeightReferenceResponseDto
{
    public Guid Id { get; set; }
    public Guid AxleConfigurationId { get; set; }
    public int AxlePosition { get; set; }
    public int AxleLegalWeightKg { get; set; }
    public string AxleGrouping { get; set; } = string.Empty;
    public Guid AxleGroupId { get; set; }
    public string? AxleGroupCode { get; set; }
    public string? AxleGroupName { get; set; }
    public Guid? TyreTypeId { get; set; }
    public string? TyreTypeCode { get; set; }
    public string? TyreTypeName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for lookup data (dropdown options)
/// </summary>
public class AxleGroupLookupDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int TypicalWeightKg { get; set; }
    public string? Description { get; set; }
}

public class TyreTypeLookupDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? TypicalMaxWeightKg { get; set; }
    public string? Description { get; set; }
}
