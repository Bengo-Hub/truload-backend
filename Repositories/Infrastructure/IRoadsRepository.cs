using TruLoad.Backend.Models;

namespace TruLoad.Backend.Repositories.Infrastructure;

/// <summary>
/// Repository for roads master data
/// </summary>
public interface IRoadsRepository
{
    Task<(List<Roads> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, bool includeInactive = false, string? search = null, CancellationToken cancellationToken = default);
    Task<List<Roads>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<List<Roads>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Roads?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Roads?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<List<Roads>> GetByRoadClassAsync(string roadClass, CancellationToken cancellationToken = default);
    Task<List<Roads>> GetByDistrictAsync(Guid districtId, CancellationToken cancellationToken = default);
    Task<Roads> CreateAsync(Roads road, CancellationToken cancellationToken = default);
    Task<Roads> UpdateAsync(Roads road, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
