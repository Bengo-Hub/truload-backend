namespace TruLoad.Backend.DTOs.Notifications;

public class NotificationProviderDto
{
    public string ProviderType { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class ProviderSettingsDto
{
    public string ProviderType { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public Dictionary<string, string> Settings { get; set; } = new();
}

public class SaveProviderSettingsRequest
{
    public required string ProviderType { get; set; }
    public required string ProviderName { get; set; }
    public Dictionary<string, string> Settings { get; set; } = new();
}

public class SelectProviderRequest
{
    public required string ProviderType { get; set; }
    public required string ProviderName { get; set; }
    public string Environment { get; set; } = "production";
}

public class WorkflowPreferenceItem
{
    public bool EmailEnabled { get; set; }
    public bool PushEnabled { get; set; }
    public bool SmsEnabled { get; set; }
}

public class WorkflowPreferencesDto
{
    public WorkflowPreferenceItem OverloadAlert { get; set; } = new() { EmailEnabled = true };
    public WorkflowPreferenceItem CaseCreated { get; set; } = new() { EmailEnabled = true };
    public WorkflowPreferenceItem CaseEscalated { get; set; } = new() { EmailEnabled = true };
    public WorkflowPreferenceItem InvoiceIssued { get; set; } = new() { EmailEnabled = true };
    public WorkflowPreferenceItem InvoiceOverdue { get; set; } = new() { EmailEnabled = true };
    public WorkflowPreferenceItem WeighingCompleted { get; set; } = new() { EmailEnabled = false };
    public WorkflowPreferenceItem ScheduledReport { get; set; } = new() { EmailEnabled = true };
    public WorkflowPreferenceItem UserRegistered { get; set; } = new() { EmailEnabled = false };
    public WorkflowPreferenceItem PasswordChanged { get; set; } = new() { EmailEnabled = false };
}

public class RegisterDeviceTokenRequest
{
    public required string Token { get; set; }
    public string Platform { get; set; } = "web";
    public string Provider { get; set; } = "fcm";
}

public class DeviceTokenItemDto
{
    public Guid Id { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
