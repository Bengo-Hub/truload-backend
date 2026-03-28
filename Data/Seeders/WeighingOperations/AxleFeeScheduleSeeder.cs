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

        // ===== Traffic Act GVW Fee Bands (KES native) =====
        // Kenya Traffic Act Cap 403 uses flat fee thresholds in KES (NOT per-kg rates).
        // FeePerKgKes = 0 because Traffic Act uses flat fees, not per-kg.
        // FlatFeeKes = the actual Traffic Act penalty amount.
        // USD columns are reference-only estimates for reporting.
        feeSchedules.AddRange(new[]
        {
            new AxleFeeSchedule
            {
                Id = Guid.NewGuid(),
                LegalFramework = "TRAFFIC_ACT",
                FeeType = "GVW",
                OverloadMinKg = 1,
                OverloadMaxKg = 1000,
                FeePerKgKes = 0m,
                FlatFeeKes = 10_000m,  // trafficoverloadCharges: 1000kg → KSh 10,000
                FeePerKgUsd = 0.30m,
                FlatFeeUsd = 50m,
                DemeritPoints = 1,
                PenaltyDescription = "Traffic Act GVW overload (1-1000 kg) - KSh 10,000 flat fine",
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
                FeePerKgKes = 0m,
                FlatFeeKes = 20_000m,  // trafficoverloadCharges: 2000kg → KSh 20,000
                FeePerKgUsd = 0.50m,
                FlatFeeUsd = 100m,
                DemeritPoints = 3,
                PenaltyDescription = "Traffic Act GVW overload (1001-2000 kg) - KSh 20,000 flat fine",
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
                FeePerKgKes = 0m,
                FlatFeeKes = 30_000m,  // trafficoverloadCharges: 3000kg → KSh 30,000
                FeePerKgUsd = 0.75m,
                FlatFeeUsd = 200m,
                DemeritPoints = 4,
                PenaltyDescription = "Traffic Act GVW overload (2001-3000 kg) - KSh 30,000 flat fine",
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
                OverloadMaxKg = 5000,
                FeePerKgKes = 0m,
                FlatFeeKes = 60_000m,  // trafficoverloadCharges: 5000kg → KSh 60,000
                FeePerKgUsd = 1.00m,
                FlatFeeUsd = 500m,
                DemeritPoints = 6,
                PenaltyDescription = "Traffic Act GVW overload (3001-5000 kg) - KSh 60,000 flat fine",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            },
            new AxleFeeSchedule
            {
                Id = Guid.NewGuid(),
                LegalFramework = "TRAFFIC_ACT",
                FeeType = "GVW",
                OverloadMinKg = 5001,
                OverloadMaxKg = 10000,
                FeePerKgKes = 0m,
                FlatFeeKes = 350_000m,  // trafficoverloadCharges: 10000kg → KSh 350,000
                FeePerKgUsd = 2.00m,
                FlatFeeUsd = 1000m,
                DemeritPoints = 10,
                PenaltyDescription = "Traffic Act GVW overload (5001-10000 kg) - KSh 350,000 flat fine",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            },
            new AxleFeeSchedule
            {
                Id = Guid.NewGuid(),
                LegalFramework = "TRAFFIC_ACT",
                FeeType = "GVW",
                OverloadMinKg = 10001,
                OverloadMaxKg = null,
                FeePerKgKes = 0m,
                FlatFeeKes = 400_000m,  // trafficoverloadCharges: 10001+kg → KSh 400,000 (max)
                FeePerKgUsd = 3.00m,
                FlatFeeUsd = 2000m,
                DemeritPoints = 12,
                PenaltyDescription = "Traffic Act GVW overload (>10000 kg) - KSh 400,000 max fine + prosecution",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            }
        });

        await _context.AxleFeeSchedules.AddRangeAsync(feeSchedules);
        await _context.SaveChangesAsync();
    }
}
