using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.DTOs.Auth;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Services.Interfaces;
using TruLoad.Backend.Services.Interfaces.Auth;

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
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        SignInManager<ApplicationUser> signInManager,
        IJwtService jwtService,
        IPermissionService permissionService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _signInManager = signInManager;
        _jwtService = jwtService;
        _permissionService = permissionService;
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

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            OrganizationId = request.OrganizationId,
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

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        // Get user roles and permissions
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
        
        // Generate JWT tokens
        var accessToken = _jwtService.GenerateAccessToken(user, roles, uniquePermissions);
        var refreshToken = _jwtService.GenerateRefreshToken();

        _logger.LogInformation("User {Email} logged in successfully", request.Email);

        return Ok(new
        {
            accessToken,
            refreshToken,
            expiresIn = 3600, // 1 hour
            user = new
            {
                id = user.Id,
                email = user.Email,
                fullName = user.FullName,
                roles = roles,
                permissions = uniquePermissions, // Permission codes for frontend RBAC
                organizationId = user.OrganizationId,
                stationId = user.StationId,
                departmentId = user.DepartmentId
            }
        });
    }

    /// <summary>
    /// Refresh access token using refresh token.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var isValid = _jwtService.ValidateRefreshToken(request.RefreshToken);
        if (!isValid)
        {
            return Unauthorized(new { message = "Invalid refresh token" });
        }

        var userId = _jwtService.GetUserIdFromToken(request.AccessToken);
        if (userId == Guid.Empty)
        {
            return Unauthorized(new { message = "Invalid access token" });
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Unauthorized(new { message = "User not found" });
        }

        // Generate new tokens
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
        
        var newAccessToken = _jwtService.GenerateAccessToken(user, roles, uniquePermissions);
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        return Ok(new
        {
            accessToken = newAccessToken,
            refreshToken = newRefreshToken,
            expiresIn = 3600
        });
    }

    /// <summary>
    /// Logout (client-side token removal).
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        // In JWT-based auth, logout is primarily client-side
        // Server-side could add token to blacklist if needed
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

        // TODO: Send email with reset token via notifications-service
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

        _logger.LogInformation("Password reset successfully for {Email}", request.Email);

        return Ok(new { message = "Password reset successfully" });
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

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
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
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
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

        return Ok(new
        {
            id = user.Id,
            email = user.Email,
            fullName = user.FullName,
            phoneNumber = user.PhoneNumber,
            roles = roles,
            permissions = uniquePermissions,
            organizationId = user.OrganizationId,
            stationId = user.StationId,
            departmentId = user.DepartmentId,
            lastLoginAt = user.LastLoginAt,
            createdAt = user.CreatedAt
        });
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
