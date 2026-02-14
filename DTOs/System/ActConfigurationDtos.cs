using System.ComponentModel.DataAnnotations;

namespace TruLoad.Backend.DTOs.System;

/// <summary>
/// DTO for ActDefinition returned from API.
/// </summary>
public record ActDefinitionDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ActType { get; init; } = string.Empty;
    public string? FullName { get; init; }
    public string? Description { get; init; }
    public DateOnly? EffectiveDate { get; init; }
    public string ChargingCurrency { get; init; } = "KES";
    public bool IsDefault { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Full act configuration including fee schedules, tolerances, and demerit points.
/// </summary>
public record ActConfigurationDto
{
    public ActDefinitionDto Act { get; init; } = null!;
    public List<AxleFeeScheduleDto> FeeSchedules { get; init; } = [];
    public List<AxleTypeOverloadFeeScheduleDto> AxleTypeFeeSchedules { get; init; } = [];
    public List<ToleranceSettingDto> ToleranceSettings { get; init; } = [];
    public List<DemeritPointScheduleDto> DemeritPointSchedules { get; init; } = [];
}

/// <summary>
/// DTO for AxleFeeSchedule (GVW and AXLE fee bands).
/// </summary>
public record AxleFeeScheduleDto
{
    public Guid Id { get; init; }
    public string LegalFramework { get; init; } = string.Empty;
    public string FeeType { get; init; } = string.Empty;
    public int OverloadMinKg { get; init; }
    public int? OverloadMaxKg { get; init; }
    public decimal FeePerKgUsd { get; init; }
    public decimal FlatFeeUsd { get; init; }
    public int DemeritPoints { get; init; }
    public string PenaltyDescription { get; init; } = string.Empty;
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>
/// DTO for AxleTypeOverloadFeeSchedule (per-axle-type fees).
/// </summary>
public record AxleTypeOverloadFeeScheduleDto
{
    public Guid Id { get; init; }
    public int OverloadMinKg { get; init; }
    public int? OverloadMaxKg { get; init; }
    public decimal SteeringAxleFeeUsd { get; init; }
    public decimal SingleDriveAxleFeeUsd { get; init; }
    public decimal TandemAxleFeeUsd { get; init; }
    public decimal TridemAxleFeeUsd { get; init; }
    public decimal QuadAxleFeeUsd { get; init; }
    public string LegalFramework { get; init; } = string.Empty;
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>
/// DTO for ToleranceSetting.
/// </summary>
public record ToleranceSettingDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string LegalFramework { get; init; } = string.Empty;
    public decimal TolerancePercentage { get; init; }
    public int? ToleranceKg { get; init; }
    public string AppliesTo { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>
/// DTO for DemeritPointSchedule.
/// </summary>
public record DemeritPointScheduleDto
{
    public Guid Id { get; init; }
    public string ViolationType { get; init; } = string.Empty;
    public int OverloadMinKg { get; init; }
    public int? OverloadMaxKg { get; init; }
    public int Points { get; init; }
    public string LegalFramework { get; init; } = string.Empty;
    public DateTime EffectiveFrom { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>
/// Summary of acts configuration for dashboard display.
/// </summary>
public record ActConfigurationSummaryDto
{
    public int TotalActs { get; init; }
    public string DefaultActCode { get; init; } = string.Empty;
    public string DefaultActName { get; init; } = string.Empty;
    public string DefaultCurrency { get; init; } = "KES";
    public int TotalFeeSchedules { get; init; }
    public int TotalToleranceSettings { get; init; }
    public int TotalDemeritSchedules { get; init; }
}

/// <summary>
/// Request to set the default act.
/// </summary>
public record SetDefaultActRequest
{
    [Required]
    public Guid ActId { get; init; }
}
