using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models.System;

/// <summary>
/// Configuration for the exchange rate API provider.
/// Stores provider details, API key (encrypted), and sync schedule.
/// </summary>
[Table("exchange_rate_api_settings")]
public class ExchangeRateApiSettings : BaseEntity
{
    /// <summary>
    /// Provider identifier, e.g., "EXCHANGERATE_HOST"
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Provider { get; set; } = "EXCHANGERATE_HOST";

    /// <summary>
    /// Human-readable provider name
    /// </summary>
    [MaxLength(100)]
    public string ProviderName { get; set; } = "exchangerate.host";

    /// <summary>
    /// API endpoint URL
    /// </summary>
    [MaxLength(500)]
    public string ApiEndpoint { get; set; } = "https://api.exchangerate.host/live";

    /// <summary>
    /// Encrypted API access key (AES-256-GCM via existing crypto infrastructure)
    /// </summary>
    public string? EncryptedAccessKey { get; set; }

    /// <summary>
    /// Source currency for rate fetching
    /// </summary>
    [MaxLength(3)]
    public string SourceCurrency { get; set; } = "USD";

    /// <summary>
    /// Target currencies as JSON array, e.g., ["KES"]
    /// </summary>
    [MaxLength(500)]
    public string TargetCurrenciesJson { get; set; } = "[\"KES\"]";

    /// <summary>
    /// Time of day (UTC) to fetch rates
    /// </summary>
    public TimeOnly FetchTime { get; set; } = new(0, 0);

    /// <summary>
    /// Last time rates were fetched
    /// </summary>
    public DateTime? LastFetchAt { get; set; }

    /// <summary>
    /// Last fetch status: "success", "failed", "pending"
    /// </summary>
    [MaxLength(20)]
    public string? LastFetchStatus { get; set; }

    /// <summary>
    /// Error message from last failed fetch
    /// </summary>
    [MaxLength(1000)]
    public string? LastFetchError { get; set; }
}
