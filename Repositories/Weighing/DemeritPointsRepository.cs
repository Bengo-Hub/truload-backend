using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Repositories.Weighing.Interfaces;

namespace TruLoad.Backend.Repositories.Weighing;

/// <summary>
/// Repository implementation for demerit points and penalty schedule operations.
/// Implements NTSA demerit points system per Kenya Traffic Act Cap 403 Section 117A.
/// </summary>
public class DemeritPointsRepository : IDemeritPointsRepository
{
    private readonly TruLoadDbContext _context;

    public DemeritPointsRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<DemeritPointSchedule?> GetDemeritScheduleAsync(
        string legalFramework,
        string violationType,
        int overloadKg,
        CancellationToken cancellationToken = default)
    {
        return await _context.DemeritPointSchedules
            .AsNoTracking()
            .Where(d => d.LegalFramework == legalFramework.ToUpper())
            .Where(d => d.ViolationType == violationType.ToUpper())
            .Where(d => d.IsActive)
            .Where(d => d.DeletedAt == null)
            .Where(d => d.OverloadMinKg <= overloadKg && (d.OverloadMaxKg == null || d.OverloadMaxKg >= overloadKg))
            .OrderByDescending(d => d.OverloadMinKg)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> CalculatePointsAsync(
        string legalFramework,
        string violationType,
        int overloadKg,
        CancellationToken cancellationToken = default)
    {
        if (overloadKg <= 0) return 0;

        var schedule = await GetDemeritScheduleAsync(legalFramework, violationType, overloadKg, cancellationToken);
        return schedule?.Points ?? 0;
    }

    public async Task<List<DemeritPointSchedule>> GetAllSchedulesAsync(
        string legalFramework,
        CancellationToken cancellationToken = default)
    {
        return await _context.DemeritPointSchedules
            .AsNoTracking()
            .Where(d => d.LegalFramework == legalFramework.ToUpper())
            .Where(d => d.IsActive)
            .Where(d => d.DeletedAt == null)
            .OrderBy(d => d.ViolationType)
            .ThenBy(d => d.OverloadMinKg)
            .ToListAsync(cancellationToken);
    }

    public async Task<PenaltySchedule?> GetPenaltyScheduleAsync(
        int totalPoints,
        CancellationToken cancellationToken = default)
    {
        if (totalPoints <= 0) return null;

        return await _context.PenaltySchedules
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Where(p => p.DeletedAt == null)
            .Where(p => p.PointsMin <= totalPoints && (p.PointsMax == null || p.PointsMax >= totalPoints))
            .OrderByDescending(p => p.PointsMin)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<PenaltySchedule>> GetAllPenaltySchedulesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.PenaltySchedules
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Where(p => p.DeletedAt == null)
            .OrderBy(p => p.PointsMin)
            .ToListAsync(cancellationToken);
    }

    public async Task<DemeritPointSchedule> CreateDemeritScheduleAsync(
        DemeritPointSchedule schedule,
        CancellationToken cancellationToken = default)
    {
        schedule.CreatedAt = DateTime.UtcNow;
        schedule.UpdatedAt = DateTime.UtcNow;

        await _context.DemeritPointSchedules.AddAsync(schedule, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return schedule;
    }

    public async Task<PenaltySchedule> CreatePenaltyScheduleAsync(
        PenaltySchedule schedule,
        CancellationToken cancellationToken = default)
    {
        schedule.CreatedAt = DateTime.UtcNow;
        schedule.UpdatedAt = DateTime.UtcNow;

        await _context.PenaltySchedules.AddAsync(schedule, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return schedule;
    }
}
