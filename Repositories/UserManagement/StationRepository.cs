using Microsoft.EntityFrameworkCore;
using truload_backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Interfaces;

namespace TruLoad.Backend.Repositories;

public class StationRepository : IStationRepository
{
    private readonly TruLoadDbContext _context;

    public StationRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<Station?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Stations
            .Include(s => s.Organization)
            .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null, cancellationToken);
    }

    public async Task<IEnumerable<Station>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Stations
            .Include(s => s.Organization)
            .Where(s => s.DeletedAt == null);

        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<Station?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Stations
            .Include(s => s.Organization)
            .FirstOrDefaultAsync(s => s.Code == code && s.DeletedAt == null, cancellationToken);
    }

    public async Task<IEnumerable<Station>> GetByTypeAsync(string stationType, CancellationToken cancellationToken = default)
    {
        return await _context.Stations
            .Include(s => s.Organization)
            .Where(s => s.StationType == stationType && s.DeletedAt == null && s.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Station>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await _context.Stations
            .Include(s => s.Organization)
            .Where(s => s.OrganizationId == organizationId && s.DeletedAt == null)
            .ToListAsync(cancellationToken);
    }

    public async Task<Station> CreateAsync(Station station, CancellationToken cancellationToken = default)
    {
        _context.Stations.Add(station);
        await _context.SaveChangesAsync(cancellationToken);
        return station;
    }

    public async Task<Station> UpdateAsync(Station station, CancellationToken cancellationToken = default)
    {
        station.UpdatedAt = DateTime.UtcNow;
        _context.Stations.Update(station);
        await _context.SaveChangesAsync(cancellationToken);
        return station;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var station = await _context.Stations.FindAsync(new object[] { id }, cancellationToken);
        if (station != null)
        {
            station.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Stations.Where(s => s.Code == code && s.DeletedAt == null);
        
        if (excludeId.HasValue)
        {
            query = query.Where(s => s.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }
}




