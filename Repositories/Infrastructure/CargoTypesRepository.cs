using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Repositories.Infrastructure;

public class CargoTypesRepository : ICargoTypesRepository
{
    private readonly TruLoadDbContext _context;

    public CargoTypesRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<List<CargoTypes>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.CargoTypes
            .Where(c => c.IsActive && c.DeletedAt == null)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CargoTypes>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.CargoTypes.Where(c => c.DeletedAt == null);
        
        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        return await query.OrderBy(c => c.Name).ToListAsync(cancellationToken);
    }

    public async Task<CargoTypes?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.CargoTypes
            .FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, cancellationToken);
    }

    public async Task<CargoTypes?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.CargoTypes
            .FirstOrDefaultAsync(c => c.Code == code && c.DeletedAt == null, cancellationToken);
    }

    public async Task<List<CargoTypes>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        return await _context.CargoTypes
            .Where(c => c.Category == category && c.IsActive && c.DeletedAt == null)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<CargoTypes> CreateAsync(CargoTypes cargoType, CancellationToken cancellationToken = default)
    {
        cargoType.Id = Guid.NewGuid();
        cargoType.CreatedAt = DateTime.UtcNow;
        cargoType.UpdatedAt = DateTime.UtcNow;

        _context.CargoTypes.Add(cargoType);
        await _context.SaveChangesAsync(cancellationToken);

        return cargoType;
    }

    public async Task<CargoTypes> UpdateAsync(CargoTypes cargoType, CancellationToken cancellationToken = default)
    {
        cargoType.UpdatedAt = DateTime.UtcNow;

        _context.CargoTypes.Update(cargoType);
        await _context.SaveChangesAsync(cancellationToken);

        return cargoType;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cargoType = await _context.CargoTypes.FindAsync(new object[] { id }, cancellationToken);
        if (cargoType == null) return false;

        cargoType.DeletedAt = DateTime.UtcNow;
        cargoType.UpdatedAt = DateTime.UtcNow;
        cargoType.IsActive = false;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
