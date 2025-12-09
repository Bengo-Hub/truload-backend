using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using truload_backend.Data;

namespace TruLoad.Backend.Data.Seeders.WeighingOperations;

/// <summary>
/// Seeds weighing operations base data: tyre types, axle groups, axle configurations, references, and fee schedules
/// Loads data from JSON seed files for maintainability and reduced parsing overhead
/// </summary>
public class WeighingOperationsSeeder
{
    private readonly TruLoadDbContext _context;
    private readonly string _seedDataPath;

    public WeighingOperationsSeeder(TruLoadDbContext context, string seedDataPath)
    {
        _context = context;
        _seedDataPath = seedDataPath;
    }

    public async Task SeedAsync()
    {
        var seedDataFile = Path.Combine(_seedDataPath, "axle-seed-data.json");
        
        if (!File.Exists(seedDataFile))
        {
            Console.WriteLine($"⚠ Seed data file not found: {seedDataFile}");
            return;
        }
        
        var jsonContent = await File.ReadAllTextAsync(seedDataFile);
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;
        
        // Load data from JSON in order
        await SeedTyreTypesAsync(root.GetProperty("tyreTypes"));
        await SeedAxleGroupsAsync(root.GetProperty("axleGroups"));
        await SeedAxleConfigurationsAsync(root.GetProperty("axleConfigurations"));
        await SeedAxleWeightReferencesAsync(root.GetProperty("axleWeightReferences"));
        await SeedAxleFeeSchedulesAsync(root.GetProperty("axleFeeSchedules"));
    }

