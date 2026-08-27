using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.DTOs.Weighing;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Services.Interfaces.Weighing;

/// <summary>
/// Service for commercial (factory/industry) weighing operations.
/// Handles two-pass weighing, stored tare, net weight calculation, and tolerance checks.
/// Does NOT include enforcement-specific logic (compliance, prosecution, yard, fees).
/// </summary>
public interface ICommercialWeighingService
{
    /// <summary>
    /// Initiates a commercial weighing transaction with weighing_mode="commercial".
    /// Generates ticket number via DocumentSequence and sets control_status to "Pending".
    /// </summary>
    Task<WeighingTransaction> InitiateCommercialWeighingAsync(
        InitiateCommercialWeighingRequest request,
        Guid userId);

    /// <summary>
    /// Captures the first weight (first pass on the scale).
    /// Sets FirstWeightKg, FirstWeightType, FirstWeightAt.
    /// </summary>
    Task<WeighingTransaction> CaptureFirstWeightAsync(
        Guid transactionId,
        CaptureFirstWeightRequest request);

    /// <summary>
    /// Captures the second weight (second pass on the scale).
    /// Auto-determines tare/gross, calculates net = gross - tare.
    /// Sets control_status to "Complete". Updates vehicle tare if tare was measured.
    /// Checks commercial tolerance and calculates discrepancy if expected weight provided.
    /// </summary>
    Task<WeighingTransaction> CaptureSecondWeightAsync(
        Guid transactionId,
        CaptureSecondWeightRequest request);

    /// <summary>
    /// Uses the vehicle's stored or preset tare weight to calculate net weight.
    /// Sets tare_source to "stored" or "preset".
    /// </summary>
    Task<WeighingTransaction> UseStoredTareAsync(
        Guid transactionId,
        UseStoredTareRequest request);

    /// <summary>
    /// Gets the full commercial weighing result for a transaction.
    /// </summary>
    Task<CommercialWeighingResultDto> GetCommercialResultAsync(Guid transactionId);

    /// <summary>
    /// Records a tare weight measurement for a vehicle: adds a VehicleTareHistory entry and,
    /// when <paramref name="setAsDefault"/> is true (the default), updates Vehicle.LastTareWeightKg /
    /// Vehicle.LastTareWeighedAt so it becomes the vehicle's stored tare for single-pass weighing.
    /// Does not call SaveChangesAsync - the caller persists as part of its own unit of work.
    /// Returns null (and logs a warning) if the vehicle doesn't exist.
    /// </summary>
    Task<VehicleTareHistory?> RecordTareWeightAsync(
        Guid vehicleId,
        int tareWeightKg,
        Guid? stationId,
        string source = "measured",
        string? notes = null,
        Guid? recordedByUserId = null,
        bool setAsDefault = true);

    /// <summary>
    /// Records a new tare weight history entry for a vehicle (Tare Register "Record Tare" dialog).
    /// Calls SaveChangesAsync itself (unlike RecordTareWeightAsync, which is a shared building block
    /// used mid-transaction elsewhere). Requires a justification (passed via Source == "manual"'s
    /// Notes) for manually-entered tare weights, same rule as UseStoredTareAsync's preset override.
    /// Throws KeyNotFoundException if the vehicle doesn't exist.
    /// </summary>
    Task<VehicleTareHistoryDto> RecordTareHistoryEntryAsync(RecordTareHistoryRequest request, Guid? recordedByUserId);

    /// <summary>
    /// Updates quality deduction and recalculates adjusted net weight.
    /// </summary>
    Task<WeighingTransaction> UpdateQualityDeductionAsync(
        Guid transactionId,
        UpdateQualityDeductionRequest request);

    /// <summary>
    /// Gets tare weight history for a vehicle.
    /// </summary>
    Task<List<VehicleTareHistoryDto>> GetVehicleTareHistoryAsync(Guid vehicleId);

    /// <summary>
    /// Gets all commercial tolerance settings for the current organization.
    /// </summary>
    Task<List<CommercialToleranceSettingDto>> GetCommercialToleranceSettingsAsync();

    /// <summary>
    /// Creates a new commercial tolerance setting.
    /// </summary>
    Task<CommercialToleranceSettingDto> CreateCommercialToleranceSettingAsync(CommercialToleranceSettingDto dto);

    /// <summary>
    /// Updates an existing commercial tolerance setting.
    /// </summary>
    Task<CommercialToleranceSettingDto> UpdateCommercialToleranceSettingAsync(Guid id, CommercialToleranceSettingDto dto);

