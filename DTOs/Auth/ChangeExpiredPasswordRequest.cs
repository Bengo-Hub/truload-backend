using System.ComponentModel.DataAnnotations;

namespace TruLoad.Backend.DTOs.Auth;

/// <summary>
/// Request to change an expired password (public endpoint; token from login response).
/// </summary>
public class ChangeExpiredPasswordRequest
{
    [Required]
    public string ChangePasswordToken { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;
}
