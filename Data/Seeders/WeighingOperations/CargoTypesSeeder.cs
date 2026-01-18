using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Data.Seeders.WeighingOperations;

/// <summary>
/// Seeds cargo types master data for weighing operations
/// Provides standard cargo classifications including general, hazardous, and perishable categories
/// </summary>
public class CargoTypesSeeder
{
    private readonly TruLoadDbContext _context;

    public CargoTypesSeeder(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync()
    {
        if (await _context.CargoTypes.AnyAsync())
        {
            return; // Already seeded
        }

        var cargoTypes = new List<CargoTypes>
        {
            // General Cargo
            new CargoTypes
            {
                Id = Guid.NewGuid(),
                Code = "GEN-001",
                Name = "General Goods",
                Category = "General",
                IsActive = true
            },
            new CargoTypes
            {
                Id = Guid.NewGuid(),
                Code = "GEN-002",
                Name = "Construction Materials",
                Category = "General",
                IsActive = true
            },
            new CargoTypes
            {
                Id = Guid.NewGuid(),
                Code = "GEN-003",
                Name = "Agricultural Produce",
                Category = "General",
                IsActive = true
            },
            new CargoTypes
            {
                Id = Guid.NewGuid(),
                Code = "GEN-004",
                Name = "Manufactured Goods",
                Category = "General",
                IsActive = true
            },
            new CargoTypes
            {
                Id = Guid.NewGuid(),
                Code = "GEN-005",
                Name = "Textiles and Garments",
                Category = "General",
                IsActive = true
            },

            // Hazardous Materials
            new CargoTypes
            {
                Id = Guid.NewGuid(),
                Code = "HAZ-001",
                Name = "Flammable Liquids",
                Category = "Hazardous",
                IsActive = true
            },
            new CargoTypes
            {
                Id = Guid.NewGuid(),
                Code = "HAZ-002",
                Name = "Toxic Substances",
                Category = "Hazardous",
                IsActive = true
            },
            new CargoTypes
            {
                Id = Guid.NewGuid(),
                Code = "HAZ-003",
                Name = "Corrosive Materials",
                Category = "Hazardous",
                IsActive = true
            },
            new CargoTypes
            {
                Id = Guid.NewGuid(),
                Code = "HAZ-004",
                Name = "Compressed Gases",
                Category = "Hazardous",
                IsActive = true
            },

            // Perishable Goods
            new CargoTypes
            {
                Id = Guid.NewGuid(),
                Code = "PER-001",
                Name = "Fresh Fruits and Vegetables",
                Category = "Perishable",
                IsActive = true
            },
            new CargoTypes
            {
                Id = Guid.NewGuid(),
                Code = "PER-002",
                Name = "Dairy Products",
                Category = "Perishable",
                IsActive = true
            },
            new CargoTypes
            {
                Id = Guid.NewGuid(),
                Code = "PER-003",
                Name = "Meat and Poultry",
                Category = "Perishable",
                IsActive = true
            },
            new CargoTypes
            {
                Id = Guid.NewGuid(),
                Code = "PER-004",
                Name = "Seafood and Fish",
                Category = "Perishable",
                IsActive = true
            },
            new CargoTypes
            {
                Id = Guid.NewGuid(),
                Code = "PER-005",
                Name = "Pharmaceuticals",
                Category = "Perishable",
                IsActive = true
            }
        };

        await _context.CargoTypes.AddRangeAsync(cargoTypes);
        await _context.SaveChangesAsync();
    }
}
