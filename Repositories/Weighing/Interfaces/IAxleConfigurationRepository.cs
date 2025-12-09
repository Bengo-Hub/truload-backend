using TruLoad.Backend.Models;

namespace TruLoad.Backend.Repositories.Weighing.Interfaces;

/// <summary>
/// Repository for axle configuration management
/// Handles both standard EAC configurations and user-derived configurations
/// </summary>
public interface IAxleConfigurationRepository
{
    /// <summary>
    /// Get all axle configurations with optional filtering
    /// </summary>
    Task<List<AxleConfiguration>> GetAllAsync(
        bool? isStandard = null,
        string? legalFramework = null,
        int? axleCount = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get axle configuration by ID including weight references
    /// </summary>
    Task<AxleConfiguration?> GetByIdAsync(
        Guid id,
        bool includeWeightReferences = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get axle configuration by code (e.g., "2A", "3B-DERIVED")
    /// </summary>
    Task<AxleConfiguration?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new derived axle configuration (not standard)
    /// Validates against business rules
    /// </summary>
    Task<AxleConfiguration> CreateDerivedConfigAsync(
        AxleConfiguration config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing derived configuration
    /// Standard configurations cannot be modified
    /// </summary>
    Task<AxleConfiguration> UpdateDerivedConfigAsync(
        AxleConfiguration config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft delete an axle configuration (IsActive = false)
    /// </summary>
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if axle code already exists
    /// </summary>
    Task<bool> CodeExistsAsync(
        string code,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate derived configuration against business rules
    /// </summary>
    Task<(bool IsValid, List<string> Errors)> ValidateDerivedConfigAsync(
        AxleConfiguration config,
        CancellationToken cancellationToken = default);
}
