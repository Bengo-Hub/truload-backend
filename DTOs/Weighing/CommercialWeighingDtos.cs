using System.ComponentModel.DataAnnotations;
using TruLoad.Backend.DTOs.Shared;

namespace TruLoad.Backend.DTOs.Weighing;

// ── Request DTOs ──

/// <summary>
/// Request to initiate a commercial weighing transaction.
/// </summary>
public class InitiateCommercialWeighingRequest
{
    [Required]
    public Guid StationId { get; set; }

    /// <summary>
    /// Vehicle ID (if known). Either VehicleId or VehicleRegNo must be provided.
    /// </summary>
    public Guid? VehicleId { get; set; }

    /// <summary>
    /// Vehicle registration number. Auto-creates vehicle if not found.
    /// </summary>
    [MaxLength(20)]
    public string? VehicleRegNo { get; set; }

    /// <summary>
    /// Cargo type ID.
    /// </summary>
    public Guid? CargoId { get; set; }

    /// <summary>
    /// Transporter ID.
    /// </summary>
    public Guid? TransporterId { get; set; }

    /// <summary>
    /// Driver ID.
    /// </summary>
    public Guid? DriverId { get; set; }

    /// <summary>
    /// Origin location ID.
    /// </summary>
    public Guid? OriginId { get; set; }

    /// <summary>
    /// Destination location ID.
    /// </summary>
    public Guid? DestinationId { get; set; }

    /// <summary>
    /// Consignment or delivery note reference number.
    /// </summary>
    [MaxLength(100)]
    public string? ConsignmentNo { get; set; }

    /// <summary>
    /// Purchase order, sales order, or dispatch order reference.
    /// </summary>
    [MaxLength(100)]
    public string? OrderReference { get; set; }

    /// <summary>
    /// Expected net weight from the order/dispatch (kg).
    /// </summary>
    public int? ExpectedNetWeightKg { get; set; }

    /// <summary>
    /// Container or trailer seal numbers (comma-separated).
    /// </summary>
    [MaxLength(200)]
    public string? SealNumbers { get; set; }

    /// <summary>
    /// Trailer registration number (for articulated vehicles).
    /// </summary>
    [MaxLength(20)]
    public string? TrailerRegNo { get; set; }

    /// <summary>
    /// Operator notes or observations.
    /// </summary>
    public string? Remarks { get; set; }

    /// <summary>
    /// Industry-specific metadata as JSON string.
    /// </summary>
    public string? IndustryMetadata { get; set; }

    /// <summary>
    /// Scale type for commercial weighing: "multideck" or "mobile".
    /// Defaults to "multideck" if not provided.
    /// </summary>
    [MaxLength(20)]
    public string? WeighingScaleType { get; set; }
}

/// <summary>
/// Request to capture the first weight (first pass on the scale).
/// </summary>
public class CaptureFirstWeightRequest
{
    /// <summary>
    /// Total measured weight in kg (GVW for multideck, sum of axles for mobile).
    /// </summary>
    [Required]
    [Range(1, 200000)]
    public int WeightKg { get; set; }

    /// <summary>
    /// Type of weight: "tare" or "gross".
    /// </summary>
    [Required]
    [MaxLength(10)]
    [RegularExpression("^(tare|gross)$", ErrorMessage = "WeightType must be 'tare' or 'gross'.")]
    public string WeightType { get; set; } = "gross";

    /// <summary>
    /// Individual axle/deck weights (optional). For mobile: per-axle kg values.
    /// For multideck: per-deck kg values (deck 1–4). Stored in weighing_axles.
    /// </summary>
    public List<int>? AxleWeights { get; set; }

    /// <summary>
    /// True when this weight was hand-entered by a supervisor (e.g. TruConnect lost the scale
    /// connection mid-capture) rather than read live from the scale. Requires the
    /// <c>manual_weight_override</c> permission and a non-empty <see cref="ManualEntryJustification"/>
    /// (enforced in the controller/service, not just here since it is conditionally required).
    /// </summary>
    public bool IsManualEntry { get; set; } = false;

