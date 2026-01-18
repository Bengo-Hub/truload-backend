using TruLoad.Backend.Models;

namespace TruLoad.Backend.Repositories.Infrastructure;

/// <summary>
/// Repository for origins and destinations master data
/// </summary>
public interface IOriginsDestinationsRepository
{
    Task<List<OriginsDestinations>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<List<OriginsDestinations>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<OriginsDestinations?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OriginsDestinations?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<List<OriginsDestinations>> GetByCountryAsync(string country, CancellationToken cancellationToken = default);
    Task<List<OriginsDestinations>> GetByLocationTypeAsync(string locationType, CancellationToken cancellationToken = default);
    Task<OriginsDestinations> CreateAsync(OriginsDestinations location, CancellationToken cancellationToken = default);
    Task<OriginsDestinations> UpdateAsync(OriginsDestinations location, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
