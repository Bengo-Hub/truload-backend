using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models;

/// <summary>
/// Legal act definitions (EAC Vehicle Load Control Act, Kenya Traffic Act).
/// Single source used across modules to avoid duplication.
/// </summary>
public class ActDefinition : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ActType { get; set; } = string.Empty; // EAC, Traffic
    public string? FullName { get; set; }
    public string? Description { get; set; }
    public DateOnly? EffectiveDate { get; set; }

    /// <summary>
    /// Default charging currency for fines under this act.
    /// Traffic Act charges in KES, EAC Act charges in USD.
    /// </summary>
    public string ChargingCurrency { get; set; } = "KES";

    // ===== Navigation Properties =====
    public ICollection<CaseRegister> CaseRegisters { get; set; } = [];
}
