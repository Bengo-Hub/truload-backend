using TruLoad.Backend.Models;

namespace TruLoad.Backend.Repositories.Interfaces;

public interface IStationRepository
{
    Task<Station?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Station>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Station?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IEnumerable<Station>> GetByTypeAsync(string stationType, CancellationToken cancellationToken = default);
    Task<IEnumerable<Station>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<Station> CreateAsync(Station station, CancellationToken cancellationToken = default);
    Task<Station> UpdateAsync(Station station, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);
}




