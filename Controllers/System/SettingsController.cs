using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.DTOs.Settings;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Services.Interfaces.System;

namespace TruLoad.Backend.Controllers.System;

/// <summary>
/// Controller for managing application-wide settings.
/// Provides endpoints for password policy, shift settings, and other configurations.
/// </summary>
[ApiController]
[Route("api/v1/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        ISettingsService settingsService,
        ILogger<SettingsController> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    /// <summary>
    /// Get all application settings.
    /// </summary>
    [HttpGet]
    [HasPermission("system.security_policy")]
    [ProducesResponseType(typeof(List<ApplicationSettingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ApplicationSettingDto>>> GetAllSettings(CancellationToken ct)
    {
        var settings = await _settingsService.GetAllSettingsAsync(ct);
        return Ok(settings);
    }

    /// <summary>
    /// Get settings by category.
    /// </summary>
    [HttpGet("category/{category}")]
    [HasPermission("system.security_policy")]
    [ProducesResponseType(typeof(List<ApplicationSettingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ApplicationSettingDto>>> GetSettingsByCategory(
        string category,
        CancellationToken ct)
    {
        var settings = await _settingsService.GetSettingsByCategoryAsync(category, ct);
        return Ok(settings);
    }

    /// <summary>
    /// Get a single setting by key.
    /// </summary>
    [HttpGet("key/{key}")]
    [HasPermission("system.security_policy")]
    [ProducesResponseType(typeof(ApplicationSettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationSettingDto>> GetSetting(string key, CancellationToken ct)
    {
        var setting = await _settingsService.GetSettingAsync(key, ct);
        if (setting == null)
        {
            return NotFound(new { message = $"Setting '{key}' not found" });
        }
        return Ok(setting);
    }

    /// <summary>
    /// Update a single setting.
    /// </summary>
    [HttpPut("key/{key}")]
    [HasPermission("system.security_policy")]
    [ProducesResponseType(typeof(ApplicationSettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApplicationSettingDto>> UpdateSetting(
        string key,
        [FromBody] UpdateSettingRequest request,
        CancellationToken ct)
    {
        try
        {
            var userId = GetUserId();
            var setting = await _settingsService.UpdateSettingAsync(key, request.SettingValue, userId, ct);
            return Ok(setting);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update multiple settings at once.
    /// </summary>
    [HttpPut("batch")]
    [HasPermission("system.security_policy")]
    [ProducesResponseType(typeof(List<ApplicationSettingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ApplicationSettingDto>>> UpdateSettingsBatch(
        [FromBody] UpdateSettingsBatchRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        var settings = await _settingsService.UpdateSettingsBatchAsync(request.Settings, userId, ct);
        return Ok(settings);
    }

    // ============================================================================
    // Password Policy Endpoints
    // ============================================================================

    /// <summary>
    /// Get current password policy configuration.
    /// </summary>
    [HttpGet("password-policy")]
    [HasPermission("system.security_policy")]
    [ProducesResponseType(typeof(PasswordPolicyDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PasswordPolicyDto>> GetPasswordPolicy(CancellationToken ct)
    {
        var policy = await _settingsService.GetPasswordPolicyAsync(ct);
        return Ok(policy);
    }

    /// <summary>
    /// Update password policy configuration.
    /// </summary>
    [HttpPut("password-policy")]
    [HasPermission("system.security_policy")]
    [ProducesResponseType(typeof(PasswordPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PasswordPolicyDto>> UpdatePasswordPolicy(
        [FromBody] UpdatePasswordPolicyRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetUserId();
        var policy = await _settingsService.UpdatePasswordPolicyAsync(request, userId, ct);

        _logger.LogInformation("Password policy updated by user {UserId}", userId);
        return Ok(policy);
    }

    // ============================================================================
    // Shift Settings Endpoints
    // ============================================================================

    /// <summary>
    /// Get current shift settings configuration.
    /// </summary>
    [HttpGet("shifts")]
    [HasPermission("system.security_policy")]
    [ProducesResponseType(typeof(ShiftSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ShiftSettingsDto>> GetShiftSettings(CancellationToken ct)
    {
        var settings = await _settingsService.GetShiftSettingsAsync(ct);
        return Ok(settings);
    }

    /// <summary>
    /// Update shift settings configuration.
    /// </summary>
    [HttpPut("shifts")]
    [HasPermission("system.security_policy")]
    [ProducesResponseType(typeof(ShiftSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ShiftSettingsDto>> UpdateShiftSettings(
        [FromBody] UpdateShiftSettingsRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetUserId();
        var settings = await _settingsService.UpdateShiftSettingsAsync(request, userId, ct);

        _logger.LogInformation("Shift settings updated by user {UserId}", userId);
        return Ok(settings);
    }

    // ============================================================================
    // Backup Settings Endpoints
    // ============================================================================

    /// <summary>
    /// Get current backup settings configuration.
    /// </summary>
    [HttpGet("backup")]
    [HasPermission("system.backup_restore")]
    [ProducesResponseType(typeof(BackupSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BackupSettingsDto>> GetBackupSettings(CancellationToken ct)
    {
        var settings = await _settingsService.GetBackupSettingsAsync(ct);
        return Ok(settings);
    }

    // ============================================================================
    // Security Overview
    // ============================================================================

    /// <summary>
    /// Get security overview including password policy and 2FA status.
    /// </summary>
    [HttpGet("security")]
    [HasPermission("system.security_policy")]
    [ProducesResponseType(typeof(SecurityOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SecurityOverviewDto>> GetSecurityOverview(CancellationToken ct)
    {
        var overview = await _settingsService.GetSecurityOverviewAsync(ct);
        return Ok(overview);
    }

    // ============================================================================
    // Restore Defaults Endpoints
    // ============================================================================

    /// <summary>
    /// Restore a single setting to its default value.
    /// </summary>
    [HttpPost("key/{key}/restore-default")]
    [HasPermission("system.security_policy")]
    [ProducesResponseType(typeof(ApplicationSettingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationSettingDto>> RestoreDefault(string key, CancellationToken ct)
    {
        try
        {
            var userId = GetUserId();
            var setting = await _settingsService.RestoreDefaultAsync(key, userId, ct);
            return Ok(setting);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Restore all settings in a category to their default values.
    /// </summary>
    [HttpPost("category/{category}/restore-defaults")]
    [HasPermission("system.security_policy")]
    [ProducesResponseType(typeof(List<ApplicationSettingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ApplicationSettingDto>>> RestoreCategoryDefaults(
        string category,
        CancellationToken ct)
    {
        try
        {
            var userId = GetUserId();
            var settings = await _settingsService.RestoreCategoryDefaultsAsync(category, userId, ct);
            return Ok(settings);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // ============================================================================
    // Rate Limit Reload
    // ============================================================================

    /// <summary>
    /// Reload rate limit settings from database without restarting the application.
    /// </summary>
    [HttpPost("reload-rate-limits")]
    [HasPermission("system.security_policy")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ReloadRateLimits()
    {
        var rateLimitSettings = HttpContext.RequestServices.GetRequiredService<RateLimitSettings>();
        var settingsService = _settingsService;

        rateLimitSettings.GlobalAuthenticatedPermit = await settingsService
            .GetSettingValueAsync(Models.System.SettingKeys.RateLimitGlobalAuthenticatedPermit, 600);
        rateLimitSettings.GlobalAuthenticatedWindowMinutes = await settingsService
            .GetSettingValueAsync(Models.System.SettingKeys.RateLimitGlobalAuthenticatedWindowMinutes, 1);
        rateLimitSettings.GlobalAnonymousPermit = await settingsService
            .GetSettingValueAsync(Models.System.SettingKeys.RateLimitGlobalAnonymousPermit, 30);
        rateLimitSettings.DashboardPermit = await settingsService
            .GetSettingValueAsync(Models.System.SettingKeys.RateLimitDashboardPermit, 800);
        rateLimitSettings.ApiPermit = await settingsService
            .GetSettingValueAsync(Models.System.SettingKeys.RateLimitApiPermit, 200);
        rateLimitSettings.WeighingPermit = await settingsService
            .GetSettingValueAsync(Models.System.SettingKeys.RateLimitWeighingPermit, 600);
        rateLimitSettings.AutoweighPermit = await settingsService
            .GetSettingValueAsync(Models.System.SettingKeys.RateLimitAutoweighPermit, 1000);
        rateLimitSettings.AuthPermit = await settingsService
            .GetSettingValueAsync(Models.System.SettingKeys.RateLimitAuthPermit, 10);
        rateLimitSettings.AuthWindowMinutes = await settingsService
            .GetSettingValueAsync(Models.System.SettingKeys.RateLimitAuthWindowMinutes, 5);
        rateLimitSettings.ReportsPermit = await settingsService
            .GetSettingValueAsync(Models.System.SettingKeys.RateLimitReportsPermit, 30);
        rateLimitSettings.SearchPermit = await settingsService
            .GetSettingValueAsync(Models.System.SettingKeys.RateLimitSearchPermit, 120);

        _logger.LogInformation("Rate limit settings reloaded by user {UserId}", GetUserId());
        return Ok(new { message = "Rate limit settings reloaded successfully" });
    }
}
