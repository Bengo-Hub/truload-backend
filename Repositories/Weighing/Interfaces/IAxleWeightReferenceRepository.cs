using TruLoad.Backend.Models;

namespace TruLoad.Backend.Repositories.Weighing.Interfaces;

/// <summary>
/// Repository for managing individual axle weight references within a configuration.
/// Each weight reference defines the permissible weight and specifications for a single axle position.
/// </summary>
public interface IAxleWeightReferenceRepository
{
    /// <summary>
    /// Get a weight reference by ID
    /// </summary>
    Task<AxleWeightReference?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all weight references for a specific axle configuration
    /// Ordered by axle position (1, 2, 3, ...)
    /// </summary>
    Task<List<AxleWeightReference>> GetByConfigurationIdAsync(
        Guid configurationId,
        bool includeRelations = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get weight reference by configuration ID and position
    /// </summary>
    Task<AxleWeightReference?> GetByPositionAsync(
        Guid configurationId,
        int position,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new weight reference for an axle position
    /// Validates position constraints and relationships
    /// </summary>
    Task<AxleWeightReference> CreateAsync(
        AxleWeightReference reference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing weight reference
    /// </summary>
    Task<AxleWeightReference> UpdateAsync(
        AxleWeightReference reference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a weight reference
    /// </summary>
    Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate weight reference constraints
    /// Checks: position validity, grouping format, weight ranges, relationships
    /// </summary>
    Task<(bool IsValid, List<string> Errors)> ValidateAsync(
        AxleWeightReference reference,
        AxleConfiguration parentConfig,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a position is already occupied in a configuration
    /// </summary>
    Task<bool> PositionExistsAsync(
        Guid configurationId,
        int position,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get count of weight references for a configuration
    /// </summary>
    Task<int> GetCountByConfigurationAsync(
        Guid configurationId,
        CancellationToken cancellationToken = default);
}
