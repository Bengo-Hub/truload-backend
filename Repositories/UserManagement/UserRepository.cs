using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.UserManagement.Interfaces;
using truload_backend.Data;

namespace TruLoad.Backend.Repositories.UserManagement.Repositories;

public class UserRepository : IUserRepository
{
    private readonly TruLoadDbContext _context;

    public UserRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.Organization)
            .Include(u => u.Department)
            .Include(u => u.Station)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id && u.DeletedAt == null, cancellationToken);
    }

    public async Task<User?> GetByAuthServiceUserIdAsync(Guid authServiceUserId, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.Organization)
            .Include(u => u.Department)
            .Include(u => u.Station)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.AuthServiceUserId == authServiceUserId && u.DeletedAt == null, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.Organization)
            .Include(u => u.Department)
            .Include(u => u.Station)
            .FirstOrDefaultAsync(u => u.Email == email && u.DeletedAt == null, cancellationToken);
    }

    public async Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.Organization)
            .Include(u => u.Department)
            .Include(u => u.Station)
            .Where(u => u.DeletedAt == null)
            .OrderBy(u => u.FullName)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<User>> GetByStationIdAsync(Guid stationId, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.Organization)
            .Include(u => u.Department)
            .Include(u => u.Station)
            .Where(u => u.StationId == stationId && u.DeletedAt == null)
            .OrderBy(u => u.FullName)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<User>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.Organization)
            .Include(u => u.Department)
            .Include(u => u.Station)
            .Where(u => u.OrganizationId == organizationId && u.DeletedAt == null)
            .OrderBy(u => u.FullName)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<User>> SearchAsync(string? search, string? status, Guid? stationId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = _context.Users
            .Include(u => u.Organization)
            .Include(u => u.Department)
            .Include(u => u.Station)
            .Where(u => u.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(u => 
                u.FullName!.ToLower().Contains(searchLower) ||
                u.Email.ToLower().Contains(searchLower) ||
                (u.Phone != null && u.Phone.Contains(searchLower)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(u => u.Status == status);
        }

        if (stationId.HasValue)
        {
            query = query.Where(u => u.StationId == stationId.Value);
        }

        return await query
            .OrderBy(u => u.FullName)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(string? search, string? status, Guid? stationId, CancellationToken cancellationToken = default)
    {
        var query = _context.Users.Where(u => u.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(u => 
                u.FullName!.ToLower().Contains(searchLower) ||
                u.Email.ToLower().Contains(searchLower) ||
                (u.Phone != null && u.Phone.Contains(searchLower)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(u => u.Status == status);
        }

        if (stationId.HasValue)
        {
            query = query.Where(u => u.StationId == stationId.Value);
        }

        return await query.CountAsync(cancellationToken);
    }

    public async Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        user.UpdatedAt = DateTime.UtcNow;
        _context.Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FindAsync(new object[] { id }, cancellationToken);
        if (user != null)
        {
            user.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users.AnyAsync(u => u.Id == id && u.DeletedAt == null, cancellationToken);
    }
}
