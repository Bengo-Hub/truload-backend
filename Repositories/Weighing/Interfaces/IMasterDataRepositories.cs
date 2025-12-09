using TruLoad.Backend.Models;

namespace TruLoad.Backend.Repositories.Weighing.Interfaces;

/// <summary>
/// Repository for tyre type master data with caching support
/// </summary>
public interface ITyreTypeRepository
{
    /// <summary>
    /// Get all active tyre types (cached for 24 hours)
    /// </summary>
    Task<List<TyreType>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get tyre type by ID
    /// </summary>
    Task<TyreType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get tyre type by code (e.g., "SINGLE", "DUAL")
    /// </summary>
    Task<TyreType?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create new tyre type (admin only)
    /// Invalidates cache
    /// </summary>
    Task<TyreType> CreateAsync(TyreType tyreType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update existing tyre type (admin only)
    /// Invalidates cache
    /// </summary>
    Task<TyreType> UpdateAsync(TyreType tyreType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft delete tyre type
    /// Invalidates cache
    /// </summary>
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository for axle group master data with caching support
/// </summary>
public interface IAxleGroupRepository
{
    /// <summary>
    /// Get all active axle groups (cached for 24 hours)
    /// </summary>
    Task<List<AxleGroup>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get axle group by ID
    /// </summary>
    Task<AxleGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get axle group by code (e.g., "SINGLE", "TANDEM")
    /// </summary>
    Task<AxleGroup?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create new axle group (admin only)
    /// Invalidates cache
    /// </summary>
    Task<AxleGroup> CreateAsync(AxleGroup axleGroup, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update existing axle group (admin only)
    /// Invalidates cache
    /// </summary>
    Task<AxleGroup> UpdateAsync(AxleGroup axleGroup, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft delete axle group
    /// Invalidates cache
    /// </summary>
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository for axle fee schedule with fee lookup capabilities
/// </summary>
public interface IAxleFeeScheduleRepository
{
    /// <summary>
    /// Get all fee schedules filtered by legal framework
    /// </summary>
    Task<List<AxleFeeSchedule>> GetAllByFrameworkAsync(
        string legalFramework,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get fee schedule by ID
    /// </summary>
    Task<AxleFeeSchedule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get applicable fee for specific overload scenario
    /// Returns fee schedule entry matching the overload range
    /// </summary>
    Task<AxleFeeSchedule?> GetFeeByOverloadAsync(
        string legalFramework,
        string feeType,
        int overloadKg,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate fee amount and demerit points for given overload
    /// </summary>
    Task<(decimal FeeAmountUsd, int DemeritPoints)?> CalculateFeeAsync(
        string legalFramework,
        string feeType,
        int overloadKg,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create new fee schedule (admin only)
    /// </summary>
    Task<AxleFeeSchedule> CreateAsync(
        AxleFeeSchedule feeSchedule,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update existing fee schedule (admin only)
    /// </summary>
    Task<AxleFeeSchedule> UpdateAsync(
        AxleFeeSchedule feeSchedule,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete fee schedule
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
