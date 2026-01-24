using TruLoad.Backend.Models.System;

namespace TruLoad.Backend.Repositories.Weighing.Interfaces;

/// <summary>
/// Repository interface for demerit points and penalty schedule operations.
/// Supports NTSA integration for license management.
/// </summary>
public interface IDemeritPointsRepository
{
    /// <summary>
    /// Get demerit points for a specific violation type and overload amount
    /// </summary>
    /// <param name="legalFramework">EAC or TRAFFIC_ACT</param>
    /// <param name="violationType">STEERING, SINGLE_DRIVE, TANDEM, TRIDEM, GVW</param>
    /// <param name="overloadKg">Overload amount in kg</param>
    /// <returns>Demerit point schedule entry</returns>
    Task<DemeritPointSchedule?> GetDemeritScheduleAsync(
        string legalFramework,
        string violationType,
        int overloadKg,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate demerit points for a violation
    /// </summary>
    /// <param name="legalFramework">EAC or TRAFFIC_ACT</param>
    /// <param name="violationType">STEERING, SINGLE_DRIVE, TANDEM, TRIDEM, GVW</param>
    /// <param name="overloadKg">Overload amount in kg</param>
    /// <returns>Points assigned</returns>
    Task<int> CalculatePointsAsync(
        string legalFramework,
        string violationType,
        int overloadKg,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all demerit point schedules for a legal framework
    /// </summary>
    Task<List<DemeritPointSchedule>> GetAllSchedulesAsync(
        string legalFramework,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get penalty schedule based on total accumulated points
    /// </summary>
    /// <param name="totalPoints">Total demerit points</param>
    /// <returns>Applicable penalty schedule</returns>
    Task<PenaltySchedule?> GetPenaltyScheduleAsync(
        int totalPoints,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all penalty schedules
    /// </summary>
    Task<List<PenaltySchedule>> GetAllPenaltySchedulesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a demerit point schedule entry
    /// </summary>
    Task<DemeritPointSchedule> CreateDemeritScheduleAsync(
        DemeritPointSchedule schedule,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a penalty schedule entry
    /// </summary>
    Task<PenaltySchedule> CreatePenaltyScheduleAsync(
        PenaltySchedule schedule,
        CancellationToken cancellationToken = default);
}