    /// <summary>
    /// Deletes a commercial tolerance setting. Throws KeyNotFoundException if not found or belongs to a different org.
    /// </summary>
    Task DeleteCommercialToleranceSettingAsync(Guid id);

    /// <summary>
    /// Approves a tolerance exception for a transaction where discrepancy exceeded configured bands.
    /// Requires weighing.override permission.
    /// </summary>
    Task<CommercialWeighingResultDto> ApproveToleranceExceptionAsync(Guid transactionId, Guid approvedByUserId);

    /// <summary>
    /// Rejects a tolerance exception for a transaction where discrepancy exceeded configured bands.
    /// Reuses the Void state transition (ControlStatus becomes "Voided") with a fixed, recorded
    /// reason so the rejection is auditable via the same VoidReason field Void already uses.
    /// Requires weighing.override permission (same policy as Approve).
    /// </summary>
    Task<CommercialWeighingResultDto> RejectToleranceExceptionAsync(Guid transactionId, string? reason, Guid rejectedByUserId);

    /// <summary>
    /// Voids a pending commercial weighing transaction.
    /// </summary>
    Task<CommercialWeighingResultDto> VoidCommercialWeighingAsync(Guid transactionId, VoidCommercialWeighingRequest request, Guid voidedByUserId);

    /// <summary>
    /// Gets pending commercial weighing transactions (first weight captured, awaiting second pass) for a station.
    /// </summary>
    Task<List<CommercialWeighingResultDto>> GetPendingCommercialTransactionsAsync(Guid stationId);

    /// <summary>
    /// Finds open first-weight-only transactions for a vehicle plate within the configured time threshold.
    /// Used by the capture screen to detect vehicles that need a second pass rather than a new transaction.
    /// When <paramref name="thresholdHours"/> is null, falls back to the configured
    /// commercial.pending_weighing_threshold_hours setting (same setting used by StaleWeighingNotificationJob).
    /// </summary>
    Task<List<CommercialWeighingResultDto>> GetPendingByPlateAsync(string vehicleRegNo, int? thresholdHours = null);

    // ============================================================================
    // Tare Anomaly Detection (Phase 7 MVP - drift vs. stored tare only)
    // ============================================================================

    /// <summary>
    /// Approves a flagged tare anomaly as-is: stamps resolver/timestamp/resolution ("Approved") and
    /// removes it from the unresolved review queue. TareAnomalyFlaggedAt/Reason are left untouched
    /// (permanent audit trail), mirroring ToleranceExceeded/ToleranceExceptionApproved on the same
    /// entity. Requires weighing.override permission (same policy as ApproveToleranceExceptionAsync).
    /// </summary>
    Task<CommercialWeighingResultDto> ApproveTareAnomalyAsync(Guid transactionId, Guid approvedByUserId);

    /// <summary>
    /// Rejects a flagged tare anomaly: stamps resolver/timestamp/resolution ("Rejected[: reason]")
    /// and removes it from the unresolved review queue. Does NOT retroactively change the already-
    /// recorded tare value on this transaction (a data-integrity call - see Stage C report) - the
    /// expectation is the vehicle's tare is re-verified on its next visit. Requires weighing.override
    /// permission (same policy as Approve).
    /// </summary>
    Task<CommercialWeighingResultDto> RejectTareAnomalyAsync(Guid transactionId, string? reason, Guid rejectedByUserId);

    /// <summary>
    /// Overrides a flagged tare anomaly with a supervisor-corrected tare value (required
    /// justification, same pattern as UseStoredTareAsync's preset-tare override). Updates the
    /// vehicle's stored tare going forward (via RecordTareWeightAsync, which also logs a
    /// VehicleTareHistory audit entry) but does NOT retroactively rewrite this transaction's already-
    /// recorded TareWeightKg/NetWeightKg/fees (see Stage C report). Requires weighing.override
    /// permission.
    /// </summary>
    Task<CommercialWeighingResultDto> OverrideTareAnomalyAsync(Guid transactionId, OverrideTareAnomalyRequest request, Guid overriddenByUserId);

    /// <summary>
    /// Lists flagged, unresolved tare anomalies for the current organization (Tare Register "Pending
    /// Review" queue) - combines both anchor types (WeighingTransaction and VehicleTareHistory, see
    /// TareAnomalyDto), newest-flagged first, optionally filtered to a station.
    /// </summary>
    Task<PagedResponse<TareAnomalyDto>> GetFlaggedTareAnomaliesAsync(Guid? stationId, int pageNumber, int pageSize);
}
