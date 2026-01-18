using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.UserManagement.Interfaces;

namespace TruLoad.Backend.Repositories.UserManagement.Repositories;

public class ShiftRotationRepository : IShiftRotationRepository
{
    private readonly TruLoadDbContext _context;

    public ShiftRotationRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<ShiftRotation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ShiftRotations
            .AsNoTracking()
            .FirstOrDefaultAsync(sr => sr.Id == id, cancellationToken);
    }

    public async Task<ShiftRotation?> GetByIdWithShiftsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ShiftRotations
            .AsNoTracking()
            .Include(sr => sr.RotationShifts)
                .ThenInclude(rs => rs.WorkShift)
            .Include(sr => sr.CurrentActiveShift)
            .FirstOrDefaultAsync(sr => sr.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<ShiftRotation>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.ShiftRotations.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(sr => sr.IsActive);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ShiftRotation>> GetAllWithShiftsAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.ShiftRotations
            .AsNoTracking()
            .Include(sr => sr.RotationShifts)
                .ThenInclude(rs => rs.WorkShift)
            .Include(sr => sr.CurrentActiveShift)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(sr => sr.IsActive);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<ShiftRotation> CreateAsync(ShiftRotation shiftRotation, CancellationToken cancellationToken = default)
    {
        _context.ShiftRotations.Add(shiftRotation);
        await _context.SaveChangesAsync(cancellationToken);
        return shiftRotation;
    }

    public async Task<ShiftRotation> UpdateAsync(ShiftRotation shiftRotation, CancellationToken cancellationToken = default)
    {
        _context.ShiftRotations.Update(shiftRotation);
        await _context.SaveChangesAsync(cancellationToken);
        return shiftRotation;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var shiftRotation = await _context.ShiftRotations
            .Include(sr => sr.RotationShifts)
            .FirstOrDefaultAsync(sr => sr.Id == id, cancellationToken);

        if (shiftRotation != null)
        {
            // Remove related rotation shifts first
            _context.RotationShifts.RemoveRange(shiftRotation.RotationShifts);
            _context.ShiftRotations.Remove(shiftRotation);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> TitleExistsAsync(string title, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.ShiftRotations.Where(sr => sr.Title == title);

        if (excludeId.HasValue)
        {
            query = query.Where(sr => sr.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }
}
