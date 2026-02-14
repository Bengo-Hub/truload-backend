using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Tests.Integration.Helpers;
using Xunit;
using FluentAssertions;

namespace TruLoad.Backend.Tests.Integration.Security;

/// <summary>
/// Integration tests for two-factor authentication (2FA) properties at the data model level.
/// Validates that IdentityUser 2FA properties (TwoFactorEnabled, authenticator tokens)
/// persist correctly through the EF Core data layer.
/// Note: ApplicationUser inherits 2FA properties from IdentityUser&lt;Guid&gt;.
/// Authenticator keys and recovery codes are stored in AspNetUserTokens via Identity's
/// UserManager, not as direct properties on ApplicationUser.
/// </summary>
public class TwoFactorTests : IAsyncLifetime
{
    private TruLoadDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _context = TestDbContextFactory.Create();
        await _context.Database.EnsureCreatedAsync();
        await TestDbContextFactory.SeedBaseData(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    #region TwoFactorEnabled Flag

    [Fact]
    public async Task User_TwoFactorEnabled_ShouldDefaultToFalse()
    {
        // Arrange & Act
        var user = await TestUserHelper.SeedTestUser(_context, "2fa-default@example.com");

        // Assert - IdentityUser defaults TwoFactorEnabled to false
        var saved = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        saved.Should().NotBeNull();
        saved!.TwoFactorEnabled.Should().BeFalse(
            "TwoFactorEnabled should default to false for new users");
    }

    [Fact]
    public async Task User_Enable2FA_ShouldSetFlag()
    {
        // Arrange
        var user = await TestUserHelper.SeedTestUser(_context, "enable-2fa@example.com");
        user.TwoFactorEnabled.Should().BeFalse();

        // Act - enable 2FA
        user.TwoFactorEnabled = true;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        saved.Should().NotBeNull();
        saved!.TwoFactorEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task User_Disable2FA_ShouldClearFlag()
    {
        // Arrange - user with 2FA enabled
        var user = await TestUserHelper.SeedTestUser(_context, "disable-2fa@example.com");
        user.TwoFactorEnabled = true;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        // Verify 2FA is enabled
        var enabled = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        enabled!.TwoFactorEnabled.Should().BeTrue();

        // Act - disable 2FA
        enabled.TwoFactorEnabled = false;
        _context.Users.Update(enabled);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        saved.Should().NotBeNull();
        saved!.TwoFactorEnabled.Should().BeFalse();
    }

    #endregion

    #region Authenticator Key Storage (via UserTokens)

    [Fact]
    public async Task User_AuthenticatorKey_ShouldBeStorableInUserTokens()
    {
        // Arrange - Identity stores authenticator keys in AspNetUserTokens table
        // with LoginProvider = "[AspNetUserStore]" and Name = "AuthenticatorKey"
        var user = await TestUserHelper.SeedTestUser(_context, "authkey@example.com");
        var authenticatorKey = "JBSWY3DPEHPK3PXP"; // Example base32 TOTP secret

        var token = new Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>
        {
            UserId = user.Id,
            LoginProvider = "[AspNetUserStore]",
            Name = "AuthenticatorKey",
            Value = authenticatorKey
        };

        // Act
        _context.Set<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().Add(token);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _context.Set<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>()
            .FirstOrDefaultAsync(t =>
                t.UserId == user.Id &&
                t.LoginProvider == "[AspNetUserStore]" &&
                t.Name == "AuthenticatorKey");

        saved.Should().NotBeNull();
        saved!.Value.Should().Be(authenticatorKey);
    }

    [Fact]
    public async Task User_AuthenticatorKey_ShouldBeRemovableOnDisable()
    {
        // Arrange - set up authenticator key
        var user = await TestUserHelper.SeedTestUser(_context, "remove-authkey@example.com");
        user.TwoFactorEnabled = true;
        _context.Users.Update(user);

        var token = new Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>
        {
            UserId = user.Id,
            LoginProvider = "[AspNetUserStore]",
            Name = "AuthenticatorKey",
            Value = "JBSWY3DPEHPK3PXP"
        };

        _context.Set<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().Add(token);
        await _context.SaveChangesAsync();

        // Act - disable 2FA and remove authenticator key
        user.TwoFactorEnabled = false;
        _context.Users.Update(user);

        var savedToken = await _context.Set<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>()
            .FirstOrDefaultAsync(t =>
                t.UserId == user.Id &&
                t.LoginProvider == "[AspNetUserStore]" &&
                t.Name == "AuthenticatorKey");

        if (savedToken != null)
        {
            _context.Set<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().Remove(savedToken);
        }
        await _context.SaveChangesAsync();

        // Assert
        var remaining = await _context.Set<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>()
            .FirstOrDefaultAsync(t =>
                t.UserId == user.Id &&
                t.LoginProvider == "[AspNetUserStore]" &&
                t.Name == "AuthenticatorKey");

        remaining.Should().BeNull("authenticator key should be removed when 2FA is disabled");

        var updatedUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        updatedUser!.TwoFactorEnabled.Should().BeFalse();
    }

    #endregion

    #region Recovery Codes Storage (via UserTokens)

    [Fact]
    public async Task User_RecoveryCodes_ShouldBeStorableInUserTokens()
    {
        // Arrange - Identity stores recovery codes in AspNetUserTokens
        // with LoginProvider = "[AspNetUserStore]" and Name = "RecoveryCodes"
        var user = await TestUserHelper.SeedTestUser(_context, "recovery@example.com");
        user.TwoFactorEnabled = true;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        // Recovery codes are typically stored as a semicolon-separated string
        var recoveryCodes = "ABC12345;DEF67890;GHI11223;JKL44556;MNO77889";

        var token = new Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>
        {
            UserId = user.Id,
            LoginProvider = "[AspNetUserStore]",
            Name = "RecoveryCodes",
            Value = recoveryCodes
        };

        // Act
        _context.Set<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().Add(token);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _context.Set<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>()
            .FirstOrDefaultAsync(t =>
                t.UserId == user.Id &&
                t.LoginProvider == "[AspNetUserStore]" &&
                t.Name == "RecoveryCodes");

        saved.Should().NotBeNull();
        saved!.Value.Should().Be(recoveryCodes);
        saved.Value!.Split(';').Should().HaveCount(5, "should store 5 recovery codes");
    }

    [Fact]
    public async Task User_RecoveryCodes_ShouldBeUpdatableAfterUse()
    {
        // Arrange - user has 5 recovery codes, uses one
        var user = await TestUserHelper.SeedTestUser(_context, "use-recovery@example.com");
        user.TwoFactorEnabled = true;
        _context.Users.Update(user);

        var initialCodes = "ABC12345;DEF67890;GHI11223;JKL44556;MNO77889";
        var token = new Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>
        {
            UserId = user.Id,
            LoginProvider = "[AspNetUserStore]",
            Name = "RecoveryCodes",
            Value = initialCodes
        };

        _context.Set<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().Add(token);
        await _context.SaveChangesAsync();

        // Act - simulate using one recovery code (remove it from the list)
        var savedToken = await _context.Set<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>()
            .FirstAsync(t =>
                t.UserId == user.Id &&
                t.LoginProvider == "[AspNetUserStore]" &&
                t.Name == "RecoveryCodes");

        var remainingCodes = "DEF67890;GHI11223;JKL44556;MNO77889"; // ABC12345 was used
        savedToken.Value = remainingCodes;
        await _context.SaveChangesAsync();

        // Assert
        var updated = await _context.Set<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>()
            .FirstOrDefaultAsync(t =>
                t.UserId == user.Id &&
                t.LoginProvider == "[AspNetUserStore]" &&
                t.Name == "RecoveryCodes");

        updated.Should().NotBeNull();
        updated!.Value!.Split(';').Should().HaveCount(4, "one recovery code was consumed");
        updated.Value.Should().NotContain("ABC12345");
    }

    #endregion

    #region 2FA and Lockout Interaction

    [Fact]
    public async Task User_With2FAEnabled_ShouldStillSupportLockout()
    {
        // Arrange - user with both 2FA and lockout enabled
        var user = await TestUserHelper.SeedTestUser(_context, "2fa-lockout@example.com");
        user.TwoFactorEnabled = true;
        user.LockoutEnabled = true;
        user.AccessFailedCount = 5;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(30);

        // Act
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        // Assert - both features should coexist
        var saved = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        saved.Should().NotBeNull();
        saved!.TwoFactorEnabled.Should().BeTrue();
        saved.LockoutEnabled.Should().BeTrue();
        saved.AccessFailedCount.Should().Be(5);
        saved.LockoutEnd.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    #endregion
}
