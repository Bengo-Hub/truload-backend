namespace TruLoad.Backend.DTOs.Weighing;

/// <summary>
/// DTO for updating an existing axle configuration
/// Only derived configurations can be updated
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
    /// Gross Vehicle Weight permissible in kg
    /// </summary>
    public int GvwPermissibleKg { get; set; }

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
}
