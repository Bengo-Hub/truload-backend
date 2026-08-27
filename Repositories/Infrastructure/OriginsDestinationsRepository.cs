using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Data;
using TruLoad.Backend.Middleware;

namespace TruLoad.Backend.Repositories.Infrastructure;

public class OriginsDestinationsRepository : IOriginsDestinationsRepository
{
    private readonly TruLoadDbContext _context;
    private readonly ITenantContext _tenantContext;

    public OriginsDestinationsRepository(TruLoadDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// List/search queries return rows that are either shared/global (OrganizationId == null) or
    /// belong to the caller's current tenant - same NULL=shared convention and filter shape already
    /// used by DriverRepository.SearchAsync for Driver.OrganizationId.
    /// </summary>
    public async Task<List<OriginsDestinations>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        var orgId = _tenantContext.OrganizationId;
        return await _context.OriginsDestinations
            .Where(o => o.IsActive && o.DeletedAt == null)
            .Where(o => o.OrganizationId == null || o.OrganizationId == orgId)
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<OriginsDestinations>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var orgId = _tenantContext.OrganizationId;
        var query = _context.OriginsDestinations
            .Where(o => o.DeletedAt == null)
            .Where(o => o.OrganizationId == null || o.OrganizationId == orgId);

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

    /// <summary>
    /// Scoped duplicate-code check mirroring the two filtered unique indexes on (OrganizationId,
    /// Code): pass organizationId = null to check the shared/global bucket, or a specific org id to
    /// check that org's own private bucket. Unlike GetByCodeAsync, this never treats a code taken in
    /// one org's private catalog as a conflict for a different scope.
    /// </summary>
    public async Task<bool> CodeExistsInScopeAsync(string code, Guid? organizationId, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.OriginsDestinations
            .Where(o => o.Code == code && o.DeletedAt == null && o.OrganizationId == organizationId);

        if (excludeId.HasValue)
        {
            query = query.Where(o => o.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<List<OriginsDestinations>> GetByCountryAsync(string country, CancellationToken cancellationToken = default)
    {
        var orgId = _tenantContext.OrganizationId;
        return await _context.OriginsDestinations
            .Where(o => o.Country == country && o.IsActive && o.DeletedAt == null)
            .Where(o => o.OrganizationId == null || o.OrganizationId == orgId)
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<OriginsDestinations>> GetByLocationTypeAsync(string locationType, CancellationToken cancellationToken = default)
    {
        var orgId = _tenantContext.OrganizationId;
        return await _context.OriginsDestinations
            .Where(o => o.LocationType == locationType && o.IsActive && o.DeletedAt == null)
            .Where(o => o.OrganizationId == null || o.OrganizationId == orgId)
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
