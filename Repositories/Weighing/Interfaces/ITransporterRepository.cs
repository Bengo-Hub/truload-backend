using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Repositories.Weighing.Interfaces;

/// <summary>
/// Repository for transporter master data
/// </summary>
public interface ITransporterRepository
{
    Task<List<Transporter>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<List<Transporter>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Transporter?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Transporter?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<List<Transporter>> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task<Transporter> CreateAsync(Transporter transporter, CancellationToken cancellationToken = default);
    Task<Transporter> UpdateAsync(Transporter transporter, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
