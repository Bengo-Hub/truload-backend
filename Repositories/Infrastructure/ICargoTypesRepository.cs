using TruLoad.Backend.Models;

namespace TruLoad.Backend.Repositories.Infrastructure;

/// <summary>
/// Repository for cargo types master data
/// </summary>
public interface ICargoTypesRepository
{
    Task<List<CargoTypes>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<List<CargoTypes>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<CargoTypes?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CargoTypes?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<List<CargoTypes>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);
    Task<CargoTypes> CreateAsync(CargoTypes cargoType, CancellationToken cancellationToken = default);
    Task<CargoTypes> UpdateAsync(CargoTypes cargoType, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
