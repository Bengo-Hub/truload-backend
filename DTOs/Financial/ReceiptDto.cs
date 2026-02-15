using TruLoad.Backend.DTOs.Shared;

namespace TruLoad.Backend.DTOs.Financial;

/// <summary>
/// Receipt Data Transfer Object
/// </summary>
public class ReceiptDto
{
    public Guid Id { get; set; }
    public string ReceiptNo { get; set; } = string.Empty;
    public Guid InvoiceId { get; set; }
    public string? InvoiceNo { get; set; }
    public decimal AmountPaid { get; set; }
    public string Currency { get; set; } = "USD";
    public string PaymentMethod { get; set; } = "cash";
    public string? TransactionReference { get; set; }
    public Guid IdempotencyKey { get; set; }
    public Guid? ReceivedById { get; set; }
    public string? ReceivedByName { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? PaymentChannel { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Record payment request
/// </summary>
public class RecordPaymentRequest
{
    /// <summary>
    /// Amount being paid
    /// </summary>
    public decimal AmountPaid { get; set; }

    /// <summary>
    /// Currency (must match invoice currency)
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Payment method: cash, mobile_money, bank_transfer, card
    /// </summary>
    public string PaymentMethod { get; set; } = "cash";

    /// <summary>
    /// External transaction reference (e.g., M-Pesa code)
    /// </summary>
    public string? TransactionReference { get; set; }

    /// <summary>
    /// Client-generated idempotency key for duplicate prevention
    /// </summary>
    public Guid IdempotencyKey { get; set; }
}

/// <summary>
/// Void receipt request
/// </summary>
public class VoidReceiptRequest
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Receipt search criteria
/// </summary>
public class ReceiptSearchCriteria : PagedRequest
{
    public string? ReceiptNo { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid? StationId { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime? PaymentDateFrom { get; set; }
    public DateTime? PaymentDateTo { get; set; }
    public Guid? ReceivedById { get; set; }
}

/// <summary>
/// Receipt statistics response DTO matching frontend ReceiptStatistics type
/// </summary>
public class ReceiptStatisticsDto
{
    public int Total { get; set; }
    public int TodayCount { get; set; }
    public decimal TodayAmount { get; set; }
    public decimal TotalCollected { get; set; }
    public List<PaymentMethodBreakdown> ByPaymentMethod { get; set; } = new();

    // Per-currency breakdown (prevents mixing KES + USD)
    public decimal TodayAmountKes { get; set; }
    public decimal TodayAmountUsd { get; set; }
    public decimal TotalCollectedKes { get; set; }
    public decimal TotalCollectedUsd { get; set; }
}

public class PaymentMethodBreakdown
{
    public string Method { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Amount { get; set; }
}
