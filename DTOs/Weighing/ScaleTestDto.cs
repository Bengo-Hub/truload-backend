using System.ComponentModel.DataAnnotations;

namespace TruLoad.Backend.DTOs.Weighing;

/// <summary>
/// Response DTO for a scale test (daily calibration verification).
/// </summary>
public class ScaleTestDto
{
    public Guid Id { get; set; }
    public Guid StationId { get; set; }
    public string? StationName { get; set; }
    public string? StationCode { get; set; }

    /// <summary>
    /// Direction/bound for bidirectional stations (A or B).
    /// </summary>
    public string? Bound { get; set; }

    /// <summary>
    /// Type of test: calibration_weight or vehicle
    /// </summary>
    public string TestType { get; set; } = "calibration_weight";

    /// <summary>
    /// Vehicle plate number for vehicle-based tests
    /// </summary>
    public string? VehiclePlate { get; set; }

    /// <summary>
    /// Weighing mode: mobile or multideck
    /// </summary>
    public string? WeighingMode { get; set; }

    /// <summary>
    /// Expected test weight in kg.
    /// </summary>
    public int? TestWeightKg { get; set; }

    /// <summary>
    /// Actual measured weight in kg.
    /// </summary>
    public int? ActualWeightKg { get; set; }

    /// <summary>
    /// Result of the scale test: pass or fail.
    /// </summary>
    public string Result { get; set; } = "pass";

    /// <summary>
    /// Deviation from expected weight in kg.
    /// </summary>
    public int? DeviationKg { get; set; }

    /// <summary>
    /// Additional details or notes about the test.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// When the test was performed.
    /// </summary>
    public DateTime CarriedAt { get; set; }

    /// <summary>
    /// ID of the officer who performed the test.
    /// </summary>
    public Guid CarriedById { get; set; }

    /// <summary>
    /// Name of the officer who performed the test.
    /// </summary>
    public string? CarriedByName { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Request DTO for creating a new scale test.
/// </summary>
public class CreateScaleTestRequest
{
    [Required]
    public Guid StationId { get; set; }

    /// <summary>
    /// Direction/bound for bidirectional stations (A or B).
    /// Required for bidirectional stations.
    /// </summary>
    [StringLength(10)]
    public string? Bound { get; set; }

    /// <summary>
    /// Type of test: calibration_weight or vehicle
    /// </summary>
    [StringLength(50)]
    public string TestType { get; set; } = "calibration_weight";

    /// <summary>
    /// Vehicle plate number for vehicle-based tests.
    /// Required when TestType is "vehicle".
    /// </summary>
    [StringLength(20)]
    public string? VehiclePlate { get; set; }

    /// <summary>
    /// Weighing mode: mobile or multideck
    /// </summary>
    [StringLength(20)]
    public string? WeighingMode { get; set; }

    /// <summary>
    /// Expected test weight in kg.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int? TestWeightKg { get; set; }

    /// <summary>
    /// Actual measured weight in kg.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int? ActualWeightKg { get; set; }

    /// <summary>
    /// Result of the scale test: pass or fail.
    /// </summary>
    [Required]
    [StringLength(20)]
    public string Result { get; set; } = "pass";

    /// <summary>
    /// Deviation from expected weight in kg.
    /// </summary>
    public int? DeviationKg { get; set; }

    /// <summary>
    /// Additional details or notes about the test.
    /// </summary>
    [StringLength(2000)]
    public string? Details { get; set; }
}

/// <summary>
/// Response DTO for checking scale test status.
/// </summary>
public class ScaleTestStatusDto
{
    /// <summary>
    /// Whether a valid (passing) scale test exists for today.
    /// </summary>
    public bool HasValidTest { get; set; }

    /// <summary>
    /// The latest scale test for today, if any.
    /// </summary>
    public ScaleTestDto? LatestTest { get; set; }

    /// <summary>
    /// Whether weighing is allowed (requires valid test).
    /// </summary>
    public bool WeighingAllowed { get; set; }

    /// <summary>
    /// Message explaining the status.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Station ID for which the status was checked.
    /// </summary>
    public Guid StationId { get; set; }

    /// <summary>
    /// Bound for which the status was checked (if applicable).
    /// </summary>
    public string? Bound { get; set; }
}

/// <summary>
/// Request DTO for checking scale test status.
/// </summary>
public class CheckScaleTestStatusRequest
{
    [Required]
    public Guid StationId { get; set; }

    /// <summary>
    /// Direction/bound to check (optional).
    /// </summary>
    [StringLength(10)]
    public string? Bound { get; set; }
}
