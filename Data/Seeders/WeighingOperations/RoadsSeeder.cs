using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Data.Seeders.WeighingOperations;

/// <summary>
/// Seeds roads master data. Road–county and road–district links are seeded in KenyaRoadsCourtsSeeder (many-to-many).
/// </summary>
public class RoadsSeeder
{
    private readonly TruLoadDbContext _context;

    public RoadsSeeder(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync()
    {
        if (await _context.Roads.AnyAsync())
        {
            return; // Already seeded
        }

        var roads = new List<Roads>
        {
            new Roads { Id = Guid.NewGuid(), Code = "A1", Name = "Nairobi-Mombasa Highway", RoadClass = "A", TotalLengthKm = 485.5m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "A2", Name = "Nairobi-Nakuru-Kisumu Highway", RoadClass = "A", TotalLengthKm = 347.2m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "A3", Name = "Nairobi-Namanga Highway", RoadClass = "A", TotalLengthKm = 164.8m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "A104", Name = "Nairobi-Thika Highway", RoadClass = "A", TotalLengthKm = 50.4m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "B1", Name = "Mombasa-Malindi Road", RoadClass = "B", TotalLengthKm = 120.3m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "B3", Name = "Kisumu-Busia Road", RoadClass = "B", TotalLengthKm = 95.7m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "B5", Name = "Nakuru-Eldoret Road", RoadClass = "B", TotalLengthKm = 160.2m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "C26", Name = "Nairobi-Kiambu Road", RoadClass = "C", TotalLengthKm = 18.5m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "C34", Name = "Mombasa-Lunga Lunga Road", RoadClass = "C", TotalLengthKm = 112.8m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "D371", Name = "Nairobi-Ongata Rongai Road", RoadClass = "D", TotalLengthKm = 25.4m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "D403", Name = "Nakuru-Naivasha Road", RoadClass = "D", TotalLengthKm = 45.6m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "E856", Name = "Nairobi Eastern Bypass", RoadClass = "E", TotalLengthKm = 32.4m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "E920", Name = "Mombasa Southern Bypass", RoadClass = "E", TotalLengthKm = 15.2m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "A109", Name = "Langata Road", RoadClass = "A", TotalLengthKm = 12.5m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "C101", Name = "Uhuru Highway", RoadClass = "C", TotalLengthKm = 8.2m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "C102", Name = "Waiyaki Way", RoadClass = "C", TotalLengthKm = 15.3m, IsActive = true },
        };

        await _context.Roads.AddRangeAsync(roads);
        await _context.SaveChangesAsync();
    }
}
