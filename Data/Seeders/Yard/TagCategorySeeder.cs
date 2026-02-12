using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Yard;

namespace TruLoad.Backend.Data.Seeders.Yard;

/// <summary>
/// Seeds tag categories for vehicle tagging system.
/// Categories include road authority tags (KeNHA, KURA, KeRRA) and enforcement tags.
/// All seeders are idempotent - safe to run multiple times.
/// </summary>
public static class TagCategorySeeder
{
    public static async Task SeedAsync(TruLoadDbContext context)
    {
        if (await context.TagCategories.AnyAsync()) return;

        var categories = new List<TagCategory>
        {
            new()
            {
                Code = "KENHA",
                Name = "KeNHA Tag",
                Description = "Kenya National Highways Authority - Vehicle flagged for violations on national highways (A-roads, B-roads). Requires hold and special release authorization.",
                IsActive = true
            },
            new()
            {
                Code = "KURA",
                Name = "KURA Tag",
                Description = "Kenya Urban Roads Authority - Vehicle flagged for violations on urban roads. Requires hold and special release authorization.",
                IsActive = true
            },
            new()
            {
                Code = "KERRA",
                Name = "KeRRA Tag",
                Description = "Kenya Rural Roads Authority - Vehicle flagged for violations on rural roads. Requires hold and special release authorization.",
                IsActive = true
            },
            new()
            {
                Code = "HABITUAL_OFFENDER",
                Name = "Habitual Offender",
                Description = "Vehicle flagged as a repeat offender with multiple overload violations. Subject to enhanced penalties and mandatory yard detention.",
                IsActive = true
            },
            new()
            {
                Code = "STOLEN_VEHICLE",
                Name = "Stolen Vehicle",
                Description = "Vehicle reported stolen in police records. Must be detained and police notified immediately.",
                IsActive = true
            },
            new()
            {
                Code = "SUSPENDED_LICENSE",
                Name = "Suspended License",
                Description = "Vehicle or operator license suspended. Vehicle must be held until license status is resolved.",
                IsActive = true
            },
            new()
            {
                Code = "COURT_ORDER",
                Name = "Court Order Hold",
                Description = "Vehicle held pursuant to a court order. Cannot be released without court authorization.",
                IsActive = true
            },
            new()
            {
                Code = "CUSTOMS_HOLD",
                Name = "Customs Hold",
                Description = "Vehicle flagged by Kenya Revenue Authority (KRA) Customs department. Requires customs clearance before release.",
                IsActive = true
            },
            new()
            {
                Code = "INSURANCE_EXPIRED",
                Name = "Insurance Expired",
                Description = "Vehicle insurance expired or invalid. Must provide valid insurance before release.",
                IsActive = true
            },
            new()
            {
                Code = "INSPECTION_DUE",
                Name = "Inspection Due",
                Description = "Vehicle overdue for mandatory NTSA inspection. Requires valid inspection certificate.",
                IsActive = true
            }
        };

        await context.TagCategories.AddRangeAsync(categories);
        await context.SaveChangesAsync();
    }
}
