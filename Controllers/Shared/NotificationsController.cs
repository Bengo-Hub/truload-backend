using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.DTOs.Notifications;
using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.Models.Notifications;
using TruLoad.Backend.Services.Interfaces.Shared;

namespace TruLoad.Backend.Controllers.Shared;

/// <summary>
/// API endpoints for managing notifications and retrieving templates.
/// </summary>
[ApiController]
[Route("api/v1/shared/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationService notificationService,
        ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Returns available notification templates from the centralized service.
    /// </summary>
    [HttpGet("templates")]
    [ProducesResponseType(typeof(List<NotificationTemplateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<NotificationTemplateDto>>> GetTemplates([FromQuery] string? channel, CancellationToken ct)
    {
        var templates = await _notificationService.GetTemplatesAsync(channel, ct);
        return Ok(templates);
    }

    /// <summary>
    /// Registers or updates a user's push notification subscription.
    /// </summary>
    [HttpPost("push-subscription")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> UpdatePushSubscription([FromBody] PushSubscriptionDto subscription, CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized();
        }

        var result = await _notificationService.UpdatePushSubscriptionAsync(userId, subscription, ct);
        if (result)
        {
            return Ok(new { message = "Push subscription updated successfully" });
        }

        return BadRequest(new { error = "Failed to update push subscription" });
    }

    /// <summary>
    /// Returns the current user's notification inbox.
    /// </summary>
    [HttpGet("inbox")]
    [ProducesResponseType(typeof(List<UserNotification>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserNotification>>> GetInbox([FromQuery] bool? isRead, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized();
        }

        var notifications = await _notificationService.GetUserNotificationsAsync(userId, isRead, limit, ct);
        return Ok(notifications);
    }

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    [HttpPost("inbox/{id}/read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized();
        }

        var result = await _notificationService.MarkAsReadAsync(id, userId, ct);
        if (result) return Ok();

        return NotFound();
    }

    /// <summary>
    /// Deletes a notification from the inbox.
    /// </summary>
    [HttpDelete("inbox/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteNotification(Guid id, CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized();
        }

        var result = await _notificationService.DeleteNotificationAsync(id, userId, ct);
        if (result) return Ok();

        return NotFound();
    }

    // ── Provider proxy endpoints ──────────────────────────────────────────────

    /// <summary>Returns email/SMS providers available on the platform.</summary>
    [HttpGet("providers/available")]
    [ProducesResponseType(typeof(List<NotificationProviderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetAvailableProviders(CancellationToken ct)
    {
        var providers = await _notificationService.GetAvailableProvidersAsync(ct);
        return Ok(new { providers });
    }

    /// <summary>Returns the tenant's currently selected providers.</summary>
    [HttpGet("providers/selected")]
    [ProducesResponseType(typeof(List<NotificationProviderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetSelectedProviders(CancellationToken ct)
    {
        var selected = await _notificationService.GetSelectedProvidersAsync(ct);
        return Ok(new { selected });
    }

    /// <summary>Sets the tenant's preferred provider for a channel.</summary>
    [HttpPost("providers/select")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> SelectProvider([FromBody] SelectProviderRequest request, CancellationToken ct)
    {
        var ok = await _notificationService.SelectProviderAsync(request, ct);
        if (!ok) return BadRequest(new { error = "Failed to select provider" });
        return Ok(new { message = "Provider selected" });
    }

    /// <summary>Returns settings for a specific provider (secrets masked).</summary>
    [HttpGet("providers/settings")]
    [ProducesResponseType(typeof(ProviderSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetProviderSettings([FromQuery] string providerType, [FromQuery] string providerName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(providerType) || string.IsNullOrWhiteSpace(providerName))
            return BadRequest(new { error = "provider_type and provider_name are required" });

        var settings = await _notificationService.GetProviderSettingsAsync(providerType, providerName, ct);
        if (settings == null) return NotFound();
        return Ok(settings);
    }

    /// <summary>Saves tenant-level provider settings.</summary>
    [HttpPost("providers/settings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> SaveProviderSettings([FromBody] SaveProviderSettingsRequest request, CancellationToken ct)
    {
        var ok = await _notificationService.SaveProviderSettingsAsync(request, ct);
        if (!ok) return BadRequest(new { error = "Failed to save provider settings" });
        return Ok(new { message = "Settings saved" });
    }

    // ── Workflow preference endpoints ─────────────────────────────────────────

    /// <summary>Returns the tenant's workflow notification preferences.</summary>
    [HttpGet("workflow-preferences")]
    [ProducesResponseType(typeof(WorkflowPreferencesDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<WorkflowPreferencesDto>> GetWorkflowPreferences(CancellationToken ct)
    {
        var prefs = await _notificationService.GetWorkflowPreferencesAsync(ct);
        return Ok(prefs);
    }

    /// <summary>Saves the tenant's workflow notification preferences.</summary>
    [HttpPut("workflow-preferences")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> SaveWorkflowPreferences([FromBody] WorkflowPreferencesDto prefs, CancellationToken ct)
    {
        await _notificationService.SaveWorkflowPreferencesAsync(prefs, ct);
        return Ok(new { message = "Workflow preferences saved" });
    }

    // ── Test email ────────────────────────────────────────────────────────────

    /// <summary>Sends a test email to confirm the notifications-api integration is working.</summary>
    [HttpPost("test-email")]
    [Authorize(Roles = "Superuser,System Admin,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SendTestEmail([FromBody] SendTestEmailRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Recipient))
            return BadRequest(new { error = "recipient is required" });

        var ok = await _notificationService.SendEmailAsync(
            "system_test",
            request.Recipient,
            request.Recipient,
            new Dictionary<string, object>
            {
                ["sent_at"] = DateTime.UtcNow.ToString("O"),
                ["brand_name"] = "TruLoad",
                ["brand_primary_color"] = "#1a1a2e"
            },
            "TruLoad — Test Notification",
            ct);

        if (!ok)
            return BadRequest(new { error = "Email send failed — check notifications-api provider configuration" });

        return Ok(new { message = "Test email sent successfully" });
    }

    // ── Push device token endpoints ───────────────────────────────────────────

    /// <summary>Returns active push device tokens for the current user.</summary>
    [HttpGet("push/tokens")]
    [ProducesResponseType(typeof(List<DeviceTokenItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetDeviceTokens(CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized();

        var tokens = await _notificationService.GetDeviceTokensAsync(userId, ct);
        return Ok(new { tokens });
    }

    /// <summary>Registers an FCM device token for push notifications.</summary>
    [HttpPost("push/tokens")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> RegisterDeviceToken([FromBody] RegisterDeviceTokenRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized();

        var ok = await _notificationService.RegisterDeviceTokenAsync(userId, request, ct);
        if (!ok) return BadRequest(new { error = "Failed to register device token" });
        return Ok(new { message = "Device token registered" });
    }

    /// <summary>Deactivates a push device token.</summary>
    [HttpDelete("push/tokens")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> DeleteDeviceToken([FromBody] DeleteTokenRequest request, CancellationToken ct)
    {
        var ok = await _notificationService.DeleteDeviceTokenAsync(request.Token, ct);
        if (!ok) return NotFound();
        return Ok(new { message = "Device token removed" });
    }
}

public sealed record DeleteTokenRequest(string Token);
public sealed record SendTestEmailRequest(string Recipient);
