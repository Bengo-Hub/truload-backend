using System.ComponentModel.DataAnnotations;

namespace TruLoad.Backend.DTOs.Auth;

public class RegisterRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public string FullName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public Guid? OrganizationId { get; set; }

    public Guid? StationId { get; set; }

    public Guid? DepartmentId { get; set; }
}
