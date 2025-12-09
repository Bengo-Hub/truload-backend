using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using TruLoad.Backend.DTOs.Auth;
using TruLoad.Backend.Services.Interfaces.Auth;

namespace TruLoad.Backend.Services.Implementations.Auth;

/// <summary>
/// Implementation of SSO authentication service.
/// Proxies login requests to external SSO service and generates local JWT tokens.
/// </summary>
public class SsoAuthService : ISsoAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ISsoUserSyncService _userSyncService;
    private readonly ILogger<SsoAuthService> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    private const string SsoBaseUrl = "https://sso.codevertexitsolutions.com";
    private const string TokenEndpoint = "/oauth/token";

    public SsoAuthService(
        HttpClient httpClient,
        IConfiguration configuration,
        ISsoUserSyncService userSyncService,
        ILogger<SsoAuthService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _userSyncService = userSyncService ?? throw new ArgumentNullException(nameof(userSyncService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public async Task<LoginResponse> ProxyLoginAsync(LoginRequest loginRequest, CancellationToken cancellationToken = default)
    {
        if (loginRequest == null)
            throw new ArgumentNullException(nameof(loginRequest));

        if (string.IsNullOrWhiteSpace(loginRequest.Email) || string.IsNullOrWhiteSpace(loginRequest.Password) ||
            string.IsNullOrWhiteSpace(loginRequest.TenantSlug))
            throw new ArgumentException("Email, password, and tenant slug are required", nameof(loginRequest));

        try
        {
            _logger.LogInformation("Proxying login request to SSO: Email={Email}, Tenant={Tenant}",
                loginRequest.Email, loginRequest.TenantSlug);

            // Proxy request to SSO service
            var ssoResponse = await ProxySsoRequestAsync(loginRequest, cancellationToken);

            if (!string.IsNullOrEmpty(ssoResponse.Error))
            {
                _logger.LogWarning("SSO authentication failed: Error={Error}, Description={Description}",
                    ssoResponse.Error, ssoResponse.ErrorDescription);
                throw new InvalidOperationException($"SSO authentication failed: {ssoResponse.ErrorDescription}");
            }

            // Parse SSO JWT token
            var principal = ParseSsoToken(ssoResponse.AccessToken);
            if (principal == null)
                throw new InvalidOperationException("Failed to parse SSO JWT token");

            // Extract claims from SSO token
            var ssoUserId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? principal.FindFirst("sub")?.Value;
            var email = principal.FindFirst(ClaimTypes.Email)?.Value ?? loginRequest.Email;
            var tenantSlug = principal.FindFirst("tenant_slug")?.Value ?? loginRequest.TenantSlug;
            var role = principal.FindFirst(ClaimTypes.Role)?.Value ?? "user";
            var isSuperUser = bool.TryParse(principal.FindFirst("is_superuser")?.Value, out var superUser) && superUser;
            var fullName = principal.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(ssoUserId))
                throw new InvalidOperationException("SSO token missing user ID claim");

            // Sync user to local database
            var localUser = await _userSyncService.SyncUserFromSsoAsync(
                ssoUserId, email, tenantSlug, role, isSuperUser, fullName, cancellationToken);

            // Generate local JWT token
            var localToken = GenerateLocalJwtToken(localUser, tenantSlug, role, isSuperUser);
            var expiresIn = ssoResponse.ExpiresIn;
            var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn).ToUnixTimeSeconds();

            _logger.LogInformation("User login successful: UserId={UserId}, Email={Email}, Role={Role}",
                localUser.Id, email, role);

            return new LoginResponse
            {
                Token = localToken,
                ExpiresAt = expiresAt,
                User = new LoginResponseUser
                {
                    Id = localUser.Id,
                    Email = localUser.Email,
                    FullName = localUser.FullName,
                    TenantId = localUser.OrganizationId ?? Guid.Empty,
                    TenantSlug = tenantSlug,
                    RoleId = Guid.Empty, // TODO: Get from UserRole assignment
                    RoleName = role,
                    IsSuperUser = isSuperUser
                }
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during SSO proxy request");
            throw new InvalidOperationException("Failed to connect to SSO service", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SSO login proxy: Email={Email}", loginRequest.Email);
            throw;
        }
    }

    public ClaimsPrincipal? ParseSsoToken(string jwtToken)
    {
        if (string.IsNullOrWhiteSpace(jwtToken))
            return null;

        try
        {
            // For development/testing: parse token without validation
            // In production, validate signature with SSO public key
            var principal = _tokenHandler.ReadJwtToken(jwtToken);
            
            var claims = new List<Claim>();
            foreach (var claim in principal.Claims)
            {
                claims.Add(new Claim(claim.Type, claim.Value));
            }

            // Add additional claims for easier access
            var identity = new ClaimsIdentity(claims, "SSO");
            return new ClaimsPrincipal(identity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing SSO JWT token");
            return null;
        }
    }

    private async Task<SsoResponse> ProxySsoRequestAsync(LoginRequest loginRequest, CancellationToken cancellationToken)
    {
        var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "password" },
            { "username", loginRequest.Email },
            { "password", loginRequest.Password },
            { "tenant", loginRequest.TenantSlug },
            { "scope", "openid email profile" },
            { "client_id", _configuration["SSO:ClientId"] ?? "truload-backend" }
        });

        var ssoUrl = $"{SsoBaseUrl}{TokenEndpoint}";
        _logger.LogDebug("Proxying SSO request to: {Url}", ssoUrl);

        var response = await _httpClient.PostAsync(ssoUrl, requestContent, cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Try to parse error response
            try
            {
                return JsonSerializer.Deserialize<SsoResponse>(content) ?? new SsoResponse
                {
                    Error = "unknown_error",
                    ErrorDescription = $"SSO service returned: {response.StatusCode}"
                };
            }
            catch
            {
                return new SsoResponse
                {
                    Error = "invalid_response",
                    ErrorDescription = "Failed to parse SSO error response"
                };
            }
        }

        return JsonSerializer.Deserialize<SsoResponse>(content) 
            ?? throw new InvalidOperationException("Failed to deserialize SSO response");
    }

    private string GenerateLocalJwtToken(Models.User user, string tenantSlug, string role, bool isSuperUser)
    {
        // TODO: Implement JWT generation using configured signing key
        // For now, return placeholder - will implement in next step
        throw new NotImplementedException("JWT token generation to be implemented");
    }
}
