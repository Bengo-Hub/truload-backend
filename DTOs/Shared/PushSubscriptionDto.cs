namespace TruLoad.Backend.DTOs.Shared;

/// <summary>
/// DTO for push notification subscription from the frontend.
/// Matches the web push standard PushSubscription object.
/// </summary>
public class PushSubscriptionDto
{
    public string Endpoint { get; set; } = string.Empty;
    public PushSubscriptionKeysDto Keys { get; set; } = new();
    public string? DeviceName { get; set; }
}

public class PushSubscriptionKeysDto
{
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
}
