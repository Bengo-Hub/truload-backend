using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Constants;
using TruLoad.Backend.DTOs.Auth;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Services.Interfaces;
using TruLoad.Backend.Services.Interfaces.Auth;
using TruLoad.Backend.Services.Interfaces.Shared;
using TruLoad.Backend.Services.Interfaces.System;
using TruLoad.Backend.Repositories.UserManagement.Interfaces;

namespace TruLoad.Backend.Controllers;

/// <summary>
/// Authentication controller handling user registration, login, password management.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IJwtService _jwtService;
    private readonly IPermissionService _permissionService;
    private readonly INotificationService _notificationService;
    private readonly ISettingsService _settingsService;
    private readonly IUserShiftRepository _userShiftRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IStationRepository _stationRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        SignInManager<ApplicationUser> signInManager,
        IJwtService jwtService,
        IPermissionService permissionService,
        INotificationService notificationService,
        ISettingsService settingsService,
        IUserShiftRepository userShiftRepository,
        IOrganizationRepository organizationRepository,
        IStationRepository stationRepository,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _signInManager = signInManager;
        _jwtService = jwtService;
        _permissionService = permissionService;
        _notificationService = notificationService;
        _settingsService = settingsService;
        _userShiftRepository = userShiftRepository;
        _organizationRepository = organizationRepository;
        _stationRepository = stationRepository;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user account.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var orgId = request.OrganizationId;
        if (!orgId.HasValue)
        {
            var kura = await _organizationRepository.GetByCodeAsync("KURA");
            orgId = kura?.Id;
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            OrganizationId = orgId,
            StationId = request.StationId,
            DepartmentId = request.DepartmentId,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return BadRequest(ModelState);
        }

        user.LastPasswordChangeAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User {Email} registered successfully", request.Email);

        return Ok(new
        {
            message = "User registered successfully",
            userId = user.Id,
            email = user.Email
        });
    }

    /// <summary>
    /// Login with email and password.
    /// </summary>
    /// <remarks>
    /// Default Admin Credentials (Development):
    /// - Email: gadmin@masterspace.co.ke
    /// - Password: ChangeMe123!
    /// 
    /// The response includes:
    /// - JWT access token with embedded user/role/permission claims
    /// - Refresh token for token renewal
    /// - User profile with roles and permissions array
    /// </remarks>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                return Unauthorized(new { message = "Account is locked out" });
            }
            return Unauthorized(new { message = "Invalid email or password" });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var isSuperUser = roles.Contains("SUPERUSER", StringComparer.OrdinalIgnoreCase);

        // Superusers can log in to any org/station (platform admin); skip tenant org/station validation
        if (!isSuperUser && !string.IsNullOrWhiteSpace(request.OrganizationCode))
        {
            var codeTrimmed = request.OrganizationCode.Trim();
            var org = await _organizationRepository.GetByCodeAsync(codeTrimmed)
                ?? await _organizationRepository.GetByCodeAsync(codeTrimmed.ToUpperInvariant())
                ?? await _organizationRepository.GetByCodeAsync(codeTrimmed.ToLowerInvariant());
            if (org == null)
            {
                return StatusCode(403, new { message = "Invalid organisation." });
            }
            if (user.OrganizationId != org.Id)
            {
                _logger.LogWarning("User {Email} attempted login for organisation {OrgCode} but belongs to different org.", request.Email, request.OrganizationCode);
                return StatusCode(403, new { message = "You are not assigned to this organisation." });
            }

            if (!string.IsNullOrWhiteSpace(request.StationCode))
            {
                var stations = await _stationRepository.GetByOrganizationIdAsync(org.Id);
                var selectedStation = stations.FirstOrDefault(s => string.Equals(s.Code, request.StationCode.Trim(), StringComparison.OrdinalIgnoreCase));
                if (selectedStation == null)
                {
                    return StatusCode(403, new { message = "Invalid station for this organisation." });
                }
                if (user.StationId.HasValue)
                {
                    var userStation = await _stationRepository.GetByIdAsync(user.StationId.Value);
                    var isHqUser = userStation?.IsHq ?? false;
                    if (!isHqUser && user.StationId.Value != selectedStation.Id)
                    {
                        _logger.LogWarning("User {Email} (station-linked) attempted login to station {StationCode}.", request.Email, request.StationCode);
                        return StatusCode(403, new { message = "You can only log in to your assigned station." });
                    }
                }
            }
        }

        // Enforce password expiry: block login until user changes password
        var passwordPolicy = await _settingsService.GetPasswordPolicyAsync();
        if (passwordPolicy.PasswordExpiryDays > 0)
        {
            var lastChange = user.LastPasswordChangeAt ?? user.CreatedAt;
            var expiryDate = lastChange.AddDays(passwordPolicy.PasswordExpiryDays);
            if (DateTime.UtcNow > expiryDate)
            {
                var changeToken = _jwtService.GenerateChangeExpiredPasswordToken(user.Id);
                _logger.LogInformation("Login blocked for {Email}: password expired. User must change password.", request.Email);
                return Unauthorized(new
                {
                    message = "Your password has expired. Please set a new password to continue.",
                    passwordExpired = true,
                    changePasswordToken = changeToken
                });
            }
        }

        // Check if 2FA is enabled for this user
        var is2FAEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
        if (is2FAEnabled)
        {
            // Return a short-lived challenge token; client must verify TOTP before getting full JWT
            var challengeToken = _jwtService.GenerateTwoFactorChallengeToken(user.Id);
            _logger.LogInformation("2FA challenge issued for user {Email}", request.Email);
            return Ok(new TwoFactorChallengeResponse
            {
                Requires2FA = true,
                TwoFactorToken = challengeToken
            });
        }

        // If organization requires 2FA for shift login and user is not excluded, allow login but signal that 2FA must be enabled (frontend will force profile 2FA setup)
        var shiftSettings = await _settingsService.GetShiftSettingsAsync();
        var require2FASetup = false;
        if (shiftSettings.Require2FA && !is2FAEnabled)
        {
            var userRoles = await _userManager.GetRolesAsync(user);
            var excludedRoles = (shiftSettings.ExcludedRoles ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var isExcluded = userRoles.Any(r => excludedRoles.Contains(r, StringComparer.OrdinalIgnoreCase));
            if (!isExcluded)
            {
                _logger.LogInformation("User {Email} must enable 2FA (policy). Returning requires2FASetup so frontend can force profile setup.", request.Email);
                require2FASetup = true;
            }
        }

        // Complete login (shared logic for normal login and post-2FA verification)
        return await CompleteLoginAsync(user, require2FASetup);
    }

    /// <summary>
    /// Shared login completion logic: shift check, token generation, response.
    /// Used by both Login() and the 2FA verify endpoint.
    /// When require2FASetup is true, response includes requires2FASetup so frontend can force user to enable 2FA from profile.
    /// </summary>
    private async Task<IActionResult> CompleteLoginAsync(ApplicationUser user, bool require2FASetup = false)
    {
        var roles = await _userManager.GetRolesAsync(user);

        // Shift enforcement check
        var shiftSettings = await _settingsService.GetShiftSettingsAsync();
        if (shiftSettings.EnforceShiftOnLogin && !shiftSettings.BypassShiftCheck)
        {
            var excludedRoles = (shiftSettings.ExcludedRoles ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var isExcluded = roles.Any(r => excludedRoles.Contains(r, StringComparer.OrdinalIgnoreCase));

            if (!isExcluded)
            {
                var hasActiveShift = await _userShiftRepository.HasActiveShiftAsync(user.Id);
                if (!hasActiveShift)
                {
                    _logger.LogWarning("Login denied for {Email}: no active shift assigned", user.Email);
                    return Unauthorized(new { message = "You are not assigned to an active shift. Please contact your supervisor." });
                }
            }
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        // Get permissions for all user roles
        var allPermissions = new List<string>();
        foreach (var roleName in roles)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role != null)
            {
                var permissions = await _permissionService.GetPermissionsForRoleAsync(role.Id);
                allPermissions.AddRange(permissions.Select(p => p.Code));
            }
        }
        var uniquePermissions = allPermissions.Distinct().ToList();

        // HQ users: assigned station is HQ; they can access all stations (no station filter unless they select one)
        var isHqUser = false;
        if (user.StationId.HasValue)
        {
            var userStation = await _stationRepository.GetByIdAsync(user.StationId.Value);
            isHqUser = userStation?.IsHq ?? false;
        }

        // Generate JWT access token (include isHqUser so middleware does not apply station filter when HQ user does not send X-Station-ID)
        var accessToken = _jwtService.GenerateAccessToken(user, roles, uniquePermissions, isHqUser);

        // Store refresh token server-side (hashed)
        var refreshToken = await _jwtService.StoreRefreshTokenAsync(user.Id);

        _logger.LogInformation("User {Email} logged in successfully", user.Email);

        // Create an Identity cookie for browser clients
        await _signInManager.SignInAsync(user, isPersistent: false);

        var isSuperUser = roles.Contains("SUPERUSER", StringComparer.OrdinalIgnoreCase);

        // Resolve organization code for frontend routing (e.g. redirect to /{orgSlug}/auth)
        string? organizationCode = null;
        if (user.OrganizationId.HasValue)
        {
            var org = await _organizationRepository.GetByIdAsync(user.OrganizationId.Value);
            organizationCode = org?.Code;
        }

        var response = new
        {
            accessToken,
            refreshToken,
            expiresIn = 3600,
            user = new
            {
                id = user.Id,
                email = user.Email,
                fullName = user.FullName,
                roles = roles,
                permissions = uniquePermissions,
                isSuperUser,
                organizationId = user.OrganizationId ?? (await _organizationRepository.GetByCodeAsync("KURA"))?.Id,
                organizationCode,
                stationId = user.StationId,
                isHqUser,
                departmentId = user.DepartmentId
            },
            requires2FASetup = require2FASetup ? true : (bool?)null
        };
        return Ok(response);
    }

    /// <summary>
    /// Complete login by verifying 2FA code after receiving a challenge token.
    /// </summary>
    [HttpPost("login/2fa-verify")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginVerify2FA([FromBody] LoginVerify2FARequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Validate the 2FA challenge token
        var userId = _jwtService.ValidateTwoFactorChallengeToken(request.TwoFactorToken);
        if (!userId.HasValue)
        {
            return Unauthorized(new { message = "Invalid or expired 2FA challenge token. Please login again." });
        }

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user == null)
        {
            return Unauthorized(new { message = "User not found" });
        }

        // Verify the TOTP code
        var sanitizedCode = request.Code.Replace(" ", "").Replace("-", "");
        bool isValid;

        if (request.UseRecoveryCode)
        {
            var redeemResult = await _userManager.RedeemTwoFactorRecoveryCodeAsync(user, sanitizedCode);
            isValid = redeemResult.Succeeded;
            if (isValid) _logger.LogInformation("Recovery code used for 2FA login by user {UserId}", userId);
        }
        else
        {
            isValid = await _userManager.VerifyTwoFactorTokenAsync(
                user,
                _userManager.Options.Tokens.AuthenticatorTokenProvider,
                sanitizedCode);
        }

        if (!isValid)
        {
            _logger.LogWarning("Invalid 2FA code during login for user {UserId}", userId);
            return Unauthorized(new { message = "Invalid verification code" });
        }

        _logger.LogInformation("2FA verification successful for user {Email}", user.Email);
        return await CompleteLoginAsync(user);
    }

    /// <summary>
    /// Refresh access token using refresh token.
    /// Validates the refresh token against the database and performs token rotation.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Validate and rotate refresh token (DB-backed)
        var (isValid, newRefreshToken, userId) = await _jwtService.ValidateAndRotateRefreshTokenAsync(request.RefreshToken);
        if (!isValid || newRefreshToken == null)
        {
            return Unauthorized(new { message = "Invalid or expired refresh token" });
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Unauthorized(new { message = "User not found" });
        }

        // Generate new access token
        var roles = await _userManager.GetRolesAsync(user);
        var allPermissions = new List<string>();
        foreach (var roleName in roles)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role != null)
            {
                var permissions = await _permissionService.GetPermissionsForRoleAsync(role.Id);
                allPermissions.AddRange(permissions.Select(p => p.Code));
            }
        }
        var uniquePermissions = allPermissions.Distinct().ToList();
        var isHqUser = false;
        if (user.StationId.HasValue)
        {
            var userStation = await _stationRepository.GetByIdAsync(user.StationId.Value);
            isHqUser = userStation?.IsHq ?? false;
        }
        var newAccessToken = _jwtService.GenerateAccessToken(user, roles, uniquePermissions, isHqUser);

        return Ok(new
        {
            accessToken = newAccessToken,
            refreshToken = newRefreshToken,
            expiresIn = 3600
        });
    }

    /// <summary>
    /// Logout and revoke all refresh tokens for the current user.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userIdStr = User.FindFirst(global::System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdStr) && Guid.TryParse(userIdStr, out var userId))
        {
            await _jwtService.RevokeAllUserTokensAsync(userId);
        }

        _logger.LogInformation("User logged out");
        return Ok(new { message = "Logged out successfully" });
    }

    /// <summary>
    /// Request password reset email.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            // Don't reveal that user doesn't exist
            return Ok(new { message = "If the email exists, a password reset link has been sent" });
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        // Build reset URL for frontend
        var frontendUrl = _configuration["FrontendUrl"] ?? "https://truload.masterspace.co.ke";
        var resetUrl = $"{frontendUrl}/auth/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";

        // Send password reset email via notifications-service
        var emailSent = await _notificationService.SendEmailAsync(
            "auth/password_reset",
            user.Email!,
            user.FullName ?? user.Email!,
            new Dictionary<string, object>
            {
                ["reset_url"] = resetUrl,
                ["reset_token"] = token,
                ["user_name"] = user.FullName ?? user.Email!,
                ["expiry_hours"] = 24
            },
            "Password Reset Request - TruLoad");

        if (!emailSent)
        {
            _logger.LogWarning("Failed to send password reset email for {Email}", request.Email);
        }

        _logger.LogInformation("Password reset requested for {Email}", request.Email);

        return Ok(new { message = "If the email exists, a password reset link has been sent" });
    }

    /// <summary>
    /// Reset password using token from email.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return BadRequest(new { message = "Invalid request" });
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return BadRequest(ModelState);
        }

        user.LastPasswordChangeAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Password reset successfully for {Email}", request.Email);

        return Ok(new { message = "Password reset successfully" });
    }

    /// <summary>
    /// Change expired password (public). Called when login returns passwordExpired and changePasswordToken.
    /// User must set a new password meeting policy before they can log in again.
    /// </summary>
    [HttpPost("change-expired-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ChangeExpiredPassword([FromBody] ChangeExpiredPasswordRequest request)
    {
        if (string.IsNullOrEmpty(request.ChangePasswordToken) || string.IsNullOrEmpty(request.NewPassword))
        {
            return BadRequest(new { message = "Token and new password are required" });
        }

        var userId = _jwtService.ValidateChangeExpiredPasswordToken(request.ChangePasswordToken);
        if (!userId.HasValue)
        {
            return Unauthorized(new { message = "Invalid or expired token. Please try logging in again to get a new link." });
        }

        var user = await _userManager.FindByIdAsync(userId.Value.ToString());
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return BadRequest(ModelState);
        }

        user.LastPasswordChangeAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Expired password changed for user {UserId}", user.Id);
        return Ok(new { message = "Password changed successfully. You can now log in." });
    }

    /// <summary>
    /// Get password policy (public). For use on login, register, forgot-password, reset-password and change-expired-password pages.
    /// </summary>
    [HttpGet("password-policy")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPasswordPolicyPublic(CancellationToken ct)
    {
        var policy = await _settingsService.GetPasswordPolicyAsync(ct);
        return Ok(policy);
    }

    /// <summary>
    /// Change password for authenticated user.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = User.FindFirst(global::System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return BadRequest(ModelState);
        }

        user.LastPasswordChangeAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Password changed successfully for user {UserId}", userId);

        return Ok(new { message = "Password changed successfully" });
    }

    /// <summary>
    /// Get current authenticated user profile with permissions.
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirst(global::System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        var roles = await _userManager.GetRolesAsync(user);
        
        // Get permissions for all user roles
        var allPermissions = new List<string>();
        foreach (var roleName in roles)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role != null)
            {
                var permissions = await _permissionService.GetPermissionsForRoleAsync(role.Id);
                allPermissions.AddRange(permissions.Select(p => p.Code));
            }
        }
        var uniquePermissions = allPermissions.Distinct().ToList();

        // Check if user has SUPERUSER role (bypasses all permission checks on frontend)
        var isSuperUser = roles.Contains("Superuser", StringComparer.OrdinalIgnoreCase);

        string? organizationCode = null;
        string? tenantType = null;
        List<string>? enabledModules = null;
        if (user.OrganizationId.HasValue)
        {
            var org = await _organizationRepository.GetByIdAsync(user.OrganizationId.Value);
            if (org != null)
            {
                organizationCode = org.Code;
                tenantType = org.TenantType;
                enabledModules = ResolveEnabledModulesForOrg(org);
            }
        }

        var isHqUser = false;
        if (user.StationId.HasValue)
        {
            var userStation = await _stationRepository.GetByIdAsync(user.StationId.Value);
            isHqUser = userStation?.IsHq ?? false;
        }

        return Ok(new
        {
            id = user.Id,
            email = user.Email,
            fullName = user.FullName,
            phoneNumber = user.PhoneNumber,
            roles = roles,
            permissions = uniquePermissions,
            isSuperUser,
            organizationId = user.OrganizationId,
            organizationCode,
            tenantType,
            enabledModules,
            stationId = user.StationId,
            isHqUser,
            departmentId = user.DepartmentId,
            lastLoginAt = user.LastLoginAt,
            createdAt = user.CreatedAt
        });
    }

    private static List<string> ResolveEnabledModulesForOrg(Organization org)
    {
        if (!string.IsNullOrWhiteSpace(org.EnabledModulesJson))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(org.EnabledModulesJson);
                if (list != null && list.Count > 0)
                    return list;
            }
            catch { /* use defaults */ }
        }
        if (string.Equals(org.TenantType, TenantModules.TenantTypeCommercialWeighing, StringComparison.OrdinalIgnoreCase))
            return TenantModules.DefaultCommercialWeighingModules.ToList();
        return TenantModules.AllModules.ToList();
    }

    /// <summary>
    /// Alias for GetProfile - GET /auth/me returns current user.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        return await GetProfile();
    }
}
