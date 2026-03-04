using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Data.Seeders.WeighingOperations;

/// <summary>
/// Seeds roads master data with district linkages
/// Provides standard road classifications (A, B, C, D, E) for weighing station locations
/// Note: DistrictId will be null if districts are not seeded; safe to run before district seeder
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

        // Roads seeded without district linkage for now
        // Districts can be linked later when Counties/Districts system is fully implemented
        var roads = new List<Roads>
        {
            // Class A - International Trunk Roads
            new Roads
            {
                Id = Guid.NewGuid(),
                Code = "A1",
                Name = "Nairobi-Mombasa Highway",
                RoadClass = "A",
                DistrictId = null,
                TotalLengthKm = 485.5m,
                IsActive = true
            },
            new Roads
            {
                Id = Guid.NewGuid(),
                Code = "A2",
                Name = "Nairobi-Nakuru-Kisumu Highway",
                RoadClass = "A",
                DistrictId = null,
                TotalLengthKm = 347.2m,
                IsActive = true
            },
            new Roads
            {
                Id = Guid.NewGuid(),
                Code = "A3",
                Name = "Nairobi-Namanga Highway",
                RoadClass = "A",
                DistrictId = null,
                TotalLengthKm = 164.8m,
                IsActive = true
            },
            new Roads
            {
                Id = Guid.NewGuid(),
                Code = "A104",
                Name = "Nairobi-Thika Highway",
                RoadClass = "A",
                DistrictId = null,
                TotalLengthKm = 50.4m,
                IsActive = true
            },

            // Class B - National Trunk Roads
            new Roads
            {
                Id = Guid.NewGuid(),
                Code = "B1",
                Name = "Mombasa-Malindi Road",
                RoadClass = "B",
                DistrictId = null,
                TotalLengthKm = 120.3m,
                IsActive = true
            },
            new Roads
            {
                Id = Guid.NewGuid(),
                Code = "B3",
                Name = "Kisumu-Busia Road",
                RoadClass = "B",
                DistrictId = null,
                TotalLengthKm = 95.7m,
                IsActive = true
            },
            new Roads
            {
                Id = Guid.NewGuid(),
                Code = "B5",
                Name = "Nakuru-Eldoret Road",
                RoadClass = "B",
                TotalLengthKm = 160.2m,
                IsActive = true
            },

            // Class C - Primary Roads
            new Roads
            {
                Id = Guid.NewGuid(),
                Code = "C26",
                Name = "Nairobi-Kiambu Road",
                RoadClass = "C",
                DistrictId = null,
                TotalLengthKm = 18.5m,
                IsActive = true
            },
            new Roads
            {
                Id = Guid.NewGuid(),
                Code = "C34",
                Name = "Mombasa-Lunga Lunga Road",
                RoadClass = "C",
                DistrictId = null,
                TotalLengthKm = 112.8m,
                IsActive = true
            },

            // Class D - Secondary Roads
            new Roads
            {
                Id = Guid.NewGuid(),
                Code = "D371",
                Name = "Nairobi-Ongata Rongai Road",
                RoadClass = "D",
                DistrictId = null,
                TotalLengthKm = 25.4m,
                IsActive = true
            },
            new Roads
            {
                Id = Guid.NewGuid(),
                Code = "D403",
                Name = "Nakuru-Naivasha Road",
                RoadClass = "D",
                TotalLengthKm = 45.6m,
                IsActive = true
            },

            // Class E - Minor Roads
            new Roads
            {
                Id = Guid.NewGuid(),
                Code = "E856",
                Name = "Nairobi Eastern Bypass",
                RoadClass = "E",
                DistrictId = null,
                TotalLengthKm = 32.4m,
                IsActive = true
            },
            new Roads
            {
                Id = Guid.NewGuid(),
                Code = "E920",
                Name = "Mombasa Southern Bypass",
                RoadClass = "E",
                DistrictId = null,
                TotalLengthKm = 15.2m,
                IsActive = true
            },

            // Urban / Traffic Act roads (Nairobi and common weighing locations)
            new Roads
            {
                Id = Guid.NewGuid(),
                Code = "A109",
                Name = "Langata Road",
                RoadClass = "A",
                DistrictId = null,
                TotalLengthKm = 12.5m,
                IsActive = true
            },
            new Roads
            {
                Id = Guid.NewGuid(),
                Code = "C101",
                Name = "Uhuru Highway",
                RoadClass = "C",
                DistrictId = null,
                TotalLengthKm = 8.2m,
                IsActive = true
            },
            new Roads
            {
                Id = Guid.NewGuid(),
                Code = "C102",
                Name = "Waiyaki Way",
                RoadClass = "C",
                DistrictId = null,
                TotalLengthKm = 15.3m,
                IsActive = true
            }
        };

        await _context.Roads.AddRangeAsync(roads);
        await _context.SaveChangesAsync();
    }
}
