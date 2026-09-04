using TruLoad.Backend.Models;

namespace TruLoad.Backend.Repositories.UserManagement.Interfaces;

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Organization?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<List<Organization>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Organization> CreateAsync(Organization organization, CancellationToken cancellationToken = default);
    Task<Organization> UpdateAsync(Organization organization, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<Organization?> GetBySsoTenantSlugAsync(string ssoTenantSlug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns EVERY active organisation sharing the given SSO tenant slug, ordered by
    /// <see cref="Organization.CreatedAt"/> ascending. codevertex-demo now maps to multiple
    /// outlet-scoped organisations (CODEVERTEX-DEMO plus per-vertical outlets synced by
    /// AuthDemoSyncService) that all carry the same slug — callers that need to disambiguate
    /// between them (see <c>AuthController.SsoExchange</c>) use this instead of the single-result
    /// <see cref="GetBySsoTenantSlugAsync"/>, which is unordered and picks arbitrarily among
    /// multiple matches. Ascending CreatedAt makes index 0 a stable "oldest/primary" tie-break for
    /// the one-time case where no other signal is available (a brand-new, never-before-seen user).
    /// </summary>
    Task<List<Organization>> GetAllBySsoTenantSlugAsync(string ssoTenantSlug, CancellationToken cancellationToken = default);
}
