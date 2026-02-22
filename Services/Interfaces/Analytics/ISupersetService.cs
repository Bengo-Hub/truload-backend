using TruLoad.Backend.DTOs.Analytics;

namespace TruLoad.Backend.Services.Interfaces.Analytics;

/// <summary>
/// Service for integrating with Apache Superset for analytics and dashboards.
/// </summary>
public interface ISupersetService
{
    /// <summary>
    /// Get a guest token for embedding Superset dashboards.
    /// </summary>
    Task<SupersetGuestTokenResponse> GetGuestTokenAsync(SupersetGuestTokenRequest request, string? username = null, string? firstName = null, string? lastName = null, CancellationToken ct = default);

    /// <summary>
    /// List available dashboards from Superset.
    /// </summary>
    Task<List<SupersetDashboardDto>> GetDashboardsAsync(CancellationToken ct = default);

    /// <summary>
    /// Get a specific dashboard by ID.
    /// </summary>
    Task<SupersetDashboardDto?> GetDashboardAsync(int dashboardId, CancellationToken ct = default);

    /// <summary>
    /// Execute a natural language query using Ollama for text-to-SQL conversion.
    /// </summary>
    Task<NaturalLanguageQueryResponse> ExecuteNaturalLanguageQueryAsync(NaturalLanguageQueryRequest request, CancellationToken ct = default);
}
