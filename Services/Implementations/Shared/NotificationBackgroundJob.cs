using Hangfire;
using TruLoad.Backend.Services.Interfaces.Shared;

namespace TruLoad.Backend.Services.Implementations.Shared;

/// <summary>
/// Hangfire background job for processing notifications asynchronously with retries.
/// </summary>
public class NotificationBackgroundJob
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationBackgroundJob> _logger;

    public NotificationBackgroundJob(
        INotificationService notificationService,
        ILogger<NotificationBackgroundJob> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Processes a queued email notification.
    /// </summary>
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task SendEmailAsync(
        string templateName,
        string recipientEmail,
        string recipientName,
        Dictionary<string, object> templateData,
        string? subject = null)
    {
        _logger.LogInformation("Processing background email job for {Email} using template {Template}", 
            recipientEmail, templateName);

        var success = await _notificationService.SendEmailAsync(
            templateName,
            recipientEmail,
            recipientName,
            templateData,
            subject);

        if (!success)
        {
            throw new Exception($"Failed to send email to {recipientEmail}. Job will retry.");
        }
    }

    /// <summary>
    /// Processes a queued SMS notification.
    /// </summary>
    [AutomaticRetry(Attempts = 5, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task SendSmsAsync(
        string phoneNumber,
        string message)
    {
        _logger.LogInformation("Processing background SMS job for {PhoneNumber}", phoneNumber);

        var success = await _notificationService.SendSmsAsync(phoneNumber, message);

        if (!success)
        {
            throw new Exception($"Failed to send SMS to {phoneNumber}. Job will retry.");
        }
    }

    /// <summary>
    /// Processes a queued push notification.
    /// </summary>
    public async Task SendPushNotificationAsync(
        Guid userId,
        string title,
        string body,
        Dictionary<string, string>? data = null)
    {
        _logger.LogInformation("Processing background push notification job for user {UserId}", userId);

        var success = await _notificationService.SendPushNotificationAsync(userId, title, body, data);

        if (!success)
        {
            _logger.LogWarning("Failed to send push notification to user {UserId}. Not retrying for push.", userId);
        }
    }
}
