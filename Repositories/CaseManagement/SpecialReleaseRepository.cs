using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.CaseManagement;

namespace TruLoad.Backend.Repositories.CaseManagement;

public class SpecialReleaseRepository : ISpecialReleaseRepository
{
    private readonly TruLoadDbContext _context;

    public SpecialReleaseRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<SpecialRelease?> GetByIdAsync(Guid id)
    {
        return await _context.SpecialReleases
            .AsNoTracking()
            .Include(sr => sr.CaseRegister)
                .ThenInclude(cr => cr.ViolationType)
            .Include(sr => sr.CaseRegister)
                .ThenInclude(cr => cr.CaseStatus)
            .Include(sr => sr.ReleaseType)
            .FirstOrDefaultAsync(sr => sr.Id == id);
    }

    public async Task<SpecialRelease?> GetByCertificateNoAsync(string certificateNo)
    {
        return await _context.SpecialReleases
            .AsNoTracking()
            .Include(sr => sr.CaseRegister)
            .Include(sr => sr.ReleaseType)
            .FirstOrDefaultAsync(sr => sr.CertificateNo == certificateNo);
    }

    public async Task<IEnumerable<SpecialRelease>> GetByCaseRegisterIdAsync(Guid caseRegisterId)
    {
        return await _context.SpecialReleases
            .AsNoTracking()
            .Include(sr => sr.ReleaseType)
            .Where(sr => sr.CaseRegisterId == caseRegisterId)
            .OrderByDescending(sr => sr.CreatedAt)
            .ToListAsync();
    }

    public async Task<(List<SpecialRelease> Items, int TotalCount)> GetPendingApprovalsAsync(
        string? caseNo = null,
        string? releaseType = null,
        DateTime? from = null,
        DateTime? to = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        var query = _context.SpecialReleases
            .AsNoTracking()
            .Include(sr => sr.CaseRegister)
            .Include(sr => sr.ReleaseType)
            .Where(sr => !sr.IsApproved && !sr.IsRejected)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(caseNo))
            query = query.Where(sr => sr.CaseRegister != null && sr.CaseRegister.CaseNo.Contains(caseNo));

        if (!string.IsNullOrWhiteSpace(releaseType))
            query = query.Where(sr => sr.ReleaseType != null && sr.ReleaseType.Name.Contains(releaseType));

        if (from.HasValue)
            query = query.Where(sr => sr.CreatedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(sr => sr.CreatedAt <= to.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(sr => sr.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<IEnumerable<SpecialRelease>> GetApprovedReleasesAsync(
        DateTime? from = null,
        DateTime? to = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        var query = _context.SpecialReleases
            .AsNoTracking()
            .Include(sr => sr.CaseRegister)
            .Include(sr => sr.ReleaseType)
            .AsQueryable();

        if (from.HasValue)
            query = query.Where(sr => sr.IssuedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(sr => sr.IssuedAt <= to.Value);

        return await query
            .OrderByDescending(sr => sr.IssuedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<SpecialRelease> CreateAsync(SpecialRelease specialRelease)
    {
        specialRelease.CreatedAt = DateTime.UtcNow;
        // Note: SpecialRelease model doesn't have UpdatedAt field

        _context.SpecialReleases.Add(specialRelease);
        await _context.SaveChangesAsync();
        return specialRelease;
    }

    public async Task<SpecialRelease> UpdateAsync(SpecialRelease specialRelease)
    {
        // Note: SpecialRelease model doesn't have UpdatedAt field

        _context.SpecialReleases.Update(specialRelease);
        await _context.SaveChangesAsync();
        return specialRelease;
    }

    public async Task<string> GenerateNextCertificateNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"SR-{year}-";

        var lastCertNo = await _context.SpecialReleases
            .Where(sr => sr.CertificateNo.StartsWith(prefix))
            .OrderByDescending(sr => sr.CertificateNo)
            .Select(sr => sr.CertificateNo)
            .FirstOrDefaultAsync();

        if (lastCertNo == null)
            return $"{prefix}00001";

        var lastNumber = int.Parse(lastCertNo.Split('-').Last());
        var nextNumber = lastNumber + 1;
        return $"{prefix}{nextNumber:D5}";
    }

    public async Task<bool> CertificateNumberExistsAsync(string certificateNo)
    {
        return await _context.SpecialReleases.AnyAsync(sr => sr.CertificateNo == certificateNo);
    }
}
