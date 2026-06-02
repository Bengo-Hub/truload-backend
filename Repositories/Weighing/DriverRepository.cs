using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Weighing;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Data.Repositories.Weighing;

public class DriverRepository : IDriverRepository
{
    private readonly TruLoadDbContext _context;
    private readonly ITenantContext _tenantContext;

    public DriverRepository(TruLoadDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<Driver?> GetByIdAsync(Guid id)
    {
        return await _context.Drivers
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<Driver?> GetByIdNumberAsync(string idNumber)
    {
        return await _context.Drivers
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.IdNumber == idNumber);
    }

    public async Task<Driver?> GetByLicenseAsync(string licenseNo)
    {
        return await _context.Drivers
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DrivingLicenseNo == licenseNo);
    }

    /// <summary>
    /// Search drivers by name, ID number, or license. When query is empty, returns all drivers (up to 500) for dropdowns and setup tabs.
    /// Filters to drivers that are global (OrganizationId == null) or belong to the current tenant.
    /// </summary>
    public async Task<IEnumerable<Driver>> SearchAsync(string query, Guid? transporterId = null)
    {
        var orgId = _tenantContext.OrganizationId;

        var q = _context.Drivers
            .AsNoTracking()
            .Where(d => d.OrganizationId == null || d.OrganizationId == orgId);

        if (transporterId.HasValue)
            q = q.Where(d => d.TransporterId == transporterId.Value);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            q = q.Where(d => (d.FullNames != null && d.FullNames.Contains(term)) ||
                             (d.Surname != null && d.Surname.Contains(term)) ||
                             (d.IdNumber != null && d.IdNumber.Contains(term)) ||
                             (d.DrivingLicenseNo != null && d.DrivingLicenseNo.Contains(term)));
        }

        return await q.OrderBy(d => d.Surname).ThenBy(d => d.FullNames).Take(500).ToListAsync();
    }

    public async Task<Driver> CreateAsync(Driver driver)
    {
        _context.Drivers.Add(driver);
        await _context.SaveChangesAsync();
        return driver;
    }

    public async Task UpdateAsync(Driver driver)
    {
        _context.Drivers.Update(driver);
        await _context.SaveChangesAsync();
    }

    public async Task<Driver?> FindActiveByNameAsync(string fullNames, string surname)
    {
        var fn = (fullNames ?? string.Empty).Trim();
        var sn = (surname ?? string.Empty).Trim();
        if (fn.Length == 0 || sn.Length == 0) return null;

        return await _context.Drivers
            .Where(d => d.DeletedAt == null
                && d.FullNames != null && d.Surname != null
                && d.FullNames.ToLower() == fn.ToLower()
                && d.Surname.ToLower() == sn.ToLower())
            // Prefer an existing record that already has an ID number.
            .OrderByDescending(d => d.IdNumber != null)
            .ThenBy(d => d.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<DriverDeduplicationResult> DeduplicateAsync()
    {
        var result = new DriverDeduplicationResult();

        var drivers = await _context.Drivers
            .Where(d => d.DeletedAt == null)
            .ToListAsync();

        // Group by normalized (full name + surname); only collapse groups with >1 record.
        var groups = drivers
            .GroupBy(d => $"{(d.FullNames ?? string.Empty).Trim().ToLowerInvariant()}|{(d.Surname ?? string.Empty).Trim().ToLowerInvariant()}")
            .Where(g => g.Count() > 1);

        await using var tx = await _context.Database.BeginTransactionAsync();

        foreach (var group in groups)
        {
            var ordered = group
                // Survivor preference: has ID number, then has license, then oldest record.
                .OrderByDescending(d => d.IdNumber != null)
                .ThenByDescending(d => d.DrivingLicenseNo != null)
                .ThenBy(d => d.CreatedAt)
                .ToList();

            var survivor = ordered.First();
            var duplicates = ordered.Skip(1).ToList();
            var dupIds = duplicates.Select(d => d.Id).ToList();

            // Merge any unique fields the survivor is missing from the duplicates.
            foreach (var dup in duplicates)
            {
                survivor.IdNumber ??= dup.IdNumber;
                survivor.DrivingLicenseNo ??= dup.DrivingLicenseNo;
                survivor.NtsaId ??= dup.NtsaId;
                survivor.PhoneNumber ??= dup.PhoneNumber;
                survivor.Email ??= dup.Email;
                survivor.Gender ??= dup.Gender;
                survivor.Nationality ??= dup.Nationality;
                survivor.DateOfBirth ??= dup.DateOfBirth;
                survivor.Address ??= dup.Address;
                survivor.LicenseClass ??= dup.LicenseClass;
                survivor.LicenseIssueDate ??= dup.LicenseIssueDate;
                survivor.LicenseExpiryDate ??= dup.LicenseExpiryDate;
                survivor.TransporterId ??= dup.TransporterId;
                survivor.OrganizationId ??= dup.OrganizationId;
            }
            survivor.UpdatedAt = DateTime.UtcNow;

            // Repoint every FK reference to the survivor (bulk SQL — no change tracking).
            result.ReferencesRepointed += await _context.WeighingTransactions
                .Where(w => w.DriverId.HasValue && dupIds.Contains(w.DriverId.Value))
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.DriverId, survivor.Id));
            result.ReferencesRepointed += await _context.CaseRegisters
                .Where(c => c.DriverId.HasValue && dupIds.Contains(c.DriverId.Value))
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.DriverId, survivor.Id));
            result.ReferencesRepointed += await _context.CaseParties
                .Where(p => p.DriverId.HasValue && dupIds.Contains(p.DriverId.Value))
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.DriverId, survivor.Id));
            result.ReferencesRepointed += await _context.DriverDemeritRecords
                .Where(r => dupIds.Contains(r.DriverId))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.DriverId, survivor.Id));

            // Soft-delete the duplicates.
            foreach (var dup in duplicates)
            {
                dup.DeletedAt = DateTime.UtcNow;
                dup.IsActive = false;
                dup.UpdatedAt = DateTime.UtcNow;
            }

            result.GroupsMerged++;
            result.DriversRemoved += duplicates.Count;
            result.Details.Add(
                $"{survivor.FullNames} {survivor.Surname} (ID {survivor.IdNumber ?? "—"}): merged {duplicates.Count} duplicate(s) into {survivor.Id}");
        }

        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        return result;
    }
}
