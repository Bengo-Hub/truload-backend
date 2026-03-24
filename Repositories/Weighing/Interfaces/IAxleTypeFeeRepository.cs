using TruLoad.Backend.Models.System;

namespace TruLoad.Backend.Repositories.Weighing.Interfaces;

/// <summary>
/// Repository interface for AxleTypeOverloadFeeSchedule operations.
/// Provides per-axle-type fee lookup capabilities.
/// </summary>
public interface IAxleTypeFeeRepository
{
    /// <summary>
    /// Get all fee schedules for a legal framework
    /// </summary>
    Task<List<AxleTypeOverloadFeeSchedule>> GetAllByFrameworkAsync(
        string legalFramework,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get fee schedule by overload amount
    /// </summary>
    Task<AxleTypeOverloadFeeSchedule?> GetByOverloadAsync(
        string legalFramework,
        int overloadKg,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate fee for a specific axle type and overload
    /// </summary>
    /// <param name="legalFramework">EAC or TRAFFIC_ACT</param>
    /// <param name="axleType">Steering, SingleDrive, Tandem, Tridem, Quad</param>
    /// <param name="overloadKg">Overload amount in kg</param>
    /// <returns>Fee amount in USD</returns>
    Task<decimal> CalculateFeeAsync(
        string legalFramework,
        string axleType,
        int overloadKg,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate fee for a specific axle type, overload, and currency.
    /// </summary>
    Task<decimal> CalculateFeeAsync(
        string legalFramework,
        string axleType,
        int overloadKg,
        string currency,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new fee schedule
    /// </summary>
    Task<AxleTypeOverloadFeeSchedule> CreateAsync(
        AxleTypeOverloadFeeSchedule feeSchedule,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing fee schedule
    /// </summary>
    Task<AxleTypeOverloadFeeSchedule> UpdateAsync(
        AxleTypeOverloadFeeSchedule feeSchedule,
        CancellationToken cancellationToken = default);
}
