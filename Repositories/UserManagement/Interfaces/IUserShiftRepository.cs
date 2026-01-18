using TruLoad.Backend.Models;

namespace TruLoad.Backend.Repositories.UserManagement.Interfaces;

public interface IUserShiftRepository
{
    Task<UserShift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<UserShift>> GetByUserIdAsync(Guid userId, bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<UserShift?> GetActiveShiftForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<UserShift>> GetByWorkShiftIdAsync(Guid workShiftId, bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<IEnumerable<UserShift>> GetByShiftRotationIdAsync(Guid shiftRotationId, bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<UserShift> CreateAsync(UserShift userShift, CancellationToken cancellationToken = default);
    Task<UserShift> UpdateAsync(UserShift userShift, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> HasActiveShiftAsync(Guid userId, CancellationToken cancellationToken = default);
}
