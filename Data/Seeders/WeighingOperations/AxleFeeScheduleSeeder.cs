using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Data.Seeders.WeighingOperations;

/// <summary>
/// Seeds EAC and Traffic Act fee bands for overload penalties
/// Based on EAC Vehicle Load Control Act (2016) and Kenya Traffic Act (Cap 403)
/// </summary>
public class AxleFeeScheduleSeeder
{
    private readonly TruLoadDbContext _context;

    public AxleFeeScheduleSeeder(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync()
    {
        if (await _context.AxleFeeSchedules.AnyAsync())
        {
            return; // Already seeded
        }

        var effectiveFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var feeSchedules = new List<AxleFeeSchedule>();

        // ===== EAC GVW Fee Bands =====
        feeSchedules.AddRange(new[]
        {
            new AxleFeeSchedule
            {
                Id = Guid.NewGuid(),
                LegalFramework = "EAC",
                FeeType = "GVW",
                OverloadMinKg = 1,
                OverloadMaxKg = 500,
                FeePerKgUsd = 0.50m,
                FlatFeeUsd = 0,
                DemeritPoints = 0,
                PenaltyDescription = "Minor GVW overload (1-500 kg) - redistributable",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            },
            new AxleFeeSchedule
            {
                Id = Guid.NewGuid(),
                LegalFramework = "EAC",
                FeeType = "GVW",
                OverloadMinKg = 501,
                OverloadMaxKg = 1000,
                FeePerKgUsd = 0.75m,
                FlatFeeUsd = 0,
                DemeritPoints = 2,
                PenaltyDescription = "Moderate GVW overload (501-1000 kg) - redistributable",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            },
            new AxleFeeSchedule
            {
                Id = Guid.NewGuid(),
                LegalFramework = "EAC",
                FeeType = "GVW",
                OverloadMinKg = 1001,
                OverloadMaxKg = 1500,
                FeePerKgUsd = 1.00m,
                FlatFeeUsd = 0,
                DemeritPoints = 4,
                PenaltyDescription = "High GVW overload (1001-1500 kg) - redistributable",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            },
            new AxleFeeSchedule
            {
                Id = Guid.NewGuid(),
                LegalFramework = "EAC",
                FeeType = "GVW",
                OverloadMinKg = 1501,
                OverloadMaxKg = 3000,
                FeePerKgUsd = 2.50m, // 5× multiplier (non-redistributable)
                FlatFeeUsd = 0,
                DemeritPoints = 6,
                PenaltyDescription = "Severe GVW overload (1501-3000 kg) - non-redistributable, 5× penalty",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            },
            new AxleFeeSchedule
            {
                Id = Guid.NewGuid(),
                LegalFramework = "EAC",
                FeeType = "GVW",
                OverloadMinKg = 3001,
                OverloadMaxKg = null, // No upper limit
                FeePerKgUsd = 5.00m, // 10× multiplier (extreme overload)
                FlatFeeUsd = 500m,
                DemeritPoints = 10,
                PenaltyDescription = "Extreme GVW overload (>3000 kg) - vehicle prohibition mandatory",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            }
        });

        // ===== EAC Axle Fee Bands =====
        feeSchedules.AddRange(new[]
        {
            new AxleFeeSchedule
            {
                Id = Guid.NewGuid(),
                LegalFramework = "EAC",
                FeeType = "AXLE",
                OverloadMinKg = 1,
                OverloadMaxKg = 200,
                FeePerKgUsd = 0.40m,
                FlatFeeUsd = 0,
                DemeritPoints = 0,
                PenaltyDescription = "Minor axle overload (1-200 kg) - redistributable",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            },
            new AxleFeeSchedule
            {
                Id = Guid.NewGuid(),
                LegalFramework = "EAC",
                FeeType = "AXLE",
                OverloadMinKg = 201,
                OverloadMaxKg = 500,
                FeePerKgUsd = 0.60m,
                FlatFeeUsd = 0,
                DemeritPoints = 2,
                PenaltyDescription = "Moderate axle overload (201-500 kg) - redistributable",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            },
            new AxleFeeSchedule
            {
                Id = Guid.NewGuid(),
                LegalFramework = "EAC",
                FeeType = "AXLE",
                OverloadMinKg = 501,
                OverloadMaxKg = 1000,
                FeePerKgUsd = 1.00m,
                FlatFeeUsd = 0,
                DemeritPoints = 4,
                PenaltyDescription = "High axle overload (501-1000 kg) - redistributable",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            },
            new AxleFeeSchedule
            {
                Id = Guid.NewGuid(),
                LegalFramework = "EAC",
                FeeType = "AXLE",
                OverloadMinKg = 1001,
                OverloadMaxKg = 1500,
                FeePerKgUsd = 1.50m,
                FlatFeeUsd = 0,
                DemeritPoints = 5,
                PenaltyDescription = "Severe axle overload (1001-1500 kg) - mandatory reweigh",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            },
            new AxleFeeSchedule
            {
                Id = Guid.NewGuid(),
                LegalFramework = "EAC",
                FeeType = "AXLE",
                OverloadMinKg = 1501,
                OverloadMaxKg = null,
                FeePerKgUsd = 3.00m, // 5× multiplier (non-redistributable)
                FlatFeeUsd = 200m,
                DemeritPoints = 8,
                PenaltyDescription = "Extreme axle overload (>1500 kg) - non-redistributable, 5× penalty",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            }
        });

        // ===== Traffic Act GVW Fee Bands (KES native — no USD→KES conversion at runtime) =====
        // Kenya Traffic Act Cap 403 charges in KES. USD columns kept as reference only.
        feeSchedules.AddRange(new[]
        {
            new AxleFeeSchedule
            {
                Id = Guid.NewGuid(),
                LegalFramework = "TRAFFIC_ACT",
                FeeType = "GVW",
                OverloadMinKg = 1,
                OverloadMaxKg = 500,
                FeePerKgKes = 39m,
                FlatFeeKes = 6_500m,
                FeePerKgUsd = 0.30m,
                FlatFeeUsd = 50m,
                DemeritPoints = 0,
                PenaltyDescription = "Minor GVW overload (1-500 kg) - Traffic Act fine",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            },
            new AxleFeeSchedule
            {
                Id = Guid.NewGuid(),
                LegalFramework = "TRAFFIC_ACT",
                FeeType = "GVW",
                OverloadMinKg = 501,
                OverloadMaxKg = 1000,
                FeePerKgKes = 65m,
                FlatFeeKes = 13_000m,
                FeePerKgUsd = 0.50m,
                FlatFeeUsd = 100m,
                DemeritPoints = 2,
                PenaltyDescription = "Moderate GVW overload (501-1000 kg) - Traffic Act fine",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            },
            new AxleFeeSchedule
            {
                Id = Guid.NewGuid(),
                LegalFramework = "TRAFFIC_ACT",
                FeeType = "GVW",
                OverloadMinKg = 1001,
                OverloadMaxKg = 2000,
                FeePerKgKes = 97.50m,
                FlatFeeKes = 26_000m,
                FeePerKgUsd = 0.75m,
                FlatFeeUsd = 200m,
                DemeritPoints = 4,
                PenaltyDescription = "High GVW overload (1001-2000 kg) - Traffic Act fine + prohibition",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            },
            new AxleFeeSchedule
            {
                Id = Guid.NewGuid(),
                LegalFramework = "TRAFFIC_ACT",
                FeeType = "GVW",
                OverloadMinKg = 2001,
                OverloadMaxKg = 3000,
                FeePerKgKes = 130m,
                FlatFeeKes = 65_000m,
                FeePerKgUsd = 1.00m,
                FlatFeeUsd = 500m,
                DemeritPoints = 6,
                PenaltyDescription = "Severe GVW overload (2001-3000 kg) - Court appearance mandatory",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            },
            new AxleFeeSchedule
            {
                Id = Guid.NewGuid(),
                LegalFramework = "TRAFFIC_ACT",
                FeeType = "GVW",
                OverloadMinKg = 3001,
                OverloadMaxKg = null,
                FeePerKgKes = 260m,
                FlatFeeKes = 130_000m,
                FeePerKgUsd = 2.00m,
                FlatFeeUsd = 1000m,
                DemeritPoints = 10,
                PenaltyDescription = "Extreme GVW overload (>3000 kg) - Automatic prosecution + vehicle impound",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            }
        });

        await _context.AxleFeeSchedules.AddRangeAsync(feeSchedules);
        await _context.SaveChangesAsync();
    }
}
