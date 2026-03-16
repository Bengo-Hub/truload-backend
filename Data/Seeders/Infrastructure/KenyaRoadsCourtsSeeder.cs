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
            ["A1"] = new[] { "Nairobi City", "Mombasa", "Kwale", "Kilifi", "Tana River", "Lamu", "Taita-Taveta", "Garissa", "Wajir", "Mandera" },
            ["A2"] = new[] { "Nairobi City", "Kiambu", "Murang'a", "Kirinyaga", "Nyeri", "Laikipia", "Isiolo", "Marsabit" },
            ["A3"] = new[] { "Kiambu", "Machakos", "Kitui", "Tana River", "Garissa" },
            ["A8"] = new[] { "Nairobi City", "Kiambu", "Nakuru", "Kericho", "Uasin Gishu", "Kakamega", "Bungoma", "Busia" },
            ["A104"] = new[] { "Nairobi City", "Kajiado" },
            ["A109"] = new[] { "Mombasa", "Kwale" },
            ["B1"] = new[] { "Mombasa", "Kilifi" },
            ["B3"] = new[] { "Nakuru", "Bomet", "Kericho", "Kisumu" },
            ["B8"] = new[] { "Mombasa", "Kilifi", "Tana River", "Garissa" },
            ["B5"] = new[] { "Nakuru", "Laikipia", "Nyeri" },
            ["C26"] = new[] { "Nairobi City", "Kiambu" },
            ["C101"] = new[] { "Nairobi City" },
            ["C102"] = new[] { "Nairobi City", "Kiambu" },
            ["C13"] = new[] { "Kiambu", "Murang'a" },
            ["D371"] = new[] { "Nairobi City", "Kajiado" },
            ["D403"] = new[] { "Nakuru" },
            ["E856"] = new[] { "Nairobi City", "Kiambu", "Machakos" },
            ["E920"] = new[] { "Mombasa", "Kwale" },
            ["E200"] = new[] { "Nairobi City", "Kiambu" },
            ["S1"] = new[] { "Nairobi City", "Kiambu" },
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
                
                // Link to all districts in this county (Road passes through the whole county conceptually for filtering)
                var countyDistricts = districts.Where(d => d.CountyId == county.Id).ToList();
                foreach (var d in countyDistricts)
                {
                    roadDistricts.Add(new RoadDistrict { RoadId = road.Id, DistrictId = d.Id });
                }
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
            // Seed multiple courts for major counties
            var countyCourts = new List<string>();
            if (county.Name == "Nairobi City")
            {
                countyCourts.AddRange(new[] { "Milimani Law Courts", "Kibera Law Courts", "Makadara Law Courts" });
            }
            else if (county.Name == "Mombasa")
            {
                countyCourts.AddRange(new[] { "Mombasa Law Courts", "Shanzu Law Courts" });
            }
            else
            {
                countyCourts.Add($"{county.Name} Magistrate's Court");
            }

            var countyDistricts = districts.Where(d => d.CountyId == county.Id).ToList();
            
            for (int i = 0; i < countyCourts.Count; i++)
            {
                var districtId = countyDistricts.ElementAtOrDefault(i)?.Id ?? countyDistricts.FirstOrDefault()?.Id;
                
                courts.Add(new Court
                {
                    Id = Guid.NewGuid(),
                    Code = $"{county.Code}-MC-{i+1:D2}",
                    Name = countyCourts[i],
                    Location = $"{county.Name}, Kenya",
                    CourtType = "magistrate",
                    CountyId = county.Id,
                    DistrictId = districtId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        _context.Courts.AddRange(courts);
        await _context.SaveChangesAsync();
    }
}
