using System.ComponentModel.DataAnnotations;

namespace TruLoad.Backend.DTOs.Analytics;

/// <summary>
/// Request for a Superset guest token for embedding dashboards.
/// </summary>
public record SupersetGuestTokenRequest
{
    /// <summary>
    /// Dashboard IDs to grant access to.
    /// </summary>
    [Required]
    public List<int> DashboardIds { get; init; } = new();

    /// <summary>
    /// Optional filters to apply to the embedded dashboard.
    /// </summary>
    public Dictionary<string, string>? Filters { get; init; }
}

/// <summary>
/// Response containing the Superset guest token.
/// </summary>
public record SupersetGuestTokenResponse(
    string Token,
    DateTime ExpiresAt
);

/// <summary>
/// Information about a Superset dashboard.
/// </summary>
public record SupersetDashboardDto(
    int Id,
    string Title,
    string? Slug,
    string? Url,
    string? ThumbnailUrl,
    bool Published,
    DateTime? CreatedAt,
    DateTime? ChangedAt
);

/// <summary>
/// Request for natural language query conversion.
/// </summary>
public record NaturalLanguageQueryRequest
{
    /// <summary>
    /// The natural language question (e.g., "How many weighing transactions were there last month?")
    /// </summary>
    [Required]
    [MaxLength(1000)]
    public string Question { get; init; } = string.Empty;

    /// <summary>
    /// Optional context about available tables/columns.
    /// </summary>
    public string? SchemaContext { get; init; }
}

/// <summary>
/// Response from natural language query execution.
/// </summary>
public record NaturalLanguageQueryResponse(
    string OriginalQuestion,
    string GeneratedSql,
    List<Dictionary<string, object>>? Results,
    string? Error,
    bool Success
);

/// <summary>
/// Request for an async natural language query via SignalR.
/// </summary>
public record AsyncNaturalLanguageQueryRequest
{
    /// <summary>
    /// The natural language question.
    /// </summary>
    [Required]
    [MaxLength(1000)]
    public string Question { get; init; } = string.Empty;

    /// <summary>
    /// Optional context about available tables/columns.
    /// </summary>
    public string? SchemaContext { get; init; }

    /// <summary>
    /// SignalR connection ID to push results to.
    /// </summary>
    [Required]
    public string ConnectionId { get; init; } = string.Empty;
}

/// <summary>
/// Response returned immediately when an async query is submitted.
/// </summary>
public record AsyncQueryAcceptedResponse(string JobId);

/// <summary>
/// Superset API authentication response.
/// </summary>
public record SupersetAuthResponse(
    string AccessToken,
    string RefreshToken
);

/// <summary>
/// Configuration for Superset connection.
/// </summary>
public class SupersetOptions
{
    public const string SectionName = "Superset";

    public string BaseUrl { get; set; } = "https://superset.codevertexitsolutions.com";
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "admin123";
    public int GuestTokenExpiryMinutes { get; set; } = 300;
    public int DatabaseId { get; set; } = 1;

    /// <summary>
    /// Optional tag to filter dashboards by service (e.g., "truload").
    /// Only dashboards whose title or slug contains this tag will be returned.
    /// If empty, all dashboards are returned.
    /// </summary>
    public string ServiceTag { get; set; } = "";
}

/// <summary>
/// Configuration for Ollama LLM connection.
/// </summary>
public class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama2";
    public int TimeoutSeconds { get; set; } = 60;
}
