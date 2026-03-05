using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.CaseManagement;

namespace TruLoad.Backend.Data.Seeders.Infrastructure;

/// <summary>
/// Seeds road–county and road–district (many-to-many) links for Kenya, and magistrate courts per county.
/// Run after KenyaCountiesDistrictsSeeder and RoadsSeeder.
/// </summary>
public class KenyaRoadsCourtsSeeder
{
    private readonly TruLoadDbContext _context;

    public KenyaRoadsCourtsSeeder(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync()
    {
        await SeedRoadCountyDistrictLinksAsync();
        await SeedCourtsAsync();
    }

    private async Task SeedRoadCountyDistrictLinksAsync()
    {
        if (await _context.RoadCounties.AnyAsync())
            return;

        var counties = await _context.Counties.Where(c => c.DeletedAt == null).ToListAsync();
        var districts = await _context.Districts.Where(d => d.DeletedAt == null).ToListAsync();
        var roads = await _context.Roads.Where(r => r.DeletedAt == null).ToListAsync();
        if (counties.Count == 0 || roads.Count == 0)
            return;

        var countyByName = counties.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        // Map road code -> county names (road passes through these counties). Based on KeNHA/geography.
        var roadToCountyNames = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["A1"] = new[] { "Nairobi City", "Machakos", "Kajiado", "Makueni", "Kitui", "Taita-Taveta", "Kilifi", "Mombasa" },
            ["A2"] = new[] { "Nairobi City", "Kiambu", "Nakuru", "Kericho", "Kisumu" },
            ["A3"] = new[] { "Nairobi City", "Kajiado" },
            ["A104"] = new[] { "Nairobi City", "Kiambu" },
            ["B1"] = new[] { "Mombasa", "Kilifi" },
            ["B3"] = new[] { "Kisumu", "Busia" },
            ["B5"] = new[] { "Nakuru", "Uasin Gishu" },
            ["C26"] = new[] { "Nairobi City", "Kiambu" },
            ["C34"] = new[] { "Mombasa", "Kwale" },
            ["D371"] = new[] { "Nairobi City", "Kajiado" },
            ["D403"] = new[] { "Nakuru" },
            ["E856"] = new[] { "Nairobi City" },
            ["E920"] = new[] { "Mombasa" },
            ["A109"] = new[] { "Nairobi City" },
            ["C101"] = new[] { "Nairobi City" },
            ["C102"] = new[] { "Nairobi City", "Kiambu" },
        };

        var roadCounties = new List<RoadCounty>();
        var roadDistricts = new List<RoadDistrict>();

        foreach (var road in roads)
        {
            if (!roadToCountyNames.TryGetValue(road.Code, out var countyNames))
                continue;

            foreach (var name in countyNames)
            {
                if (!countyByName.TryGetValue(name, out var county))
                    continue;

                roadCounties.Add(new RoadCounty { RoadId = road.Id, CountyId = county.Id });
                var countyDistricts = districts.Where(d => d.CountyId == county.Id).ToList();
                foreach (var d in countyDistricts)
                    roadDistricts.Add(new RoadDistrict { RoadId = road.Id, DistrictId = d.Id });
            }
        }

        if (roadCounties.Count > 0)
        {
            _context.RoadCounties.AddRange(roadCounties);
            _context.RoadDistricts.AddRange(roadDistricts);
            await _context.SaveChangesAsync();
        }
    }

    private async Task SeedCourtsAsync()
    {
        if (await _context.Courts.AnyAsync(c => c.DeletedAt == null))
            return;

        var counties = await _context.Counties.Where(c => c.DeletedAt == null).OrderBy(c => c.Name).ToListAsync();
        var districts = await _context.Districts.Where(d => d.DeletedAt == null).ToListAsync();

        var courts = new List<Court>();
        foreach (var county in counties)
        {
            var countyDistricts = districts.Where(d => d.CountyId == county.Id).Take(3).ToList();
            var firstDistrictId = countyDistricts.FirstOrDefault()?.Id;

            var courtName = county.Name == "Nairobi City"
                ? "Nairobi Law Courts (Milimani)"
                : $"{county.Name} Magistrate's Court";
            var code = county.Name == "Nairobi City" ? "NAI-MIL" : $"{county.Code}-MC";

            courts.Add(new Court
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = courtName,
                Location = $"{county.Name}, Kenya",
                CourtType = "magistrate",
                CountyId = county.Id,
                DistrictId = firstDistrictId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        _context.Courts.AddRange(courts);
        await _context.SaveChangesAsync();
    }
}
