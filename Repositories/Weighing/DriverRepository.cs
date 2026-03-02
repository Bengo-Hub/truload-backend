using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Data.Repositories.Weighing;

public class DriverRepository : IDriverRepository
{
    private readonly TruLoadDbContext _context;

    public DriverRepository(TruLoadDbContext context)
    {
        _context = context;
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
    /// </summary>
    public async Task<IEnumerable<Driver>> SearchAsync(string query)
    {
        var q = _context.Drivers.AsNoTracking();

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
}
