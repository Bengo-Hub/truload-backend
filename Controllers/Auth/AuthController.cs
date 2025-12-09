using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.DTOs.Auth;
using TruLoad.Backend.Services.Interfaces.Auth;

namespace TruLoad.Backend.Controllers.Auth;

/// <summary>
/// Authentication controller for SSO login and token management.
/// Proxies credentials to centralized auth-service and manages user synchronization.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly ISsoAuthService _ssoAuthService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(ISsoAuthService ssoAuthService, ILogger<AuthController> logger)
    {
        _ssoAuthService = ssoAuthService;
        _logger = logger;
    }

    /// <summary>
    /// Login endpoint - proxies credentials to auth-service, syncs user, returns local JWT token.
    /// </summary>
    /// <param name="request">Login request containing email, password, and tenant_slug</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LoginResponse with JWT token and user details, or error response</returns>
    /// <response code="200">Login successful, returns token and user details</response>
    /// <response code="400">Invalid request (missing fields, validation errors)</response>
    /// <response code="401">Authentication failed (invalid credentials or auth-service error)</response>
    /// <response code="500">Internal server error during user sync or token generation</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate request
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || 
                string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.TenantSlug))
            {
                _logger.LogWarning("Login attempt with missing required fields");
                return BadRequest(new ErrorResponse
                {
                    Error = "InvalidRequest",
                    Message = "Email, password, and tenant_slug are required"
                });
            }

            _logger.LogInformation("Login attempt for user {Email} in tenant {TenantSlug}", request.Email, request.TenantSlug);

            // Proxy credentials to auth-service and sync user
            var response = await _ssoAuthService.ProxyLoginAsync(request, cancellationToken);

            // Check if auth-service returned an error
            if (!string.IsNullOrEmpty(response.Error))
            {
                _logger.LogWarning("Authentication failed for user {Email}: {Error}", request.Email, response.Error);
                return Unauthorized(new ErrorResponse
                {
                    Error = "AuthenticationFailed",
                    Message = response.ErrorDescription ?? "Invalid credentials or authentication service error"
                });
            }

            _logger.LogInformation("Login successful for user {Email}", request.Email);
            return Ok(response);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during login for user {Email}", request?.Email);
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Error = "ServiceError",
                Message = "Authentication service is unavailable. Please try again later."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login for user {Email}", request?.Email);
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "An unexpected error occurred during login. Please try again later."
            });
        }
    }

    /// <summary>
    /// Health check endpoint for auth service availability.
    /// </summary>
    /// <returns>Health status</returns>
    /// <response code="200">Auth service is healthy</response>
    [HttpGet("health")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Health()
    {
        return Ok(new HealthResponse
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow
        });
    }
}

/// <summary>
/// Standard error response format for consistent error handling across the API.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Error code (e.g., "InvalidRequest", "AuthenticationFailed", "ServiceError")
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional additional error details
    /// </summary>
    public Dictionary<string, object>? Details { get; set; }
}

/// <summary>
/// Health check response format.
/// </summary>
public class HealthResponse
{
    /// <summary>
    /// Health status ("healthy", "degraded", "unhealthy")
    /// </summary>
    public string Status { get; set; } = "healthy";

    /// <summary>
    /// Timestamp of the health check
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