    /// <summary>
    /// Required when <see cref="IsManualEntry"/> is true. Recorded onto the transaction's Remarks
    /// for audit purposes (see docs: "Scale fault during capture").
    /// </summary>
    [MaxLength(500)]
    public string? ManualEntryJustification { get; set; }
}

/// <summary>
/// Request to capture the second weight (second pass on the scale).
/// </summary>
public class CaptureSecondWeightRequest
{
    /// <summary>
    /// Total measured weight in kg. System auto-determines tare or gross
    /// based on the first weight type.
    /// </summary>
    [Required]
    [Range(1, 200000)]
    public int WeightKg { get; set; }

    /// <summary>
    /// Individual axle/deck weights for the second pass (optional).
    /// </summary>
    public List<int>? AxleWeights { get; set; }

    /// <summary>
    /// Expected net weight from order/dispatch (optional).
    /// Provided at second-weight capture so tolerance can be evaluated immediately.
    /// </summary>
    [Range(0, 200000)]
    public int? ExpectedNetWeightKg { get; set; }

    /// <summary>
    /// True when this weight was hand-entered by a supervisor (e.g. TruConnect lost the scale
    /// connection mid-capture) rather than read live from the scale. Requires the
    /// <c>manual_weight_override</c> permission and a non-empty <see cref="ManualEntryJustification"/>
    /// (enforced in the controller/service, not just here since it is conditionally required).
    /// </summary>
    public bool IsManualEntry { get; set; } = false;

    /// <summary>
    /// Required when <see cref="IsManualEntry"/> is true. Recorded onto the transaction's Remarks
    /// for audit purposes (see docs: "Scale fault during capture").
    /// </summary>
    [MaxLength(500)]
    public string? ManualEntryJustification { get; set; }
}

/// <summary>
/// Request to use a stored/preset tare weight instead of measuring.
/// </summary>
public class UseStoredTareRequest
{
    /// <summary>
    /// Optional override tare weight in kg. If null, the vehicle's stored tare is used.
    /// Providing this sets TareSource to "preset" and requires <see cref="Justification"/>.
    /// </summary>
    [Range(1, 100000)]
    public int? OverrideTareWeightKg { get; set; }

    /// <summary>
    /// Required when <see cref="OverrideTareWeightKg"/> is provided - the docs (tare-management.md)
    /// call this "Preset Tare" (a supervisor manually entering a tare weight instead of measuring
    /// or reusing a stored value) and require justification for audit purposes. Enforced in
    /// <c>CommercialWeighingService.UseStoredTareAsync</c>, not just here, since it is only
    /// conditionally required. Persisted onto the transaction's Remarks (no dedicated column).
    /// </summary>
    [MaxLength(500)]
    public string? Justification { get; set; }
}

/// <summary>
/// Request to update quality deduction on a completed commercial weighing.
/// </summary>
public class UpdateQualityDeductionRequest
{
    /// <summary>
    /// Quality deduction in kg (e.g., moisture, foreign matter). Used as-is only as a fallback
    /// when the cargo type has no MoistureTargetPercent/ForeignMatterLimitPercent configured, or
    /// when neither actual percentage below is supplied. When actual percentages ARE supplied and
    /// the cargo type has quality-deduction rules configured, the formula-computed value overrides
    /// this field server-side (see CommercialWeighingService.UpdateQualityDeductionAsync).
    /// </summary>
    [Required]
    [Range(0, 100000)]
    public int QualityDeductionKg { get; set; }

    /// <summary>
    /// Reason for the quality deduction.
    /// </summary>
    [MaxLength(500)]
    public string? Reason { get; set; }

    /// <summary>
    /// Actual measured moisture percentage for this load. When supplied and the cargo type has a
    /// MoistureTargetPercent configured, the moisture deduction is computed as
    /// NetWeightKg * (ActualMoisturePercent - CargoType.MoistureTargetPercent) / 100 (only when
    /// actual exceeds target, per setup.md's documented formula).
    /// </summary>
    [Range(0, 100)]
    public decimal? ActualMoisturePercent { get; set; }

