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
            // International Trunk Roads (Class A)
            new Roads { Id = Guid.NewGuid(), Code = "A1", Name = "Nairobi-Mombasa Highway", RoadClass = "A", TotalLengthKm = 485.5m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "A2", Name = "Nairobi-Thika-Isiolo-Moyale Road", RoadClass = "A", TotalLengthKm = 800.0m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "A3", Name = "Thika-Garissa-Liboi Road", RoadClass = "A", TotalLengthKm = 550.0m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "A8", Name = "Nairobi-Nakuru-Eldoret-Malaba Road", RoadClass = "A", TotalLengthKm = 450.0m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "A104", Name = "Nairobi-Namanga Road", RoadClass = "A", TotalLengthKm = 160.0m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "A109", Name = "Mombasa-Lunga Lunga Road", RoadClass = "A", TotalLengthKm = 110.0m, IsActive = true },

            // National Trunk Roads (Class B)
            new Roads { Id = Guid.NewGuid(), Code = "B1", Name = "Mombasa-Malindi Road", RoadClass = "B", TotalLengthKm = 120.3m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "B3", Name = "Mau Summit-Kericho-Kisumu Road", RoadClass = "B", TotalLengthKm = 150.0m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "B8", Name = "Mombasa-Garissa Road", RoadClass = "B", TotalLengthKm = 463.0m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "B5", Name = "Nakuru-Nyahururu-Nyeri Road", RoadClass = "B", TotalLengthKm = 160.2m, IsActive = true },

            // Primary Roads (Class C)
            new Roads { Id = Guid.NewGuid(), Code = "C26", Name = "Nairobi-Kiambu Road", RoadClass = "C", TotalLengthKm = 18.5m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "C101", Name = "Uhuru Highway", RoadClass = "C", TotalLengthKm = 8.2m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "C102", Name = "Waiyaki Way", RoadClass = "C", TotalLengthKm = 15.3m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "C13", Name = "Thika Circular Road", RoadClass = "C", TotalLengthKm = 22.0m, IsActive = true },

            // Secondary Roads (Class D)
            new Roads { Id = Guid.NewGuid(), Code = "D371", Name = "Nairobi-Ongata Rongai Road", RoadClass = "D", TotalLengthKm = 25.4m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "D403", Name = "Nakuru-Naivasha Road", RoadClass = "D", TotalLengthKm = 45.6m, IsActive = true },

            // Minor Roads (Class E) & Bypasses
            new Roads { Id = Guid.NewGuid(), Code = "E856", Name = "Nairobi Eastern Bypass", RoadClass = "E", TotalLengthKm = 32.4m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "E920", Name = "Mombasa Southern Bypass", RoadClass = "E", TotalLengthKm = 15.2m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "E200", Name = "Nairobi Northern Bypass", RoadClass = "E", TotalLengthKm = 21.0m, IsActive = true },
            new Roads { Id = Guid.NewGuid(), Code = "S1", Name = "Nairobi Southern Bypass", RoadClass = "S", TotalLengthKm = 28.6m, IsActive = true }
        };

        await _context.Roads.AddRangeAsync(roads);
        await _context.SaveChangesAsync();
    }
}
