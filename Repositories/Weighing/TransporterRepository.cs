using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Repositories.Weighing.Interfaces;

namespace TruLoad.Backend.Repositories.Weighing;

/// <summary>
/// Repository for transporter master data
/// </summary>
public class TransporterRepository : ITransporterRepository
{
    private readonly TruLoadDbContext _context;

    public TransporterRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<List<Transporter>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Transporters
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Transporter>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Transporters.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        return await query.OrderBy(t => t.Name).ToListAsync(cancellationToken);
    }

    public async Task<Transporter?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Transporters
            .AsNoTracking()
            .Include(t => t.Vehicles)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Transporter?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Transporters
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Code.ToUpper() == code.ToUpper(), cancellationToken);
    }

    public async Task<List<Transporter>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetAllActiveAsync(cancellationToken);
        }

        var normalizedQuery = query.ToUpperInvariant().Trim();

        return await _context.Transporters
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Where(t =>
                t.Name.ToUpper().Contains(normalizedQuery) ||
                t.Code.ToUpper().Contains(normalizedQuery) ||
                (t.RegistrationNo != null && t.RegistrationNo.ToUpper().Contains(normalizedQuery)) ||
                (t.Phone != null && t.Phone.Contains(normalizedQuery)) ||
                (t.Email != null && t.Email.ToUpper().Contains(normalizedQuery)) ||
                (t.NtacNo != null && t.NtacNo.ToUpper().Contains(normalizedQuery)))
            .OrderBy(t => t.Name)
            .Take(50)
            .ToListAsync(cancellationToken);
    }

    public async Task<Transporter> CreateAsync(Transporter transporter, CancellationToken cancellationToken = default)
    {
        transporter.CreatedAt = DateTime.UtcNow;
        transporter.UpdatedAt = DateTime.UtcNow;
        transporter.IsActive = true;

        _context.Transporters.Add(transporter);
        await _context.SaveChangesAsync(cancellationToken);
        return transporter;
    }

    public async Task<Transporter> UpdateAsync(Transporter transporter, CancellationToken cancellationToken = default)
    {
        transporter.UpdatedAt = DateTime.UtcNow;
        _context.Transporters.Update(transporter);
        await _context.SaveChangesAsync(cancellationToken);
        return transporter;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var transporter = await _context.Transporters.FindAsync(new object[] { id }, cancellationToken);
        if (transporter == null) return false;

        transporter.IsActive = false;
        transporter.DeletedAt = DateTime.UtcNow;
        transporter.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Transporters
            .AsNoTracking()
            .AnyAsync(t => t.Id == id && t.IsActive, cancellationToken);
    }
}