    /// <summary>
    /// Actual measured foreign-matter percentage for this load. When supplied and the cargo type
    /// has a ForeignMatterLimitPercent configured, the foreign-matter deduction is computed as
    /// NetWeightKg * ActualForeignMatterPercent / 100 (only when actual exceeds the limit, per
    /// setup.md's documented formula).
    /// </summary>
    [Range(0, 100)]
    public decimal? ActualForeignMatterPercent { get; set; }
}

/// <summary>
/// Request to record a new tare weight history entry for a vehicle (Tare Register "Record Tare"
/// dialog). Distinct from <see cref="UseStoredTareRequest"/>, which consumes an existing/preset
/// tare during a two-pass weighing rather than logging a new measurement.
/// </summary>
public class RecordTareHistoryRequest
{
    [Required]
    public Guid VehicleId { get; set; }

    [Required]
    [Range(1, 100000)]
    public int TareWeightKg { get; set; }

    /// <summary>
    /// Source of this tare entry: "measured" (from scale) or "manual" (operator input).
    /// Matches the vehicle_tare_history.source check constraint (chk_tare_source).
    /// </summary>
    [Required]
    public string Source { get; set; } = "measured";

    /// <summary>
    /// Optional notes about this measurement. Required when <see cref="Source"/> is "manual" -
    /// a manually punched-in tare weight (not read off the scale) is treated the same as the
    /// "preset" tare override on <see cref="UseStoredTareRequest"/>, which also requires a
    /// recorded justification for audit purposes.
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// When true (default), also updates the vehicle's stored tare (Vehicle.LastTareWeightKg /
    /// LastTareWeighedAt) so it is used by single-pass commercial weighing going forward.
    /// </summary>
    public bool SetAsDefault { get; set; } = true;
}

/// <summary>
/// Request to reject a flagged tare anomaly (Phase 7 MVP). Reason is optional - rejection does not
/// change the already-recorded tare value (see CommercialWeighingService.RejectTareAnomalyAsync);
/// it simply records that a supervisor reviewed and dismissed the flag, expecting the vehicle's tare
/// to be re-verified on its next visit.
/// </summary>
public class ResolveTareAnomalyRequest
{
    [MaxLength(500)]
    public string? Reason { get; set; }
}

/// <summary>
/// Request to override a flagged tare anomaly with a supervisor-corrected tare value (Phase 7 MVP).
/// Mirrors <see cref="UseStoredTareRequest"/>'s required-justification pattern for preset tare
/// overrides. Updates the vehicle's stored tare going forward via the existing
/// CommercialWeighingService.RecordTareWeightAsync helper (which also logs a VehicleTareHistory
/// audit entry) - does NOT retroactively rewrite the flagged transaction's already-recorded
/// TareWeightKg/NetWeightKg (see CommercialWeighingService.OverrideTareAnomalyAsync for reasoning).
/// </summary>
public class OverrideTareAnomalyRequest
{
    [Required]
    [Range(1, 100000)]
    public int CorrectedTareWeightKg { get; set; }

    [Required]
    [MaxLength(500)]
    public string Justification { get; set; } = string.Empty;
}

/// <summary>
/// A single flagged, unresolved tare anomaly for the Tare Register "Pending Review" queue.
/// <see cref="AnchorType"/> distinguishes the two places an anomaly can be flagged (Phase 7 MVP):
/// "WeighingTransaction" for the two-pass capture / stored-tare-override paths (resolvable via
/// CommercialWeighingController's {id}/approve|reject|override-tare-anomaly, TicketNumber populated),
/// or "VehicleTareHistory" for the standalone Tare Register "Record Tare" dialog
/// (RecordTareHistoryEntryAsync), which isn't tied to any live transaction and is instead resolvable
/// via tare-history/{id}/approve|reject|override-tare-anomaly - see CommercialWeighingService for why
/// this entity was chosen as the fallback anchor. Callers should route each row's resolution action
/// to the matching endpoint family based on AnchorType.
/// </summary>
public class TareAnomalyDto
{
    public string AnchorType { get; set; } = "WeighingTransaction";
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public string? VehicleRegNo { get; set; }
    public string? TicketNumber { get; set; }
    public DateTime? FlaggedAt { get; set; }
    public string? Reason { get; set; }
    public int? TareWeightKg { get; set; }
    public Guid? StationId { get; set; }
    public string? StationName { get; set; }
}

