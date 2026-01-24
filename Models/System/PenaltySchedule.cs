using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.System;

/// <summary>
/// Penalty schedule based on accumulated demerit points.
/// Implements NTSA license management penalties per Traffic Act Cap 403 Section 117A.
/// </summary>
public class PenaltySchedule : BaseEntity
{
    /// <summary>
    /// Minimum points for this penalty tier (inclusive)
    /// </summary>
    public int PointsMin { get; set; }

    /// <summary>
    /// Maximum points for this penalty tier (inclusive, NULL = no upper limit)
    /// </summary>
    public int? PointsMax { get; set; }

    /// <summary>
    /// Description of the penalty action
    /// Examples: "Warning letter", "Vehicle inspection required", "License suspension"
    /// </summary>
    public string PenaltyDescription { get; set; } = string.Empty;

    /// <summary>
    /// License suspension period in days (NULL if no suspension)
    /// NTSA Thresholds:
    /// 10-13 points: 180 days (6 months)
    /// 14-19 points: 365 days (1 year)
    /// 20+ points: 730 days (2 years)
    /// </summary>
    public int? SuspensionDays { get; set; }

    /// <summary>
    /// Whether this penalty requires court prosecution
    /// </summary>
    public bool RequiresCourt { get; set; } = false;

    /// <summary>
    /// Additional fine amount in USD for this penalty tier
    /// </summary>
    public decimal AdditionalFineUsd { get; set; } = 0m;

    /// <summary>
    /// Additional fine amount in KES for this penalty tier
    /// </summary>
    public decimal AdditionalFineKes { get; set; } = 0m;
}