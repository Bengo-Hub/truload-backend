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

        // ===== EAC GVW Fee Bands (1st conviction) =====
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
                ConvictionNumber = 1,
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
                ConvictionNumber = 1,
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
                ConvictionNumber = 1,
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
                ConvictionNumber = 1,
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
                ConvictionNumber = 1,
                DemeritPoints = 10,
                PenaltyDescription = "Extreme GVW overload (>3000 kg) - vehicle prohibition mandatory",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            }
        });

        // ===== EAC Axle Fee Bands (1st conviction) =====
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
                ConvictionNumber = 1,
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
                ConvictionNumber = 1,
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
                ConvictionNumber = 1,
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
                ConvictionNumber = 1,
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
                ConvictionNumber = 1,
                DemeritPoints = 8,
                PenaltyDescription = "Extreme axle overload (>1500 kg) - non-redistributable, 5× penalty",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            }
        });

        // ===== EAC GVW Fee Bands (2nd conviction - 2× penalty per Section 20 EAC VLC Act) =====
        feeSchedules.AddRange(new[]
        {
            new AxleFeeSchedule
            {
                Id = Guid.NewGuid(),
                LegalFramework = "EAC",
                FeeType = "GVW",
                OverloadMinKg = 1,
                OverloadMaxKg = 500,
                FeePerKgUsd = 1.00m, // 2× of 0.50
                FlatFeeUsd = 0,
                ConvictionNumber = 2,
                DemeritPoints = 2,
                PenaltyDescription = "EAC 2nd conviction GVW (1-500 kg) - 2x penalty per Section 20 EAC VLC Act",
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
                FeePerKgUsd = 1.50m, // 2× of 0.75
                FlatFeeUsd = 0,
                ConvictionNumber = 2,
                DemeritPoints = 4,
                PenaltyDescription = "EAC 2nd conviction GVW (501-1000 kg) - 2x penalty per Section 20 EAC VLC Act",
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
                FeePerKgUsd = 2.00m, // 2× of 1.00
                FlatFeeUsd = 0,
                ConvictionNumber = 2,
                DemeritPoints = 6,
                PenaltyDescription = "EAC 2nd conviction GVW (1001-1500 kg) - 2x penalty per Section 20 EAC VLC Act",
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
                FeePerKgUsd = 5.00m, // 2× of 2.50
                FlatFeeUsd = 0,
                ConvictionNumber = 2,
                DemeritPoints = 8,
                PenaltyDescription = "EAC 2nd conviction GVW (1501-3000 kg) - 2x penalty per Section 20 EAC VLC Act",
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
                OverloadMaxKg = null,
                FeePerKgUsd = 10.00m, // 2× of 5.00
                FlatFeeUsd = 1000m, // 2× of 500
                ConvictionNumber = 2,
                DemeritPoints = 15,
                PenaltyDescription = "EAC 2nd conviction GVW (>3000 kg) - 2x penalty per Section 20 EAC VLC Act",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            }
        });

        // ===== EAC Axle Fee Bands (2nd conviction - 2× penalty per Section 20 EAC VLC Act) =====
        feeSchedules.AddRange(new[]
        {
            new AxleFeeSchedule
            {
                Id = Guid.NewGuid(),
                LegalFramework = "EAC",
                FeeType = "AXLE",
                OverloadMinKg = 1,
                OverloadMaxKg = 200,
                FeePerKgUsd = 0.80m, // 2× of 0.40
                FlatFeeUsd = 0,
                ConvictionNumber = 2,
                DemeritPoints = 2,
                PenaltyDescription = "EAC 2nd conviction Axle (1-200 kg) - 2x penalty per Section 20 EAC VLC Act",
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
                FeePerKgUsd = 1.20m, // 2× of 0.60
                FlatFeeUsd = 0,
                ConvictionNumber = 2,
                DemeritPoints = 4,
                PenaltyDescription = "EAC 2nd conviction Axle (201-500 kg) - 2x penalty per Section 20 EAC VLC Act",
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
                FeePerKgUsd = 2.00m, // 2× of 1.00
                FlatFeeUsd = 0,
                ConvictionNumber = 2,
                DemeritPoints = 6,
                PenaltyDescription = "EAC 2nd conviction Axle (501-1000 kg) - 2x penalty per Section 20 EAC VLC Act",
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
                FeePerKgUsd = 3.00m, // 2× of 1.50
                FlatFeeUsd = 0,
                ConvictionNumber = 2,
                DemeritPoints = 7,
                PenaltyDescription = "EAC 2nd conviction Axle (1001-1500 kg) - 2x penalty per Section 20 EAC VLC Act",
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
                FeePerKgUsd = 6.00m, // 2× of 3.00
                FlatFeeUsd = 400m, // 2× of 200
                ConvictionNumber = 2,
                DemeritPoints = 12,
                PenaltyDescription = "EAC 2nd conviction Axle (>1500 kg) - 2x penalty per Section 20 EAC VLC Act",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            }
        });

        // ===== Traffic Act GVW Fee Bands (KES native) =====
        // Rule 41(2) of the Traffic Amendment Rules, 2008 - Traffic Act Cap 403 Laws of Kenya
        // "Excess Axle Load Weights Fine Schedule"
        // Uses flat fees in KES (NOT per-kg rates). No USD equivalent needed.
        // Separate bands for 1st and 2nd conviction as mandated by Rule 41(2).
        feeSchedules.AddRange(CreateTrafficActBands(effectiveFrom));

        await _context.AxleFeeSchedules.AddRangeAsync(feeSchedules);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Creates Traffic Act Cap 403 fee bands per Rule 41(2) of the Traffic Amendment Rules, 2008.
    /// 11 bands for 1st conviction, 11 bands for 2nd conviction (22 total).
    /// 2nd conviction fines are exactly 2× the 1st conviction values.
    /// </summary>
    private static List<AxleFeeSchedule> CreateTrafficActBands(DateTime effectiveFrom)
    {
        // Rule 41(2) fine schedule: (minKg, maxKg, 1stConvictionKes, demeritPoints)
        var bands = new (int Min, int? Max, decimal FirstFine, int Demerit)[]
        {
            (1,    999,    5_000m,  1),
            (1000, 1999,  10_000m,  2),
            (2000, 2999,  15_000m,  3),
            (3000, 3999,  20_000m,  4),
            (4000, 4999,  30_000m,  5),
            (5000, 5999,  50_000m,  6),
            (6000, 6999,  75_000m,  7),
            (7000, 7999, 100_000m,  8),
            (8000, 8999, 150_000m,  9),
            (9000, 9999, 175_000m, 10),
            (10000, null, 200_000m, 12),
        };

        var result = new List<AxleFeeSchedule>();

        foreach (var (min, max, firstFine, demerit) in bands)
        {
            var rangeLabel = max.HasValue ? $"{min}-{max} kg" : $"≥{min} kg";

            // 1st conviction
            result.Add(new AxleFeeSchedule
            {
                Id = Guid.NewGuid(),
                LegalFramework = "TRAFFIC_ACT",
                FeeType = "GVW",
                OverloadMinKg = min,
                OverloadMaxKg = max,
                FeePerKgKes = 0m,
                FlatFeeKes = firstFine,
                FeePerKgUsd = 0m,
                FlatFeeUsd = 0m,
                ConvictionNumber = 1,
                DemeritPoints = demerit,
                PenaltyDescription = $"Traffic Act Rule 41(2) 1st conviction ({rangeLabel}) - KSh {firstFine:N0}",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            });

            // 2nd conviction (exactly 2× the 1st conviction fine)
            var secondFine = firstFine * 2;
            result.Add(new AxleFeeSchedule
            {
                Id = Guid.NewGuid(),
                LegalFramework = "TRAFFIC_ACT",
                FeeType = "GVW",
                OverloadMinKg = min,
                OverloadMaxKg = max,
                FeePerKgKes = 0m,
                FlatFeeKes = secondFine,
                FeePerKgUsd = 0m,
                FlatFeeUsd = 0m,
                ConvictionNumber = 2,
                DemeritPoints = demerit + 2, // Higher demerit for repeat offender
                PenaltyDescription = $"Traffic Act Rule 41(2) 2nd conviction ({rangeLabel}) - KSh {secondFine:N0}",
                EffectiveFrom = effectiveFrom,
                EffectiveTo = null,
                IsActive = true
            });
        }

        return result;
    }
}
