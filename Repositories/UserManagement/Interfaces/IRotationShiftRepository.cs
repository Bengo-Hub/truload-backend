using TruLoad.Backend.Models;

namespace TruLoad.Backend.Repositories.UserManagement.Interfaces;

public interface IRotationShiftRepository
{
    Task<IEnumerable<RotationShift>> GetByRotationIdAsync(Guid rotationId, CancellationToken cancellationToken = default);
    Task<RotationShift?> GetByRotationAndWorkShiftAsync(Guid rotationId, Guid workShiftId, CancellationToken cancellationToken = default);
    Task<RotationShift> CreateAsync(RotationShift rotationShift, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid rotationId, Guid workShiftId, CancellationToken cancellationToken = default);
    Task DeleteAllByRotationIdAsync(Guid rotationId, CancellationToken cancellationToken = default);
}
