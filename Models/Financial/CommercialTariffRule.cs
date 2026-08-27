using TruLoad.Backend.Models.Common;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Models.Financial;

/// <summary>
/// Per-organisation commercial weighing fee rule. Replaces (as an override, not a removal) the
/// single flat Organization.CommercialWeighingFeeKes scalar with a real rate table: an org can
/// define multiple rules by vehicle type / axle count / weight bracket, plus a transporter-specific
/// contract override that takes priority over any bracket rule. Organization.CommercialWeighingFeeKes
/// remains the zero-config fallback when no rule matches — this table is purely additive.
/// </summary>
public class CommercialTariffRule : TenantAwareEntity
{
    /// <summary>
    /// Optional transporter contract override. When set, this rule applies ONLY to that
    /// transporter's weighing sessions and is matched before any org-wide bracket rule,
    /// regardless of vehicle/axle/weight criteria on this row.
    /// </summary>
    public Guid? TransporterId { get; set; }

    /// <summary>Optional vehicle type match (e.g. "Truck", "Trailer"). Null = any vehicle type.</summary>
    public string? VehicleType { get; set; }

    /// <summary>Minimum axle count (inclusive). Null = no lower bound.</summary>
    public int? AxleCountMin { get; set; }

    /// <summary>Maximum axle count (inclusive). Null = no upper bound.</summary>
    public int? AxleCountMax { get; set; }

    /// <summary>Minimum gross weight in kg (inclusive). Null = no lower bound.</summary>
    public int? WeightBracketMinKg { get; set; }

    /// <summary>Maximum gross weight in kg (inclusive). Null = no upper bound.</summary>
    public int? WeightBracketMaxKg { get; set; }

    /// <summary>Fee charged (KES) when this rule matches a completed commercial weighing.</summary>
    public decimal FeeKes { get; set; }

    /// <summary>Effective start date for this rule.</summary>
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;

    /// <summary>Effective end date. Null = currently active indefinitely.</summary>
    public DateTime? EffectiveTo { get; set; }

    /// <summary>Display label for the setup UI (e.g. "Heavy trucks (5+ axles)", "Acme Transporters contract rate").</summary>
    public string? Label { get; set; }

    // Navigation property
    public virtual Transporter? Transporter { get; set; }
}
