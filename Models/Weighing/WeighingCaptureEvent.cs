using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Common;
using TruLoad.Backend.Models.Identity;

namespace TruLoad.Backend.Models.Weighing;

/// <summary>
/// A single weight capture within a commercial weighing transaction's lifecycle: the first weight
/// (SequenceNo 1), the second weight (SequenceNo 2), or a subsequent reweigh (SequenceNo 3+, when
/// the vehicle returns to adjust cargo to hit a target weight before finalizing).
///
/// This is purely additive audit/history detail alongside WeighingTransaction's existing
/// FirstWeightKg/SecondWeightKg columns, which are kept unchanged for backward compatibility with
/// every existing PDF/report/dashboard/CSV consumer: "First*" always mirrors event #1, and
/// "Second*" mirrors whichever event actually finalized (closed and billed) the transaction -
/// not necessarily SequenceNo 2 when reweighs occurred first.
/// </summary>
[Table("weighing_capture_events")]
public class WeighingCaptureEvent : TenantAwareEntity
{
    [Column("weighing_transaction_id")]
    public Guid WeighingTransactionId { get; set; }

    /// <summary>1-based capture order: 1 = first weight, 2 = second weight, 3+ = reweighs.</summary>
    [Column("sequence_no")]
    public int SequenceNo { get; set; }

    /// <summary>
    /// Reweigh label shown to staff/on the ticket: null for SequenceNo 1-2 (first/second weight),
    /// else SequenceNo - 2 (i.e. "1st reweigh", "2nd reweigh", ...). Reweighs are their own count,
    /// never described as "3rd/4th weight" - kept as a distinct field from SequenceNo for that reason.
    /// </summary>
    [Column("reweigh_no")]
    public int? ReweighNo { get; set; }

    [Column("weight_kg")]
    public int WeightKg { get; set; }

    /// <summary>"tare" or "gross".</summary>
    [MaxLength(10)]
    [Column("weight_type")]
    public string WeightType { get; set; } = string.Empty;

    [Column("captured_at")]
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Mirrors WeighingTransaction.CaptureSource: manual/auto/frontend/stored/preset.</summary>
    [MaxLength(20)]
    [Column("capture_source")]
    public string CaptureSource { get; set; } = "manual";

    [Column("is_manual_entry")]
    public bool IsManualEntry { get; set; } = false;

    [MaxLength(500)]
    [Column("manual_entry_justification")]
    public string? ManualEntryJustification { get; set; }

    /// <summary>Per-deck/axle readings for this pass, same shape as the AxleWeights request field.</summary>
    [Column("deck_readings", TypeName = "jsonb")]
    public string? DeckReadings { get; set; }

    [Column("captured_by_user_id")]
    public Guid? CapturedByUserId { get; set; }

    /// <summary>True when this was the event that closed (finalized/billed) the transaction.</summary>
    [Column("is_finalizing_event")]
    public bool IsFinalizingEvent { get; set; } = false;

    /// <summary>
    /// Why a reweigh was needed, e.g. "over_limit_adjust_cargo", "confirm_weight_recheck". Required
    /// when this event doesn't finalize the transaction, or when attaching outside the auto-match
    /// window (see CommercialWeighingService.CaptureSecondWeightAsync).
    /// </summary>
    [MaxLength(500)]
    [Column("reweigh_reason")]
    public string? ReweighReason { get; set; }

    // Relationship configured in WeighingModuleDbContextConfiguration via a composite FK
    // {WeighingTransactionId, OrganizationId} -> WeighingTransaction's composite PK {Id,
    // OrganizationId} (used for table partitioning) - same pattern as WeighingAxle's own FK to
    // WeighingTransaction.
    public virtual WeighingTransaction? WeighingTransaction { get; set; }

    [ForeignKey("CapturedByUserId")]
    public virtual ApplicationUser? CapturedByUser { get; set; }
}
