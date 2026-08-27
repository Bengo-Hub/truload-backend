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

    /// <summary>
    /// Checks whether <paramref name="code"/> is already taken within a specific uniqueness scope:
    /// the shared/global catalog when <paramref name="organizationId"/> is null, or that org's own
    /// private catalog when it isn't. Mirrors the two filtered unique indexes on (OrganizationId,
    /// Code) - use this (not <see cref="GetByCodeAsync"/>, which looks across every scope) for
    /// duplicate-code checks on Create/Update.
    /// </summary>
    Task<bool> CodeExistsInScopeAsync(string code, Guid? organizationId, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<List<CargoTypes>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);
    Task<CargoTypes> CreateAsync(CargoTypes cargoType, CancellationToken cancellationToken = default);
    Task<CargoTypes> UpdateAsync(CargoTypes cargoType, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
