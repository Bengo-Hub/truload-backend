using TruLoad.Backend.Models;

namespace TruLoad.Backend.Repositories.UserManagement.Interfaces;

public interface IShiftRotationRepository
{
    Task<ShiftRotation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ShiftRotation?> GetByIdWithShiftsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<ShiftRotation>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<IEnumerable<ShiftRotation>> GetAllWithShiftsAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<ShiftRotation> CreateAsync(ShiftRotation shiftRotation, CancellationToken cancellationToken = default);
    Task<ShiftRotation> UpdateAsync(ShiftRotation shiftRotation, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> TitleExistsAsync(string title, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
