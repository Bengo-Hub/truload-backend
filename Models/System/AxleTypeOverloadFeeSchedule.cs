
namespace TruLoad.Backend.Models.System;
using TruLoad.Backend.Models.Common;

/// <summary>
/// Per-axle-type fee schedule for regulatory compliance with Kenya Traffic Act Cap 403 and EAC Act 2016.
/// Different axle types (Steering, SingleDrive, Tandem, Tridem) have different fee rates.
/// This implements the KenloadV2 approach of axle-type-specific fee calculation.
/// </summary>
public class AxleTypeOverloadFeeSchedule : BaseEntity
{
    /// <summary>
    /// Minimum overload in kg (inclusive)
    /// </summary>
    public int OverloadMinKg { get; set; }

    /// <summary>
    /// Maximum overload in kg (inclusive, NULL = no upper limit)
    /// </summary>
    public int? OverloadMaxKg { get; set; }

    /// <summary>
    /// Fee for steering axle overload (USD)
    /// Steering axle limit: 7,000 kg (EAC Act 2016)
    /// </summary>
    public decimal SteeringAxleFeeUsd { get; set; }

    /// <summary>
    /// Fee for single drive axle overload (USD)
    /// Single drive axle limit: 10,000 kg (dual tyres)
    /// </summary>
    public decimal SingleDriveAxleFeeUsd { get; set; }

    /// <summary>
    /// Fee for tandem axle group overload (USD)
    /// Tandem axle limit: 16,000 kg (2 axles < 1.8m spacing)
    /// </summary>
    public decimal TandemAxleFeeUsd { get; set; }

    /// <summary>
    /// Fee for tridem axle group overload (USD)
    /// Tridem axle limit: 24,000 kg (3 axles < 1.8m spacing)
    /// </summary>
    public decimal TridemAxleFeeUsd { get; set; }

    /// <summary>
    /// Fee for quad axle group overload (USD)
    /// For special configurations with 4 axles
    /// </summary>
    public decimal QuadAxleFeeUsd { get; set; } = 0m;

    /// <summary>
    /// Legal framework: EAC or TRAFFIC_ACT
    /// </summary>
    public string LegalFramework { get; set; } = string.Empty;

    /// <summary>
    /// Effective start date for this fee schedule
    /// </summary>
    public DateTime EffectiveFrom { get; set; }

    /// <summary>
    /// Effective end date (NULL = currently active)
    /// </summary>
    public DateTime? EffectiveTo { get; set; }
}
