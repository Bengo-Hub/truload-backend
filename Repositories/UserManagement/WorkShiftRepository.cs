using Microsoft.EntityFrameworkCore;
using truload_backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Interfaces;

namespace TruLoad.Backend.Repositories;

public class WorkShiftRepository : IWorkShiftRepository
{
    private readonly TruLoadDbContext _context;

    public WorkShiftRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<WorkShift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.WorkShifts
            .FirstOrDefaultAsync(ws => ws.Id == id && ws.DeletedAt == null, cancellationToken);
    }

    public async Task<WorkShift?> GetByIdWithSchedulesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.WorkShifts
            .Include(ws => ws.WorkShiftSchedules)
            .FirstOrDefaultAsync(ws => ws.Id == id && ws.DeletedAt == null, cancellationToken);
    }

    public async Task<IEnumerable<WorkShift>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.WorkShifts
            .Where(ws => ws.DeletedAt == null);

        if (!includeInactive)
        {
            query = query.Where(ws => ws.IsActive);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<WorkShift>> GetAllWithSchedulesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.WorkShifts
            .Include(ws => ws.WorkShiftSchedules)
            .Where(ws => ws.DeletedAt == null);

        if (!includeInactive)
        {
            query = query.Where(ws => ws.IsActive);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<WorkShift?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.WorkShifts
            .Include(ws => ws.WorkShiftSchedules)
            .FirstOrDefaultAsync(ws => ws.Name == name && ws.DeletedAt == null, cancellationToken);
    }

    public async Task<WorkShift> CreateAsync(WorkShift workShift, CancellationToken cancellationToken = default)
    {
        _context.WorkShifts.Add(workShift);
        await _context.SaveChangesAsync(cancellationToken);
        return workShift;
    }

    public async Task<WorkShift> UpdateAsync(WorkShift workShift, CancellationToken cancellationToken = default)
    {
        workShift.UpdatedAt = DateTime.UtcNow;
        _context.WorkShifts.Update(workShift);
        await _context.SaveChangesAsync(cancellationToken);
        return workShift;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var workShift = await _context.WorkShifts
            .Include(ws => ws.WorkShiftSchedules)
            .FirstOrDefaultAsync(ws => ws.Id == id, cancellationToken);
            
        if (workShift != null)
        {
            workShift.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.WorkShifts.Where(ws => ws.Name == name && ws.DeletedAt == null);
        
        if (excludeId.HasValue)
        {
            query = query.Where(ws => ws.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }
}




