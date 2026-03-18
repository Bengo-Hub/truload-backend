using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Data.Repositories.Weighing;

public interface IWeighingRepository
{
    Task<WeighingTransaction> CreateTransactionAsync(WeighingTransaction transaction);
    Task<WeighingTransaction?> GetTransactionByIdAsync(Guid id);
    Task<WeighingTransaction?> GetTransactionByTicketNumberAsync(string ticketNumber);
    Task<WeighingTransaction> UpdateTransactionAsync(WeighingTransaction transaction);
    Task SaveTransactionWithNewAxlesAsync(WeighingTransaction transaction);
    Task DeleteAxlesByTransactionIdAsync(Guid transactionId);
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
        string sortOrder = "desc",
        string? weighingType = null);

    /// <summary>
    /// Lightweight search without navigation property includes.
    /// Used for statistics/aggregation queries where related entities are not needed.
    /// </summary>
    Task<(List<WeighingTransaction> Items, int TotalCount)> SearchTransactionsLightAsync(
        Guid? stationId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int take = 10000);

    /// <summary>
    /// Gets a transaction by client-generated local ID for offline idempotency.
    /// </summary>
    Task<WeighingTransaction?> GetByClientLocalIdAsync(string clientLocalId);

    /// <summary>
    /// Gets the most recent weighing transaction for a vehicle (by vehicle ID).
    /// Used to copy driver, transporter, origin, destination, cargo, road, location from last weighing when creating a new transaction.
    /// </summary>
    Task<WeighingTransaction?> GetLastWeighingByVehicleAsync(Guid vehicleId, int limit = 1);

    /// <summary>
    /// Gets the latest auto-weigh transaction for a vehicle at a station.
    /// Used to find existing auto-weigh records when frontend submits final capture.
    /// </summary>
    /// <param name="vehicleRegNumber">Vehicle registration number</param>
    /// <param name="stationId">Station ID</param>
    /// <param name="bound">Optional bound for bidirectional stations</param>
    /// <returns>Latest auto-weigh transaction or null</returns>
    Task<WeighingTransaction?> GetLatestAutoweighByVehicleAsync(string vehicleRegNumber, Guid stationId, string? bound = null);

    /// <summary>
    /// Marks all pending auto-weigh transactions for a vehicle at station as not_weighed.
    /// Called when a new weighing session starts without completing the previous one.
    /// </summary>
    /// <param name="vehicleRegNumber">Vehicle registration number (if null, marks all pending for station)</param>
    /// <param name="stationId">Station ID</param>
    /// <param name="excludeTransactionId">Transaction ID to exclude from marking</param>
    /// <returns>Number of transactions marked</returns>
    Task<int> MarkPendingAsNotWeighedAsync(string? vehicleRegNumber, Guid stationId, Guid? excludeTransactionId = null);
}
