using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Data;
using TruLoad.Backend.Middleware;

namespace TruLoad.Backend.Repositories.Infrastructure;

public class CargoTypesRepository : ICargoTypesRepository
{
    private readonly TruLoadDbContext _context;
    private readonly ITenantContext _tenantContext;

    public CargoTypesRepository(TruLoadDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// List/search queries return rows that are either shared/global (OrganizationId == null) or
    /// belong to the caller's current tenant - same NULL=shared convention and filter shape already
    /// used by DriverRepository.SearchAsync for Driver.OrganizationId.
    /// </summary>
    public async Task<List<CargoTypes>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        var orgId = _tenantContext.OrganizationId;
        return await _context.CargoTypes
            .Where(c => c.IsActive && c.DeletedAt == null)
            .Where(c => c.OrganizationId == null || c.OrganizationId == orgId)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CargoTypes>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var orgId = _tenantContext.OrganizationId;
        var query = _context.CargoTypes
            .Where(c => c.DeletedAt == null)
            .Where(c => c.OrganizationId == null || c.OrganizationId == orgId);

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

    /// <summary>
    /// Scoped duplicate-code check mirroring the two filtered unique indexes on (OrganizationId,
    /// Code): pass organizationId = null to check the shared/global bucket, or a specific org id to
    /// check that org's own private bucket. Unlike GetByCodeAsync, this never treats a code taken in
    /// one org's private catalog as a conflict for a different scope.
    /// </summary>
    public async Task<bool> CodeExistsInScopeAsync(string code, Guid? organizationId, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.CargoTypes
            .Where(c => c.Code == code && c.DeletedAt == null && c.OrganizationId == organizationId);

        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<List<CargoTypes>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        var orgId = _tenantContext.OrganizationId;
        return await _context.CargoTypes
            .Where(c => c.Category == category && c.IsActive && c.DeletedAt == null)
            .Where(c => c.OrganizationId == null || c.OrganizationId == orgId)
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
