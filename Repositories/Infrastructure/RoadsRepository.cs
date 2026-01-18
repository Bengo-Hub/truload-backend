using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Repositories.Infrastructure;

public class RoadsRepository : IRoadsRepository
{
    private readonly TruLoadDbContext _context;

    public RoadsRepository(TruLoadDbContext context)
    {
        _context = context;
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

    public async Task<List<Roads>> GetByDistrictAsync(Guid districtId, CancellationToken cancellationToken = default)
    {
        return await _context.Roads
            .Where(r => r.DistrictId == districtId && r.IsActive && r.DeletedAt == null)
            .OrderBy(r => r.Name)
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
