namespace TruLoad.Backend.Configuration;

/// <summary>
/// Configuration options for the centralized Go notifications-service.
/// </summary>
public class NotificationServiceOptions
{
    public const string SectionName = "NotificationService";

    /// <summary>
    /// Base URL of the notifications-service (e.g., "http://notifications-service.notifications.svc.cluster.local:4000")
    /// </summary>
    public required string BaseUrl { get; set; }

    /// <summary>
    /// Tenant identifier for TruLoad (e.g., "truload")
    /// </summary>
    public required string TenantId { get; set; }

    /// <summary>
    /// Optional API key for authentication (X-API-Key header)
    /// If empty, will use JWT authentication instead
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// HTTP request timeout in seconds (default: 30)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
