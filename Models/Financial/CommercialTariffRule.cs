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

    /// <summary>
    /// Fee charged (KES) when this rule matches a completed commercial weighing. Interpreted
    /// according to <see cref="RateBasis"/>: a flat amount, or a rate applied to the transaction's
    /// net weight (per tonne / per kg) — e.g. a quarry charging transporters per tonne weighed, or
    /// a facility invoicing its own client on aggregated tonnage at a negotiated per-tonne rate.
    /// </summary>
    public decimal FeeKes { get; set; }

    /// <summary>
    /// How <see cref="FeeKes"/> is applied: "PerTonne" (FeeKes x net weight in tonnes — the
    /// default, since most commercial weighing tenants bill by tonnage), "PerKg" (FeeKes x net
    /// weight in kg), or "Flat" (a fixed amount per matching weighing — also what a rule with a
    /// non-Immediate <see cref="BillingPeriod"/> effectively becomes "per transaction" under, since
    /// the period rolls up FeeKes x transaction-count). Plain validated string, not a Postgres enum
    /// type, matching the existing convention for small classification fields on this model
    /// (<see cref="RateBasisValues"/> is the allow-list).
    /// </summary>
    public string RateBasis { get; set; } = RateBasisValues.PerTonne;

    /// <summary>
    /// How often a matching weighing's fee is actually invoiced: "Immediate" (the original/default
    /// behavior — one invoice per weighing, right when it completes) or "Daily"/"Weekly"/"Monthly"
    /// (the fee is accrued into a <see cref="CommercialTariffAccrual"/> row instead, and
    /// <c>CommercialPeriodicBillingJob</c> rolls up every accrual for the same org+transporter+period
    /// into ONE invoice once that period has fully elapsed — e.g. a client who pays a transporter
    /// monthly based on aggregated tonnage). <see cref="BillingPeriodValues"/> is the allow-list.
    /// </summary>
    public string BillingPeriod { get; set; } = BillingPeriodValues.Immediate;

    /// <summary>Effective start date for this rule.</summary>
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;

    /// <summary>Effective end date. Null = currently active indefinitely.</summary>
    public DateTime? EffectiveTo { get; set; }

    /// <summary>Display label for the setup UI (e.g. "Heavy trucks (5+ axles)", "Acme Transporters contract rate").</summary>
    public string? Label { get; set; }

    // Navigation property
    public virtual Transporter? Transporter { get; set; }
}

/// <summary>Allow-listed values for <see cref="CommercialTariffRule.RateBasis"/>.</summary>
public static class RateBasisValues
{
    public const string Flat = "Flat";
    public const string PerTonne = "PerTonne";
    public const string PerKg = "PerKg";

    public static readonly string[] All = [Flat, PerTonne, PerKg];
}

/// <summary>Allow-listed values for <see cref="CommercialTariffRule.BillingPeriod"/>.</summary>
public static class BillingPeriodValues
{
    /// <summary>Invoice immediately when the weighing completes (original/default behavior).</summary>
    public const string Immediate = "Immediate";
    public const string Daily = "Daily";
    public const string Weekly = "Weekly";
    public const string Monthly = "Monthly";

    public static readonly string[] All = [Immediate, Daily, Weekly, Monthly];
}
