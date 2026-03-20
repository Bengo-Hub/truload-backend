using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Data.Seeders.WeighingOperations;

/// <summary>
/// Seeds weighing operations base data from split JSON files:
///   - core-axle-seed-data.json      → tyre types, axle groups, fee schedules
///   - axle-configs-seed-data.json   → all axle configurations (standard + derived)
///   - axle-refs-{N}-axle.json       → weight references split by axle count (1-7)
///
/// Fully idempotent — safe to run multiple times without dropping the DB.
/// Upserts by natural key (Code, AxleCode, Position, etc.).
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
        // 1. Core data: tyre types, axle groups, fee schedules
        var coreFile = Path.Combine(_seedDataPath, "core-axle-seed-data.json");
        if (File.Exists(coreFile))
        {
            var coreJson = await File.ReadAllTextAsync(coreFile);
            using var coreDoc = JsonDocument.Parse(coreJson);
            var root = coreDoc.RootElement;

            if (root.TryGetProperty("tyreTypes", out var tt)) await SeedTyreTypesAsync(tt);
            if (root.TryGetProperty("axleGroups", out var ag)) await SeedAxleGroupsAsync(ag);
            if (root.TryGetProperty("axleFeeSchedules", out var fs)) await SeedAxleFeeSchedulesAsync(fs);
        }
        else
        {
            Console.WriteLine($"⚠ Core seed data not found: {coreFile}");
        }

        // 2. Axle configurations
        var configsFile = Path.Combine(_seedDataPath, "axle-configs-seed-data.json");
        if (File.Exists(configsFile))
        {
            var configsJson = await File.ReadAllTextAsync(configsFile);
            using var configsDoc = JsonDocument.Parse(configsJson);
            var configsArray = configsDoc.RootElement.GetProperty("axleConfigurations");

            // Pre-scan refs to know which configs have references
            var codesWithRefs = await PreScanReferenceCodes();
            await SeedAxleConfigurationsAsync(configsArray, codesWithRefs);
        }
        else
        {
            Console.WriteLine($"⚠ Configs seed data not found: {configsFile}");
        }

        // 3. Weight references from per-axle-count files
        for (int n = 1; n <= 7; n++)
        {
            var refsFile = Path.Combine(_seedDataPath, $"axle-refs-{n}-axle.json");
            if (File.Exists(refsFile))
            {
                var refsJson = await File.ReadAllTextAsync(refsFile);
                using var refsDoc = JsonDocument.Parse(refsJson);
                var refsArray = refsDoc.RootElement.GetProperty("axleWeightReferences");
                await SeedAxleWeightReferencesAsync(refsArray, n);
            }
        }

        // Also try the original combined file as fallback
        var combinedRefsFile = Path.Combine(_seedDataPath, "axle-refs-seed-data.json");
        if (File.Exists(combinedRefsFile))
        {
            var existingRefCount = await _context.AxleWeightReferences.CountAsync();
            if (existingRefCount == 0)
            {
                Console.WriteLine("  Loading refs from combined fallback file...");
                var refsJson = await File.ReadAllTextAsync(combinedRefsFile);
                using var refsDoc = JsonDocument.Parse(refsJson);
                var refsArray = refsDoc.RootElement.GetProperty("axleWeightReferences");
                await SeedAxleWeightReferencesAsync(refsArray, 0);
            }
        }

        // 4. Cleanup orphans (configs with no refs)
        await CleanupOrphanedConfigurationsAsync();
    }

    /// <summary>Pre-scan all ref files to build a set of axle codes that have weight references.</summary>
    private async Task<HashSet<string>> PreScanReferenceCodes()
    {
        var codes = new HashSet<string>();

        for (int n = 1; n <= 7; n++)
        {
            var refsFile = Path.Combine(_seedDataPath, $"axle-refs-{n}-axle.json");
            if (File.Exists(refsFile))
            {
                var json = await File.ReadAllTextAsync(refsFile);
                using var doc = JsonDocument.Parse(json);
                foreach (var el in doc.RootElement.GetProperty("axleWeightReferences").EnumerateArray())
                {
                    var code = el.GetProperty("axleCode").GetString();
                    if (!string.IsNullOrEmpty(code)) codes.Add(code);
                }
            }
        }

        // Also check the combined file
        var combinedFile = Path.Combine(_seedDataPath, "axle-refs-seed-data.json");
        if (File.Exists(combinedFile))
        {
            var json = await File.ReadAllTextAsync(combinedFile);
            using var doc = JsonDocument.Parse(json);
            foreach (var el in doc.RootElement.GetProperty("axleWeightReferences").EnumerateArray())
            {
                var code = el.GetProperty("axleCode").GetString();
                if (!string.IsNullOrEmpty(code)) codes.Add(code);
            }
        }

        return codes;
    }

    private async Task SeedTyreTypesAsync(JsonElement tyreTypesElement)
    {
        Console.WriteLine("=== Seeding Tyre Types ===");
        int seeded = 0, updated = 0;

        foreach (var el in tyreTypesElement.EnumerateArray())
        {
            var code = el.GetProperty("code").GetString() ?? "";
            var existing = await _context.TyreTypes.FirstOrDefaultAsync(tt => tt.Code == code);

            if (existing == null)
            {
                await _context.TyreTypes.AddAsync(new TyreType
                {
                    Code = code,
                    Name = el.GetProperty("name").GetString() ?? "",
                    Description = el.GetProperty("description").GetString(),
                    TypicalMaxWeightKg = el.GetProperty("typicalMaxWeightKg").GetInt32(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                seeded++;
            }
            else
            {
                // Update fields if changed
                var name = el.GetProperty("name").GetString() ?? "";
                if (existing.Name != name) { existing.Name = name; updated++; }
            }
        }

        await _context.SaveChangesAsync();
        Console.WriteLine($"✓ Tyre types: {seeded} seeded, {updated} updated");
    }

    private async Task SeedAxleGroupsAsync(JsonElement axleGroupsElement)
    {
        Console.WriteLine("=== Seeding Axle Groups ===");
        int seeded = 0;

        foreach (var el in axleGroupsElement.EnumerateArray())
        {
            var code = el.GetProperty("code").GetString() ?? "";
            var existing = await _context.AxleGroups.FirstOrDefaultAsync(ag => ag.Code == code);

            if (existing == null)
            {
                await _context.AxleGroups.AddAsync(new AxleGroup
                {
                    Id = Guid.NewGuid(),
                    Code = code,
                    Name = el.GetProperty("name").GetString() ?? "",
                    Description = el.GetProperty("description").GetString(),
                    TypicalWeightKg = el.GetProperty("typicalWeightKg").GetInt32(),
                    MinSpacingFeet = el.TryGetProperty("minSpacingFeet", out var min) && min.ValueKind != JsonValueKind.Null
                        ? (decimal?)min.GetDecimal() : null,
                    MaxSpacingFeet = el.TryGetProperty("maxSpacingFeet", out var max) && max.ValueKind != JsonValueKind.Null
                        ? (decimal?)max.GetDecimal() : null,
                    AxleCountInGroup = el.GetProperty("axleCountInGroup").GetInt32(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                seeded++;
            }
        }

        await _context.SaveChangesAsync();
        Console.WriteLine($"✓ Seeded {seeded} axle groups");
    }

    private async Task SeedAxleConfigurationsAsync(JsonElement configurationsElement, HashSet<string> codesWithRefs)
    {
        Console.WriteLine("=== Seeding Axle Configurations ===");
        int seeded = 0, updated = 0, skipped = 0;
        var processedCodes = new HashSet<string>();

        foreach (var el in configurationsElement.EnumerateArray())
        {
            var axleCode = el.GetProperty("axleCode").GetString() ?? "";
            if (string.IsNullOrEmpty(axleCode) || processedCodes.Contains(axleCode)) continue;
            processedCodes.Add(axleCode);

            // Skip configs with no weight references
            if (!codesWithRefs.Contains(axleCode)) { skipped++; continue; }

            var axleNumber = el.GetProperty("axleNumber").GetInt32();
            var gvw = el.GetProperty("gvw").GetInt32();
            var axleName = el.TryGetProperty("axleName", out var nameEl) ? nameEl.GetString() ?? axleCode : axleCode;

            // Standard = simple codes (no pipes, no asterisk patterns with pipes)
            // Derived = pipe notation (e.g., "3*S|DD||", "5*S|DD|D|D")
            var isStandard = !axleCode.Contains('|');

            var existing = await _context.AxleConfigurations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(ac => ac.AxleCode == axleCode);

            if (existing == null)
            {
                await _context.AxleConfigurations.AddAsync(new AxleConfiguration
                {
                    Id = Guid.NewGuid(),
                    AxleCode = axleCode,
                    AxleName = axleName,
                    Description = isStandard
                        ? $"Standard configuration: {axleNumber} axles"
                        : $"Derived configuration: {axleNumber} axles",
                    AxleNumber = axleNumber,
                    GvwPermissibleKg = gvw,
                    IsStandard = isStandard,
                    LegalFramework = "BOTH",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                seeded++;
            }
            else
            {
                // Update GVW and name if changed
                if (existing.GvwPermissibleKg != gvw || existing.AxleName != axleName)
                {
                    existing.GvwPermissibleKg = gvw;
                    existing.AxleName = axleName;
                    existing.UpdatedAt = DateTime.UtcNow;
                    updated++;
                }
            }
        }

        await _context.SaveChangesAsync();
        Console.WriteLine($"✓ Configs: {seeded} seeded, {updated} updated, {skipped} skipped (no refs)");
    }

    private async Task SeedAxleWeightReferencesAsync(JsonElement refsElement, int axleCount)
    {
        var label = axleCount > 0 ? $"{axleCount}-axle" : "combined";
        Console.WriteLine($"=== Seeding Weight References ({label}) ===");

        // Load lookups
        var axleGroups = await _context.AxleGroups.ToListAsync();
        var tyreTypes = await _context.TyreTypes.ToListAsync();
        var configs = await _context.AxleConfigurations.IgnoreQueryFilters().ToListAsync();
        var configMap = configs.ToDictionary(c => c.AxleCode, c => c.Id);

        int seeded = 0, updated = 0, skipped = 0;

        foreach (var el in refsElement.EnumerateArray())
        {
            var axleCode = el.GetProperty("axleCode").GetString() ?? "";
            var position = el.GetProperty("axlePosition").GetInt32();

            if (!configMap.TryGetValue(axleCode, out var configId)) { skipped++; continue; }

            var groupCode = el.GetProperty("axleGroupCode").GetString() ?? "";
            var axleGroup = axleGroups.FirstOrDefault(ag => ag.Code == groupCode);
            if (axleGroup == null) { skipped++; continue; }

            var tyreCode = el.TryGetProperty("tyreTypeCode", out var tc) && tc.ValueKind != JsonValueKind.Null
                ? tc.GetString() : null;
            var tyreType = string.IsNullOrEmpty(tyreCode) ? null : tyreTypes.FirstOrDefault(tt => tt.Code == tyreCode);

            var legalWeight = el.GetProperty("legalWeightKg").GetInt32();
            var grouping = el.GetProperty("axleGrouping").GetString() ?? "A";

            var existing = await _context.AxleWeightReferences
                .FirstOrDefaultAsync(r => r.AxleConfigurationId == configId && r.AxlePosition == position);

            if (existing == null)
            {
                await _context.AxleWeightReferences.AddAsync(new AxleWeightReference
                {
                    Id = Guid.NewGuid(),
                    AxleConfigurationId = configId,
                    AxlePosition = position,
                    AxleLegalWeightKg = legalWeight,
                    AxleGroupId = axleGroup.Id,
                    AxleGrouping = grouping,
                    TyreTypeId = tyreType?.Id,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                seeded++;
            }
            else
            {
                // Update if weight or group changed
                if (existing.AxleLegalWeightKg != legalWeight || existing.AxleGroupId != axleGroup.Id)
                {
                    existing.AxleLegalWeightKg = legalWeight;
                    existing.AxleGroupId = axleGroup.Id;
                    existing.AxleGrouping = grouping;
                    existing.TyreTypeId = tyreType?.Id;
                    updated++;
                }
            }
        }

        await _context.SaveChangesAsync();
        Console.WriteLine($"✓ Refs ({label}): {seeded} seeded, {updated} updated, {skipped} skipped");
    }

    private async Task SeedAxleFeeSchedulesAsync(JsonElement feeSchedulesElement)
    {
        Console.WriteLine("=== Seeding Axle Fee Schedules ===");
        int seeded = 0;

        foreach (var el in feeSchedulesElement.EnumerateArray())
        {
            var legalFramework = (el.GetProperty("legalFramework").GetString() ?? "").ToUpperInvariant() switch
            {
                "TRAFFICACT" => "TRAFFIC_ACT",
                _ => el.GetProperty("legalFramework").GetString()?.ToUpperInvariant() ?? "EAC"
            };
            var feeType = (el.GetProperty("feeType").GetString() ?? "").ToUpperInvariant();
            var overloadMin = el.GetProperty("overloadMinKg").GetInt32();
            var overloadMax = el.TryGetProperty("overloadMaxKg", out var maxEl) && maxEl.ValueKind != JsonValueKind.Null
                ? (int?)maxEl.GetInt32() : null;

            var existing = await _context.AxleFeeSchedules
                .FirstOrDefaultAsync(afs =>
                    afs.LegalFramework == legalFramework &&
                    afs.FeeType == feeType &&
                    afs.OverloadMinKg == overloadMin);

            if (existing == null)
            {
                var todayUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
                await _context.AxleFeeSchedules.AddAsync(new AxleFeeSchedule
                {
                    Id = Guid.NewGuid(),
                    LegalFramework = legalFramework,
                    FeeType = feeType,
                    OverloadMinKg = overloadMin,
                    OverloadMaxKg = overloadMax,
                    FeePerKgUsd = el.GetProperty("feePerKgUsd").GetDecimal(),
                    FlatFeeUsd = el.GetProperty("flatFeeUsd").GetDecimal(),
                    DemeritPoints = el.GetProperty("demeritPoints").GetInt32(),
                    PenaltyDescription = el.GetProperty("penaltyDescription").GetString() ?? "",
                    EffectiveFrom = todayUtc,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                seeded++;
            }
        }

        await _context.SaveChangesAsync();
        Console.WriteLine($"✓ Seeded {seeded} fee schedules");
    }

    private async Task CleanupOrphanedConfigurationsAsync()
    {
        var orphans = await _context.AxleConfigurations
            .IgnoreQueryFilters()
            .Where(ac => !_context.AxleWeightReferences.Any(r => r.AxleConfigurationId == ac.Id))
            .ToListAsync();

        if (orphans.Count > 0)
        {
            _context.AxleConfigurations.RemoveRange(orphans);
            await _context.SaveChangesAsync();
            Console.WriteLine($"✓ Removed {orphans.Count} orphaned configs (no weight refs)");
        }
    }
}
