using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Data.Repositories.Weighing;

public interface IPermitRepository
{
    Task<Permit?> GetActivePermitForVehicleAsync(Guid vehicleId);
    Task<Permit?> GetByIdAsync(Guid id);
    Task<Permit?> GetByPermitNoAsync(string permitNo);
    Task<IEnumerable<Permit>> GetByVehicleIdAsync(Guid vehicleId);
    Task<Permit> CreateAsync(Permit permit);
    Task UpdateAsync(Permit permit);
}