// ── Response DTOs ──

/// <summary>
/// Full result DTO for a commercial weighing transaction.
/// </summary>
public class CommercialWeighingResultDto
{
    public Guid Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string ControlStatus { get; set; } = string.Empty;
    public string WeighingMode { get; set; } = "commercial";
    public string? WeighingScaleType { get; set; }

    // Vehicle info
    public Guid VehicleId { get; set; }
    public string VehicleRegNumber { get; set; } = string.Empty;
    public string? VehicleMake { get; set; }
    public string? VehicleModel { get; set; }
    public string? TrailerRegNo { get; set; }

    // People
    public Guid? DriverId { get; set; }
    public string? DriverName { get; set; }
    public Guid? TransporterId { get; set; }
    public string? TransporterName { get; set; }
    public string? WeighedByUserName { get; set; }

    // Station
    public Guid StationId { get; set; }
    public string? StationName { get; set; }

    // Weight fields
    public int? FirstWeightKg { get; set; }
    public string? FirstWeightType { get; set; }
    public DateTime? FirstWeightAt { get; set; }

    public int? SecondWeightKg { get; set; }
    public string? SecondWeightType { get; set; }
    public DateTime? SecondWeightAt { get; set; }

    public int? TareWeightKg { get; set; }
    public int? GrossWeightKg { get; set; }
    public int? NetWeightKg { get; set; }
    public string? TareSource { get; set; }

    // Quality and adjustments
    public int? QualityDeductionKg { get; set; }
    public int? AdjustedNetWeightKg { get; set; }

    /// <summary>Actual measured moisture percentage that drove MoistureDeductionKg, when supplied.</summary>
    public decimal? ActualMoisturePercent { get; set; }

    /// <summary>Actual measured foreign-matter percentage that drove ForeignMatterDeductionKg, when supplied.</summary>
    public decimal? ActualForeignMatterPercent { get; set; }

    /// <summary>Moisture portion of QualityDeductionKg, computed from the setup.md formula.</summary>
    public decimal? MoistureDeductionKg { get; set; }

    /// <summary>Foreign-matter portion of QualityDeductionKg, computed from the setup.md formula.</summary>
    public decimal? ForeignMatterDeductionKg { get; set; }

    /// <summary>
    /// Which deduction type(s) drove QualityDeductionKg — derived from the stored measurements
    /// rather than a separate enum column: "moisture" / "foreignMatter" (formula-driven) or
    /// "manual" (flat kg entry with no actual percentages, e.g. cargo type has no quality rules).
    /// </summary>
    public List<string> QualityDeductionTypesApplied { get; set; } = new();

    // Order/consignment
    public string? ConsignmentNo { get; set; }
    public string? OrderReference { get; set; }
    public int? ExpectedNetWeightKg { get; set; }
    public int? WeightDiscrepancyKg { get; set; }
    public string? SealNumbers { get; set; }
    public string? Remarks { get; set; }

    // Route & Cargo
    public Guid? OriginId { get; set; }
    public string? SourceLocation { get; set; }
    public Guid? DestinationId { get; set; }
    public string? DestinationLocation { get; set; }
    public Guid? CargoId { get; set; }
    public string? CargoType { get; set; }

