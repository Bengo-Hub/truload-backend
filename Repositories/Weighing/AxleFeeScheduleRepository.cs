using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Repositories.Weighing.Interfaces;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Repositories.Weighing;

/// <summary>
/// Repository implementation for AxleFeeSchedule entity with fee lookup capabilities
/// </summary>
public class AxleFeeScheduleRepository : IAxleFeeScheduleRepository
{
    private readonly TruLoadDbContext _context;

    public AxleFeeScheduleRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<List<AxleFeeSchedule>> GetAllByFrameworkAsync(
        string legalFramework,
        CancellationToken cancellationToken = default)
    {
        return await _context.AxleFeeSchedules
            .AsNoTracking()
            .Where(f => f.LegalFramework == legalFramework.ToUpper())
            .Where(f => f.IsActive)
            .OrderBy(f => f.FeeType)
            .ThenBy(f => f.OverloadMinKg)
            .ToListAsync(cancellationToken);
    }

    public async Task<AxleFeeSchedule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.AxleFeeSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public async Task<AxleFeeSchedule?> GetFeeByOverloadAsync(
        string legalFramework, 
        string feeType, 
        int overloadKg,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow.Date;

        return await _context.AxleFeeSchedules
            .AsNoTracking()
            .Where(f => f.LegalFramework == legalFramework.ToUpper())
            .Where(f => f.FeeType == feeType.ToUpper())
            .Where(f => f.IsActive)
            .Where(f => f.EffectiveFrom <= now && (f.EffectiveTo == null || f.EffectiveTo >= now))
            .Where(f => f.OverloadMinKg <= overloadKg && (f.OverloadMaxKg == null || f.OverloadMaxKg >= overloadKg))
            .OrderBy(f => f.OverloadMinKg)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(decimal FeeAmountUsd, int DemeritPoints)?> CalculateFeeAsync(
        string legalFramework,
        string feeType,
        int overloadKg,
        CancellationToken cancellationToken = default)
    {
        return await CalculateFeeAsync(legalFramework, feeType, overloadKg, "USD", cancellationToken);
    }

    public async Task<(decimal FeeAmountUsd, int DemeritPoints)?> CalculateFeeAsync(
        string legalFramework,
        string feeType,
        int overloadKg,
        string currency,
        CancellationToken cancellationToken = default)
    {
        var schedule = await GetFeeByOverloadAsync(legalFramework, feeType, overloadKg, cancellationToken);

        if (schedule == null)
        {
            return null;
        }

        // Calculate fee using KES or USD columns based on act's charging currency
        decimal fee;
        if (currency.Equals("KES", StringComparison.OrdinalIgnoreCase) && (schedule.FeePerKgKes > 0 || schedule.FlatFeeKes > 0))
        {
            fee = (overloadKg * schedule.FeePerKgKes) + schedule.FlatFeeKes;
        }
        else
        {
            fee = (overloadKg * schedule.FeePerKgUsd) + schedule.FlatFeeUsd;
        }

        return (fee, schedule.DemeritPoints);
    }

    public async Task<AxleFeeSchedule> CreateAsync(AxleFeeSchedule feeSchedule, CancellationToken cancellationToken = default)
    {
        feeSchedule.CreatedAt = DateTime.UtcNow;
        feeSchedule.UpdatedAt = DateTime.UtcNow;
        
        await _context.AxleFeeSchedules.AddAsync(feeSchedule, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        
        return feeSchedule;
    }

    public async Task<AxleFeeSchedule> UpdateAsync(AxleFeeSchedule feeSchedule, CancellationToken cancellationToken = default)
    {
        feeSchedule.UpdatedAt = DateTime.UtcNow;
        
        _context.AxleFeeSchedules.Update(feeSchedule);
        await _context.SaveChangesAsync(cancellationToken);
        
        return feeSchedule;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var feeSchedule = await GetByIdAsync(id, cancellationToken);
        if (feeSchedule != null)
        {
            _context.AxleFeeSchedules.Remove(feeSchedule);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        return false;
    }
}
