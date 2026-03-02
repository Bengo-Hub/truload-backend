using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Data.Repositories.Weighing;

public class VehicleRepository : IVehicleRepository
{
    private readonly TruLoadDbContext _context;

    public VehicleRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<Vehicle?> GetByIdAsync(Guid id)
    {
        return await _context.Vehicles
            .AsNoTracking()
            .Include(v => v.Owner)
            .Include(v => v.Transporter)
            .FirstOrDefaultAsync(v => v.Id == id);
    }

    public async Task<Vehicle?> GetByRegNoAsync(string regNo)
    {
        return await _context.Vehicles
            .AsNoTracking()
            .Include(v => v.Owner)
            .Include(v => v.Transporter)
            .FirstOrDefaultAsync(v => v.RegNo == regNo);
    }

    /// <summary>
    /// Search vehicles by reg no, chassis, or engine. When query is empty, returns all vehicles (up to 500) for dropdowns and setup tabs.
    /// </summary>
    public async Task<IEnumerable<Vehicle>> SearchAsync(string query)
    {
        var q = _context.Vehicles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            q = q.Where(v => (v.RegNo != null && v.RegNo.Contains(term)) ||
                             (v.ChassisNo != null && v.ChassisNo.Contains(term)) ||
                             (v.EngineNo != null && v.EngineNo.Contains(term)));
        }

        return await q.OrderBy(v => v.RegNo).Take(500).ToListAsync();
    }

    public async Task<Vehicle> CreateAsync(Vehicle vehicle)
    {
        _context.Vehicles.Add(vehicle);
        await _context.SaveChangesAsync();
        return vehicle;
    }

    public async Task UpdateAsync(Vehicle vehicle)
    {
        _context.Vehicles.Update(vehicle);
        await _context.SaveChangesAsync();
    }
}
