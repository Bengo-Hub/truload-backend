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

    public async Task<IEnumerable<Driver>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<Driver>();

        return await _context.Drivers
            .AsNoTracking()
            .Where(d => d.FullNames.Contains(query) ||
                        d.Surname.Contains(query) ||
                        d.IdNumber.Contains(query) ||
                        d.DrivingLicenseNo.Contains(query))
            .Take(20)
            .ToListAsync();
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
