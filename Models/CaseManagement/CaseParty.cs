using TruLoad.Backend.Models.Common;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Models.CaseManagement;

/// <summary>
/// Case Party - tracks all parties involved in a case.
///
/// This is a flexible entity that can link to:
/// - ApplicationUser: For police officers, IOs, or other system users who need login
/// - Driver: For driver defendants, does not need login
/// - VehicleOwner: For vehicle owner defendants, does not need login
/// - Transporter: For transporter company defendants, does not need login
///
/// Design Principle: DO NOT repeat fields that exist in linked entities.
/// Use FK relationships and navigation properties to access data.
/// </summary>
public class CaseParty : TenantAwareEntity
{
    /// <summary>
    /// Foreign key to case register
    /// </summary>
    public Guid CaseRegisterId { get; set; }

    /// <summary>
    /// Party role in the case
    /// - investigating_officer: IO assigned to case
    /// - ocs: Officer Commanding Station who approved/supervised
    /// - arresting_officer: Officer who made the arrest
    /// - prosecutor: Legal prosecutor
    /// - defendant_driver: Driver charged with violation
    /// - defendant_owner: Vehicle owner charged
    /// - defendant_transporter: Transporter company charged
    /// - witness: Witness to the violation
    /// - complainant: Person who filed complaint
    /// </summary>
    public string PartyRole { get; set; } = "defendant_driver";

    /// <summary>
    /// Link to ApplicationUser (for police/prosecutors/IOs who need system login).
    /// Mutually exclusive with DriverId, VehicleOwnerId, TransporterId.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Link to Driver (for driver defendants).
    /// Mutually exclusive with UserId.
    /// </summary>
    public Guid? DriverId { get; set; }

    /// <summary>
    /// Link to VehicleOwner (for owner defendants).
    /// Mutually exclusive with UserId.
    /// </summary>
    public Guid? VehicleOwnerId { get; set; }

    /// <summary>
    /// Link to Transporter (for company defendants).
    /// Mutually exclusive with UserId.
    /// </summary>
    public Guid? TransporterId { get; set; }

    /// <summary>
    /// For external parties not in the system (witnesses, etc.)
    /// Only used when no FK link exists.
    /// </summary>
    public string? ExternalName { get; set; }

    /// <summary>
    /// External party ID document (for non-system parties)
    /// </summary>
    public string? ExternalIdNumber { get; set; }

    /// <summary>
    /// External party contact (for non-system parties)
    /// </summary>
    public string? ExternalPhone { get; set; }

    /// <summary>
    /// Notes about this party's involvement
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Whether this party is currently active in the case
    /// </summary>
    public bool IsCurrentlyActive { get; set; } = true;

    /// <summary>
    /// When this party was added to the case
    /// </summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this party was removed from the case (if applicable)
    /// </summary>
    public DateTime? RemovedAt { get; set; }

    // Navigation properties - DO NOT add redundant fields
    // Use these to access party details (name, rank, contact, etc.)
    public CaseRegister? CaseRegister { get; set; }
    public ApplicationUser? User { get; set; }
    public Driver? Driver { get; set; }
    public VehicleOwner? VehicleOwner { get; set; }
    public Transporter? Transporter { get; set; }
}
