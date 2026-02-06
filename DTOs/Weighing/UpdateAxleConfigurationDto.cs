namespace TruLoad.Backend.DTOs.Weighing;

/// <summary>
/// DTO for updating an existing axle configuration.
/// When weight references are provided, GVW is recalculated from their sum.
/// </summary>
public class UpdateAxleConfigurationDto
{
    /// <summary>
    /// Display name
    /// </summary>
    public string AxleName { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Legal framework applicability
    /// </summary>
    public string? LegalFramework { get; set; }

    /// <summary>
    /// Optional visual diagram URL
    /// </summary>
    public string? VisualDiagramUrl { get; set; }

    /// <summary>
    /// Optional notes
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Is this configuration active?
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Updated weight references. When provided, replaces all existing refs
    /// and GVW is recalculated from the sum of AxleLegalWeightKg values.
    /// </summary>
    public List<UpdateAxleWeightReferenceInlineDto>? WeightReferences { get; set; }
}

/// <summary>
/// Inline weight reference for updating alongside axle configuration.
/// If Id is provided, updates existing; otherwise creates new.
/// </summary>
public class UpdateAxleWeightReferenceInlineDto
{
    public Guid? Id { get; set; }
    public int AxlePosition { get; set; }
    public int AxleLegalWeightKg { get; set; }
    public string AxleGrouping { get; set; } = string.Empty;
    public Guid AxleGroupId { get; set; }
    public Guid? TyreTypeId { get; set; }
}
