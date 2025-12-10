using TruLoad.Backend.Models;

namespace TruLoad.Backend.Repositories.UserManagement.Interfaces;

public interface IDepartmentRepository
{
    Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Department>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<IEnumerable<Department>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<Department?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<Department> CreateAsync(Department department, CancellationToken cancellationToken = default);
    Task<Department> UpdateAsync(Department department, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);
}




