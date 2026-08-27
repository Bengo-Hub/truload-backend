using TruLoad.Backend.Models.Identity;

namespace TruLoad.Backend.Services.Interfaces.Auth;

public interface IJwtService
{
    /// <summary>
    /// Generate JWT access token for authenticated user. isPlatformOwner stamps an
    /// is_platform_owner claim (parity with the Go services' auth-api-derived claim of the same
    /// name) — purely informational/auditable; the actual cross-tenant bypass comes from the
    /// Superuser role already present in `roles`.
    /// </summary>
    string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles, IEnumerable<string> permissions, bool isHqUser = false, bool isPlatformOwner = false);

    /// <summary>
    /// Generate a cryptographically random refresh token string
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Store refresh token hash in database, linked to user
    /// </summary>
    Task<string> StoreRefreshTokenAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Validate refresh token against database and rotate (revoke old, issue new)
    /// </summary>
    Task<(bool isValid, string? newRefreshToken, Guid userId)> ValidateAndRotateRefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Revoke all refresh tokens for a user (e.g., on logout)
    /// </summary>
    Task RevokeAllUserTokensAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Generate a short-lived JWT for 2FA challenge (5 min expiry, purpose=2fa-challenge)
    /// </summary>
    string GenerateTwoFactorChallengeToken(Guid userId);

    /// <summary>
    /// Validate a 2FA challenge token and extract user ID
    /// </summary>
    Guid? ValidateTwoFactorChallengeToken(string token);

    /// <summary>
    /// Generate a short-lived JWT for change-expired-password flow (15 min expiry, purpose=change_expired_password)
    /// </summary>
    string GenerateChangeExpiredPasswordToken(Guid userId);

    /// <summary>
    /// Validate change-expired-password token and extract user ID
    /// </summary>
    Guid? ValidateChangeExpiredPasswordToken(string token);

    /// <summary>
    /// Get user ID from access token (without full validation)
    /// </summary>
    Guid? GetUserIdFromToken(string token);

    /// <summary>
    /// Generate a short-lived (5 min) SSO exchange token used only for the /auth/select-station
    /// call after a successful SSO token exchange. Embeds userId, orgId (the RESOLVED target org
    /// — for a platform owner this may differ from the user's own OrganizationId), and whether
    /// this login carries the platform-owner claim.
    /// </summary>
    string GenerateSsoExchangeToken(Guid userId, Guid orgId, bool isPlatformOwner = false);

    /// <summary>
    /// Validate an SSO exchange token and extract userId + orgId + isPlatformOwner.
    /// Returns null if token is invalid, expired, or wrong purpose.
    /// </summary>
    (Guid userId, Guid orgId, bool isPlatformOwner)? ValidateSsoExchangeToken(string token);
}
