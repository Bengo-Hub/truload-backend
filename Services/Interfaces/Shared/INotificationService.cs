namespace TruLoad.Backend.Services.Interfaces.Shared;

/// <summary>
/// Shared notification service interface for TruLoad backend.
/// Integrates with the centralized notifications-service to send emails, SMS, and push notifications.
/// All modules (weighing, case management, user management) should use this service instead of individual integrations.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Send email notification using a template.
    /// </summary>
    /// <param name="templateName">Name of the email template (e.g., "password_reset", "shift_assignment")</param>
    /// <param name="recipientEmail">Recipient email address</param>
    /// <param name="recipientName">Recipient name</param>
    /// <param name="templateData">Dictionary of data to populate the template</param>
    /// <param name="subject">Email subject (optional, can be defined in template)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if notification was sent successfully</returns>
    Task<bool> SendEmailAsync(
        string templateName,
        string recipientEmail,
        string recipientName,
        Dictionary<string, object> templateData,
        string? subject = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send SMS notification.
    /// </summary>
    /// <param name="phoneNumber">Recipient phone number (E.164 format recommended)</param>
    /// <param name="message">SMS message content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if SMS was sent successfully</returns>
    Task<bool> SendSmsAsync(
        string phoneNumber,
        string message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send push notification to user's device.
    /// </summary>
    /// <param name="userId">User ID to send notification to</param>
    /// <param name="title">Notification title</param>
    /// <param name="body">Notification body</param>
    /// <param name="data">Additional data payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if push notification was sent successfully</returns>
    Task<bool> SendPushNotificationAsync(
        Guid userId,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send multi-channel notification (email + SMS + push).
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="emailTemplate">Email template name</param>
    /// <param name="smsMessage">SMS message</param>
    /// <param name="pushTitle">Push notification title</param>
    /// <param name="pushBody">Push notification body</param>
    /// <param name="templateData">Template data for email</param>
    /// <param name="channels">Channels to use (email, sms, push)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of channel results</returns>
    Task<Dictionary<string, bool>> SendMultiChannelAsync(
        Guid userId,
        string emailTemplate,
        string smsMessage,
        string pushTitle,
        string pushBody,
        Dictionary<string, object> templateData,
        string[] channels = null!, // defaults to ["email"]
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send batch email notifications.
    /// </summary>
    /// <param name="recipients">List of recipient email addresses</param>
    /// <param name="templateName">Email template name</param>
    /// <param name="templateData">Template data (shared across all recipients)</param>
    /// <param name="subject">Email subject</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of successfully sent emails</returns>
    Task<int> SendBatchEmailAsync(
        List<(string Email, string Name)> recipients,
        string templateName,
        Dictionary<string, object> templateData,
        string? subject = null,
        CancellationToken cancellationToken = default);
}
