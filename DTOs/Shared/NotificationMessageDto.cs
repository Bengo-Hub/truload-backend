namespace TruLoad.Backend.DTOs.Shared;

/// <summary>
/// Request DTO for sending notification message to Go notifications-service.
/// Maps to CreateMessageRequest in Go service.
/// </summary>
public class NotificationMessageRequest
{
    /// <summary>
    /// Notification channel: "email", "sms", "push"
    /// </summary>
    public required string Channel { get; set; }

    /// <summary>
    /// Tenant identifier (e.g., "truload")
    /// </summary>
    public required string Tenant { get; set; }

    /// <summary>
    /// Template identifier (e.g., "password_reset", "shift_assignment", "invoice_due")
    /// </summary>
    public required string Template { get; set; }

    /// <summary>
    /// Template data for variable substitution
    /// </summary>
    public required Dictionary<string, object> Data { get; set; }

    /// <summary>
    /// Recipient addresses (email addresses for email, phone numbers for SMS, user IDs for push)
    /// </summary>
    public required List<string> To { get; set; }

    /// <summary>
    /// Additional metadata (e.g., subject for email, provider preference)
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Response DTO from Go notifications-service after enqueuing message.
/// </summary>
public class NotificationEnqueueResponse
{
    /// <summary>
    /// Status: "queued", "duplicate"
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Request ID for tracking
    /// </summary>
    public string RequestId { get; set; } = string.Empty;
}

/// <summary>
/// Error response from Go notifications-service.
/// </summary>
public class NotificationErrorResponse
{
    /// <summary>
    /// Error message
    /// </summary>
    public string Error { get; set; } = string.Empty;
}
