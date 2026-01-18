using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Data.Repositories.Weighing;

public class PermitRepository : IPermitRepository
{
    private readonly TruLoadDbContext _context;

    public PermitRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<Permit?> GetActivePermitForVehicleAsync(Guid vehicleId)
    {
        var now = DateTime.UtcNow;
        return await _context.Permits
            .AsNoTracking()
            .Include(p => p.PermitType)
            .Where(p => p.VehicleId == vehicleId &&
                        p.Status == "active" &&
                        p.ValidFrom <= now &&
                        p.ValidTo >= now)
            .OrderByDescending(p => p.ValidTo)
            .FirstOrDefaultAsync();
    }

    public async Task<Permit?> GetByIdAsync(Guid id)
    {
        return await _context.Permits
            .AsNoTracking()
            .Include(p => p.PermitType)
            .Include(p => p.Vehicle)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<Permit>> GetByVehicleIdAsync(Guid vehicleId)
    {
        return await _context.Permits
            .AsNoTracking()
            .Include(p => p.PermitType)
            .Where(p => p.VehicleId == vehicleId)
            .OrderByDescending(p => p.ValidTo)
            .ToListAsync();
    }

    public async Task<Permit> CreateAsync(Permit permit)
    {
        _context.Permits.Add(permit);
        await _context.SaveChangesAsync();
        return permit;
    }

    public async Task UpdateAsync(Permit permit)
    {
        _context.Permits.Entry(permit).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }
}
