using System.ComponentModel.DataAnnotations;

namespace TruLoad.Backend.DTOs.Auth;

/// <summary>
/// Response containing TOTP setup information for 2FA enrollment.
/// </summary>
public record TwoFactorSetupResponse(
    string SharedKey,
    string AuthenticatorUri,
    string QrCodeDataUrl
);

/// <summary>
/// Request to verify and enable 2FA with a TOTP code.
/// </summary>
public record Enable2FARequest
{
    /// <summary>
    /// 6-digit verification code from authenticator app.
    /// </summary>
    [Required]
    [StringLength(7, MinimumLength = 6, ErrorMessage = "Verification code must be 6-7 digits")]
    [RegularExpression(@"^\d{6,7}$", ErrorMessage = "Verification code must contain only digits")]
    public string VerificationCode { get; init; } = string.Empty;
}

/// <summary>
/// Response after successfully enabling 2FA.
/// </summary>
public record Enable2FAResponse(
    bool Success,
    string[] RecoveryCodes
);

/// <summary>
/// Request to verify 2FA during login.
/// </summary>
public record Verify2FARequest
{
    /// <summary>
    /// 6-digit code from authenticator or recovery code.
    /// </summary>
    [Required]
    [StringLength(32, MinimumLength = 6, ErrorMessage = "Code must be 6-32 characters")]
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Whether this is a recovery code instead of TOTP.
    /// </summary>
    public bool UseRecoveryCode { get; init; }

    /// <summary>
    /// Remember this device for 30 days.
    /// </summary>
    public bool RememberDevice { get; init; }
}

/// <summary>
/// Request to disable 2FA.
/// </summary>
public record Disable2FARequest
{
    /// <summary>
    /// Current password for security verification.
    /// </summary>
    [Required]
    public string Password { get; init; } = string.Empty;
}

/// <summary>
/// Response containing new recovery codes.
/// </summary>
public record RecoveryCodesResponse(
    string[] RecoveryCodes,
    int RemainingCodes
);

/// <summary>
/// 2FA status information for a user.
/// </summary>
public record TwoFactorStatusResponse(
    bool IsEnabled,
    bool HasAuthenticator,
    int RecoveryCodesRemaining,
    bool IsMachineRemembered
);

/// <summary>
/// Response when login requires 2FA verification.
/// </summary>
public record TwoFactorChallengeResponse
{
    /// <summary>Indicates the client must complete 2FA.</summary>
    public bool Requires2FA { get; init; } = true;

    /// <summary>Short-lived JWT (5 min) for completing 2FA verification.</summary>
    public string TwoFactorToken { get; init; } = string.Empty;
}

/// <summary>
/// Request to verify 2FA during login flow (unauthenticated).
/// </summary>
public record LoginVerify2FARequest
{
    /// <summary>The 2FA challenge token from login response.</summary>
    [Required]
    public string TwoFactorToken { get; init; } = string.Empty;

    /// <summary>6-digit TOTP code or recovery code.</summary>
    [Required]
    [StringLength(32, MinimumLength = 6)]
    public string Code { get; init; } = string.Empty;

    /// <summary>Whether using a recovery code instead of TOTP.</summary>
    public bool UseRecoveryCode { get; init; }
}
