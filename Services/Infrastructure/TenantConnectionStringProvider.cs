using Microsoft.Extensions.Configuration;

namespace TruLoad.Backend.Services.Infrastructure;

/// <summary>
/// Singleton service that resolves per-tenant database connection strings.
///
/// Usage: Add a "TenantDatabases" section to appsettings.json (or K8s env vars):
/// <code>
/// "TenantDatabases": {
///   "kura": "Host=pg.internal;Database=kuraweigh;Username=app;Password=${KURA_DB_PASS}"
/// }
/// </code>
/// At runtime, if the current tenant slug matches a key in this map, the
/// tenant-specific connection string is returned.  Otherwise the shared
/// "DefaultConnection" is used (all commercial and enforcement tenants that
/// share the central truload database).
///
/// Connection strings for tenants with dedicated DBs must be injected via
/// Kubernetes secrets (never hardcoded here), e.g.:
///   TENANTDATABASES__KURA=Host=...;Database=kuraweigh;...
/// </summary>
public sealed class TenantConnectionStringProvider
{
    private readonly Dictionary<string, string> _tenantMap;
    private readonly string _defaultConnectionString;

    public TenantConnectionStringProvider(IConfiguration configuration, string defaultConnectionString)
    {
        _defaultConnectionString = defaultConnectionString;

        _tenantMap = configuration
            .GetSection("TenantDatabases")
            .GetChildren()
            .Where(c => !string.IsNullOrWhiteSpace(c.Value))
            .ToDictionary(
                c => c.Key.ToLowerInvariant(),
                c => c.Value!,
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the connection string for the given tenant slug.
    /// Falls back to the shared default when no dedicated DB is configured.
    /// </summary>
    public string Resolve(string? tenantSlug)
    {
        if (!string.IsNullOrWhiteSpace(tenantSlug)
            && _tenantMap.TryGetValue(tenantSlug.ToLowerInvariant(), out var tenantCs))
        {
            return tenantCs;
        }
        return _defaultConnectionString;
    }

    public bool HasTenantDatabase(string tenantSlug) =>
        _tenantMap.ContainsKey(tenantSlug.ToLowerInvariant());
}
