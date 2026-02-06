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

    public async Task<IEnumerable<Vehicle>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<Vehicle>();

        return await _context.Vehicles
            .AsNoTracking()
            .Where(v => v.RegNo.Contains(query) ||
                        (v.ChassisNo != null && v.ChassisNo.Contains(query)) ||
                        (v.EngineNo != null && v.EngineNo.Contains(query)))
            .Take(20)
            .ToListAsync();
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
