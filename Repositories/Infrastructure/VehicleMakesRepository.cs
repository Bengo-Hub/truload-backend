using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models.Infrastructure;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Repositories.Infrastructure;

public class VehicleMakesRepository : IVehicleMakesRepository
{
    private readonly TruLoadDbContext _context;

    public VehicleMakesRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<List<VehicleMake>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.VehicleMakes
            .Where(m => m.IsActive && m.DeletedAt == null)
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<VehicleMake>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.VehicleMakes.Where(m => m.DeletedAt == null);

        if (!includeInactive)
            query = query.Where(m => m.IsActive);

        return await query.OrderBy(m => m.Name).ToListAsync(cancellationToken);
    }

    public async Task<VehicleMake?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.VehicleMakes
            .FirstOrDefaultAsync(m => m.Id == id && m.DeletedAt == null, cancellationToken);
    }

    public async Task<VehicleMake?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.VehicleMakes
            .FirstOrDefaultAsync(m => m.Code == code && m.DeletedAt == null, cancellationToken);
    }

    public async Task<VehicleMake?> GetByCodeIncludingDeletedAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.VehicleMakes
            .FirstOrDefaultAsync(m => m.Code == code, cancellationToken);
    }

    public async Task<List<VehicleMake>> GetByCountryAsync(string country, CancellationToken cancellationToken = default)
    {
        return await _context.VehicleMakes
            .Where(m => m.Country == country && m.IsActive && m.DeletedAt == null)
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<VehicleMake> CreateAsync(VehicleMake make, CancellationToken cancellationToken = default)
    {
        make.Id = Guid.NewGuid();
        make.CreatedAt = DateTime.UtcNow;
        make.UpdatedAt = DateTime.UtcNow;

        _context.VehicleMakes.Add(make);
        await _context.SaveChangesAsync(cancellationToken);

        return make;
    }

    public async Task<VehicleMake> UpdateAsync(VehicleMake make, CancellationToken cancellationToken = default)
    {
        make.UpdatedAt = DateTime.UtcNow;

        _context.VehicleMakes.Update(make);
        await _context.SaveChangesAsync(cancellationToken);

        return make;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var make = await _context.VehicleMakes.FindAsync(new object[] { id }, cancellationToken);
        if (make == null) return false;

        make.DeletedAt = DateTime.UtcNow;
        make.UpdatedAt = DateTime.UtcNow;
        make.IsActive = false;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
