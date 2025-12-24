using TruLoad.Backend.Models.Identity;

namespace TruLoad.Backend.Services.Interfaces.Auth;

public interface IJwtService
{
    /// <summary>
    /// Generate JWT access token for authenticated user
    /// </summary>
    string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles, IEnumerable<string> permissions);

    /// <summary>
    /// Generate refresh token
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Validate refresh token
    /// </summary>
    bool ValidateRefreshToken(string refreshToken);

    /// <summary>
    /// Get user ID from access token
    /// </summary>
    Guid? GetUserIdFromToken(string token);
}
