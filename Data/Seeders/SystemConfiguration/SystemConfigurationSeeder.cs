using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using truload_backend.Data;

namespace TruLoad.Backend.Data.Seeders.SystemConfiguration;

/// <summary>
/// Seeds system configuration data: permit types, tolerance settings
/// Idempotent - safe to run multiple times
/// </summary>
public class SystemConfigurationSeeder
{
    private readonly TruLoadDbContext _context;

    public SystemConfigurationSeeder(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync()
    {
        await SeedPermitTypesAsync();
        await SeedToleranceSettingsAsync();
    }

    private async Task SeedPermitTypesAsync()
    {
        var permitTypes = new[]
        {
            new PermitType
            {
                Id = Guid.NewGuid(),
                Code = "2A",
                Name = "Permit 2A - Single Journey",
                Description = "Single journey permit with axle and GVW extensions for overloaded vehicles",
                AxleExtensionKg = 3000,
                GvwExtensionKg = 1000,
                ValidityDays = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new PermitType
            {
                Id = Guid.NewGuid(),
                Code = "3A",
                Name = "Permit 3A - Multiple Journey",
                Description = "Multiple journey permit for vehicles requiring repeated overloading within validity period",
                AxleExtensionKg = 2000,
                GvwExtensionKg = 2000,
                ValidityDays = 30,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new PermitType
            {
                Id = Guid.NewGuid(),
                Code = "3B",
                Name = "Permit 3B - Extended Multiple Journey",
                Description = "Extended multiple journey permit with higher extensions for special cargo",
                AxleExtensionKg = 3000,
                GvwExtensionKg = 3000,
                ValidityDays = 90,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new PermitType
            {
                Id = Guid.NewGuid(),
                Code = "OVERLOAD",
                Name = "Overload Permit - Special Cargo",
                Description = "Special permit for exceptional cargo requiring significant weight extensions",
                AxleExtensionKg = 5000,
                GvwExtensionKg = 5000,
                ValidityDays = 7,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new PermitType
            {
                Id = Guid.NewGuid(),
                Code = "SPECIAL",
                Name = "Special Permit - Custom Configuration",
                Description = "Special permit with custom weight extensions determined case-by-case",
                AxleExtensionKg = 0,
                GvwExtensionKg = 0,
                ValidityDays = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        foreach (var permitType in permitTypes)
        {
            var existing = await _context.PermitTypes
                .FirstOrDefaultAsync(pt => pt.Code == permitType.Code);
            
            if (existing == null)
            {
                await _context.PermitTypes.AddAsync(permitType);
                Console.WriteLine($"✓ Seeded permit type: {permitType.Name} ({permitType.Code})");
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedToleranceSettingsAsync()
    {
        var effectiveDate = DateTime.UtcNow.Date;
        
        var toleranceSettings = new[]
        {
            new ToleranceSetting
            {
                Id = Guid.NewGuid(),
                Code = "EAC_GVW_TOLERANCE",
                Name = "EAC GVW Tolerance",
                LegalFramework = "EAC",
                TolerancePercentage = 5.0m,
                ToleranceKg = null,
                AppliesTo = "GVW",
                Description = "5% tolerance for Gross Vehicle Weight under EAC legal framework",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ToleranceSetting
            {
                Id = Guid.NewGuid(),
                Code = "EAC_AXLE_TOLERANCE",
                Name = "EAC Axle Weight Tolerance",
                LegalFramework = "EAC",
                TolerancePercentage = 5.0m,
                ToleranceKg = null,
                AppliesTo = "AXLE",
                Description = "5% tolerance for individual axle weights under EAC legal framework",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ToleranceSetting
            {
                Id = Guid.NewGuid(),
                Code = "TRAFFIC_ACT_GVW_TOLERANCE",
                Name = "Traffic Act GVW Tolerance",
                LegalFramework = "TRAFFIC_ACT",
                TolerancePercentage = 0.0m,
                ToleranceKg = null,
                AppliesTo = "GVW",
                Description = "Zero tolerance for Gross Vehicle Weight under Traffic Act (strict enforcement)",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ToleranceSetting
            {
                Id = Guid.NewGuid(),
                Code = "TRAFFIC_ACT_AXLE_TOLERANCE",
                Name = "Traffic Act Axle Weight Tolerance",
                LegalFramework = "TRAFFIC_ACT",
                TolerancePercentage = 0.0m,
                ToleranceKg = null,
                AppliesTo = "AXLE",
                Description = "Zero tolerance for individual axle weights under Traffic Act (strict enforcement)",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ToleranceSetting
            {
                Id = Guid.NewGuid(),
                Code = "BOTH_GVW_TOLERANCE",
                Name = "Combined Framework GVW Tolerance",
                LegalFramework = "BOTH",
                TolerancePercentage = 5.0m,
                ToleranceKg = null,
                AppliesTo = "GVW",
                Description = "Default 5% tolerance when both frameworks apply (use most lenient)",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ToleranceSetting
            {
                Id = Guid.NewGuid(),
                Code = "BOTH_AXLE_TOLERANCE",
                Name = "Combined Framework Axle Weight Tolerance",
                LegalFramework = "BOTH",
                TolerancePercentage = 5.0m,
                ToleranceKg = null,
                AppliesTo = "AXLE",
                Description = "Default 5% tolerance for axle weights when both frameworks apply (use most lenient)",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        foreach (var setting in toleranceSettings)
        {
            var existing = await _context.ToleranceSettings
                .FirstOrDefaultAsync(ts => ts.Code == setting.Code);
            
            if (existing == null)
            {
                await _context.ToleranceSettings.AddAsync(setting);
                Console.WriteLine($"✓ Seeded tolerance setting: {setting.Name} ({setting.Code})");
            }
        }

        await _context.SaveChangesAsync();
    }
}
