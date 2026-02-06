using TruLoad.Backend.DTOs.Auth;

namespace TruLoad.Backend.Services.Interfaces.Auth;

/// <summary>
/// Service for managing two-factor authentication (2FA) operations.
/// Supports TOTP (Time-based One-Time Password) via authenticator apps.
/// </summary>
public interface ITwoFactorService
{
    /// <summary>
    /// Gets the 2FA status for a user.
    /// </summary>
    Task<TwoFactorStatusResponse> GetStatusAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Generates a new TOTP setup for a user.
    /// Returns the shared key, authenticator URI, and QR code for enrollment.
    /// </summary>
    Task<TwoFactorSetupResponse> GenerateSetupAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Verifies the TOTP code and enables 2FA for the user.
    /// Returns recovery codes upon successful setup.
    /// </summary>
    Task<Enable2FAResponse> EnableAsync(Guid userId, string verificationCode, CancellationToken ct = default);

    /// <summary>
    /// Disables 2FA for a user after password verification.
    /// </summary>
    Task<bool> DisableAsync(Guid userId, string password, CancellationToken ct = default);

    /// <summary>
    /// Verifies a 2FA code (TOTP or recovery code) for login.
    /// </summary>
    Task<bool> VerifyCodeAsync(Guid userId, string code, bool useRecoveryCode, CancellationToken ct = default);

    /// <summary>
    /// Generates new recovery codes, invalidating any existing ones.
    /// </summary>
    Task<RecoveryCodesResponse> RegenerateRecoveryCodesAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Resets the authenticator key, requiring the user to re-enroll.
    /// </summary>
    Task<bool> ResetAuthenticatorAsync(Guid userId, CancellationToken ct = default);
}
