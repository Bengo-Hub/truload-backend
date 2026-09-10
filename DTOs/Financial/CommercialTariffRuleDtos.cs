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

    /// <summary>When set, invoices from this rule bill this transporter/customer instead of the
    /// vehicle's own operator — e.g. a client a quarry hauls on behalf of. Null = bill the vehicle's
    /// own transporter (the default, unchanged behaviour).</summary>
    public Guid? BilledToTransporterId { get; init; }
    public string? BilledToTransporterName { get; init; }

    /// <summary>Optional cargo/material type match — see <c>CommercialTariffRule.CargoTypeId</c>.</summary>
    public Guid? CargoTypeId { get; init; }
    public string? CargoTypeName { get; init; }
    public string? VehicleType { get; init; }
    public int? AxleCountMin { get; init; }
    public int? AxleCountMax { get; init; }
    public int? WeightBracketMinKg { get; init; }
    public int? WeightBracketMaxKg { get; init; }
    public decimal FeeKes { get; init; }

    /// <summary>"PerTonne" (default), "PerKg", or "Flat" — see <c>RateBasisValues</c>.</summary>
    public string RateBasis { get; init; } = "PerTonne";

    /// <summary>Optional minimum charge (KES) floor for PerTonne/PerKg rules — see
    /// <c>CommercialTariffRule.MinimumChargeKes</c>.</summary>
    public decimal? MinimumChargeKes { get; init; }

    /// <summary>"Immediate" (default — one invoice per weighing) or "Daily"/"Weekly"/"BiWeekly"/
    /// "Monthly"/"Quarterly"/"Yearly" (accrued and rolled into one invoice per period) — see
    /// <c>BillingPeriodValues</c>.</summary>
    public string BillingPeriod { get; init; } = "Immediate";

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

    /// <summary>Optional billing override — see <c>CommercialTariffRule.BilledToTransporterId</c>.</summary>
    public Guid? BilledToTransporterId { get; init; }

    /// <summary>Optional cargo/material type match — see <c>CommercialTariffRule.CargoTypeId</c>.</summary>
    public Guid? CargoTypeId { get; init; }
    public string? VehicleType { get; init; }
    public int? AxleCountMin { get; init; }
    public int? AxleCountMax { get; init; }
    public int? WeightBracketMinKg { get; init; }
    public int? WeightBracketMaxKg { get; init; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "Fee must be zero or greater")]
    public decimal FeeKes { get; init; }

    /// <summary>"PerTonne" (default), "PerKg", or "Flat" — see <c>RateBasisValues</c>.</summary>
    [RegularExpression("^(Flat|PerTonne|PerKg)$", ErrorMessage = "RateBasis must be Flat, PerTonne, or PerKg.")]
    public string RateBasis { get; init; } = "PerTonne";

    /// <summary>Optional minimum charge (KES) floor for PerTonne/PerKg rules — see
    /// <c>CommercialTariffRule.MinimumChargeKes</c>.</summary>
    [Range(0, double.MaxValue, ErrorMessage = "Minimum charge must be zero or greater")]
    public decimal? MinimumChargeKes { get; init; }

    /// <summary>"Immediate" (default), "Daily", "Weekly", "BiWeekly", "Monthly", "Quarterly", or
    /// "Yearly" — see <c>BillingPeriodValues</c>.</summary>
    [RegularExpression("^(Immediate|Daily|Weekly|BiWeekly|Monthly|Quarterly|Yearly)$", ErrorMessage = "BillingPeriod must be Immediate, Daily, Weekly, BiWeekly, Monthly, Quarterly, or Yearly.")]
    public string BillingPeriod { get; init; } = "Immediate";

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

    /// <summary>Optional billing override — see <c>CommercialTariffRule.BilledToTransporterId</c>.</summary>
    public Guid? BilledToTransporterId { get; init; }

    /// <summary>Optional cargo/material type match — see <c>CommercialTariffRule.CargoTypeId</c>.</summary>
    public Guid? CargoTypeId { get; init; }
    public string? VehicleType { get; init; }
    public int? AxleCountMin { get; init; }
    public int? AxleCountMax { get; init; }
    public int? WeightBracketMinKg { get; init; }
    public int? WeightBracketMaxKg { get; init; }
    public decimal? FeeKes { get; init; }

    /// <summary>"PerTonne", "PerKg", or "Flat" — see <c>RateBasisValues</c>. Null = leave unchanged.</summary>
    [RegularExpression("^(Flat|PerTonne|PerKg)$", ErrorMessage = "RateBasis must be Flat, PerTonne, or PerKg.")]
    public string? RateBasis { get; init; }

    /// <summary>Optional minimum charge (KES) floor for PerTonne/PerKg rules — see
    /// <c>CommercialTariffRule.MinimumChargeKes</c>.</summary>
    [Range(0, double.MaxValue, ErrorMessage = "Minimum charge must be zero or greater")]
    public decimal? MinimumChargeKes { get; init; }

    /// <summary>"Immediate", "Daily", "Weekly", "BiWeekly", "Monthly", "Quarterly", or "Yearly" —
    /// see <c>BillingPeriodValues</c>. Null = leave unchanged.</summary>
    [RegularExpression("^(Immediate|Daily|Weekly|BiWeekly|Monthly|Quarterly|Yearly)$", ErrorMessage = "BillingPeriod must be Immediate, Daily, Weekly, BiWeekly, Monthly, Quarterly, or Yearly.")]
    public string? BillingPeriod { get; init; }

    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public string? Label { get; init; }
    public bool? IsActive { get; init; }
}
