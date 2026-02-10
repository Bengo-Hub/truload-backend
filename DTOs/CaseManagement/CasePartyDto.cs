namespace TruLoad.Backend.DTOs.CaseManagement;

/// <summary>
/// Case Party Data Transfer Object
/// </summary>
public class CasePartyDto
{
    public Guid Id { get; set; }
    public Guid CaseRegisterId { get; set; }
    public string PartyRole { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public Guid? DriverId { get; set; }
    public string? DriverName { get; set; }
    public Guid? VehicleOwnerId { get; set; }
    public string? VehicleOwnerName { get; set; }
    public Guid? TransporterId { get; set; }
    public string? TransporterName { get; set; }
    public string? ExternalName { get; set; }
    public string? ExternalIdNumber { get; set; }
    public string? ExternalPhone { get; set; }
    public string? Notes { get; set; }
    public bool IsCurrentlyActive { get; set; }
    public DateTime AddedAt { get; set; }
    public DateTime? RemovedAt { get; set; }
}

/// <summary>
/// Add Case Party Request
/// </summary>
public class AddCasePartyRequest
{
    public string PartyRole { get; set; } = "defendant_driver";
    public Guid? UserId { get; set; }
    public Guid? DriverId { get; set; }
    public Guid? VehicleOwnerId { get; set; }
    public Guid? TransporterId { get; set; }
    public string? ExternalName { get; set; }
    public string? ExternalIdNumber { get; set; }
    public string? ExternalPhone { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Update Case Party Request
/// </summary>
public class UpdateCasePartyRequest
{
    public string? Notes { get; set; }
    public bool? IsCurrentlyActive { get; set; }
}
