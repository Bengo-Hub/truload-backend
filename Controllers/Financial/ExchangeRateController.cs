using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.Financial;
using TruLoad.Backend.Services.Interfaces.Financial;

namespace TruLoad.Backend.Controllers.Financial;

/// <summary>
/// Controller for exchange rate management.
/// Supports manual rates, API settings, and rate history.
/// </summary>
[ApiController]
[Route("api/v1/exchange-rates")]
[Authorize]
public class ExchangeRateController : ControllerBase
{
    private readonly ICurrencyService _currencyService;
    private readonly ILogger<ExchangeRateController> _logger;

    public ExchangeRateController(
        ICurrencyService currencyService,
        ILogger<ExchangeRateController> logger)
    {
        _currencyService = currencyService;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    /// <summary>
    /// Get the current USD/KES exchange rate.
    /// </summary>
    [HttpGet("current")]
    [HasPermission("config.read")]
    [ProducesResponseType(typeof(CurrentRateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentRateResponse>> GetCurrentRate(
        [FromQuery] string from = "USD",
        [FromQuery] string to = "KES",
        CancellationToken ct = default)
    {
        var rate = await _currencyService.GetCurrentRateAsync(from, to, ct);
        return Ok(rate);
    }

    /// <summary>
    /// Get exchange rate history.
    /// </summary>
    [HttpGet("history")]
    [HasPermission("config.read")]
    [ProducesResponseType(typeof(List<ExchangeRateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ExchangeRateDto>>> GetRateHistory(
        [FromQuery] string from = "USD",
        [FromQuery] string to = "KES",
        [FromQuery] int days = 30,
        CancellationToken ct = default)
    {
        var history = await _currencyService.GetRateHistoryAsync(from, to, days, ct);
        return Ok(history);
    }

    /// <summary>
    /// Set a manual exchange rate.
    /// </summary>
    [HttpPost("manual")]
    [HasPermission("config.update")]
    [ProducesResponseType(typeof(ExchangeRateDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExchangeRateDto>> SetManualRate(
        [FromBody] SetManualRateRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        var rate = await _currencyService.SetManualRateAsync(request, userId, ct);
        return Ok(rate);
    }

    /// <summary>
    /// Get API provider settings (access key masked).
    /// </summary>
    [HttpGet("api-settings")]
    [HasPermission("config.read")]
    [ProducesResponseType(typeof(ExchangeRateApiSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExchangeRateApiSettingsDto>> GetApiSettings(CancellationToken ct)
    {
        var settings = await _currencyService.GetApiSettingsAsync(ct);
        if (settings == null)
            return NotFound(new { message = "No API settings configured" });
        return Ok(settings);
    }

    /// <summary>
    /// Update API provider settings.
    /// </summary>
    [HttpPut("api-settings")]
    [HasPermission("config.update")]
    [ProducesResponseType(typeof(ExchangeRateApiSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExchangeRateApiSettingsDto>> UpdateApiSettings(
        [FromBody] UpdateApiSettingsRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        var settings = await _currencyService.UpdateApiSettingsAsync(request, userId, ct);
        return Ok(settings);
    }

    /// <summary>
    /// Trigger an immediate exchange rate fetch from the API.
    /// </summary>
    [HttpPost("fetch-now")]
    [HasPermission("config.update")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> FetchNow(CancellationToken ct)
    {
        await _currencyService.FetchRatesFromApiAsync(ct);
        return Ok(new { message = "Exchange rate fetch completed" });
    }
}
