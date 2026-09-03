using TruLoad.Backend.Models.Common;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Models.Financial;

/// <summary>
/// One completed weighing's fee, computed at capture time but held back from immediate invoicing
/// because the matched <see cref="CommercialTariffRule.BillingPeriod"/> is Daily/Weekly/Monthly
/// rather than Immediate. <c>CommercialPeriodicBillingJob</c> groups accruals by
/// (OrganizationId, TransporterId, PeriodKey) once that period has fully elapsed and rolls them
/// into ONE invoice — e.g. a quarry client paying monthly based on aggregated tonnage.
/// </summary>
public class CommercialTariffAccrual : TenantAwareEntity
{
    /// <summary>
    /// No EF navigation/FK to WeighingTransaction on purpose — WeighingTransaction's primary key is
    /// composite (Id, OrganizationId) because it's partitioned by org, the same reason
    /// Invoice.WeighingId is a loose scalar with no FK constraint. Query WeighingTransactions by
    /// this id directly when needed.
    /// </summary>
    public Guid WeighingId { get; set; }

    public Guid? TariffRuleId { get; set; }
    public virtual CommercialTariffRule? TariffRule { get; set; }

    public Guid? TransporterId { get; set; }
    public virtual Transporter? Transporter { get; set; }

    /// <summary>"Daily", "Weekly", or "Monthly" — copied from the matched rule at accrual time so a
    /// later edit to the rule doesn't retroactively change how an already-accrued row gets grouped.</summary>
    public string BillingPeriod { get; set; } = BillingPeriodValues.Daily;

    /// <summary>
    /// Groups accruals belonging to the same billing period, e.g. "2026-09-03" (Daily),
    /// "2026-W36" (Weekly, ISO week), or "2026-09" (Monthly) — all in EAT calendar terms, matching
    /// how the rest of this platform resolves "day"/"week"/"month" boundaries.
    /// </summary>
    public string PeriodKey { get; set; } = string.Empty;

    public int? NetWeightKg { get; set; }

    /// <summary>This weighing's contribution to the period's total (already computed via
    /// <c>ApplyRateBasis</c> at accrual time — the periodic job just sums these, it does not
    /// re-derive rates, so a mid-period rate change never rewrites already-accrued amounts).</summary>
    public decimal ComputedAmountKes { get; set; }

    /// <summary>"pending" (not yet invoiced) or "invoiced".</summary>
    public string Status { get; set; } = "pending";

    /// <summary>Set once <c>CommercialPeriodicBillingJob</c> rolls this accrual into an invoice.</summary>
    public Guid? InvoiceId { get; set; }
    public virtual Invoice? Invoice { get; set; }
}
