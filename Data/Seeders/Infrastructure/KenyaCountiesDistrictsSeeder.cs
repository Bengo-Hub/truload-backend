using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.Infrastructure;

namespace TruLoad.Backend.Data.Seeders.Infrastructure;

/// <summary>
/// Seeds Kenyan counties (47) and subcounties with correct FK relationships.
/// When counties already exist (e.g. after District→Subcounty migration), seeds or repairs subcounties
/// so that subcounties have valid CountyId referencing existing counties.
/// </summary>
public class KenyaCountiesDistrictsSeeder
{
    private readonly TruLoadDbContext _context;

    public KenyaCountiesDistrictsSeeder(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync()
    {
        if (!await _context.Counties.AnyAsync())
        {
            await SeedCountiesAndSubcountiesAsync();
            return;
        }

        var counties = await _context.Counties.Where(c => c.DeletedAt == null).OrderBy(c => c.Name).ToListAsync();
        var countyIds = new HashSet<Guid>(counties.Select(c => c.Id));

        var existingSubcounties = await _context.Subcounties.Where(s => s.DeletedAt == null).ToListAsync();
        var subcountyCount = existingSubcounties.Count;

        bool needReseed = subcountyCount == 0 ||
            !existingSubcounties.Any(s => countyIds.Contains(s.CountyId));

        if (!needReseed)
            return;

        if (subcountyCount > 0)
        {
            await _context.RoadSubcounties.ExecuteDeleteAsync();
            await _context.Subcounties.ExecuteDeleteAsync();
            await _context.SaveChangesAsync();
        }

        var subcounties = new List<Subcounty>();
        foreach (var county in counties)
        {
            var names = GetSubcountiesForCounty(county.Name);
            for (int i = 0; i < names.Length; i++)
            {
                subcounties.Add(new Subcounty
                {
                    Id = Guid.NewGuid(),
                    CountyId = county.Id,
                    Code = $"{county.Code}-{(i + 1):D2}",
                    Name = names[i],
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }
        _context.Subcounties.AddRange(subcounties);
        await _context.SaveChangesAsync();
    }

    private async Task SeedCountiesAndSubcountiesAsync()
    {
        var countyNames = new[]
        {
            "Baringo", "Bomet", "Bungoma", "Busia", "Elgeyo-Marakwet", "Embu", "Garissa", "Homa Bay", "Isiolo", "Kajiado",
            "Kakamega", "Kericho", "Kiambu", "Kilifi", "Kirinyaga", "Kisii", "Kisumu", "Kitui", "Kwale", "Laikipia",
            "Lamu", "Machakos", "Makueni", "Mandera", "Marsabit", "Meru", "Migori", "Mombasa", "Murang'a", "Nairobi City",
            "Nakuru", "Nandi", "Narok", "Nyamira", "Nyandarua", "Nyeri", "Samburu", "Siaya", "Taita-Taveta", "Tana River",
            "Tharaka-Nithi", "Trans Nzoia", "Turkana", "Uasin Gishu", "Vihiga", "Wajir", "West Pokot"
        };

        var counties = new List<Counties>();
        for (int i = 0; i < countyNames.Length; i++)
        {
            var c = new Counties
            {
                Id = Guid.NewGuid(),
                Code = $"KE{(i + 1):D2}",
                Name = countyNames[i],
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            counties.Add(c);
        }
        _context.Counties.AddRange(counties);
        await _context.SaveChangesAsync();

        var subcounties = new List<Subcounty>();
        foreach (var county in counties)
        {
            var names = GetSubcountiesForCounty(county.Name);
            for (int i = 0; i < names.Length; i++)
            {
                subcounties.Add(new Subcounty
                {
                    Id = Guid.NewGuid(),
                    CountyId = county.Id,
                    Code = $"{county.Code}-{(i + 1):D2}",
                    Name = names[i],
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }
        _context.Subcounties.AddRange(subcounties);
        await _context.SaveChangesAsync();
    }

    private static string[] GetSubcountiesForCounty(string countyName)
    {
        return countyName switch
        {
            "Nairobi City" => new[] { "Dagoretti North", "Dagoretti South", "Embakasi Central", "Embakasi East", "Embakasi North", "Embakasi South", "Embakasi West", "Kamukunji", "Kasarani", "Kibra", "Langata", "Makadara", "Mathare", "Roysambu", "Ruaraka", "Starehe", "Westlands" },
            "Mombasa" => new[] { "Changamwe", "Jomvu", "Kisauni", "Likoni", "Mvita", "Nyali" },
            "Kisumu" => new[] { "Kisumu Central", "Kisumu East", "Kisumu West", "Seme", "Muhoroni", "Nyakach", "Nyando" },
            "Nakuru" => new[] { "Njoro", "Molo", "Naivasha", "Gilgil", "Kuresoi North", "Kuresoi South", "Subukia", "Rongai", "Bahati", "Nakuru Town East", "Nakuru Town West" },
            "Kiambu" => new[] { "Gatundu North", "Gatundu South", "Githunguri", "Juja", "Kabete", "Kiambaa", "Kiambu", "Kikuyu", "Lari", "Limuru", "Ruiru", "Thika Town" },
            "Meru" => new[] { "Buuri", "Igembe Central", "Igembe North", "Igembe South", "Tigania Central", "Tigania East", "Tigania West", "Meru Central" },
            "Kakamega" => new[] { "Butere", "Kakamega Central", "Kakamega East", "Kakamega North", "Kakamega South", "Khwisero", "Lugari", "Likuyani", "Malava", "Matungu", "Mumias East", "Mumias West", "Navakholo", "Lugari" },
            "Bungoma" => new[] { "Bumula", "Kabuchai", "Kanduyi", "Kimilili", "Mt. Elgon", "Sirisia", "Tongaren", "Webuye East", "Webuye West" },
            "Kericho" => new[] { "Ainamoi", "Belgut", "Bureti", "Kipkelion East", "Kipkelion West", "Soin/Sigowet" },
            "Uasin Gishu" => new[] { "Ainabkoi", "Kapseret", "Kesses", "Moiben", "Soy", "Turbo" },
            "Nyeri" => new[] { "Kieni East", "Kieni West", "Mathira East", "Mathira West", "Mukurweini", "Nyeri Town", "Othaya", "Tetu" },
            "Machakos" => new[] { "Kangundo", "Kathiani", "Machakos Town", "Mavoko", "Masinga", "Matungulu", "Mwala", "Yatta" },
            "Kajiado" => new[] { "Isinya", "Kajiado Central", "Kajiado East", "Kajiado North", "Kajiado West", "Loitokitok", "Mashuuru" },
            "Kilifi" => new[] { "Ganze", "Kaloleni", "Kilifi North", "Kilifi South", "Magarini", "Malindi", "Rabai" },
            "Kisii" => new[] { "Bobasi", "Bomachoge Chache", "Bomachoge Borabu", "Bonchari", "Kitutu Chache North", "Kitutu Chache South", "Nyaribari Chache", "Nyaribari Masaba", "South Mugirango" },
            "Migori" => new[] { "Awendo", "Kuria East", "Kuria West", "Nyatike", "Rongo", "Suna East", "Suna West", "Uriri" },
            "Homa Bay" => new[] { "Homa Bay Town", "Kabondo Kasipul", "Karachuonyo", "Kasipul", "Mbita", "Ndhiwa", "Rangwe", "Suba" },
            "Siaya" => new[] { "Alego Usonga", "Bondo", "Gem", "Rarieda", "Ugenya", "Unguja" },
            "Busia" => new[] { "Budalangi", "Butula", "Funyula", "Nambale", "Teso North", "Teso South" },
            "Bomet" => new[] { "Bomet Central", "Bomet East", "Chepalungu", "Konoin", "Sotik" },
            "Baringo" => new[] { "Baringo Central", "Baringo North", "Baringo South", "Eldama Ravine", "Mogotio", "Tiaty" },
            "Laikipia" => new[] { "Laikipia Central", "Laikipia East", "Laikipia North", "Laikipia West" },
            "Narok" => new[] { "Narok East", "Narok North", "Narok South", "Narok West", "Emurua Dikirr", "Kilgoris" },
            "Kitui" => new[] { "Kitui Central", "Kitui East", "Kitui Rural", "Kitui South", "Mwingi Central", "Mwingi North", "Mwingi West" },
            "Makueni" => new[] { "Kibwezi West", "Kibwezi East", "Kilome", "Makueni", "Mbooni", "Kaiti" },
            "Garissa" => new[] { "Daadab", "Fafi", "Garissa Township", "Hulugho", "Ijara", "Lagdera", "Balambala" },
            "Wajir" => new[] { "Eldas", "Tarbaj", "Wajir East", "Wajir North", "Wajir South", "Wajir West" },
            "Mandera" => new[] { "Banissa", "Lafey", "Mandera East", "Mandera North", "Mandera South", "Mandera West" },
            "Marsabit" => new[] { "Laisamis", "Moyale", "North Horr", "Saku" },
            "Isiolo" => new[] { "Isiolo", "Garbatulla", "Merti" },
            "Turkana" => new[] { "Loima", "Turkana Central", "Turkana East", "Turkana North", "Turkana South", "Turkana West" },
            "West Pokot" => new[] { "Kipkomo", "Pokot Central", "Pokot South", "Sigor" },
            "Samburu" => new[] { "Samburu East", "Samburu North", "Samburu West" },
            "Trans Nzoia" => new[] { "Cherangany", "Endebess", "Kiminini", "Kwanza", "Saboti" },
            "Elgeyo-Marakwet" => new[] { "Keiyo North", "Keiyo South", "Marakwet East", "Marakwet West" },
            "Nandi" => new[] { "Aldai", "Chesumei", "Emgwen", "Mosop", "Nandi Hills", "Tinderet" },
            "Vihiga" => new[] { "Emuhaya", "Hamisi", "Luanda", "Sabatia", "Vihiga" },
            "Nyamira" => new[] { "Borabu", "Manga", "Masaba North", "Nyamira North", "Nyamira South" },
            "Nyandarua" => new[] { "Kinangop", "Kipipiri", "Ndaragwa", "Ol Kalou", "Ol Jorok" },
            "Kirinyaga" => new[] { "Gichugu", "Kirinyaga Central", "Mwea", "Ndia" },
            "Murang'a" => new[] { "Gatanga", "Kandara", "Kangema", "Kigumo", "Kiharu", "Mathioya", "Murang'a South" },
            "Tharaka-Nithi" => new[] { "Chuka/Igambang'ombe", "Maara", "Tharaka" },
            "Embu" => new[] { "Manyatta", "Mbeere North", "Mbeere South", "Runyenjes" },
            "Kwale" => new[] { "Kinango", "Lunga Lunga", "Matuga", "Msambweni" },
            "Tana River" => new[] { "Bura", "Galole", "Garsen" },
            "Lamu" => new[] { "Lamu East", "Lamu West" },
            "Taita-Taveta" => new[] { "Mwatate", "Taveta", "Voi", "Wundanyi" },
            _ => new[] { $"{countyName} Central", $"{countyName} East", $"{countyName} North", $"{countyName} South", $"{countyName} West" }
        };
    }
}
