namespace TruLoad.Backend.DTOs.CaseManagement;

/// <summary>
/// Compliance Certificate Data Transfer Object
/// </summary>
public class ComplianceCertificateDto
{
    public Guid Id { get; set; }
    public string CertificateNo { get; set; } = string.Empty;
    public Guid CaseRegisterId { get; set; }
    public string? CaseNo { get; set; }
    public Guid WeighingId { get; set; }
    public string? WeighingTicketNo { get; set; }
    public Guid? LoadCorrectionMemoId { get; set; }
    public string? MemoNo { get; set; }
    public Guid IssuedById { get; set; }
    public string? IssuedByName { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
