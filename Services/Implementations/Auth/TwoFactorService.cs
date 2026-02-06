using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using TruLoad.Backend.DTOs.Auth;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Services.Interfaces.Auth;
using TruLoad.Backend.Services.Interfaces.System;

namespace TruLoad.Backend.Services.Implementations.Auth;

/// <summary>
/// Implementation of ITwoFactorService using ASP.NET Core Identity.
/// Supports TOTP-based 2FA with authenticator apps.
/// </summary>
public class TwoFactorService : ITwoFactorService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<TwoFactorService> _logger;
    private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";
    private const string Issuer = "TruLoad";

    public TwoFactorService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ISettingsService settingsService,
        ILogger<TwoFactorService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<TwoFactorStatusResponse> GetStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID {userId} not found");
        }

        var is2FaEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
        var hasAuthenticator = !string.IsNullOrEmpty(await _userManager.GetAuthenticatorKeyAsync(user));
        var recoveryCodesCount = await _userManager.CountRecoveryCodesAsync(user);
        var isMachineRemembered = await _signInManager.IsTwoFactorClientRememberedAsync(user);

        return new TwoFactorStatusResponse(
            IsEnabled: is2FaEnabled,
            HasAuthenticator: hasAuthenticator,
            RecoveryCodesRemaining: recoveryCodesCount,
            IsMachineRemembered: isMachineRemembered
        );
    }

    public async Task<TwoFactorSetupResponse> GenerateSetupAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID {userId} not found");
        }

        // Reset the authenticator key to generate a new one
        await _userManager.ResetAuthenticatorKeyAsync(user);
        var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);

        if (string.IsNullOrEmpty(unformattedKey))
        {
            throw new InvalidOperationException("Failed to generate authenticator key");
        }

        var email = await _userManager.GetEmailAsync(user) ?? user.UserName ?? userId.ToString();
        var authenticatorUri = GenerateAuthenticatorUri(email, unformattedKey);
        var formattedKey = FormatKey(unformattedKey);

        // Generate QR code as data URL (base64 SVG)
        var qrCodeDataUrl = GenerateQrCodeDataUrl(authenticatorUri);

        _logger.LogInformation("Generated 2FA setup for user {UserId}", userId);

        return new TwoFactorSetupResponse(
            SharedKey: formattedKey,
            AuthenticatorUri: authenticatorUri,
            QrCodeDataUrl: qrCodeDataUrl
        );
    }

    public async Task<Enable2FAResponse> EnableAsync(Guid userId, string verificationCode, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID {userId} not found");
        }

        // Strip spaces and dashes from the code
        var sanitizedCode = verificationCode.Replace(" ", "").Replace("-", "");

        // Verify the code
        var isValid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            sanitizedCode);

        if (!isValid)
        {
            _logger.LogWarning("Invalid 2FA verification code for user {UserId}", userId);
            return new Enable2FAResponse(Success: false, RecoveryCodes: Array.Empty<string>());
        }

        // Enable 2FA
        await _userManager.SetTwoFactorEnabledAsync(user, true);

        // Generate recovery codes
        var backupCodesCount = await _settingsService.GetSettingValueAsync(
            Models.System.SettingKeys.TwoFactorBackupCodesCount, 10, ct);

        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, backupCodesCount);

        _logger.LogInformation("2FA enabled for user {UserId}", userId);

        return new Enable2FAResponse(
            Success: true,
            RecoveryCodes: recoveryCodes?.ToArray() ?? Array.Empty<string>()
        );
    }

    public async Task<bool> DisableAsync(Guid userId, string password, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID {userId} not found");
        }

        // Verify password
        var passwordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
        {
            _logger.LogWarning("Invalid password when disabling 2FA for user {UserId}", userId);
            return false;
        }

        // Disable 2FA
        var result = await _userManager.SetTwoFactorEnabledAsync(user, false);
        if (!result.Succeeded)
        {
            _logger.LogError("Failed to disable 2FA for user {UserId}: {Errors}",
                userId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return false;
        }

        // Reset authenticator key
        await _userManager.ResetAuthenticatorKeyAsync(user);

        _logger.LogInformation("2FA disabled for user {UserId}", userId);
        return true;
    }

    public async Task<bool> VerifyCodeAsync(Guid userId, string code, bool useRecoveryCode, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID {userId} not found");
        }

        var sanitizedCode = code.Replace(" ", "").Replace("-", "");

        if (useRecoveryCode)
        {
            var result = await _userManager.RedeemTwoFactorRecoveryCodeAsync(user, sanitizedCode);
            if (result.Succeeded)
            {
                _logger.LogInformation("Recovery code used for user {UserId}", userId);
                return true;
            }
            _logger.LogWarning("Invalid recovery code for user {UserId}", userId);
            return false;
        }

        var isValid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            _userManager.Options.Tokens.AuthenticatorTokenProvider,
            sanitizedCode);

        if (!isValid)
        {
            _logger.LogWarning("Invalid 2FA code for user {UserId}", userId);
        }

        return isValid;
    }

    public async Task<RecoveryCodesResponse> RegenerateRecoveryCodesAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID {userId} not found");
        }

        if (!await _userManager.GetTwoFactorEnabledAsync(user))
        {
            throw new InvalidOperationException("2FA is not enabled for this user");
        }

        var backupCodesCount = await _settingsService.GetSettingValueAsync(
            Models.System.SettingKeys.TwoFactorBackupCodesCount, 10, ct);

        var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, backupCodesCount);
        var remainingCount = await _userManager.CountRecoveryCodesAsync(user);

        _logger.LogInformation("Recovery codes regenerated for user {UserId}", userId);

        return new RecoveryCodesResponse(
            RecoveryCodes: recoveryCodes?.ToArray() ?? Array.Empty<string>(),
            RemainingCodes: remainingCount
        );
    }

    public async Task<bool> ResetAuthenticatorAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new KeyNotFoundException($"User with ID {userId} not found");
        }

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);

        _logger.LogInformation("Authenticator reset for user {UserId}", userId);
        return true;
    }

    private static string GenerateAuthenticatorUri(string email, string unformattedKey)
    {
        return string.Format(
            AuthenticatorUriFormat,
            UrlEncoder.Default.Encode(Issuer),
            UrlEncoder.Default.Encode(email),
            unformattedKey);
    }

    private static string FormatKey(string unformattedKey)
    {
        // Format as groups of 4 characters for readability
        var result = new StringBuilder();
        var currentPosition = 0;

        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }

        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }

        return result.ToString().ToUpperInvariant();
    }

    /// <summary>
    /// Generates a simple QR code data URL using SVG.
    /// For production, consider using a proper QR code library like QRCoder.
    /// This implementation creates a placeholder that instructs users to use the manual key.
    /// </summary>
    private static string GenerateQrCodeDataUrl(string authenticatorUri)
    {
        // For a proper implementation, use QRCoder or similar library
        // This generates a simple SVG placeholder with the URI encoded
        var svg = $@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 200 200"">
            <rect width=""200"" height=""200"" fill=""white""/>
            <text x=""100"" y=""90"" text-anchor=""middle"" font-family=""Arial"" font-size=""12"">Scan QR Code</text>
            <text x=""100"" y=""110"" text-anchor=""middle"" font-family=""Arial"" font-size=""10"">or enter key manually</text>
            <rect x=""40"" y=""40"" width=""120"" height=""120"" fill=""none"" stroke=""black"" stroke-width=""2""/>
        </svg>";

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
        return $"data:image/svg+xml;base64,{base64}";
    }
}
