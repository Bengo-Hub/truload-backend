using TruLoad.Backend.Models;

namespace TruLoad.Backend.Repositories.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByAuthServiceUserIdAsync(Guid authServiceUserId, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<User>> GetByStationIdAsync(Guid stationId, CancellationToken cancellationToken = default);
    Task<List<User>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<List<User>> SearchAsync(string? search, string? status, Guid? stationId, int skip, int take, CancellationToken cancellationToken = default);
    Task<int> CountAsync(string? search, string? status, Guid? stationId, CancellationToken cancellationToken = default);
    Task<User> CreateAsync(User user, CancellationToken cancellationToken = default);
    Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
