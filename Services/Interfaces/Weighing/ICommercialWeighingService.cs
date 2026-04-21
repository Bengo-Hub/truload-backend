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
    /// Records a tare weight measurement for a vehicle.
    /// Updates Vehicle.LastTareWeightKg, Vehicle.LastTareWeighedAt, and adds VehicleTareHistory entry.
    /// </summary>
    Task RecordTareWeightAsync(Guid vehicleId, int tareWeightKg, Guid? stationId, string source = "measured", string? notes = null);

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
}
