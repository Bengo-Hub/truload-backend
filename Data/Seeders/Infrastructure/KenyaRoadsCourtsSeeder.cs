using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Models.Infrastructure;

namespace TruLoad.Backend.Data.Seeders.Infrastructure;

/// <summary>
/// Seeds road–county and road–subcounty (many-to-many) links for Kenya, and magistrate courts per county.
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
        await SeedRoadCountySubcountyLinksAsync();
        await SeedCourtsAsync();
    }

    private async Task SeedRoadCountySubcountyLinksAsync()
    {
        var counties = await _context.Counties.Where(c => c.DeletedAt == null).ToListAsync();
        var subcounties = await _context.Subcounties.Where(s => s.DeletedAt == null).ToListAsync();
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
        var roadSubcounties = new List<RoadSubcounty>();

        foreach (var road in roads)
        {
            if (!roadToCountyNames.TryGetValue(road.Code, out var countyNames))
                continue;

            foreach (var name in countyNames)
            {
                if (!countyByName.TryGetValue(name, out var county))
                    continue;

                roadCounties.Add(new RoadCounty { RoadId = road.Id, CountyId = county.Id });

                var countySubcounties = subcounties.Where(s => s.CountyId == county.Id).ToList();
                foreach (var s in countySubcounties)
                {
                    roadSubcounties.Add(new RoadSubcounty { RoadId = road.Id, SubcountyId = s.Id });
                }
            }
        }

        var hasRoadCounties = await _context.RoadCounties.AnyAsync();
        if (!hasRoadCounties && roadCounties.Count > 0)
        {
            _context.RoadCounties.AddRange(roadCounties);
            await _context.SaveChangesAsync();
        }

        var hasRoadSubcounties = await _context.RoadSubcounties.AnyAsync();
        if (!hasRoadSubcounties && roadSubcounties.Count > 0)
        {
            _context.RoadSubcounties.AddRange(roadSubcounties);
            await _context.SaveChangesAsync();
        }
    }

    private async Task SeedCourtsAsync()
    {
        var counties = await _context.Counties.Where(c => c.DeletedAt == null).OrderBy(c => c.Name).ToListAsync();
        var subcounties = await _context.Subcounties.Where(s => s.DeletedAt == null).ToListAsync();

        if (!await _context.Courts.AnyAsync(c => c.DeletedAt == null))
        {
            var courts = new List<Court>();
            foreach (var county in counties)
            {
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

                var countySubcounties = subcounties.Where(s => s.CountyId == county.Id).ToList();

                for (int i = 0; i < countyCourts.Count; i++)
                {
                    var subcountyId = countySubcounties.ElementAtOrDefault(i)?.Id ?? countySubcounties.FirstOrDefault()?.Id;

                    courts.Add(new Court
                    {
                        Id = Guid.NewGuid(),
                        Code = $"{county.Code}-MC-{i + 1:D2}",
                        Name = countyCourts[i],
                        Location = $"{county.Name}, Kenya",
                        CourtType = "magistrate",
                        CountyId = county.Id,
                        SubcountyId = subcountyId,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            _context.Courts.AddRange(courts);
            await _context.SaveChangesAsync();
            return;
        }

        await RepairCourtSubcountyReferencesAsync(subcounties);
    }

    /// <summary>
    /// Updates courts whose SubcountyId no longer exists (e.g. after subcounty re-seed) to the first subcounty of their county.
    /// </summary>
    private async Task RepairCourtSubcountyReferencesAsync(List<Subcounty> subcounties)
    {
        var validSubcountyIds = subcounties.Select(s => s.Id).ToHashSet();
        var countyToFirstSubcounty = subcounties
            .GroupBy(s => s.CountyId)
            .ToDictionary(g => g.Key, g => g.First().Id);

        var courts = await _context.Courts.Where(c => c.DeletedAt == null).ToListAsync();
        var updated = false;
        foreach (var court in courts)
        {
            if (court.CountyId is null)
                continue;
            var currentValid = court.SubcountyId.HasValue && validSubcountyIds.Contains(court.SubcountyId.Value);
            if (currentValid)
                continue;
            court.SubcountyId = countyToFirstSubcounty.TryGetValue(court.CountyId.Value, out var firstId) ? firstId : null;
            court.UpdatedAt = DateTime.UtcNow;
            updated = true;
        }

        if (updated)
            await _context.SaveChangesAsync();
    }
}
