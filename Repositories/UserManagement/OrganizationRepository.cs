using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Interfaces;
using truload_backend.Data;

namespace TruLoad.Backend.Repositories;

public class OrganizationRepository : IOrganizationRepository
{
    private readonly TruLoadDbContext _context;

    public OrganizationRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Organizations
            .Include(o => o.Departments)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<Organization?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Organizations
            .FirstOrDefaultAsync(o => o.Code == code, cancellationToken);
    }

    public async Task<List<Organization>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Organizations.AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(o => o.IsActive);
        }

        return await query
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Organization> CreateAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        _context.Organizations.Add(organization);
        await _context.SaveChangesAsync(cancellationToken);
        return organization;
    }

    public async Task<Organization> UpdateAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        _context.Organizations.Update(organization);
        await _context.SaveChangesAsync(cancellationToken);
        return organization;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var organization = await _context.Organizations.FindAsync(new object[] { id }, cancellationToken);
        if (organization != null)
        {
            _context.Organizations.Remove(organization);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Organizations.AnyAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Organizations.Where(o => o.Code == code);

        if (excludeId.HasValue)
        {
            query = query.Where(o => o.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }
}
