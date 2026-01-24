using TruLoad.Backend.Models.Infrastructure;

namespace TruLoad.Backend.Repositories.Infrastructure;

/// <summary>
/// Repository for scale test/calibration management
/// </summary>
public interface IScaleTestRepository
{
    /// <summary>
    /// Get all scale tests for a station, optionally filtered by bound
    /// </summary>
    Task<List<ScaleTest>> GetByStationAsync(Guid stationId, string? bound = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get latest scale test for a station, optionally filtered by bound
    /// </summary>
    Task<ScaleTest?> GetLatestByStationAsync(Guid stationId, string? bound = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get scale test by ID
    /// </summary>
    Task<ScaleTest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get tests within date range for a station, optionally filtered by bound
    /// </summary>
    Task<List<ScaleTest>> GetByDateRangeAsync(
        Guid stationId,
        DateTime fromDate,
        DateTime toDate,
        string? bound = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if station has passed daily calibration (within last 24 hours), optionally for specific bound
    /// </summary>
    Task<bool> HasPassedDailyCalibrationalAsync(Guid stationId, string? bound = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all failed tests for a station, optionally filtered by bound
    /// </summary>
    Task<List<ScaleTest>> GetFailedTestsAsync(Guid stationId, string? bound = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create new scale test record
    /// </summary>
    Task<ScaleTest> CreateAsync(ScaleTest scaleTest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update existing scale test
    /// </summary>
    Task<ScaleTest> UpdateAsync(ScaleTest scaleTest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft delete scale test
    /// </summary>
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get scale tests carried out by a specific user
    /// </summary>
    Task<List<ScaleTest>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get today's passing scale test for a station and bound (if any)
    /// </summary>
    Task<ScaleTest?> GetTodaysPassingTestAsync(Guid stationId, string? bound = null, CancellationToken cancellationToken = default);
}
