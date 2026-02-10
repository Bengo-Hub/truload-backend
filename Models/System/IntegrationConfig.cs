using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.System;

/// <summary>
/// Stores integration configurations with encrypted credentials in the database.
/// Supports multiple providers (eCitizen/Pesaflow, NTSA, KeNHA, etc.).
/// Credentials are encrypted at rest using AES-256-GCM.
/// In development, seeded from appsettings.Development.json.
/// In production, managed via admin API.
/// </summary>
public class IntegrationConfig : BaseEntity
{
    /// <summary>
    /// Unique provider identifier (e.g., "ecitizen_pesaflow", "ntsa", "kenha")
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name for the integration
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for the integration API (e.g., "https://test.pesaflow.com")
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// AES-256-GCM encrypted JSON blob containing all credentials.
    /// For Pesaflow: { "ApiKey", "ApiSecret", "ApiClientId", "ServiceId" }
    /// Never exposed in DTOs or API responses.
    /// </summary>
    public string EncryptedCredentials { get; set; } = string.Empty;

    /// <summary>
    /// JSON blob of API endpoint paths (not encrypted - these are public).
    /// Example: { "OAuth": "/api/oauth/generate/token", "CreateInvoice": "/api/invoice/create" }
    /// </summary>
    public string EndpointsJson { get; set; } = "{}";

    /// <summary>
    /// Auto-generated webhook URL based on AppBaseUrl + internal route.
    /// Example: "https://api.truload.co.ke/api/v1/payments/webhook/ecitizen"
    /// </summary>
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// Auto-generated callback URL for payment success redirects.
    /// Example: "https://api.truload.co.ke/api/v1/payments/callback/ecitizen"
    /// </summary>
    public string? CallbackUrl { get; set; }

    /// <summary>
    /// Application's public base URL used to auto-generate webhook/callback URLs.
    /// Set from environment configuration.
    /// </summary>
    public string? AppBaseUrl { get; set; }

    /// <summary>
    /// Integration environment: "test", "sandbox", or "production"
    /// </summary>
    public string? Environment { get; set; } = "test";

    /// <summary>
    /// Description of the integration purpose
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Timestamp when credentials were last rotated
    /// </summary>
    public DateTime? CredentialsRotatedAt { get; set; }
}
