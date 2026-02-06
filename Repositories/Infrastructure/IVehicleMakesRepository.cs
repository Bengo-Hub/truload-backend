using TruLoad.Backend.Models.Infrastructure;

namespace TruLoad.Backend.Repositories.Infrastructure;

/// <summary>
/// Repository for vehicle makes master data
/// </summary>
public interface IVehicleMakesRepository
{
    Task<List<VehicleMake>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<List<VehicleMake>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<VehicleMake?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<VehicleMake?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<List<VehicleMake>> GetByCountryAsync(string country, CancellationToken cancellationToken = default);
    Task<VehicleMake> CreateAsync(VehicleMake make, CancellationToken cancellationToken = default);
    Task<VehicleMake> UpdateAsync(VehicleMake make, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
