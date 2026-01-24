using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Data.Seeders.SystemConfiguration;

/// <summary>
/// Seeds system configuration data: permit types, tolerance settings,
/// axle type fee schedules, demerit point schedules, and penalty schedules.
/// Implements Kenya Traffic Act Cap 403 and EAC Act 2016 regulatory requirements.
/// Idempotent - safe to run multiple times.
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
        await SeedAxleTypeOverloadFeeSchedulesAsync();
        await SeedDemeritPointSchedulesAsync();
        await SeedPenaltySchedulesAsync();
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
            },
            // Operational tolerance for auto-release warning (200kg default)
            new ToleranceSetting
            {
                Id = Guid.NewGuid(),
                Code = "OPERATIONAL_TOLERANCE",
                Name = "Operational Auto-Release Tolerance",
                LegalFramework = "BOTH",
                TolerancePercentage = 0.0m,
                ToleranceKg = 200,
                AppliesTo = "OPERATIONAL",
                Description = "Operational tolerance (200kg) for minor overloads - auto-release with warning, no yard detention",
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

    /// <summary>
    /// Seeds axle type-specific overload fee schedules per Kenya Traffic Act Cap 403.
    /// Different axle types have different fee rates based on overload amount.
    /// </summary>
    private async Task SeedAxleTypeOverloadFeeSchedulesAsync()
    {
        var effectiveDate = DateTime.UtcNow.Date;

        // Kenya Traffic Act Cap 403 fee structure - per axle type
        // Fee bands based on overload ranges with type-specific rates
        var feeSchedules = new[]
        {
            // Band 1: 0-2000 kg overload
            new AxleTypeOverloadFeeSchedule
            {
                OverloadMinKg = 0,
                OverloadMaxKg = 2000,
                SteeringAxleFeeUsd = 50.00m,
                SingleDriveAxleFeeUsd = 75.00m,
                TandemAxleFeeUsd = 100.00m,
                TridemAxleFeeUsd = 125.00m,
                QuadAxleFeeUsd = 150.00m,
                LegalFramework = "TRAFFIC_ACT",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null
            },
            // Band 2: 2001-5000 kg overload
            new AxleTypeOverloadFeeSchedule
            {
                OverloadMinKg = 2001,
                OverloadMaxKg = 5000,
                SteeringAxleFeeUsd = 100.00m,
                SingleDriveAxleFeeUsd = 150.00m,
                TandemAxleFeeUsd = 200.00m,
                TridemAxleFeeUsd = 250.00m,
                QuadAxleFeeUsd = 300.00m,
                LegalFramework = "TRAFFIC_ACT",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null
            },
            // Band 3: 5001-10000 kg overload
            new AxleTypeOverloadFeeSchedule
            {
                OverloadMinKg = 5001,
                OverloadMaxKg = 10000,
                SteeringAxleFeeUsd = 200.00m,
                SingleDriveAxleFeeUsd = 300.00m,
                TandemAxleFeeUsd = 400.00m,
                TridemAxleFeeUsd = 500.00m,
                QuadAxleFeeUsd = 600.00m,
                LegalFramework = "TRAFFIC_ACT",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null
            },
            // Band 4: 10001-20000 kg overload
            new AxleTypeOverloadFeeSchedule
            {
                OverloadMinKg = 10001,
                OverloadMaxKg = 20000,
                SteeringAxleFeeUsd = 400.00m,
                SingleDriveAxleFeeUsd = 600.00m,
                TandemAxleFeeUsd = 800.00m,
                TridemAxleFeeUsd = 1000.00m,
                QuadAxleFeeUsd = 1200.00m,
                LegalFramework = "TRAFFIC_ACT",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null
            },
            // Band 5: >20000 kg overload (severe)
            new AxleTypeOverloadFeeSchedule
            {
                OverloadMinKg = 20001,
                OverloadMaxKg = null,
                SteeringAxleFeeUsd = 800.00m,
                SingleDriveAxleFeeUsd = 1200.00m,
                TandemAxleFeeUsd = 1600.00m,
                TridemAxleFeeUsd = 2000.00m,
                QuadAxleFeeUsd = 2400.00m,
                LegalFramework = "TRAFFIC_ACT",
                EffectiveFrom = effectiveDate,
                EffectiveTo = null
            }
        };

        foreach (var schedule in feeSchedules)
        {
            var existing = await _context.AxleTypeOverloadFeeSchedules
                .FirstOrDefaultAsync(f => f.OverloadMinKg == schedule.OverloadMinKg
                    && f.LegalFramework == schedule.LegalFramework);

            if (existing == null)
            {
                await _context.AxleTypeOverloadFeeSchedules.AddAsync(schedule);
                Console.WriteLine($"✓ Seeded axle type fee schedule: {schedule.OverloadMinKg}-{schedule.OverloadMaxKg ?? 999999}kg");
            }
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds demerit point schedules per Kenya Traffic Act Cap 403 Section 117A.
    /// Points are assigned based on violation type and overload severity.
    /// </summary>
    private async Task SeedDemeritPointSchedulesAsync()
    {
        var effectiveDate = DateTime.UtcNow.Date;

        // Kenya Traffic Act Cap 403 Section 117A - NTSA Demerit Points System
        var demeritSchedules = new[]
        {
            // Steering axle violations (lower impact = lower points)
            new DemeritPointSchedule { ViolationType = "STEERING", OverloadMinKg = 0, OverloadMaxKg = 2000, Points = 1, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "STEERING", OverloadMinKg = 2001, OverloadMaxKg = 5000, Points = 2, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "STEERING", OverloadMinKg = 5001, OverloadMaxKg = 10000, Points = 3, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "STEERING", OverloadMinKg = 10001, OverloadMaxKg = 20000, Points = 5, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "STEERING", OverloadMinKg = 20001, OverloadMaxKg = null, Points = 10, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },

            // Single drive axle violations
            new DemeritPointSchedule { ViolationType = "SINGLE_DRIVE", OverloadMinKg = 0, OverloadMaxKg = 2000, Points = 1, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "SINGLE_DRIVE", OverloadMinKg = 2001, OverloadMaxKg = 5000, Points = 2, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "SINGLE_DRIVE", OverloadMinKg = 5001, OverloadMaxKg = 10000, Points = 3, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "SINGLE_DRIVE", OverloadMinKg = 10001, OverloadMaxKg = 20000, Points = 5, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "SINGLE_DRIVE", OverloadMinKg = 20001, OverloadMaxKg = null, Points = 10, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },

            // Tandem axle violations (grouped = stricter)
            new DemeritPointSchedule { ViolationType = "TANDEM", OverloadMinKg = 0, OverloadMaxKg = 2000, Points = 2, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TANDEM", OverloadMinKg = 2001, OverloadMaxKg = 5000, Points = 3, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TANDEM", OverloadMinKg = 5001, OverloadMaxKg = 10000, Points = 4, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TANDEM", OverloadMinKg = 10001, OverloadMaxKg = 20000, Points = 6, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TANDEM", OverloadMinKg = 20001, OverloadMaxKg = null, Points = 12, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },

            // Tridem axle violations (highest impact group)
            new DemeritPointSchedule { ViolationType = "TRIDEM", OverloadMinKg = 0, OverloadMaxKg = 2000, Points = 2, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TRIDEM", OverloadMinKg = 2001, OverloadMaxKg = 5000, Points = 3, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TRIDEM", OverloadMinKg = 5001, OverloadMaxKg = 10000, Points = 5, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TRIDEM", OverloadMinKg = 10001, OverloadMaxKg = 20000, Points = 7, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "TRIDEM", OverloadMinKg = 20001, OverloadMaxKg = null, Points = 15, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },

            // GVW violations (overall vehicle weight)
            new DemeritPointSchedule { ViolationType = "GVW", OverloadMinKg = 0, OverloadMaxKg = 2000, Points = 1, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "GVW", OverloadMinKg = 2001, OverloadMaxKg = 5000, Points = 2, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "GVW", OverloadMinKg = 5001, OverloadMaxKg = 10000, Points = 3, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "GVW", OverloadMinKg = 10001, OverloadMaxKg = 20000, Points = 5, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate },
            new DemeritPointSchedule { ViolationType = "GVW", OverloadMinKg = 20001, OverloadMaxKg = null, Points = 10, LegalFramework = "TRAFFIC_ACT", EffectiveFrom = effectiveDate }
        };

        foreach (var schedule in demeritSchedules)
        {
            var existing = await _context.DemeritPointSchedules
                .FirstOrDefaultAsync(d => d.ViolationType == schedule.ViolationType
                    && d.OverloadMinKg == schedule.OverloadMinKg
                    && d.LegalFramework == schedule.LegalFramework);

            if (existing == null)
            {
                await _context.DemeritPointSchedules.AddAsync(schedule);
                Console.WriteLine($"✓ Seeded demerit points: {schedule.ViolationType} {schedule.OverloadMinKg}-{schedule.OverloadMaxKg ?? 999999}kg = {schedule.Points} points");
            }
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds penalty schedules based on accumulated demerit points.
    /// Implements NTSA license management penalties per Traffic Act Cap 403 Section 117A.
    /// </summary>
    private async Task SeedPenaltySchedulesAsync()
    {
        var penaltySchedules = new[]
        {
            new PenaltySchedule
            {
                PointsMin = 1,
                PointsMax = 3,
                PenaltyDescription = "Warning letter issued. Transporter informed of violation and instructed to ensure future compliance.",
                SuspensionDays = null,
                RequiresCourt = false,
                AdditionalFineUsd = 0m,
                AdditionalFineKes = 0m
            },
            new PenaltySchedule
            {
                PointsMin = 4,
                PointsMax = 6,
                PenaltyDescription = "Vehicle inspection required. Transporter must present vehicle for inspection within 30 days.",
                SuspensionDays = null,
                RequiresCourt = false,
                AdditionalFineUsd = 50m,
                AdditionalFineKes = 7500m
            },
            new PenaltySchedule
            {
                PointsMin = 7,
                PointsMax = 9,
                PenaltyDescription = "Driver license under review. Driver must attend NTSA safety course within 60 days.",
                SuspensionDays = null,
                RequiresCourt = false,
                AdditionalFineUsd = 100m,
                AdditionalFineKes = 15000m
            },
            new PenaltySchedule
            {
                PointsMin = 10,
                PointsMax = 13,
                PenaltyDescription = "License suspension - 6 months. Driver prohibited from operating commercial vehicles.",
                SuspensionDays = 180,
                RequiresCourt = false,
                AdditionalFineUsd = 200m,
                AdditionalFineKes = 30000m
            },
            new PenaltySchedule
            {
                PointsMin = 14,
                PointsMax = 19,
                PenaltyDescription = "License suspension - 1 year. Driver prohibited from operating commercial vehicles. Transporter fined.",
                SuspensionDays = 365,
                RequiresCourt = true,
                AdditionalFineUsd = 500m,
                AdditionalFineKes = 75000m
            },
            new PenaltySchedule
            {
                PointsMin = 20,
                PointsMax = null,
                PenaltyDescription = "License suspension - 2 years. Mandatory court prosecution. Possible imprisonment per Traffic Act.",
                SuspensionDays = 730,
                RequiresCourt = true,
                AdditionalFineUsd = 1000m,
                AdditionalFineKes = 150000m
            }
        };

        foreach (var penalty in penaltySchedules)
        {
            var existing = await _context.PenaltySchedules
                .FirstOrDefaultAsync(p => p.PointsMin == penalty.PointsMin);

            if (existing == null)
            {
                await _context.PenaltySchedules.AddAsync(penalty);
                Console.WriteLine($"✓ Seeded penalty schedule: {penalty.PointsMin}-{penalty.PointsMax ?? 999} points = {penalty.PenaltyDescription.Substring(0, Math.Min(50, penalty.PenaltyDescription.Length))}...");
            }
        }

        await _context.SaveChangesAsync();
    }
}
