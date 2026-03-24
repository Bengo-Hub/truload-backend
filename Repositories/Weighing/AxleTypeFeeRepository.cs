using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Repositories.Weighing.Interfaces;

namespace TruLoad.Backend.Repositories.Weighing;

/// <summary>
/// Repository implementation for AxleTypeOverloadFeeSchedule with per-axle-type fee lookup.
/// Implements KenloadV2 approach of differentiated fees by axle type (Steering, Tandem, Tridem, etc.)
/// </summary>
public class AxleTypeFeeRepository : IAxleTypeFeeRepository
{
    private readonly TruLoadDbContext _context;

    public AxleTypeFeeRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<List<AxleTypeOverloadFeeSchedule>> GetAllByFrameworkAsync(
        string legalFramework,
        CancellationToken cancellationToken = default)
    {
        return await _context.AxleTypeOverloadFeeSchedules
            .AsNoTracking()
            .Where(f => f.LegalFramework == legalFramework.ToUpper())
            .Where(f => f.IsActive)
            .Where(f => f.DeletedAt == null)
            .OrderBy(f => f.OverloadMinKg)
            .ToListAsync(cancellationToken);
    }

    public async Task<AxleTypeOverloadFeeSchedule?> GetByOverloadAsync(
        string legalFramework,
        int overloadKg,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow.Date;

        return await _context.AxleTypeOverloadFeeSchedules
            .AsNoTracking()
            .Where(f => f.LegalFramework == legalFramework.ToUpper())
            .Where(f => f.IsActive)
            .Where(f => f.DeletedAt == null)
            .Where(f => f.EffectiveFrom <= now && (f.EffectiveTo == null || f.EffectiveTo >= now))
            .Where(f => f.OverloadMinKg <= overloadKg && (f.OverloadMaxKg == null || f.OverloadMaxKg >= overloadKg))
            .OrderBy(f => f.OverloadMinKg)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<decimal> CalculateFeeAsync(
        string legalFramework,
        string axleType,
        int overloadKg,
        CancellationToken cancellationToken = default)
    {
        return await CalculateFeeAsync(legalFramework, axleType, overloadKg, "USD", cancellationToken);
    }

    public async Task<decimal> CalculateFeeAsync(
        string legalFramework,
        string axleType,
        int overloadKg,
        string currency,
        CancellationToken cancellationToken = default)
    {
        if (overloadKg <= 0) return 0m;

        var schedule = await GetByOverloadAsync(legalFramework, overloadKg, cancellationToken);
        if (schedule == null) return 0m;

        // Return fee based on axle type and currency
        if (currency.Equals("KES", StringComparison.OrdinalIgnoreCase))
        {
            return axleType.ToUpper() switch
            {
                "STEERING" => schedule.SteeringAxleFeeKes,
                "SINGLEDRIVE" or "SINGLE_DRIVE" => schedule.SingleDriveAxleFeeKes,
                "TANDEM" => schedule.TandemAxleFeeKes,
                "TRIDEM" => schedule.TridemAxleFeeKes,
                "QUAD" => schedule.QuadAxleFeeKes,
                _ => schedule.SingleDriveAxleFeeKes
            };
        }

        return axleType.ToUpper() switch
        {
            "STEERING" => schedule.SteeringAxleFeeUsd,
            "SINGLEDRIVE" or "SINGLE_DRIVE" => schedule.SingleDriveAxleFeeUsd,
            "TANDEM" => schedule.TandemAxleFeeUsd,
            "TRIDEM" => schedule.TridemAxleFeeUsd,
            "QUAD" => schedule.QuadAxleFeeUsd,
            _ => schedule.SingleDriveAxleFeeUsd
        };
    }

    public async Task<AxleTypeOverloadFeeSchedule> CreateAsync(
        AxleTypeOverloadFeeSchedule feeSchedule,
        CancellationToken cancellationToken = default)
    {
        feeSchedule.CreatedAt = DateTime.UtcNow;
        feeSchedule.UpdatedAt = DateTime.UtcNow;

        await _context.AxleTypeOverloadFeeSchedules.AddAsync(feeSchedule, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return feeSchedule;
    }

    public async Task<AxleTypeOverloadFeeSchedule> UpdateAsync(
        AxleTypeOverloadFeeSchedule feeSchedule,
        CancellationToken cancellationToken = default)
    {
        feeSchedule.UpdatedAt = DateTime.UtcNow;

        _context.AxleTypeOverloadFeeSchedules.Update(feeSchedule);
        await _context.SaveChangesAsync(cancellationToken);

        return feeSchedule;
    }
}
