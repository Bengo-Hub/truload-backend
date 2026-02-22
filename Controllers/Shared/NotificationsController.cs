using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}
