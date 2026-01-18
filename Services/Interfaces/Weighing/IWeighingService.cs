using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Models;

namespace TruLoad.Backend.Services.Interfaces.Weighing;

public interface IWeighingService
{
    Task<WeighingTransaction> InitiateWeighingAsync(string ticketNumber, Guid stationId, Guid userId);
    Task<WeighingTransaction> InitiateReweighAsync(Guid originalTransactionId, string ticketNumber, Guid userId);
    Task<WeighingTransaction> CaptureWeightsAsync(Guid transactionId, List<WeighingAxle> axles);
    Task<WeighingTransaction> CalculateComplianceAsync(Guid transactionId);
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
