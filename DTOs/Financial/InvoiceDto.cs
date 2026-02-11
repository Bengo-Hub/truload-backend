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
    public Guid? CaseRegisterId { get; set; }
    public Guid? ProsecutionCaseId { get; set; }
    public string? Status { get; set; }
    public DateTime? GeneratedFrom { get; set; }
    public DateTime? GeneratedTo { get; set; }
    public DateTime? DueFrom { get; set; }
    public DateTime? DueTo { get; set; }
}
