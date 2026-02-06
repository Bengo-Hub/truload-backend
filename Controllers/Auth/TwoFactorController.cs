using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.DTOs.Auth;
using TruLoad.Backend.Services.Interfaces.Auth;
using TruLoad.Backend.Authorization.Attributes;

namespace TruLoad.Backend.Controllers;

/// <summary>
/// Controller for managing two-factor authentication (2FA) settings.
/// </summary>
[ApiController]
[Route("api/v1/auth/2fa")]
[Authorize]
public class TwoFactorController : ControllerBase
{
    private readonly ITwoFactorService _twoFactorService;
    private readonly ILogger<TwoFactorController> _logger;

    public TwoFactorController(
        ITwoFactorService twoFactorService,
        ILogger<TwoFactorController> logger)
    {
        _twoFactorService = twoFactorService;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(global::System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    /// <summary>
    /// Get the current 2FA status for the authenticated user.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(TwoFactorStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TwoFactorStatusResponse>> GetStatus(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        try
        {
            var status = await _twoFactorService.GetStatusAsync(userId, ct);
            return Ok(status);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Generate a new 2FA setup (authenticator key and QR code).
    /// </summary>
    [HttpPost("setup")]
    [ProducesResponseType(typeof(TwoFactorSetupResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TwoFactorSetupResponse>> GenerateSetup(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        try
        {
            var setup = await _twoFactorService.GenerateSetupAsync(userId, ct);
            return Ok(setup);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Enable 2FA by verifying the authenticator code.
    /// Returns recovery codes upon success.
    /// </summary>
    [HttpPost("enable")]
    [ProducesResponseType(typeof(Enable2FAResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Enable2FAResponse>> Enable(
        [FromBody] Enable2FARequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        try
        {
            var result = await _twoFactorService.EnableAsync(userId, request.VerificationCode, ct);
            if (!result.Success)
            {
                return BadRequest(new { message = "Invalid verification code. Please try again." });
            }

            _logger.LogInformation("2FA enabled for user {UserId}", userId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Disable 2FA for the authenticated user.
    /// Requires password verification.
    /// </summary>
    [HttpPost("disable")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Disable(
        [FromBody] Disable2FARequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        try
        {
            var result = await _twoFactorService.DisableAsync(userId, request.Password, ct);
            if (!result)
            {
                return BadRequest(new { message = "Invalid password. 2FA was not disabled." });
            }

            _logger.LogInformation("2FA disabled for user {UserId}", userId);
            return Ok(new { message = "Two-factor authentication has been disabled." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Verify a 2FA code (used during login when 2FA is enabled).
    /// </summary>
    [HttpPost("verify")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Verify(
        [FromBody] Verify2FARequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized(new { message = "User not authenticated" });
        }

        try
        {
            var isValid = await _twoFactorService.VerifyCodeAsync(
                userId,
                request.Code,
                request.UseRecoveryCode,
                ct);

            if (!isValid)
            {
                return BadRequest(new { message = "Invalid verification code." });
            }

            return Ok(new { message = "Verification successful." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Regenerate recovery codes. Invalidates all existing codes.
    /// </summary>
    [HttpPost("recovery-codes/regenerate")]
    [ProducesResponseType(typeof(RecoveryCodesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecoveryCodesResponse>> RegenerateRecoveryCodes(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        try
        {
            var result = await _twoFactorService.RegenerateRecoveryCodesAsync(userId, ct);
            _logger.LogInformation("Recovery codes regenerated for user {UserId}", userId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Reset authenticator app. User will need to re-enroll.
    /// </summary>
    [HttpPost("reset")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ResetAuthenticator(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        try
        {
            await _twoFactorService.ResetAuthenticatorAsync(userId, ct);
            _logger.LogInformation("Authenticator reset for user {UserId}", userId);
            return Ok(new { message = "Authenticator app has been reset. Please set up 2FA again." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
