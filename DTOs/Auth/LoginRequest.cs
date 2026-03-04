using System.ComponentModel.DataAnnotations;

namespace TruLoad.Backend.DTOs.Auth;

/// <summary>
/// Request model for user login.
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// User email address.
    /// </summary>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string Email { get; set; } = "gadmin@masterspace.co.ke";

    /// <summary>
    /// User password.
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = "ChangeMe123!";

    /// <summary>
    /// Optional organisation code (e.g. from tenant login URL) for validation and branding.
    /// When provided, user must belong to this organisation.
    /// </summary>
    public string? OrganizationCode { get; set; }

    /// <summary>
    /// Optional station code (e.g. from pre-login station selection). When provided with OrganisationCode,
    /// user must be allowed to log in to this station (same station as assigned, or HQ user).
    /// </summary>
    public string? StationCode { get; set; }
}
