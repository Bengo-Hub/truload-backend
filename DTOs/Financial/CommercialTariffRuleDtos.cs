using System.ComponentModel.DataAnnotations;

namespace TruLoad.Backend.DTOs.Financial;

/// <summary>
/// DTO for CommercialTariffRule.
/// </summary>
public record CommercialTariffRuleDto
{
    public Guid Id { get; init; }
    public Guid? TransporterId { get; init; }
    public string? TransporterName { get; init; }
    public string? VehicleType { get; init; }
    public int? AxleCountMin { get; init; }
    public int? AxleCountMax { get; init; }
    public int? WeightBracketMinKg { get; init; }
    public int? WeightBracketMaxKg { get; init; }
    public decimal FeeKes { get; init; }
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public string? Label { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Request to create a commercial tariff rule.
/// </summary>
public record CreateCommercialTariffRuleRequest
{
    public Guid? TransporterId { get; init; }
    public string? VehicleType { get; init; }
    public int? AxleCountMin { get; init; }
    public int? AxleCountMax { get; init; }
    public int? WeightBracketMinKg { get; init; }
    public int? WeightBracketMaxKg { get; init; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Fee must be zero or greater")]
    public decimal FeeKes { get; init; }

    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public string? Label { get; init; }
}

/// <summary>
/// Request to update a commercial tariff rule (partial update).
/// </summary>
public record UpdateCommercialTariffRuleRequest
{
    public Guid? TransporterId { get; init; }
    public string? VehicleType { get; init; }
    public int? AxleCountMin { get; init; }
    public int? AxleCountMax { get; init; }
    public int? WeightBracketMinKg { get; init; }
    public int? WeightBracketMaxKg { get; init; }
    public decimal? FeeKes { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public string? Label { get; init; }
    public bool? IsActive { get; init; }
}
