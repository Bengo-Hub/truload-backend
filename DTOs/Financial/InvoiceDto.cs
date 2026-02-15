using TruLoad.Backend.DTOs.Shared;

namespace TruLoad.Backend.DTOs.Financial;

/// <summary>
/// Invoice Data Transfer Object
/// </summary>
public class InvoiceDto
{
    public Guid Id { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public Guid? CaseRegisterId { get; set; }
    public string? CaseNo { get; set; }
    public Guid? ProsecutionCaseId { get; set; }
    public string? ProsecutionCertificateNo { get; set; }
    public Guid? WeighingId { get; set; }
    public string? WeighingTicketNo { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceRemaining { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "pending";
    public DateTime GeneratedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public string? PesaflowInvoiceNumber { get; set; }
    public string? PesaflowPaymentReference { get; set; }
    public string? PesaflowPaymentLink { get; set; }
    public decimal? PesaflowGatewayFee { get; set; }
    public decimal? PesaflowAmountNet { get; set; }
    public decimal? PesaflowTotalAmount { get; set; }
    public string? PesaflowSyncStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Create invoice request
/// </summary>
public class CreateInvoiceRequest
{
    public Guid? CaseRegisterId { get; set; }
    public Guid? WeighingId { get; set; }
    public decimal AmountDue { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime? DueDate { get; set; }
}

/// <summary>
/// Update invoice status request
/// </summary>
public class UpdateInvoiceStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

/// <summary>
/// Invoice search criteria
/// </summary>
public class InvoiceSearchCriteria : PagedRequest
{
    public string? InvoiceNo { get; set; }
    public string? CaseNo { get; set; }
    public string? VehicleRegNumber { get; set; }
    public Guid? CaseRegisterId { get; set; }
    public Guid? ProsecutionCaseId { get; set; }
    public Guid? StationId { get; set; }
    public string? Status { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public DateTime? GeneratedFrom { get; set; }
    public DateTime? GeneratedTo { get; set; }
    public DateTime? DueFrom { get; set; }
    public DateTime? DueTo { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }

    /// <summary>Effective start date (prefers DateFrom over GeneratedFrom)</summary>
    public DateTime? EffectiveFromDate => DateFrom ?? GeneratedFrom;
    /// <summary>Effective end date (prefers DateTo over GeneratedTo)</summary>
    public DateTime? EffectiveToDate => DateTo ?? GeneratedTo;
}

/// <summary>
/// Invoice statistics response DTO matching frontend InvoiceStatistics type
/// </summary>
public class InvoiceStatisticsDto
{
    public int TotalInvoices { get; set; }
    public int PendingInvoices { get; set; }
    public int PaidInvoices { get; set; }
    public int OverdueInvoices { get; set; }
    public decimal TotalAmountDue { get; set; }
    public decimal TotalAmountPaid { get; set; }
    public decimal TotalBalance { get; set; }

    // Per-currency breakdown (prevents mixing KES + USD)
    public decimal TotalAmountDueKes { get; set; }
    public decimal TotalAmountDueUsd { get; set; }
    public decimal TotalAmountPaidKes { get; set; }
    public decimal TotalAmountPaidUsd { get; set; }
    public decimal TotalBalanceKes { get; set; }
    public decimal TotalBalanceUsd { get; set; }
}

/// <summary>
/// Invoice aging bucket for dashboard chart
/// </summary>
public class InvoiceAgingBucketDto
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public decimal Amount { get; set; }
}
