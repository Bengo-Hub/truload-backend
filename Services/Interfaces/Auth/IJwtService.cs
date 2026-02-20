using TruLoad.Backend.Models.Identity;

namespace TruLoad.Backend.Services.Interfaces.Auth;

public interface IJwtService
{
    /// <summary>
    /// Generate JWT access token for authenticated user
    /// </summary>
    string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles, IEnumerable<string> permissions);

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
    /// Get user ID from access token (without full validation)
    /// </summary>
    Guid? GetUserIdFromToken(string token);
}
