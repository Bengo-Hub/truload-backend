using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Data.Repositories.Weighing;

public interface IWeighingRepository
{
    Task<WeighingTransaction> CreateTransactionAsync(WeighingTransaction transaction);
    Task<WeighingTransaction?> GetTransactionByIdAsync(Guid id);
    Task<WeighingTransaction?> GetTransactionByTicketNumberAsync(string ticketNumber);
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
