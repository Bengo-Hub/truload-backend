using System.ComponentModel.DataAnnotations;

namespace TruLoad.Backend.DTOs.Financial;

public record ExchangeRateDto
{
    public Guid Id { get; init; }
    public string FromCurrency { get; init; } = "USD";
    public string ToCurrency { get; init; } = "KES";
    public decimal Rate { get; init; }
    public DateOnly EffectiveDate { get; init; }
    public string Source { get; init; } = "manual";
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record ExchangeRateApiSettingsDto
{
    public Guid Id { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string ProviderName { get; init; } = string.Empty;
    public string ApiEndpoint { get; init; } = string.Empty;
    public bool HasAccessKey { get; init; }
    public string SourceCurrency { get; init; } = "USD";
    public string TargetCurrenciesJson { get; init; } = "[]";
    public TimeOnly FetchTime { get; init; }
    public DateTime? LastFetchAt { get; init; }
    public string? LastFetchStatus { get; init; }
    public string? LastFetchError { get; init; }
    public bool IsActive { get; init; }
}

public record SetManualRateRequest
{
    [Required]
    public decimal Rate { get; init; }

    public string FromCurrency { get; init; } = "USD";
    public string ToCurrency { get; init; } = "KES";
}

public record UpdateApiSettingsRequest
{
    public string? Provider { get; init; }
    public string? ProviderName { get; init; }
    public string? ApiEndpoint { get; init; }
    public string? AccessKey { get; init; }
    public string? SourceCurrency { get; init; }
    public string? TargetCurrenciesJson { get; init; }
    public TimeOnly? FetchTime { get; init; }
    public bool? IsActive { get; init; }
}

public record CurrentRateResponse
{
    public decimal Rate { get; init; }
    public string FromCurrency { get; init; } = "USD";
    public string ToCurrency { get; init; } = "KES";
    public DateOnly EffectiveDate { get; init; }
    public string Source { get; init; } = "manual";
    public DateTime? LastUpdated { get; init; }
}
