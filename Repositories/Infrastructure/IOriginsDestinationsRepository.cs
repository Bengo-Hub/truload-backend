using TruLoad.Backend.Models;

namespace TruLoad.Backend.Repositories.Infrastructure;

/// <summary>
/// Repository for origins and destinations master data
/// </summary>
public interface IOriginsDestinationsRepository
{
    Task<List<OriginsDestinations>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<List<OriginsDestinations>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<OriginsDestinations?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OriginsDestinations?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether <paramref name="code"/> is already taken within a specific uniqueness scope:
    /// the shared/global catalog when <paramref name="organizationId"/> is null, or that org's own
    /// private catalog when it isn't. Mirrors the two filtered unique indexes on (OrganizationId,
    /// Code) - use this (not <see cref="GetByCodeAsync"/>, which looks across every scope) for
    /// duplicate-code checks on Create/Update.
    /// </summary>
    Task<bool> CodeExistsInScopeAsync(string code, Guid? organizationId, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<List<OriginsDestinations>> GetByCountryAsync(string country, CancellationToken cancellationToken = default);
    Task<List<OriginsDestinations>> GetByLocationTypeAsync(string locationType, CancellationToken cancellationToken = default);
    Task<OriginsDestinations> CreateAsync(OriginsDestinations location, CancellationToken cancellationToken = default);
    Task<OriginsDestinations> UpdateAsync(OriginsDestinations location, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