    // Tolerance
    public bool ToleranceExceeded { get; set; }
    public string? ToleranceDisplay { get; set; }
    public bool ToleranceExceptionApproved { get; set; }
    public Guid? ToleranceExceptionApprovedBy { get; set; }
    public DateTime? ToleranceExceptionApprovedAt { get; set; }

    // Tare anomaly (Phase 7 MVP - drift vs. stored tare only)
    public DateTime? TareAnomalyFlaggedAt { get; set; }
    public string? TareAnomalyReason { get; set; }
    public Guid? TareAnomalyResolvedByUserId { get; set; }
    public DateTime? TareAnomalyResolvedAt { get; set; }
    public string? TareAnomalyResolution { get; set; }

    // Axle / deck weights (stored per-pass in weighing_axles)
    public List<CommercialAxleWeightDto> FirstPassAxles { get; set; } = new();
    public List<CommercialAxleWeightDto> SecondPassAxles { get; set; } = new();

    // Invoice / payment (set after weighing completes)
    public string? InvoiceNo { get; set; }
    public string? InvoiceStatus { get; set; }
    public decimal? InvoiceAmountKes { get; set; }
    public string? TreasuryIntentId { get; set; }
    public string? TreasuryPaymentUrl { get; set; }

    // Metadata
    public string? IndustryMetadata { get; set; }
    public DateTime WeighedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Void info
    public DateTime? VoidedAt { get; set; }
    public string? VoidReason { get; set; }
}

public class CommercialAxleWeightDto
{
    public int AxleNumber { get; set; }
    public int WeightKg { get; set; }
    public string Pass { get; set; } = "first";
}

/// <summary>
/// CRUD DTO for commercial tolerance settings.
/// </summary>
public class CommercialToleranceSettingDto
{
    public Guid? Id { get; set; }

    /// <summary>
    /// Type of tolerance: "percentage" or "absolute".
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string ToleranceType { get; set; } = "percentage";

    /// <summary>
    /// Tolerance value. If percentage, e.g. 0.5 means 0.5%. If absolute, value in kg.
    /// </summary>
    [Required]
    public decimal ToleranceValue { get; set; }

    /// <summary>
    /// Maximum tolerance cap in kg (applies when using percentage). Null means no cap.
    /// </summary>
    public int? MaxToleranceKg { get; set; }

    /// <summary>
    /// Optional: scope to a specific cargo type.
    /// </summary>
    public Guid? CargoTypeId { get; set; }
    public string? CargoTypeName { get; set; }

    /// <summary>
    /// Description or label for this tolerance rule.
    /// </summary>
    [MaxLength(200)]
    public string? Description { get; set; }
}

/// <summary>Request to void a pending commercial weighing transaction.</summary>
public class VoidCommercialWeighingRequest
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Vehicle tare weight history entry.
/// </summary>
public class VehicleTareHistoryDto
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public string? VehicleRegNo { get; set; }
    public int TareWeightKg { get; set; }
    public DateTime WeighedAt { get; set; }
    public Guid? StationId { get; set; }
    public string? StationName { get; set; }
    public string Source { get; set; } = "measured";
    public string? Notes { get; set; }
    public Guid? RecordedByUserId { get; set; }
    public string? RecordedByName { get; set; }

    // Tare anomaly (Phase 7 MVP) - set when this entry was flagged via RecordTareHistoryEntryAsync
    // (the standalone "Record Tare" dialog isn't tied to a WeighingTransaction, so it's flagged here).
    public DateTime? TareAnomalyFlaggedAt { get; set; }
    public string? TareAnomalyReason { get; set; }

    /// <summary>User ID of the supervisor who resolved (approved/rejected/overrode) the flagged anomaly.</summary>
    public Guid? TareAnomalyResolvedByUserId { get; set; }

    /// <summary>Timestamp when the anomaly was resolved. Null while still pending review.</summary>
    public DateTime? TareAnomalyResolvedAt { get; set; }

    /// <summary>Resolution outcome text (see WeighingTransaction.TareAnomalyResolution for format).</summary>
    public string? TareAnomalyResolution { get; set; }
}
