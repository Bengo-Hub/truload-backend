using TruLoad.Backend.Models;

namespace TruLoad.Backend.Repositories.Interfaces;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<List<Role>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<List<Role>> GetByIdsAsync(List<Guid> ids, CancellationToken cancellationToken = default);
    Task<Role> CreateAsync(Role role, CancellationToken cancellationToken = default);
    Task<Role> UpdateAsync(Role role, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
