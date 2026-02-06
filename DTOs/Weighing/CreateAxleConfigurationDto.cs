namespace TruLoad.Backend.DTOs.Weighing;

/// <summary>
/// DTO for creating a new derived axle configuration.
/// GVW is auto-calculated from the sum of weight reference legal weights.
/// </summary>
public class CreateAxleConfigurationDto
{
    /// <summary>
    /// Unique axle code (e.g., "2A", "5*S|DD|DD|", "3*S|DW||")
    /// </summary>
    public string AxleCode { get; set; } = string.Empty;

    /// <summary>
    /// Display name (e.g., "Two Axle A", "Five Axle Custom")
    /// </summary>
    public string AxleName { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Total number of axles (2-8)
    /// </summary>
    public int AxleNumber { get; set; }

    /// <summary>
    /// Legal framework applicability (EAC, TRAFFIC_ACT, or BOTH)
    /// </summary>
    public string? LegalFramework { get; set; }

    /// <summary>
    /// Optional visual diagram URL
    /// </summary>
    public string? VisualDiagramUrl { get; set; }

    /// <summary>
    /// Optional notes or special rules
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Weight references for each axle position.
    /// GVW is calculated as the sum of all AxleLegalWeightKg values.
    /// </summary>
    public List<CreateAxleWeightReferenceInlineDto>? WeightReferences { get; set; }
}

/// <summary>
/// Inline weight reference for creating alongside axle configuration
/// </summary>
public class CreateAxleWeightReferenceInlineDto
{
    public int AxlePosition { get; set; }
    public int AxleLegalWeightKg { get; set; }
    public string AxleGrouping { get; set; } = string.Empty;
    public Guid AxleGroupId { get; set; }
    public Guid? TyreTypeId { get; set; }
}
