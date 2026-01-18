using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models.Infrastructure;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Repositories.Infrastructure;

/// <summary>
/// Implementation of scale test repository with EF Core
/// </summary>
public class ScaleTestRepository : IScaleTestRepository
{
    private readonly TruLoadDbContext _context;

    public ScaleTestRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<List<ScaleTest>> GetByStationAsync(Guid stationId, CancellationToken cancellationToken = default)
    {
        return await _context.ScaleTests
            .Where(st => st.StationId == stationId && st.DeletedAt == null)
            .Include(st => st.Station)
            .Include(st => st.CarriedBy)
            .OrderByDescending(st => st.CarriedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ScaleTest?> GetLatestByStationAsync(Guid stationId, CancellationToken cancellationToken = default)
    {
        return await _context.ScaleTests
            .Where(st => st.StationId == stationId && st.DeletedAt == null)
            .Include(st => st.Station)
            .Include(st => st.CarriedBy)
            .OrderByDescending(st => st.CarriedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ScaleTest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ScaleTests
            .Include(st => st.Station)
            .Include(st => st.CarriedBy)
            .FirstOrDefaultAsync(st => st.Id == id && st.DeletedAt == null, cancellationToken);
    }

    public async Task<List<ScaleTest>> GetByDateRangeAsync(
        Guid stationId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.ScaleTests
            .Where(st => st.StationId == stationId &&
                        st.CarriedAt >= fromDate &&
                        st.CarriedAt <= toDate &&
                        st.DeletedAt == null)
            .Include(st => st.Station)
            .Include(st => st.CarriedBy)
            .OrderByDescending(st => st.CarriedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasPassedDailyCalibrationalAsync(Guid stationId, CancellationToken cancellationToken = default)
    {
        var twentyFourHoursAgo = DateTime.UtcNow.AddHours(-24);

        var latestTest = await _context.ScaleTests
            .Where(st => st.StationId == stationId &&
                        st.CarriedAt >= twentyFourHoursAgo &&
                        st.DeletedAt == null)
            .OrderByDescending(st => st.CarriedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return latestTest != null && latestTest.Result.ToLower() == "pass";
    }

    public async Task<List<ScaleTest>> GetFailedTestsAsync(Guid stationId, CancellationToken cancellationToken = default)
    {
        return await _context.ScaleTests
            .Where(st => st.StationId == stationId &&
                        st.Result.ToLower() == "fail" &&
                        st.DeletedAt == null)
            .Include(st => st.Station)
            .Include(st => st.CarriedBy)
            .OrderByDescending(st => st.CarriedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ScaleTest> CreateAsync(ScaleTest scaleTest, CancellationToken cancellationToken = default)
    {
        scaleTest.Id = Guid.NewGuid();
        scaleTest.CreatedAt = DateTime.UtcNow;
        scaleTest.UpdatedAt = DateTime.UtcNow;

        _context.ScaleTests.Add(scaleTest);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(scaleTest.Id, cancellationToken) ?? scaleTest;
    }

    public async Task<ScaleTest> UpdateAsync(ScaleTest scaleTest, CancellationToken cancellationToken = default)
    {
        scaleTest.UpdatedAt = DateTime.UtcNow;

        _context.ScaleTests.Update(scaleTest);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(scaleTest.Id, cancellationToken) ?? scaleTest;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var scaleTest = await _context.ScaleTests.FindAsync(new object[] { id }, cancellationToken);
        if (scaleTest == null) return false;

        scaleTest.DeletedAt = DateTime.UtcNow;
        scaleTest.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<ScaleTest>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.ScaleTests
            .Where(st => st.CarriedById == userId && st.DeletedAt == null)
            .Include(st => st.Station)
            .Include(st => st.CarriedBy)
            .OrderByDescending(st => st.CarriedAt)
            .ToListAsync(cancellationToken);
    }
}
