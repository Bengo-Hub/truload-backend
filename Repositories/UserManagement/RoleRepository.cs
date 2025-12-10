using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.UserManagement.Interfaces;
using truload_backend.Data;

namespace TruLoad.Backend.Repositories.UserManagement.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly TruLoadDbContext _context;

    public RoleRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Roles.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .FirstOrDefaultAsync(r => r.Name == name, cancellationToken);
    }

    public async Task<List<Role>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Roles.AsQueryable();
        
        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }
        
        return await query
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Role>> GetByIdsAsync(List<Guid> ids, CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<Role> CreateAsync(Role role, CancellationToken cancellationToken = default)
    {
        _context.Roles.Add(role);
        await _context.SaveChangesAsync(cancellationToken);
        return role;
    }

    public async Task<Role> UpdateAsync(Role role, CancellationToken cancellationToken = default)
    {
        _context.Roles.Update(role);
        await _context.SaveChangesAsync(cancellationToken);
        return role;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles.FindAsync(new object[] { id }, cancellationToken);
        if (role != null)
        {
            _context.Roles.Remove(role);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Roles.AnyAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Roles.Where(r => r.Name == name);

        if (excludeId.HasValue)
        {
            query = query.Where(r => r.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }
}
