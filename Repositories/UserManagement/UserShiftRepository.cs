using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.UserManagement.Interfaces;

namespace TruLoad.Backend.Repositories.UserManagement.Repositories;

public class UserShiftRepository : IUserShiftRepository
{
    private readonly TruLoadDbContext _context;

    public UserShiftRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<UserShift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.UserShifts
            .AsNoTracking()
            .Include(us => us.User)
            .Include(us => us.WorkShift)
            .Include(us => us.ShiftRotation)
            .FirstOrDefaultAsync(us => us.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<UserShift>> GetByUserIdAsync(Guid userId, bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = _context.UserShifts
            .AsNoTracking()
            .Include(us => us.WorkShift)
            .Include(us => us.ShiftRotation)
            .Where(us => us.UserId == userId);

        if (activeOnly)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            query = query.Where(us => us.EndsOn == null || us.EndsOn > today);
        }

        return await query
            .OrderByDescending(us => us.StartsOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<UserShift?> GetActiveShiftForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await _context.UserShifts
            .AsNoTracking()
            .Include(us => us.WorkShift)
                .ThenInclude(ws => ws.WorkShiftSchedules)
            .Include(us => us.ShiftRotation)
                .ThenInclude(sr => sr.RotationShifts)
            .Where(us => us.UserId == userId &&
                        us.StartsOn <= today &&
                        (us.EndsOn == null || us.EndsOn > today))
            .OrderByDescending(us => us.StartsOn)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<UserShift>> GetByWorkShiftIdAsync(Guid workShiftId, bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = _context.UserShifts
            .AsNoTracking()
            .Include(us => us.User)
            .Where(us => us.WorkShiftId == workShiftId);

        if (activeOnly)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            query = query.Where(us => us.EndsOn == null || us.EndsOn > today);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<UserShift>> GetByShiftRotationIdAsync(Guid shiftRotationId, bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = _context.UserShifts
            .AsNoTracking()
            .Include(us => us.User)
            .Where(us => us.ShiftRotationId == shiftRotationId);

        if (activeOnly)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            query = query.Where(us => us.EndsOn == null || us.EndsOn > today);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<UserShift> CreateAsync(UserShift userShift, CancellationToken cancellationToken = default)
    {
        _context.UserShifts.Add(userShift);
        await _context.SaveChangesAsync(cancellationToken);
        return userShift;
    }

    public async Task<UserShift> UpdateAsync(UserShift userShift, CancellationToken cancellationToken = default)
    {
        _context.UserShifts.Update(userShift);
        await _context.SaveChangesAsync(cancellationToken);
        return userShift;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userShift = await _context.UserShifts.FindAsync(new object[] { id }, cancellationToken);
        if (userShift != null)
        {
            _context.UserShifts.Remove(userShift);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> HasActiveShiftAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await _context.UserShifts
            .AnyAsync(us => us.UserId == userId &&
                           us.StartsOn <= today &&
                           (us.EndsOn == null || us.EndsOn > today),
                     cancellationToken);
    }
}
