using Microsoft.AspNetCore.Identity;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Services.Interfaces.System;

namespace TruLoad.Backend.Infrastructure.Security;

/// <summary>
/// Password validator that reads policy from ISettingsService at runtime.
/// This ensures changes to password settings in the admin UI take effect immediately
/// without requiring an application restart.
/// </summary>
public class DynamicPasswordValidator : IPasswordValidator<ApplicationUser>
{
    private readonly ISettingsService _settingsService;

    public DynamicPasswordValidator(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordRequired",
                Description = "Password is required."
            });
        }

        var policy = await _settingsService.GetPasswordPolicyAsync();
        var errors = new List<IdentityError>();

        if (password.Length < policy.MinLength)
        {
            errors.Add(new IdentityError
            {
                Code = "PasswordTooShort",
                Description = $"Password must be at least {policy.MinLength} characters long."
            });
        }

        if (policy.RequireUppercase && !password.Any(char.IsUpper))
        {
            errors.Add(new IdentityError
            {
                Code = "PasswordRequiresUpper",
                Description = "Password must contain at least one uppercase letter."
            });
        }

        if (policy.RequireLowercase && !password.Any(char.IsLower))
        {
            errors.Add(new IdentityError
            {
                Code = "PasswordRequiresLower",
                Description = "Password must contain at least one lowercase letter."
            });
        }

        if (policy.RequireDigit && !password.Any(char.IsDigit))
        {
            errors.Add(new IdentityError
            {
                Code = "PasswordRequiresDigit",
                Description = "Password must contain at least one digit."
            });
        }

        if (policy.RequireSpecial && !password.Any(c => !char.IsLetterOrDigit(c)))
        {
            errors.Add(new IdentityError
            {
                Code = "PasswordRequiresNonAlphanumeric",
                Description = "Password must contain at least one special character."
            });
        }

        return errors.Count > 0 ? IdentityResult.Failed(errors.ToArray()) : IdentityResult.Success;
    }
}
