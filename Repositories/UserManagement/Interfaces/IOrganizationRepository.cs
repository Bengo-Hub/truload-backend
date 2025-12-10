using TruLoad.Backend.Models;

namespace TruLoad.Backend.Repositories.UserManagement.Interfaces;

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Organization?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<List<Organization>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Organization> CreateAsync(Organization organization, CancellationToken cancellationToken = default);
    Task<Organization> UpdateAsync(Organization organization, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
