namespace TruLoad.Backend.DTOs.CaseManagement;

/// <summary>
/// Load Correction Memo Data Transfer Object
/// </summary>
public class LoadCorrectionMemoDto
{
    public Guid Id { get; set; }
    public string MemoNo { get; set; } = string.Empty;
    public Guid CaseRegisterId { get; set; }
    public string? CaseNo { get; set; }
    public Guid WeighingId { get; set; }
    public string? WeighingTicketNo { get; set; }
    public int OverloadKg { get; set; }
    public string RedistributionType { get; set; } = string.Empty;
    public DateTime? ReweighScheduledAt { get; set; }
    public Guid? ReweighWeighingId { get; set; }
    public bool ComplianceAchieved { get; set; }
    public string? ReliefTruckRegNumber { get; set; }
    public int? ReliefTruckEmptyWeightKg { get; set; }
    public Guid IssuedById { get; set; }
    public string? IssuedByName { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
