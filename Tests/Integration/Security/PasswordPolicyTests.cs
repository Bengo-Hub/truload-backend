using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Tests.Integration.Helpers;
using Xunit;
using FluentAssertions;

namespace TruLoad.Backend.Tests.Integration.Security;

/// <summary>
/// Integration tests for password policy enforcement at the database/model level.
/// Validates that ApplicationUser identity properties related to password security
/// (hashing, lockout, failed attempts) persist correctly in the data layer.
/// </summary>
public class PasswordPolicyTests : IAsyncLifetime
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

    #region Password Hash Storage

    [Fact]
    public async Task User_WithValidPassword_ShouldSave()
    {
        // Arrange - create user with a password hash (simulating Identity password storage)
        var user = TestUserHelper.CreateTestUser("strongpass@example.com");
        user.PasswordHash = "AQAAAAIAAYagAAAAEFakeHashedPasswordForTestingPurposes==";

        // Act
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        saved.Should().NotBeNull();
        saved!.Email.Should().Be("strongpass@example.com");
    }

    [Fact]
    public async Task User_PasswordHash_ShouldBeStored()
    {
        // Arrange
        var user = TestUserHelper.CreateTestUser("hashcheck@example.com");
        var expectedHash = "AQAAAAIAAYagAAAAEFakeHashedPasswordValue123456789==";
        user.PasswordHash = expectedHash;

        // Act
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Assert - verify PasswordHash is persisted and not null
        var saved = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        saved.Should().NotBeNull();
        saved!.PasswordHash.Should().NotBeNullOrEmpty();
        saved.PasswordHash.Should().Be(expectedHash);
    }

    [Fact]
    public async Task User_WithoutPasswordHash_ShouldSaveWithNullHash()
    {
        // Arrange - user created without setting PasswordHash
        var user = TestUserHelper.CreateTestUser("nopass@example.com");
        // PasswordHash defaults to null in IdentityUser

        // Act
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        saved.Should().NotBeNull();
        saved!.PasswordHash.Should().BeNull("new user without password set should have null PasswordHash");
    }

    #endregion

    #region Lockout Configuration

    [Fact]
    public async Task User_LockoutEnabled_ShouldDefaultToFalse_ForNewIdentityUser()
    {
        // Arrange - IdentityUser defaults LockoutEnabled to false;
        // the application must explicitly enable it during registration
        var user = TestUserHelper.CreateTestUser("lockout-default@example.com");

        // Act
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Assert - IdentityUser<Guid>.LockoutEnabled defaults to false
        var saved = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        saved.Should().NotBeNull();
        saved!.LockoutEnabled.Should().BeFalse(
            "IdentityUser defaults LockoutEnabled to false; enable explicitly during registration");
    }

    [Fact]
    public async Task User_LockoutEnabled_ShouldPersistWhenSetToTrue()
    {
        // Arrange - simulate enabling lockout for a user during registration
        var user = TestUserHelper.CreateTestUser("lockout-enabled@example.com");
        user.LockoutEnabled = true;

        // Act
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        saved.Should().NotBeNull();
        saved!.LockoutEnabled.Should().BeTrue();
    }

    #endregion

    #region Failed Login Tracking

    [Fact]
    public async Task User_FailedLoginCount_ShouldIncrement()
    {
        // Arrange
        var user = await TestUserHelper.SeedTestUser(_context, "failtrack@example.com");
        user.AccessFailedCount.Should().Be(0, "new user starts with zero failed attempts");

        // Act - simulate failed login attempts
        user.AccessFailedCount = 1;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        user.AccessFailedCount = 2;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        user.AccessFailedCount = 3;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        saved.Should().NotBeNull();
        saved!.AccessFailedCount.Should().Be(3);
    }

    [Fact]
    public async Task User_AfterMaxFailedAttempts_ShouldBeLocked()
    {
        // Arrange - simulate reaching lockout threshold (e.g., 5 attempts)
        var user = await TestUserHelper.SeedTestUser(_context, "locked@example.com");
        user.LockoutEnabled = true;

        const int maxFailedAttempts = 5;
        user.AccessFailedCount = maxFailedAttempts;

        // Set lockout end to 30 minutes from now (simulating what Identity would do)
        var lockoutUntil = DateTimeOffset.UtcNow.AddMinutes(30);
        user.LockoutEnd = lockoutUntil;

        // Act
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        saved.Should().NotBeNull();
        saved!.AccessFailedCount.Should().Be(maxFailedAttempts);
        saved.LockoutEnd.Should().NotBeNull();
        saved.LockoutEnd.Should().BeAfter(DateTimeOffset.UtcNow, "lockout should be in the future");
    }

    [Fact]
    public async Task User_LockoutEnd_ShouldPreventAccess_WhenInFuture()
    {
        // Arrange - user is currently locked out
        var user = await TestUserHelper.SeedTestUser(_context, "currentlylocked@example.com");
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);

        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        // Act - check lockout status
        var saved = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);

        // Assert - user is currently locked out
        saved.Should().NotBeNull();
        saved!.LockoutEnd.Should().NotBeNull();
        var isLockedOut = saved.LockoutEnabled && saved.LockoutEnd > DateTimeOffset.UtcNow;
        isLockedOut.Should().BeTrue("user with future LockoutEnd and LockoutEnabled should be considered locked out");
    }

    [Fact]
    public async Task User_LockoutEnd_ShouldAllowAccess_WhenExpired()
    {
        // Arrange - user's lockout period has expired
        var user = await TestUserHelper.SeedTestUser(_context, "unlocked@example.com");
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-5); // 5 minutes in the past

        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        // Act
        var saved = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);

        // Assert - lockout has expired
        saved.Should().NotBeNull();
        var isLockedOut = saved!.LockoutEnabled && saved.LockoutEnd > DateTimeOffset.UtcNow;
        isLockedOut.Should().BeFalse("user with past LockoutEnd should no longer be locked out");
    }

    [Fact]
    public async Task User_AccessFailedCount_ShouldResetAfterSuccessfulLogin()
    {
        // Arrange - user had previous failed attempts
        var user = await TestUserHelper.SeedTestUser(_context, "resetcount@example.com");
        user.AccessFailedCount = 3;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        // Act - simulate successful login resetting the counter
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        saved.Should().NotBeNull();
        saved!.AccessFailedCount.Should().Be(0);
        saved.LockoutEnd.Should().BeNull();
    }

    #endregion

    #region Security Stamp

    [Fact]
    public async Task User_SecurityStamp_ShouldBeSetOnCreation()
    {
        // Arrange & Act
        var user = await TestUserHelper.SeedTestUser(_context, "secstamp@example.com");

        // Assert - SecurityStamp should be set (used for invalidating cookies on password change)
        var saved = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        saved.Should().NotBeNull();
        saved!.SecurityStamp.Should().NotBeNullOrEmpty(
            "SecurityStamp is used to invalidate auth tokens on password/security changes");
    }

    [Fact]
    public async Task User_SecurityStamp_ShouldUpdateOnPasswordChange()
    {
        // Arrange
        var user = await TestUserHelper.SeedTestUser(_context, "stampchange@example.com");
        var originalStamp = user.SecurityStamp;

        // Act - simulate password change by updating SecurityStamp
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.PasswordHash = "AQAAAAIAAYagAAAAENewHashedPassword==";
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        saved.Should().NotBeNull();
        saved!.SecurityStamp.Should().NotBe(originalStamp,
            "SecurityStamp should change when password is updated");
    }

    #endregion
}
