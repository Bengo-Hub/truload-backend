using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TruLoad.Backend.Common.Constants;
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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AuthController> _logger;

    // JWKS cache: tuple of (keys, fetched-at)
    private static (IList<SecurityKey> keys, DateTime fetchedAt)? _jwksCache;
    private static readonly SemaphoreSlim _jwksCacheLock = new(1, 1);

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
        IHttpClientFactory httpClientFactory,
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
        _httpClientFactory = httpClientFactory;
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

        // NOTIFY: Welcome notification for new user
        _ = _notificationService.SendInternalNotificationAsync(
            user.Id,
            "Welcome to TruLoad",
            $"Your account has been created successfully. Welcome, {user.FullName ?? user.Email}!",
            "success");

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
                // NOTIFY: Account locked out — security alert
                _ = _notificationService.SendInternalNotificationAsync(
                    user.Id,
                    "Account Locked",
                    "Your account has been temporarily locked due to multiple failed login attempts. Contact your administrator if you need access.",
                    "warning");
                return Unauthorized(new { message = "Account is locked out" });
            }
            return Unauthorized(new { message = "Invalid email or password" });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var isSuperUser = roles.Contains("SUPERUSER", StringComparer.OrdinalIgnoreCase);

        // For superusers: resolve the requested org so their login response carries that org's context
        // (e.g. admin@codevertexafrica.com logging into /codevertex-demo/ gets CODEVERTEX-DEMO data)
        Organization? superuserContextOrg = null;
        if (isSuperUser && !string.IsNullOrWhiteSpace(request.OrganizationCode))
        {
            var codeTrimmed = request.OrganizationCode.Trim();
            superuserContextOrg = await _organizationRepository.GetByCodeAsync(codeTrimmed)
                ?? await _organizationRepository.GetByCodeAsync(codeTrimmed.ToUpperInvariant())
                ?? await _organizationRepository.GetByCodeAsync(codeTrimmed.ToLowerInvariant());
        }

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
        return await CompleteLoginAsync(user, require2FASetup, superuserContextOrg);
    }

    /// <summary>
    /// Shared login completion logic: shift check, token generation, response.
    /// Used by both Login() and the 2FA verify endpoint.
    /// When require2FASetup is true, response includes requires2FASetup so frontend can force user to enable 2FA from profile.
    /// </summary>
    private async Task<IActionResult> CompleteLoginAsync(ApplicationUser user, bool require2FASetup = false, Organization? contextOrg = null, bool isPlatformOwner = false)
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
        var accessToken = _jwtService.GenerateAccessToken(user, roles, uniquePermissions, isHqUser, isPlatformOwner);

        // Store refresh token server-side (hashed)
        var refreshToken = await _jwtService.StoreRefreshTokenAsync(user.Id);

        _logger.LogInformation("User {Email} logged in successfully", user.Email);

        // Create an Identity cookie for browser clients
        await _signInManager.SignInAsync(user, isPersistent: false);

        var isSuperUser = roles.Contains("SUPERUSER", StringComparer.OrdinalIgnoreCase);

        // Resolve organization for frontend routing and tenant-mode enforcement.
        // For superusers: use contextOrg (the org they logged into) if provided,
        // so they see that tenant's data rather than their home org's data.
        var isSuperUserForContext = roles.Contains("SUPERUSER", StringComparer.OrdinalIgnoreCase);
        Organization? resolvedOrg = (isSuperUserForContext && contextOrg != null)
            ? contextOrg
            : (user.OrganizationId.HasValue ? await _organizationRepository.GetByIdAsync(user.OrganizationId.Value) : null);

        string? organizationCode = resolvedOrg?.Code;
        string? tenantType = resolvedOrg?.TenantType;
        string? tenantUseCase = null;
        List<string>? enabledModules = null;
        if (resolvedOrg != null)
        {
            tenantUseCase = string.Equals(resolvedOrg.TenantType, TenantModules.TenantTypeCommercialWeighing, StringComparison.OrdinalIgnoreCase) ? "Commercial" : "Enforcement";
            enabledModules = ResolveEnabledModulesForOrg(resolvedOrg);
        }

        var resolvedOrgId = resolvedOrg?.Id ?? user.OrganizationId ?? (await _organizationRepository.GetByCodeAsync("KURA"))?.Id;

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
                organizationId = resolvedOrgId,
                organizationCode,
                tenantType,
                tenantUseCase,
                enabledModules,
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

        // Clear ASP.NET Identity authentication cookie
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

        // Also delete the auth cookie explicitly in case a custom cookie name is used
        Response.Cookies.Delete("TruLoad.Auth");

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

        // Build per-tenant reset URL from org.AppUrl; fall back to FrontendUrl config.
        var userOrg = user.OrganizationId.HasValue
            ? await _organizationRepository.GetByIdAsync(user.OrganizationId.Value)
            : null;
        var appUrl = userOrg?.AppUrl?.TrimEnd('/')
            ?? _configuration["FrontendUrl"]?.TrimEnd('/')
            ?? "https://truload.codevertexafrica.com";
        var orgSlug = userOrg?.Code?.ToLowerInvariant() ?? string.Empty;
        var resetPath = string.IsNullOrEmpty(orgSlug)
            ? "/auth/reset-password"
            : $"/{orgSlug}/auth/reset-password";
        var resetUrl = $"{appUrl}{resetPath}?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";

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

        // NOTIFY: Password reset confirmation
        _ = _notificationService.SendInternalNotificationAsync(
            user.Id,
            "Password Reset Successful",
            "Your password has been reset successfully. If you did not make this change, contact your administrator immediately.",
            "info");

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

        // NOTIFY: Password changed confirmation
        _ = _notificationService.SendInternalNotificationAsync(
            user.Id,
            "Password Changed",
            "Your password was changed successfully. If you did not make this change, contact your administrator immediately.",
            "info");

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
        string? tenantUseCase = null;
        List<string>? enabledModules = null;
        if (user.OrganizationId.HasValue)
        {
            var org = await _organizationRepository.GetByIdAsync(user.OrganizationId.Value);
            if (org != null)
            {
                organizationCode = org.Code;
                tenantType = org.TenantType;
                tenantUseCase = string.Equals(org.TenantType, TenantModules.TenantTypeCommercialWeighing, StringComparison.OrdinalIgnoreCase) ? "Commercial" : "Enforcement";
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
            tenantUseCase,
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

    // ── SSO / Commercial Tenant Endpoints ────────────────────────────────────────

    /// <summary>
    /// Returns public tenant info for a given org slug.
    /// Used by the login page to determine whether to show local login or redirect to SSO.
    /// No authentication required.
    /// </summary>
    [HttpGet("tenant-info")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTenantInfo([FromQuery] string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { message = "code is required" });

        var org = await _organizationRepository.GetByCodeAsync(code);
        if (org == null)
            return NotFound(new { message = "Organization not found" });

        return Ok(new
        {
            tenantType = org.TenantType ?? "AxleLoadEnforcement",
            name = org.Name,
            logoUrl = org.LogoUrl ?? "/images/" + BrandingConstants.Logos.TruLoadLogoSvg,
            ssoTenantSlug = org.SsoTenantSlug,
            organizationCode = org.Code
        });
    }

    /// <summary>
    /// Exchanges an SSO access token (from auth-api) for a short-lived truload SSO exchange token.
    /// JIT-provisions the user if not found. Does not issue a full session — returns requiresStationSelection=true.
    /// </summary>
    [HttpPost("sso-exchange")]
    [AllowAnonymous]
    public async Task<IActionResult> SsoExchange([FromBody] SsoExchangeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AccessToken))
            return BadRequest(new { message = "accessToken is required" });

        // 1. Validate SSO token via JWKS
        ClaimsPrincipal? ssoPrincipal;
        try
        {
            ssoPrincipal = await ValidateSsoTokenAsync(request.AccessToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SSO token validation failed");
            return Unauthorized(new { message = "Invalid or expired SSO token" });
        }

        if (ssoPrincipal == null)
            return Unauthorized(new { message = "Invalid SSO token" });

        // 2. Extract claims
        var email = ssoPrincipal.FindFirst(ClaimTypes.Email)?.Value
                    ?? ssoPrincipal.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
        var tenantSlug = ssoPrincipal.FindFirst("tenant_slug")?.Value;
        var fullName = ssoPrincipal.FindFirst(ClaimTypes.Name)?.Value
                       ?? ssoPrincipal.FindFirst("name")?.Value
                       ?? email;
        var isPlatformOwner = ssoPrincipal.FindFirst("is_platform_owner")?.Value == "true";

        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized(new { message = "SSO token missing email claim" });

        // 3. Resolve the target Organization.
        // Platform owner + an explicit TargetOrgCode: resolve by Organization.Code directly —
        // this is the ONLY way to reach real enforcement orgs (KURA/KENHA/KERRA), which have no
        // SsoTenantSlug of their own (they aren't auth-api tenants). Falls back to the normal
        // SsoTenantSlug resolution if no TargetOrgCode is supplied (e.g. first-ever platform-owner
        // login before they've picked an org).
        //
        // A non-platform-owner may ALSO supply TargetOrgCode, but only to pick among organisations
        // that already share their OWN token's tenant_slug (e.g. codevertex-demo's admin picking
        // between its 4 outlet organisations, 2026-09-05) — verified AFTER resolving org-by-code,
        // below, never trusted from the request alone. This can never cross into a foreign tenant's
        // organisation: the target must carry the exact same SsoTenantSlug the caller's own SSO
        // token already asserts, which auth-api (not the caller) controls. Every other SSO user
        // (a single-org tenant, or no TargetOrgCode supplied) is unaffected — falls straight through
        // to the existing slug-based resolution below, byte-identical to before this change.
        Organization? org = null;
        if (!string.IsNullOrWhiteSpace(request.TargetOrgCode) && (isPlatformOwner || !string.IsNullOrWhiteSpace(tenantSlug)))
        {
            var codeTrimmed = request.TargetOrgCode.Trim();
            var candidateOrg = await _organizationRepository.GetByCodeAsync(codeTrimmed)
                ?? await _organizationRepository.GetByCodeAsync(codeTrimmed.ToUpperInvariant())
                ?? await _organizationRepository.GetByCodeAsync(codeTrimmed.ToLowerInvariant());
            if (candidateOrg == null)
            {
                if (isPlatformOwner)
                    return NotFound(new { message = $"No TruLoad organisation with code '{request.TargetOrgCode}'" });
                // Non-platform-owner: an unresolvable code falls through to normal slug resolution
                // rather than erroring, since this parameter is optional/best-effort for them.
            }
            else if (isPlatformOwner || string.Equals(candidateOrg.SsoTenantSlug, tenantSlug, StringComparison.OrdinalIgnoreCase))
            {
                org = candidateOrg;
            }
            else
            {
                _logger.LogWarning(
                    "SSO user {Email} (tenant_slug={TenantSlug}) requested TargetOrgCode {Code} belonging to a different tenant — ignoring, falling back to slug resolution",
                    email, tenantSlug, request.TargetOrgCode);
            }
        }
        if (org == null)
        {
            if (string.IsNullOrWhiteSpace(tenantSlug))
                return Unauthorized(new { message = "SSO token missing tenant_slug claim" });

            // codevertex-demo now maps to MULTIPLE TruLoad organisations (CODEVERTEX-DEMO plus
            // per-vertical outlet organisations like CODEVERTEX-DEMO-QUARRY/-WASTE, synced by
            // AuthDemoSyncService — see its class doc comment) that all share this one SsoTenantSlug.
            // The incoming SSO access token carries NO outlet/branch claim at all (confirmed by
            // reading auth-api's oidc_handler.go token mint — outlet claims are only ever set by its
            // separate POST /auth/select-outlet step, which truload's own SSO flow never calls), so
            // the only reliable per-user disambiguation signal is the user's OWN already-resolved
            // OrganizationId, set once by AuthDemoSyncService/JIT-provisioning at first sync/login and
            // stable thereafter. Prefer that organisation when the user already exists and it's among
            // this slug's candidates; a brand-new, never-before-seen email (no local row yet) falls
            // through to the oldest candidate (index 0, ascending CreatedAt) as a single deterministic
            // default — today that's always CODEVERTEX-DEMO, which predates every outlet organisation.
            // When only one organisation carries this slug (every non-demo SSO tenant today, and the
            // demo tenant before this feature), this resolves to the exact same single candidate as
            // before — byte-identical behaviour for every existing tenant.
            var slugCandidates = await _organizationRepository.GetAllBySsoTenantSlugAsync(tenantSlug);
            if (slugCandidates.Count == 0)
            {
                _logger.LogWarning("No organization found for SSO tenant slug {TenantSlug}", tenantSlug);
                return NotFound(new { message = "No TruLoad organization mapped to this SSO tenant" });
            }

            Organization? preferredOrg = null;
            if (slugCandidates.Count > 1)
            {
                var existingUserForSlug = await _userManager.FindByEmailAsync(email);
                if (existingUserForSlug != null)
                {
                    preferredOrg = slugCandidates.FirstOrDefault(o => o.Id == existingUserForSlug.OrganizationId);
                }
            }
            org = preferredOrg ?? slugCandidates[0];
        }

        // 4. Find or JIT-provision user
        var user = await _userManager.FindByEmailAsync(email);
        if (user != null && user.OrganizationId != org.Id && !isPlatformOwner)
        {
            // User exists but belongs to a different organization — block cross-org login.
            // Platform owners are exempt: they can legitimately reach any organisation.
            _logger.LogWarning("SSO cross-org login blocked: {Email} belongs to org {UserOrg} but SSO resolved to org {SsoOrg}",
                email, user.OrganizationId, org.Id);
            return StatusCode(403, new { message = "You are not a member of this organisation. Please contact the organisation administrator to join first.", code = "org_mismatch" });
        }
        if (user == null)
        {
            // JIT-provision new user — no password (SSO-only; local login will be blocked)
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName ?? email,
                OrganizationId = org.Id,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                _logger.LogError("JIT user provisioning failed for {Email}: {Errors}",
                    email, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return StatusCode(500, new { message = "Failed to provision user account" });
            }
            _logger.LogInformation("JIT-provisioned SSO user {Email} for org {OrgCode}", email, org.Code);
        }

        if (isPlatformOwner)
        {
            // Idempotently grant Superuser — this is what makes CommercialModeFilter,
            // TenantContext's cross-tenant mode, and PermissionRequirementHandler's bypass all
            // already work with zero further changes (all three key off IsInRole("Superuser")).
            if (!await _userManager.IsInRoleAsync(user, "Superuser"))
            {
                var grantResult = await _userManager.AddToRoleAsync(user, "Superuser");
                if (!grantResult.Succeeded)
                {
                    _logger.LogError("Failed to grant Superuser to platform owner {Email}: {Errors}",
                        email, string.Join(", ", grantResult.Errors.Select(e => e.Description)));
                    return StatusCode(500, new { message = "Failed to provision platform owner access" });
                }
                _logger.LogInformation("Granted Superuser role to platform owner {Email} via SSO", email);
            }

            if (user.OrganizationId != org.Id)
            {
                _logger.LogInformation("Platform owner {Email} SSO context switched to org {OrgCode}", email, org.Code);
            }
        }

        // 5. Issue short-lived SSO exchange token (station selection required before full session).
        // Embeds the RESOLVED org (may differ from user.OrganizationId for a platform owner) so
        // SelectStation looks up stations for the org actually being accessed, without mutating
        // the user's own home OrganizationId.
        var ssoExchangeToken = _jwtService.GenerateSsoExchangeToken(user.Id, org.Id, isPlatformOwner);

        return Ok(new
        {
            requiresStationSelection = true,
            ssoExchangeToken
        });
    }

    /// <summary>
    /// Lists TruLoad organisations for the pre-session org picker shown on the SSO callback page
    /// BEFORE a full truload session exists (the picker needs to run ahead of sso-exchange so
    /// TargetOrgCode can be supplied on that call). Takes the raw SSO access token directly (not a
    /// truload session token) and re-validates it via the same JWKS check SsoExchange uses.
    ///
    /// A platform owner gets every organisation (unchanged behaviour — this is how KURA/KENHA/KERRA,
    /// which have no SsoTenantSlug of their own, are ever reached at all). A non-platform-owner gets
    /// only the organisations sharing their OWN token's tenant_slug (2026-09-05 — e.g. codevertex-demo's
    /// admin picking between its 4 outlet organisations), mirroring SsoExchange's own
    /// same-tenant-only TargetOrgCode acceptance — never another tenant's organisations. Returns an
    /// empty list (not 403) for a non-platform-owner whose tenant has only one organisation, so the
    /// frontend can treat "picker has &lt;2 options" as "skip the picker", same as before this feature
    /// existed for every non-demo tenant.
    /// </summary>
    [HttpPost("sso-platform-organizations")]
    [AllowAnonymous]
    public async Task<IActionResult> SsoPlatformOrganizations([FromBody] SsoExchangeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AccessToken))
            return BadRequest(new { message = "accessToken is required" });

        ClaimsPrincipal? ssoPrincipal;
        try
        {
            ssoPrincipal = await ValidateSsoTokenAsync(request.AccessToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SSO token validation failed (platform-organizations)");
            return Unauthorized(new { message = "Invalid or expired SSO token" });
        }

        if (ssoPrincipal == null)
            return Unauthorized(new { message = "Invalid or expired SSO token" });

        var isPlatformOwner = ssoPrincipal.FindFirst("is_platform_owner")?.Value == "true";
        if (isPlatformOwner)
        {
            var allOrgs = await _organizationRepository.GetAllAsync(includeInactive: false);
            return Ok(allOrgs
                .OrderBy(o => o.TenantType).ThenBy(o => o.Name)
                .Select(o => new { code = o.Code, name = o.Name, tenantType = o.TenantType }));
        }

        var tenantSlug = ssoPrincipal.FindFirst("tenant_slug")?.Value;
        if (string.IsNullOrWhiteSpace(tenantSlug))
            return Ok(Array.Empty<object>());

        var tenantOrgs = await _organizationRepository.GetAllBySsoTenantSlugAsync(tenantSlug);
        if (tenantOrgs.Count < 2)
            return Ok(Array.Empty<object>());

        return Ok(tenantOrgs
            .OrderBy(o => o.TenantType).ThenBy(o => o.Name)
            .Select(o => new { code = o.Code, name = o.Name, tenantType = o.TenantType }));
    }

    /// <summary>
    /// Completes station selection and issues a full truload JWT session.
    /// Accepts either an ssoExchangeToken (SSO path) or a current accessToken (local user station switch).
    /// </summary>
    [HttpPost("select-station")]
    [AllowAnonymous]
    public async Task<IActionResult> SelectStation([FromBody] SelectStationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.StationCode))
            return BadRequest(new { message = "stationCode is required" });

        ApplicationUser? user = null;
        Organization? contextOrg = null;
        var isPlatformOwner = false;

        if (!string.IsNullOrWhiteSpace(request.SsoExchangeToken))
        {
            // SSO path: validate ssoExchangeToken
            var exchangeResult = _jwtService.ValidateSsoExchangeToken(request.SsoExchangeToken);
            if (exchangeResult == null)
                return Unauthorized(new { message = "Invalid or expired SSO exchange token" });

            user = await _userManager.FindByIdAsync(exchangeResult.Value.userId.ToString());
            if (user == null)
                return Unauthorized(new { message = "User not found" });

            isPlatformOwner = exchangeResult.Value.isPlatformOwner;
            // Use the org RESOLVED at sso-exchange time (may differ from user.OrganizationId for
            // a platform owner drilling into a different org than their own) rather than the
            // user's persisted OrganizationId, so station lookup below targets the right org.
            contextOrg = await _organizationRepository.GetByIdAsync(exchangeResult.Value.orgId);
        }
        else if (!string.IsNullOrWhiteSpace(request.AccessToken))
        {
            // Local path: validate existing access token
            var userId = _jwtService.GetUserIdFromToken(request.AccessToken);
            if (!userId.HasValue)
                return Unauthorized(new { message = "Invalid access token" });

            user = await _userManager.FindByIdAsync(userId.Value.ToString());
            if (user == null)
                return Unauthorized(new { message = "User not found" });
        }
        else
        {
            return BadRequest(new { message = "Either ssoExchangeToken or accessToken is required" });
        }

        // Find station by code, scoped to the resolved context org (SSO path) or the user's own
        // organization (local path, unchanged).
        var targetOrgId = contextOrg?.Id ?? user.OrganizationId;
        Station? station = null;
        if (targetOrgId.HasValue)
        {
            var orgStations = await _stationRepository.GetByOrganizationIdAsync(targetOrgId.Value);
            station = orgStations.FirstOrDefault(s =>
                s.Code.Equals(request.StationCode, StringComparison.OrdinalIgnoreCase) && s.IsActive);
        }

        if (station == null)
            return NotFound(new { message = "Station not found or not active" });

        // Bind the station to the user and issue full JWT. For a platform owner drilling into a
        // different org than their own, OrganizationId is intentionally NOT overwritten here —
        // contextOrg carries the drill-in target through to CompleteLoginAsync instead, the same
        // pattern the local Login() endpoint already uses for a Superuser + OrganizationCode.
        user.StationId = station.Id;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        return await CompleteLoginAsync(user, contextOrg: contextOrg, isPlatformOwner: isPlatformOwner);
    }

    /// <summary>
    /// Fetches and caches JWKS from auth-api, then validates the SSO token.
    /// JWKS is cached for 24 hours.
    /// </summary>
    private async Task<ClaimsPrincipal?> ValidateSsoTokenAsync(string token)
    {
        var jwksUrl = _configuration["Auth:JwksUrl"];
        if (string.IsNullOrWhiteSpace(jwksUrl))
            throw new InvalidOperationException("Auth:JwksUrl is not configured");

        var issuer = _configuration["Auth:SsoIssuer"] ?? jwksUrl.Split("/.well-known")[0];

        // Refresh JWKS if not cached or older than 24 h
        if (_jwksCache == null || (DateTime.UtcNow - _jwksCache.Value.fetchedAt).TotalHours > 24)
        {
            await _jwksCacheLock.WaitAsync();
            try
            {
                if (_jwksCache == null || (DateTime.UtcNow - _jwksCache.Value.fetchedAt).TotalHours > 24)
                {
                    var client = _httpClientFactory.CreateClient();
                    var json = await client.GetStringAsync(jwksUrl);
                    var jwks = new JsonWebKeySet(json);
                    _jwksCache = (jwks.GetSigningKeys(), DateTime.UtcNow);
                }
            }
            finally
            {
                _jwksCacheLock.Release();
            }
        }

        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = false, // auth-api tokens may use 'truload-ui' or no audience
            ValidateLifetime = true,
            IssuerSigningKeys = _jwksCache!.Value.keys,
            ClockSkew = TimeSpan.FromSeconds(60)
        };

        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(token, validationParams, out _);
        return principal;
    }
}
