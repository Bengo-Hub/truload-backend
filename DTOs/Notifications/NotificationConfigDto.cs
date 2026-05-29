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
    /// <summary>Additional CC addresses for this specific workflow.</summary>
    public List<string> CcRecipients { get; set; } = new();
}

/// <summary>Pool/group-level default recipients shared across all workflows in the group.</summary>
public class WorkflowGroupPreferences
{
    /// <summary>Email addresses that always receive every notification in this workflow group.</summary>
    public List<string> DefaultRecipients { get; set; } = new();
}

public class WorkflowPreferencesDto
{
    // ── Per-workflow toggles ───────────────────────────────────────────────────
    public WorkflowPreferenceItem OverloadAlert { get; set; } = new() { EmailEnabled = true };
    public WorkflowPreferenceItem CaseCreated { get; set; } = new() { EmailEnabled = true };
    public WorkflowPreferenceItem CaseEscalated { get; set; } = new() { EmailEnabled = true };
    public WorkflowPreferenceItem InvoiceIssued { get; set; } = new() { EmailEnabled = true };
    public WorkflowPreferenceItem InvoiceOverdue { get; set; } = new() { EmailEnabled = true };
    public WorkflowPreferenceItem WeighingCompleted { get; set; } = new() { EmailEnabled = false };
    public WorkflowPreferenceItem ScheduledReport { get; set; } = new() { EmailEnabled = true };
    public WorkflowPreferenceItem UserRegistered { get; set; } = new() { EmailEnabled = false };
    public WorkflowPreferenceItem PasswordChanged { get; set; } = new() { EmailEnabled = false };
    // Commercial weighing events
    public WorkflowPreferenceItem WeighingTicketReady { get; set; } = new() { EmailEnabled = true };
    public WorkflowPreferenceItem ToleranceExceptionRaised { get; set; } = new() { EmailEnabled = true, PushEnabled = true };
    public WorkflowPreferenceItem StaleWeighingAlert { get; set; } = new() { EmailEnabled = true };
    public WorkflowPreferenceItem QualityDeductionApplied { get; set; } = new() { EmailEnabled = true };

    // ── Group-level default recipients ────────────────────────────────────────
    /// <summary>Weighing: overloadAlert, weighingCompleted, toleranceExceptionRaised, staleWeighingAlert, qualityDeductionApplied</summary>
    public WorkflowGroupPreferences WeighingGroup { get; set; } = new();
    /// <summary>Cases: caseCreated, caseEscalated</summary>
    public WorkflowGroupPreferences CasesGroup { get; set; } = new();
    /// <summary>Invoices: invoiceIssued, invoiceOverdue</summary>
    public WorkflowGroupPreferences InvoicesGroup { get; set; } = new();
    /// <summary>Receipts: weighingTicketReady</summary>
    public WorkflowGroupPreferences ReceiptsGroup { get; set; } = new();
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
