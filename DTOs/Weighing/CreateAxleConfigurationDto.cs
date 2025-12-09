namespace TruLoad.Backend.DTOs.Weighing;

/// <summary>
/// DTO for creating a new derived axle configuration
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
    /// Gross Vehicle Weight permissible in kg
    /// </summary>
    public int GvwPermissibleKg { get; set; }

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
}
