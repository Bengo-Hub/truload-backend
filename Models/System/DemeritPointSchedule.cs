using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.System;

/// <summary>
/// Demerit point schedule for regulatory compliance with Kenya Traffic Act Cap 403 Section 117A.
/// Points are assigned based on violation type and overload severity.
/// Implements NTSA demerit points system integration.
/// </summary>
public class DemeritPointSchedule : BaseEntity
{
    /// <summary>
    /// Violation type: STEERING, SINGLE_DRIVE, TANDEM, TRIDEM, GVW
    /// Corresponds to AxleType values plus GVW
    /// </summary>
    public string ViolationType { get; set; } = string.Empty;

    /// <summary>
    /// Minimum overload in kg (inclusive)
    /// </summary>
    public int OverloadMinKg { get; set; }

    /// <summary>
    /// Maximum overload in kg (inclusive, NULL = no upper limit)
    /// </summary>
    public int? OverloadMaxKg { get; set; }

    /// <summary>
    /// Demerit points assigned for this overload band
    /// Kenya Traffic Act Cap 403 Schedule:
    /// 0-2,000 kg: 1 point
    /// 2,001-5,000 kg: 2 points
    /// 5,001-10,000 kg: 3 points
    /// 10,001-20,000 kg: 5 points
    /// >20,000 kg: 10 points
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    /// Legal framework: EAC or TRAFFIC_ACT
    /// </summary>
    public string LegalFramework { get; set; } = string.Empty;

    /// <summary>
    /// Effective start date for this schedule
    /// </summary>
    public DateTime EffectiveFrom { get; set; }
}
