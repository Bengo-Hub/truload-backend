using Microsoft.EntityFrameworkCore;
using truload_backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Interfaces;

namespace TruLoad.Backend.Repositories;

/// <summary>
/// EF Core implementation of IPermissionRepository.
/// Handles all database operations for Permission entity.
/// </summary>
public class PermissionRepository : IPermissionRepository
{
    private readonly TruLoadDbContext _context;

    public PermissionRepository(TruLoadDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Permission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Permission?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        return await _context.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == code, cancellationToken);
    }

    public async Task<IEnumerable<Permission>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(category))
            return Enumerable.Empty<Permission>();

        return await _context.Permissions
            .AsNoTracking()
            .Where(p => p.Category == category)
            .OrderBy(p => p.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Permission>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Permission>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Permissions
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Permission>> GetForRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        return await _context.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission)
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<Permission> CreateAsync(Permission permission, CancellationToken cancellationToken = default)
    {
        if (permission == null)
            throw new ArgumentNullException(nameof(permission));

        _context.Permissions.Add(permission);
        await _context.SaveChangesAsync(cancellationToken);
        return permission;
    }

    public async Task<Permission> UpdateAsync(Permission permission, CancellationToken cancellationToken = default)
    {
        if (permission == null)
            throw new ArgumentNullException(nameof(permission));

        _context.Permissions.Update(permission);
        await _context.SaveChangesAsync(cancellationToken);
        return permission;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var permission = await GetByIdAsync(id, cancellationToken);
        if (permission == null)
            return false;

        _context.Permissions.Remove(permission);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        return await _context.Permissions
            .AsNoTracking()
            .AnyAsync(p => p.Code == code, cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Permissions
            .AsNoTracking()
            .CountAsync(cancellationToken);
    }
}
