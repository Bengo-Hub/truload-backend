namespace TruLoad.Backend.DTOs.Weighing;

/// <summary>
/// DTO for returning axle configuration data in API responses
/// </summary>
public class AxleConfigurationResponseDto
{
    public Guid Id { get; set; }
    public string AxleCode { get; set; } = string.Empty;
    public string AxleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int AxleNumber { get; set; }
    public int GvwPermissibleKg { get; set; }
    public bool IsStandard { get; set; }
    public string LegalFramework { get; set; } = "BOTH";
    public string? VisualDiagramUrl { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public int WeightReferenceCount { get; set; }
}
