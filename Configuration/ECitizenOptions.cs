namespace TruLoad.Backend.Configuration;

/// <summary>
/// Configuration binding for Services:eCitizen section in appsettings.
/// Used only for seeding IntegrationConfig in development.
/// </summary>
public class ECitizenOptions
{
    public const string SectionName = "Services:eCitizen";

    public string BaseUrl { get; set; } = string.Empty;
    public Dictionary<string, string> Endpoints { get; set; } = new();
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string ApiClientId { get; set; } = string.Empty;
}
