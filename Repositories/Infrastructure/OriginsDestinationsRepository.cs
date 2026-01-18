using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Repositories.Infrastructure;

public class OriginsDestinationsRepository : IOriginsDestinationsRepository
{
    private readonly TruLoadDbContext _context;

    public OriginsDestinationsRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<List<OriginsDestinations>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.OriginsDestinations
            .Where(o => o.IsActive && o.DeletedAt == null)
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<OriginsDestinations>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.OriginsDestinations.Where(o => o.DeletedAt == null);
        
        if (!includeInactive)
            query = query.Where(o => o.IsActive);

        return await query.OrderBy(o => o.Name).ToListAsync(cancellationToken);
    }

    public async Task<OriginsDestinations?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.OriginsDestinations
            .FirstOrDefaultAsync(o => o.Id == id && o.DeletedAt == null, cancellationToken);
    }

    public async Task<OriginsDestinations?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.OriginsDestinations
            .FirstOrDefaultAsync(o => o.Code == code && o.DeletedAt == null, cancellationToken);
    }

    public async Task<List<OriginsDestinations>> GetByCountryAsync(string country, CancellationToken cancellationToken = default)
    {
        return await _context.OriginsDestinations
            .Where(o => o.Country == country && o.IsActive && o.DeletedAt == null)
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<OriginsDestinations>> GetByLocationTypeAsync(string locationType, CancellationToken cancellationToken = default)
    {
        return await _context.OriginsDestinations
            .Where(o => o.LocationType == locationType && o.IsActive && o.DeletedAt == null)
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<OriginsDestinations> CreateAsync(OriginsDestinations location, CancellationToken cancellationToken = default)
    {
        location.Id = Guid.NewGuid();
        location.CreatedAt = DateTime.UtcNow;
        location.UpdatedAt = DateTime.UtcNow;

        _context.OriginsDestinations.Add(location);
        await _context.SaveChangesAsync(cancellationToken);

        return location;
    }

    public async Task<OriginsDestinations> UpdateAsync(OriginsDestinations location, CancellationToken cancellationToken = default)
    {
        location.UpdatedAt = DateTime.UtcNow;

        _context.OriginsDestinations.Update(location);
        await _context.SaveChangesAsync(cancellationToken);

        return location;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var location = await _context.OriginsDestinations.FindAsync(new object[] { id }, cancellationToken);
        if (location == null) return false;

        location.DeletedAt = DateTime.UtcNow;
        location.UpdatedAt = DateTime.UtcNow;
        location.IsActive = false;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
