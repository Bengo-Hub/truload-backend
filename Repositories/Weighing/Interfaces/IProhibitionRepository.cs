using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Data.Repositories.Weighing;

public interface IProhibitionRepository
{
    Task<ProhibitionOrder?> GetByWeighingIdAsync(Guid weighingId);
    Task<ProhibitionOrder> CreateAsync(ProhibitionOrder order);
    Task UpdateAsync(ProhibitionOrder order);
    Task<string> GenerateProhibitionNumberAsync();
}