    private async Task SeedTyreTypesAsync(JsonElement tyreTypesElement)
    {
        Console.WriteLine("=== Seeding Tyre Types ===");
        
        int seeded = 0;
        foreach (var tyreElement in tyreTypesElement.EnumerateArray())
        {
            var code = tyreElement.GetProperty("code").GetString() ?? "";
            
            var existing = await _context.TyreTypes
                .FirstOrDefaultAsync(tt => tt.Code == code);
            
            if (existing == null)
            {
                var tyreType = new TyreType
                {
                    Code = code,
                    Name = tyreElement.GetProperty("name").GetString() ?? "",
                    Description = tyreElement.GetProperty("description").GetString(),
                    TypicalMaxWeightKg = tyreElement.GetProperty("typicalMaxWeightKg").GetInt32(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                
                await _context.TyreTypes.AddAsync(tyreType);
                seeded++;
            }
        }

        await _context.SaveChangesAsync();
        Console.WriteLine($"✓ Seeded {seeded} tyre types");
    }

    private async Task SeedAxleGroupsAsync(JsonElement axleGroupsElement)
    {
        Console.WriteLine("=== Seeding Axle Groups ===");
        
        int seeded = 0;
        foreach (var groupElement in axleGroupsElement.EnumerateArray())
        {
            var code = groupElement.GetProperty("code").GetString() ?? "";
            
            var existing = await _context.AxleGroups
                .FirstOrDefaultAsync(ag => ag.Code == code);
            
            if (existing == null)
            {
                var group = new AxleGroup
                {
                    Id = Guid.NewGuid(),
                    Code = code,
                    Name = groupElement.GetProperty("name").GetString() ?? "",
                    Description = groupElement.GetProperty("description").GetString(),
                    TypicalWeightKg = groupElement.GetProperty("typicalWeightKg").GetInt32(),
                    MinSpacingFeet = groupElement.TryGetProperty("minSpacingFeet", out var min) && min.ValueKind != JsonValueKind.Null 
                        ? (decimal?)min.GetDecimal() : null,
                    MaxSpacingFeet = groupElement.TryGetProperty("maxSpacingFeet", out var max) && max.ValueKind != JsonValueKind.Null 
                        ? (decimal?)max.GetDecimal() : null,
                    AxleCountInGroup = groupElement.GetProperty("axleCountInGroup").GetInt32(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                
                await _context.AxleGroups.AddAsync(group);
                seeded++;
            }
        }

        await _context.SaveChangesAsync();
        Console.WriteLine($"✓ Seeded {seeded} axle groups");
    }

    private async Task SeedAxleConfigurationsAsync(JsonElement configurationsElement)
    {
        Console.WriteLine("=== Seeding Axle Configurations (Standard) ===");
        
        int seeded = 0;
        foreach (var configElement in configurationsElement.EnumerateArray())
        {
            var axleCode = configElement.GetProperty("axleCode").GetString() ?? "";
            
            var existing = await _context.AxleConfigurations
                .FirstOrDefaultAsync(ac => ac.AxleCode == axleCode);
            
            if (existing == null)
            {
                var config = new AxleConfiguration
                {
                    Id = Guid.NewGuid(),
                    AxleCode = axleCode,
                    AxleName = configElement.GetProperty("axleName").GetString() ?? axleCode,
                    Description = $"Standard configuration: {configElement.GetProperty("axleNumber").GetInt32()} axles",
                    AxleNumber = configElement.GetProperty("axleNumber").GetInt32(),
                    GvwPermissibleKg = configElement.GetProperty("gvw").GetInt32(),
                    IsStandard = true,
                    LegalFramework = "BOTH",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                await _context.AxleConfigurations.AddAsync(config);
                seeded++;
            }
        }

        await _context.SaveChangesAsync();
        Console.WriteLine($"✓ Seeded {seeded} axle configurations");
    }

    private async Task SeedAxleWeightReferencesAsync(JsonElement referencesElement)
    {
        Console.WriteLine("=== Seeding Axle Weight References ===");
        
        // Load all axle groups and tyre types once
        var axleGroups = await _context.AxleGroups.ToListAsync();
        var tyreTypes = await _context.TyreTypes.ToListAsync();
        var configurations = await _context.AxleConfigurations.ToListAsync();
        
        int seeded = 0;
        foreach (var refElement in referencesElement.EnumerateArray())
        {
            var axleCodeStr = refElement.GetProperty("axleCode").GetString() ?? "";
            var position = refElement.GetProperty("axlePosition").GetInt32();
            
            var existing = await _context.AxleWeightReferences
                .FirstOrDefaultAsync(awr => 
                    awr.AxleConfiguration!.AxleCode == axleCodeStr && 
                    awr.AxlePosition == position);
            
            if (existing == null)
            {
                var parentConfig = configurations.FirstOrDefault(ac => ac.AxleCode == axleCodeStr);
                var axleGroup = axleGroups.FirstOrDefault(ag => 
                    ag.Code == refElement.GetProperty("axleGroupCode").GetString());
                
                var tyreTypeCode = refElement.GetProperty("tyreTypeCode").ValueKind != JsonValueKind.Null 
                    ? refElement.GetProperty("tyreTypeCode").GetString() : null;
                var tyreType = string.IsNullOrEmpty(tyreTypeCode) ? null 
                    : tyreTypes.FirstOrDefault(tt => tt.Code == tyreTypeCode);
                
                if (parentConfig != null && axleGroup != null)
                {
                    var reference = new AxleWeightReference
                    {
                        Id = Guid.NewGuid(),
                        AxleConfigurationId = parentConfig.Id,
                        AxlePosition = position,
                        AxleLegalWeightKg = refElement.GetProperty("legalWeightKg").GetInt32(),
                        AxleGroupId = axleGroup.Id,
                        AxleGrouping = refElement.GetProperty("axleGrouping").GetString() ?? "",
                        TyreTypeId = tyreType?.Id,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    
                    await _context.AxleWeightReferences.AddAsync(reference);
                    seeded++;
                }
            }
        }

        await _context.SaveChangesAsync();
        Console.WriteLine($"✓ Seeded {seeded} axle weight references");
    }

    private async Task SeedAxleFeeSchedulesAsync(JsonElement feeSchedulesElement)
    {
        Console.WriteLine("=== Seeding Axle Fee Schedules ===");
        
        var today = DateOnly.FromDateTime(DateTime.Today);
        
        int seeded = 0;
        foreach (var feeElement in feeSchedulesElement.EnumerateArray())
        {
            var legalFramework = feeElement.GetProperty("legalFramework").GetString() ?? "";
            var feeType = feeElement.GetProperty("feeType").GetString() ?? "";
            var overloadMin = feeElement.GetProperty("overloadMinKg").GetInt32();
            var overloadMax = feeElement.TryGetProperty("overloadMaxKg", out var maxEl) && maxEl.ValueKind != JsonValueKind.Null 
                ? (int?)maxEl.GetInt32() : null;
            
            var existing = await _context.AxleFeeSchedules
                .FirstOrDefaultAsync(afs => 
                    afs.LegalFramework == legalFramework &&
                    afs.FeeType == feeType &&
                    afs.OverloadMinKg == overloadMin &&
                    afs.OverloadMaxKg == overloadMax &&
                    afs.EffectiveFrom == today);
            
            if (existing == null)
            {
                var schedule = new AxleFeeSchedule
                {
                    Id = Guid.NewGuid(),
                    LegalFramework = legalFramework,
                    FeeType = feeType,
                    OverloadMinKg = overloadMin,
                    OverloadMaxKg = overloadMax,
                    FeePerKgUsd = feeElement.GetProperty("feePerKgUsd").GetDecimal(),
                    FlatFeeUsd = feeElement.GetProperty("flatFeeUsd").GetDecimal(),
                    DemeritPoints = feeElement.GetProperty("demeritPoints").GetInt32(),
                    PenaltyDescription = feeElement.GetProperty("penaltyDescription").GetString(),
                    EffectiveFrom = today,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                
                await _context.AxleFeeSchedules.AddAsync(schedule);
                seeded++;
            }
        }

        await _context.SaveChangesAsync();
        Console.WriteLine($"✓ Seeded {seeded} axle fee schedules");
    }

}
