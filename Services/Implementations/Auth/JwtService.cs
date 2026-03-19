using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Services.Interfaces.Auth;

namespace TruLoad.Backend.Services.Implementations.Auth;

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;
    private readonly TruLoadDbContext _context;
    private readonly ILogger<JwtService> _logger;

    private const int RefreshTokenExpiryDays = 7;

    public JwtService(IConfiguration configuration, TruLoadDbContext context, ILogger<JwtService> logger)
    {
        _configuration = configuration;
        _context = context;
        _logger = logger;
    }

    public string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles, IEnumerable<string> permissions, bool isHqUser = false)
    {
        var secretKey = _configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey not configured");
        var issuer = _configuration["Jwt:Issuer"] ?? "https://truload-backend";
        var audience = _configuration["Jwt:Audience"] ?? "truload-frontend";
        var expirationMinutes = int.TryParse(_configuration["Jwt:ExpirationMinutes"], out var minutes) ? minutes : 60;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.FullName),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Add roles as claims
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // Add permissions as claims
        foreach (var permission in permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        // Add organization/station/department (mandatory fallback to KURA for organization)
        var orgId = user.OrganizationId;
        if (!orgId.HasValue)
        {
            // Fallback to KURA organization if not explicitly assigned
            var kura = _context.Organizations.AsNoTracking().FirstOrDefault(o => o.Code == "KURA");
            if (kura != null) orgId = kura.Id;
        }

        if (orgId.HasValue)
            claims.Add(new Claim("organization_id", orgId.Value.ToString()));

        if (user.StationId.HasValue)
            claims.Add(new Claim("station_id", user.StationId.Value.ToString()));

        if (isHqUser)
            claims.Add(new Claim("is_hq_user", "true"));

        if (user.DepartmentId.HasValue)
            claims.Add(new Claim("department_id", user.DepartmentId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public async Task<string> StoreRefreshTokenAsync(Guid userId, CancellationToken ct = default)
    {
        var rawToken = GenerateRefreshToken();
        var tokenHash = HashToken(rawToken);

        var refreshToken = new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpiryDays),
            CreatedAt = DateTime.UtcNow
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync(ct);

        _logger.LogDebug("Stored refresh token for user {UserId}", userId);
        return rawToken;
    }

    public async Task<(bool isValid, string? newRefreshToken, Guid userId)> ValidateAndRotateRefreshTokenAsync(
        string refreshToken, CancellationToken ct = default)
    {
        var tokenHash = HashToken(refreshToken);

        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (storedToken == null)
        {
            _logger.LogWarning("Refresh token not found in database");
            return (false, null, Guid.Empty);
        }

        if (!storedToken.IsActive)
        {
            // If someone tries to reuse a revoked token, revoke ALL tokens for that user
            // (possible token theft)
            if (storedToken.IsRevoked)
            {
                _logger.LogWarning("Revoked refresh token reuse detected for user {UserId}, revoking all tokens", storedToken.UserId);
                await RevokeAllUserTokensAsync(storedToken.UserId, ct);
            }
            return (false, null, Guid.Empty);
        }

        // Rotate: revoke old token and issue new one
        storedToken.RevokedAt = DateTime.UtcNow;

        var newRawToken = GenerateRefreshToken();
        var newTokenHash = HashToken(newRawToken);

        var newToken = new RefreshToken
        {
            UserId = storedToken.UserId,
            TokenHash = newTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpiryDays),
            CreatedAt = DateTime.UtcNow
        };

        storedToken.ReplacedByTokenId = newToken.Id;
        _context.RefreshTokens.Add(newToken);
        await _context.SaveChangesAsync(ct);

        _logger.LogDebug("Rotated refresh token for user {UserId}", storedToken.UserId);
        return (true, newRawToken, storedToken.UserId);
    }

    public async Task RevokeAllUserTokensAsync(Guid userId, CancellationToken ct = default)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Revoked {Count} refresh tokens for user {UserId}", activeTokens.Count, userId);
    }

    public string GenerateTwoFactorChallengeToken(Guid userId)
    {
        var secretKey = _configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey not configured");
        var issuer = _configuration["Jwt:Issuer"] ?? "https://truload-backend";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("purpose", "2fa-challenge")
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: "truload-2fa",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public Guid? ValidateTwoFactorChallengeToken(string token)
    {
        try
        {
            var secretKey = _configuration["Jwt:SecretKey"]
                ?? throw new InvalidOperationException("JWT SecretKey not configured");
            var issuer = _configuration["Jwt:Issuer"] ?? "https://truload-backend";

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = "truload-2fa",
                ValidateLifetime = true,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, validationParams, out var validatedToken);

            // Verify purpose claim
            var purposeClaim = principal.FindFirst("purpose");
            if (purposeClaim?.Value != "2fa-challenge")
                return null;

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
                return userId;

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid 2FA challenge token");
            return null;
        }
    }

    public string GenerateChangeExpiredPasswordToken(Guid userId)
    {
        var secretKey = _configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey not configured");
        var issuer = _configuration["Jwt:Issuer"] ?? "https://truload-backend";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("purpose", "change_expired_password")
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: "truload-auth",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public Guid? ValidateChangeExpiredPasswordToken(string token)
    {
        try
        {
            var secretKey = _configuration["Jwt:SecretKey"]
                ?? throw new InvalidOperationException("JWT SecretKey not configured");
            var issuer = _configuration["Jwt:Issuer"] ?? "https://truload-backend";

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = "truload-auth",
                ValidateLifetime = true,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, validationParams, out _);

            var purposeClaim = principal.FindFirst("purpose");
            if (purposeClaim?.Value != "change_expired_password")
                return null;

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
                return userId;

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid change-expired-password token");
            return null;
        }
    }

    public Guid? GetUserIdFromToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);

            var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return userId;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting user ID from token");
            return null;
        }
    }

    public string GenerateSsoExchangeToken(Guid userId, Guid orgId)
    {
        var secretKey = _configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey not configured");
        var issuer = _configuration["Jwt:Issuer"] ?? "https://truload-backend";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("org_id", orgId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("purpose", "sso-exchange")
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: "truload-sso-exchange",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (Guid userId, Guid orgId)? ValidateSsoExchangeToken(string token)
    {
        try
        {
            var secretKey = _configuration["Jwt:SecretKey"]
                ?? throw new InvalidOperationException("JWT SecretKey not configured");
            var issuer = _configuration["Jwt:Issuer"] ?? "https://truload-backend";

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = "truload-sso-exchange",
                ValidateLifetime = true,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, validationParams, out _);

            var purposeClaim = principal.FindFirst("purpose");
            if (purposeClaim?.Value != "sso-exchange")
                return null;

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
            var orgIdClaim = principal.FindFirst("org_id");

            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId)
                && orgIdClaim != null && Guid.TryParse(orgIdClaim.Value, out var orgId))
                return (userId, orgId);

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid SSO exchange token");
            return null;
        }
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
