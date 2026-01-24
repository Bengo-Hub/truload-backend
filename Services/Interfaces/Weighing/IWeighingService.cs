using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Models;
using TruLoad.Backend.DTOs.Weighing;

namespace TruLoad.Backend.Services.Interfaces.Weighing;

public interface IWeighingService
{
    /// <summary>
    /// Initiates a weighing transaction (basic - for backward compatibility).
    /// </summary>
    Task<WeighingTransaction> InitiateWeighingAsync(string ticketNumber, Guid stationId, Guid userId);

    /// <summary>
    /// Initiates a weighing transaction with scale test validation.
    /// Per FRD: Scale test must be completed once daily per station/bound before weighing operations.
    /// </summary>
    /// <param name="ticketNumber">Unique ticket identifier</param>
    /// <param name="stationId">Station where weighing occurs</param>
    /// <param name="userId">User performing the weighing</param>
    /// <param name="bound">Direction/bound for bidirectional stations (A or B)</param>
    /// <param name="scaleTestId">Optional scale test ID (auto-resolved if not provided)</param>
    /// <returns>The created weighing transaction</returns>
    /// <exception cref="InvalidOperationException">Thrown if no valid scale test exists for today</exception>
    Task<WeighingTransaction> InitiateWeighingAsync(
        string ticketNumber,
        Guid stationId,
        Guid userId,
        string? bound,
        Guid? scaleTestId);

    Task<WeighingTransaction> InitiateReweighAsync(Guid originalTransactionId, string ticketNumber, Guid userId);
    Task<WeighingTransaction> CaptureWeightsAsync(Guid transactionId, List<WeighingAxle> axles);
    Task<WeighingTransaction> CalculateComplianceAsync(Guid transactionId);

    /// <summary>
    /// Gets detailed compliance result including axle groups, fees, and demerit points
    /// Implements Kenya Traffic Act Cap 403 Section 117A for NTSA license management
    /// </summary>
    Task<WeighingComplianceResultDto> GetComplianceResultAsync(Guid transactionId);

    Task<WeighingTransaction?> GetTransactionAsync(Guid id);
    Task<WeighingTransaction> UpdateTransactionAsync(WeighingTransaction transaction);
    Task DeleteTransactionAsync(Guid id);
    Task<(List<WeighingTransaction> Items, int TotalCount)> SearchTransactionsAsync(
        Guid? stationId = null,
        string? vehicleRegNo = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? controlStatus = null,
        bool? isCompliant = null,
        Guid? operatorId = null,
        int skip = 0,
        int take = 50,
        string sortBy = "WeighedAt",
        string sortOrder = "desc");
}
