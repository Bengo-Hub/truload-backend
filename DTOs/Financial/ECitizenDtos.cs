namespace TruLoad.Backend.DTOs.Financial;

// ===== Pesaflow API Request/Response DTOs =====

/// <summary>
/// Request to create a Pesaflow invoice via the Create Invoice API.
/// </summary>
public class CreatePesaflowInvoiceRequest
{
    public Guid LocalInvoiceId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? ClientEmail { get; set; }
    public string? ClientMsisdn { get; set; }
    public string? ClientIdNumber { get; set; }
}

/// <summary>
/// Request to initiate an Online Checkout (iframe) session.
/// </summary>
public class InitiateCheckoutRequest
{
    public Guid LocalInvoiceId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? ClientEmail { get; set; }
    public string? ClientMsisdn { get; set; }
    public string? ClientIdNumber { get; set; }
    public bool SendStk { get; set; }
    public string? PictureUrl { get; set; }
}

/// <summary>
/// Response from Pesaflow Create Invoice API.
/// </summary>
public class PesaflowInvoiceResponse
{
    public bool Success { get; set; }
    public string? PesaflowInvoiceNumber { get; set; }
    public string? Message { get; set; }
    public string? CheckoutUrl { get; set; }
}

/// <summary>
/// Response from Pesaflow Online Checkout API.
/// </summary>
public class PesaflowCheckoutResponse
{
    public bool Success { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? IframeHtml { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Response from Pesaflow Query Payment Status API.
/// </summary>
public class PesaflowPaymentStatusResponse
{
    public string? Status { get; set; }
    public decimal AmountPaid { get; set; }
    public string? PaymentReference { get; set; }
    public string? PaymentChannel { get; set; }
    public DateTime? PaymentDate { get; set; }
}

/// <summary>
/// Pesaflow IPN (Instant Payment Notification) webhook payload.
/// Field names match the Pesaflow API specification.
/// </summary>
public class PesaflowIpnPayload
{
    public string? payment_channel { get; set; }
    public string? client_invoice_ref { get; set; }
    public string? payment_reference { get; set; }
    public string? currency { get; set; }
    public decimal amount_paid { get; set; }
    public decimal invoice_amount { get; set; }
    public string? status { get; set; }
    public string? invoice_number { get; set; }
    public string? payment_date { get; set; }
    public string? token_hash { get; set; }
    public decimal last_payment_amount { get; set; }
}

// ===== Integration Config DTOs =====

/// <summary>
/// DTO for IntegrationConfig (excludes encrypted credentials).
/// </summary>
public class IntegrationConfigDto
{
    public Guid Id { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string EndpointsJson { get; set; } = "{}";
    public string? WebhookUrl { get; set; }
    public string? CallbackUrl { get; set; }
    public string? AppBaseUrl { get; set; }
    public string? Environment { get; set; }
    public string? Description { get; set; }
    public DateTime? CredentialsRotatedAt { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Request to create or update an integration configuration.
/// </summary>
public class UpsertIntegrationConfigRequest
{
    public string ProviderName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public Dictionary<string, string> Credentials { get; set; } = new();
    public string EndpointsJson { get; set; } = "{}";
    public string? AppBaseUrl { get; set; }
    public string? Environment { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Result of webhook notification processing.
/// </summary>
public enum WebhookProcessingResult
{
    Success,
    AlreadyProcessed,
    InvalidSignature,
    InvoiceNotFound,
    AmountMismatch,
    PaymentFailed,
    Error
}
