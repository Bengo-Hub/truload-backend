using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Data.Seeders.WeighingOperations;

/// <summary>
/// Seeds origins and destinations master data for cargo routes
/// Provides standard locations including cities, towns, ports, and border points in East Africa
/// </summary>
public class OriginsDestinationsSeeder
{
    private readonly TruLoadDbContext _context;

    public OriginsDestinationsSeeder(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync()
    {
        if (await _context.OriginsDestinations.AnyAsync())
        {
            return; // Already seeded
        }

        var locations = new List<OriginsDestinations>
        {
            // Kenya - Major Cities
            new OriginsDestinations
            {
                Id = Guid.NewGuid(),
                Code = "KE-NBI",
                Name = "Nairobi",
                LocationType = "city",
                Country = "Kenya",
                IsActive = true
            },
            new OriginsDestinations
            {
                Id = Guid.NewGuid(),
                Code = "KE-MBA",
                Name = "Mombasa",
                LocationType = "port",
                Country = "Kenya",
                IsActive = true
            },
            new OriginsDestinations
            {
                Id = Guid.NewGuid(),
                Code = "KE-KSM",
                Name = "Kisumu",
                LocationType = "city",
                Country = "Kenya",
                IsActive = true
            },
            new OriginsDestinations
            {
                Id = Guid.NewGuid(),
                Code = "KE-NKU",
                Name = "Nakuru",
                LocationType = "city",
                Country = "Kenya",
                IsActive = true
            },
            new OriginsDestinations
            {
                Id = Guid.NewGuid(),
                Code = "KE-ELD",
                Name = "Eldoret",
                LocationType = "city",
                Country = "Kenya",
                IsActive = true
            },

            // Kenya - Border Points
            new OriginsDestinations
            {
                Id = Guid.NewGuid(),
                Code = "KE-MAL",
                Name = "Malaba Border",
                LocationType = "border",
                Country = "Kenya",
                IsActive = true
            },
            new OriginsDestinations
            {
                Id = Guid.NewGuid(),
                Code = "KE-BUS",
                Name = "Busia Border",
                LocationType = "border",
                Country = "Kenya",
                IsActive = true
            },
            new OriginsDestinations
            {
                Id = Guid.NewGuid(),
                Code = "KE-NAM",
                Name = "Namanga Border",
                LocationType = "border",
                Country = "Kenya",
                IsActive = true
            },

            // Warehouses and Depots
            new OriginsDestinations
            {
                Id = Guid.NewGuid(),
                Code = "KE-ICD",
                Name = "Inland Container Depot - Embakasi",
                LocationType = "warehouse",
                Country = "Kenya",
                IsActive = true
            },
            new OriginsDestinations
            {
                Id = Guid.NewGuid(),
                Code = "KE-EPZ",
                Name = "Export Processing Zone - Athi River",
                LocationType = "warehouse",
                Country = "Kenya",
                IsActive = true
            }
        };

        await _context.OriginsDestinations.AddRangeAsync(locations);
        await _context.SaveChangesAsync();
    }
}
