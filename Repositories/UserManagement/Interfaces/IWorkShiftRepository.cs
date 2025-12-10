using TruLoad.Backend.Models;

namespace TruLoad.Backend.Repositories.UserManagement.Interfaces;

public interface IWorkShiftRepository
{
    Task<WorkShift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkShift?> GetByIdWithSchedulesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkShift>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<IEnumerable<WorkShift>> GetAllWithSchedulesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<WorkShift?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<WorkShift> CreateAsync(WorkShift workShift, CancellationToken cancellationToken = default);
    Task<WorkShift> UpdateAsync(WorkShift workShift, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
}




