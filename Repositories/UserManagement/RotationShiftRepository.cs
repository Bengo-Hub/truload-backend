using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.UserManagement.Interfaces;

namespace TruLoad.Backend.Repositories.UserManagement.Repositories;

public class RotationShiftRepository : IRotationShiftRepository
{
    private readonly TruLoadDbContext _context;

    public RotationShiftRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<RotationShift>> GetByRotationIdAsync(Guid rotationId, CancellationToken cancellationToken = default)
    {
        return await _context.RotationShifts
            .AsNoTracking()
            .Include(rs => rs.WorkShift)
            .Where(rs => rs.RotationId == rotationId)
            .OrderBy(rs => rs.SequenceOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<RotationShift?> GetByRotationAndWorkShiftAsync(Guid rotationId, Guid workShiftId, CancellationToken cancellationToken = default)
    {
        return await _context.RotationShifts
            .AsNoTracking()
            .Include(rs => rs.WorkShift)
            .FirstOrDefaultAsync(rs => rs.RotationId == rotationId && rs.WorkShiftId == workShiftId, cancellationToken);
    }

    public async Task<RotationShift> CreateAsync(RotationShift rotationShift, CancellationToken cancellationToken = default)
    {
        _context.RotationShifts.Add(rotationShift);
        await _context.SaveChangesAsync(cancellationToken);
        return rotationShift;
    }

    public async Task DeleteAsync(Guid rotationId, Guid workShiftId, CancellationToken cancellationToken = default)
    {
        var rotationShift = await _context.RotationShifts
            .FirstOrDefaultAsync(rs => rs.RotationId == rotationId && rs.WorkShiftId == workShiftId, cancellationToken);

        if (rotationShift != null)
        {
            _context.RotationShifts.Remove(rotationShift);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteAllByRotationIdAsync(Guid rotationId, CancellationToken cancellationToken = default)
    {
        var rotationShifts = await _context.RotationShifts
            .Where(rs => rs.RotationId == rotationId)
            .ToListAsync(cancellationToken);

        if (rotationShifts.Any())
        {
            _context.RotationShifts.RemoveRange(rotationShifts);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
