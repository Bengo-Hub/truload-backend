using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Models;

/// <summary>
/// Cargo type taxonomy for weighing operations
/// </summary>
public class CargoTypes : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "General"; // General, Hazardous, Perishable
}