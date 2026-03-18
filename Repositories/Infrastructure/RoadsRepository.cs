using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.Infrastructure;

namespace TruLoad.Backend.Repositories.Infrastructure;

public class RoadsRepository : IRoadsRepository
{
    private readonly TruLoadDbContext _context;

    public RoadsRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<(List<Roads> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, bool includeInactive = false, string? search = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Roads.Where(r => r.DeletedAt == null);
        if (!includeInactive)
            query = query.Where(r => r.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(r =>
                (r.Code != null && r.Code.ToLower().Contains(term)) ||
                (r.Name != null && r.Name.ToLower().Contains(term)) ||
                (r.RoadClass != null && r.RoadClass.ToLower().Contains(term)));
        }
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(r => r.RoadClass)
            .ThenBy(r => r.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, totalCount);
    }

    public async Task<List<Roads>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Roads
            .Where(r => r.IsActive && r.DeletedAt == null)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Roads>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Roads.Where(r => r.DeletedAt == null);
        
        if (!includeInactive)
            query = query.Where(r => r.IsActive);

        return await query.OrderBy(r => r.Name).ToListAsync(cancellationToken);
    }

    public async Task<Roads?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Roads
            .FirstOrDefaultAsync(r => r.Id == id && r.DeletedAt == null, cancellationToken);
    }

    public async Task<Roads?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Roads
            .FirstOrDefaultAsync(r => r.Code == code && r.DeletedAt == null, cancellationToken);
    }

    public async Task<List<Roads>> GetByRoadClassAsync(string roadClass, CancellationToken cancellationToken = default)
    {
        return await _context.Roads
            .Where(r => r.RoadClass == roadClass && r.IsActive && r.DeletedAt == null)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Roads>> GetBySubcountyAsync(Guid subcountyId, CancellationToken cancellationToken = default)
    {
        return await _context.RoadSubcounties
            .Where(rs => rs.SubcountyId == subcountyId)
            .Join(_context.Roads.Where(r => r.IsActive && r.DeletedAt == null), rs => rs.RoadId, r => r.Id, (rs, r) => r)
            .OrderBy(r => r.Name)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Roads>> GetByCountyAsync(Guid countyId, CancellationToken cancellationToken = default)
    {
        return await _context.RoadCounties
            .Where(rc => rc.CountyId == countyId)
            .Select(rc => rc.Road)
            .Where(r => r.IsActive && r.DeletedAt == null)
            .OrderBy(r => r.Name)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<Roads> CreateAsync(Roads road, CancellationToken cancellationToken = default)
    {
        road.Id = Guid.NewGuid();
        road.CreatedAt = DateTime.UtcNow;
        road.UpdatedAt = DateTime.UtcNow;

        _context.Roads.Add(road);
        await _context.SaveChangesAsync(cancellationToken);

        return road;
    }

    public async Task<Roads> UpdateAsync(Roads road, CancellationToken cancellationToken = default)
    {
        road.UpdatedAt = DateTime.UtcNow;

        _context.Roads.Update(road);
        await _context.SaveChangesAsync(cancellationToken);

        return road;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var road = await _context.Roads.FindAsync(new object[] { id }, cancellationToken);
        if (road == null) return false;

        road.DeletedAt = DateTime.UtcNow;
        road.UpdatedAt = DateTime.UtcNow;
        road.IsActive = false;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
