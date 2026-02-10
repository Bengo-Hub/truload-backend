using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models;

namespace TruLoad.Backend.Data.Seeders.SystemConfiguration;

/// <summary>
/// Seeds ActDefinition lookup table with Kenya Traffic Act and EAC Act.
/// Required for prosecution workflow (ProsecutionCase.ActId FK).
/// </summary>
public static class ActDefinitionSeeder
{
    public static async Task SeedAsync(TruLoadDbContext context)
    {
        if (await context.ActDefinitions.AnyAsync())
            return;

        var acts = new List<ActDefinition>
        {
            new()
            {
                Code = "TRAFFIC_ACT",
                Name = "Kenya Traffic Act Cap 403",
                ActType = "Traffic",
                FullName = "The Traffic Act (Chapter 403 Laws of Kenya)",
                Description = "Kenya national traffic regulations including vehicle weight limits, road safety, and overload penalties",
                EffectiveDate = new DateOnly(1953, 1, 1),
                ChargingCurrency = "KES"
            },
            new()
            {
                Code = "EAC_ACT",
                Name = "EAC Vehicle Load Control Act 2016",
                ActType = "EAC",
                FullName = "The East African Community Vehicle Load Control Act, 2016",
                Description = "Regional harmonized vehicle load control regulations for EAC member states",
                EffectiveDate = new DateOnly(2016, 7, 1),
                ChargingCurrency = "USD"
            }
        };

        await context.ActDefinitions.AddRangeAsync(acts);
        await context.SaveChangesAsync();

        Console.WriteLine($"✓ Seeded {acts.Count} act definitions (TRAFFIC_ACT, EAC_ACT)");
    }
}
