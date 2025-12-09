using Microsoft.EntityFrameworkCore;
using truload_backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Interfaces;

namespace TruLoad.Backend.Repositories;

public class DepartmentRepository : IDepartmentRepository
{
    private readonly TruLoadDbContext _context;

    public DepartmentRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Departments
            .Include(d => d.Organization)
            .FirstOrDefaultAsync(d => d.Id == id && d.DeletedAt == null, cancellationToken);
    }

    public async Task<IEnumerable<Department>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Departments
            .Include(d => d.Organization)
            .Where(d => d.DeletedAt == null);

        if (!includeInactive)
        {
            query = query.Where(d => d.IsActive);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Department>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await _context.Departments
            .Include(d => d.Organization)
            .Where(d => d.OrganizationId == organizationId && d.DeletedAt == null)
            .ToListAsync(cancellationToken);
    }

    public async Task<Department?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Departments
            .Include(d => d.Organization)
            .FirstOrDefaultAsync(d => d.Code == code && d.DeletedAt == null, cancellationToken);
    }

    public async Task<Department> CreateAsync(Department department, CancellationToken cancellationToken = default)
    {
        _context.Departments.Add(department);
        await _context.SaveChangesAsync(cancellationToken);
        return department;
    }

    public async Task<Department> UpdateAsync(Department department, CancellationToken cancellationToken = default)
    {
        department.UpdatedAt = DateTime.UtcNow;
        _context.Departments.Update(department);
        await _context.SaveChangesAsync(cancellationToken);
        return department;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var department = await _context.Departments.FindAsync(new object[] { id }, cancellationToken);
        if (department != null)
        {
            department.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Departments.Where(d => d.Code == code && d.DeletedAt == null);
        
        if (excludeId.HasValue)
        {
            query = query.Where(d => d.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }
}




