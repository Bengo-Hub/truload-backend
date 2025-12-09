namespace TruLoad.Backend.DTOs.Weighing;

/// <summary>
/// DTO for updating an existing axle weight reference
/// </summary>
public class UpdateAxleWeightReferenceDto
{
    /// <summary>
    /// Axle position within configuration (1, 2, 3, ... up to AxleNumber)
    /// </summary>
    public int AxlePosition { get; set; }

    /// <summary>
    /// Permissible weight for this axle in kg
    /// </summary>
    public int AxleLegalWeightKg { get; set; }

    /// <summary>
    /// Axle grouping classification (A, B, C, or D)
    /// </summary>
    public string AxleGrouping { get; set; } = string.Empty;

    /// <summary>
    /// Axle group ID (foreign key)
    /// </summary>
    public Guid AxleGroupId { get; set; }

    /// <summary>
    /// Tyre type ID (foreign key, optional)
    /// </summary>
    public Guid? TyreTypeId { get; set; }

    /// <summary>
    /// Is this weight reference active?
    /// </summary>
    public bool IsActive { get; set; } = true;
}
