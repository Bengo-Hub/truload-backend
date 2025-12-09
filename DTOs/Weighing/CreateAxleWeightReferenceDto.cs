namespace TruLoad.Backend.DTOs.Weighing;

/// <summary>
/// DTO for creating a new axle weight reference (weight spec for a single axle position)
/// </summary>
public class CreateAxleWeightReferenceDto
{
    /// <summary>
    /// Parent axle configuration ID
    /// </summary>
    public Guid AxleConfigurationId { get; set; }

    /// <summary>
    /// Axle position within configuration (1, 2, 3, ... up to AxleNumber)
    /// </summary>
    public int AxlePosition { get; set; }

    /// <summary>
    /// Permissible weight for this axle in kg
    /// Typically 4750-10000 kg
    /// </summary>
    public int AxleLegalWeightKg { get; set; }

    /// <summary>
    /// Axle grouping classification (A, B, C, or D)
    /// A = Front, B = Trailer coupling, C = Mid-trailer, D = Rear trailer
    /// </summary>
    public string AxleGrouping { get; set; } = string.Empty;

    /// <summary>
    /// Axle group ID (foreign key) for specific group classification
    /// (S1, SA4, SA6, TAG8, TAG8B, TAG12, QAG16, WWW, SSS, S4, etc.)
    /// </summary>
    public Guid AxleGroupId { get; set; }

    /// <summary>
    /// Tyre type ID (foreign key) for this position
    /// S = Single, D = Dual, W = Wide single (optional)
    /// </summary>
    public Guid? TyreTypeId { get; set; }
}
