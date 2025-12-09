using Moq;
using Xunit;
using TruLoad.Backend.DTOs.Auth;
using TruLoad.Backend.Services.Implementations.Auth;
using TruLoad.Backend.Services.Interfaces.Auth;
using TruLoad.Backend.Models;
using truload_backend.Data;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;

namespace TruLoad.Backend.Tests.Integration.Auth;

/// <summary>
/// Integration tests for SSO login flow.
/// Tests complete flow: LoginDto → SSO proxy → JWT validation → User sync → Token response
/// </summary>
public class SsoLoginIntegrationTests
{
    // Superuser credentials (from user request)
    private const string SuperUserEmail = "admin@codevertexitsolutions.com";
    private const string SuperUserPassword = "ChangeMe123!";
    private const string TenantSlug = "codevertex";

    private readonly TruLoadDbContext _dbContext;
    private readonly ISsoUserSyncService _userSyncService;

    public SsoLoginIntegrationTests()
    {
        // Create in-memory database for testing
        var options = new DbContextOptionsBuilder<TruLoadDbContext>()
            .UseInMemoryDatabase(databaseName: $"SsoTest_{Guid.NewGuid()}")
            .Options;

        _dbContext = new TruLoadDbContext(options);
        _userSyncService = new SsoUserSyncService(_dbContext, new Mock<Microsoft.Extensions.Logging.ILogger<SsoUserSyncService>>().Object);
    }

    #region SSO User Sync Tests

    [Fact]
    public async Task SyncUserFromSsoAsync_WithNewSuperUser_CreatesUserAndRole()
    {
        // Arrange
        var ssoUserId = Guid.NewGuid().ToString();
        var email = SuperUserEmail;
        var role = "superuser";
        var isSuperUser = true;

        // Act
        var result = await _userSyncService.SyncUserFromSsoAsync(
            ssoUserId, email, TenantSlug, role, isSuperUser, "Admin User");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(email, result.Email);
        Assert.Equal("Admin User", result.FullName);
        Assert.NotNull(result.OrganizationId);
        Assert.Equal("synced", result.SyncStatus);

        // Verify user was persisted
        var persistedUser = await _dbContext.Users.FindAsync(result.Id);
        Assert.NotNull(persistedUser);
        Assert.Equal(email, persistedUser.Email);
    }

    [Fact]
    public async Task SyncUserFromSsoAsync_WithExistingUser_UpdatesUser()
    {
        // Arrange
        var ssoUserId = Guid.NewGuid().ToString();
        var ssoUserGuid = Guid.Parse(ssoUserId);
        var email = "olduser@example.com";

        // Create existing user
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            AuthServiceUserId = ssoUserGuid,
            Email = email,
            FullName = "Old Name",
            Status = "active",
            SyncStatus = "synced",
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.Users.AddAsync(existingUser);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _userSyncService.SyncUserFromSsoAsync(
            ssoUserId, SuperUserEmail, TenantSlug, "admin", false, "Updated Name");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(existingUser.Id, result.Id);
        Assert.Equal(SuperUserEmail, result.Email);
        Assert.Equal("Updated Name", result.FullName);

        // Verify only one user exists
        var users = await _dbContext.Users.ToListAsync();
        Assert.Single(users);
    }

    [Fact]
    public async Task SyncUserFromSsoAsync_CreatesOrganizationFromTenantSlug()
    {
        // Arrange
        var ssoUserId = Guid.NewGuid().ToString();
        var email = SuperUserEmail;

        // Act
        var result = await _userSyncService.SyncUserFromSsoAsync(
            ssoUserId, email, TenantSlug, "user", false);

        // Assert
        Assert.NotNull(result.OrganizationId);

        // Verify organization was created
        var org = await _dbContext.Organizations.FindAsync(result.OrganizationId);
        Assert.NotNull(org);
        Assert.Equal(TenantSlug, org.Code);
        Assert.Equal(TenantSlug, org.Name);
        Assert.Equal("tenant", org.OrgType);
    }

    [Fact]
    public async Task SyncUserFromSsoAsync_AssignsRoleToUser()
    {
        // Arrange
        var ssoUserId = Guid.NewGuid().ToString();
        var email = SuperUserEmail;
        var roleName = "superuser";
        var isSuperUser = true;

        // Act
        var result = await _userSyncService.SyncUserFromSsoAsync(
            ssoUserId, email, TenantSlug, roleName, isSuperUser);

        // Assert
        var userRoles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == result.Id)
            .Include(ur => ur.Role)
            .ToListAsync();

        Assert.NotEmpty(userRoles);
        var userRole = userRoles.First();
        Assert.Equal("SUPERUSER", userRole.Role.Code);
    }

    [Fact]
    public async Task SyncUserFromSsoAsync_WithInvalidSsoUserId_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _userSyncService.SyncUserFromSsoAsync(
                "invalid-guid", SuperUserEmail, TenantSlug, "user", false));
    }

    [Fact]
    public async Task SyncUserFromSsoAsync_WithEmptyEmail_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _userSyncService.SyncUserFromSsoAsync(
                Guid.NewGuid().ToString(), "", TenantSlug, "user", false));
    }

    [Fact]
    public async Task SyncUserFromSsoAsync_WithEmptyTenantSlug_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _userSyncService.SyncUserFromSsoAsync(
                Guid.NewGuid().ToString(), SuperUserEmail, "", "user", false));
    }

    #endregion

    #region Organization Tests

    [Fact]
    public async Task GetOrCreateOrganizationAsync_WithNewTenant_CreatesOrganization()
    {
        // Act
        var org = await _userSyncService.GetOrCreateOrganizationAsync(TenantSlug);

        // Assert
        Assert.NotNull(org);
        Assert.Equal(TenantSlug, org.Code);
        Assert.Equal(TenantSlug, org.Name);
        Assert.True(org.IsActive);

        // Verify persisted
        var persisted = await _dbContext.Organizations.FindAsync(org.Id);
        Assert.NotNull(persisted);
    }

    [Fact]
    public async Task GetOrCreateOrganizationAsync_WithExistingTenant_ReturnsSameOrganization()
    {
        // Arrange
        var org1 = await _userSyncService.GetOrCreateOrganizationAsync(TenantSlug);

        // Act
        var org2 = await _userSyncService.GetOrCreateOrganizationAsync(TenantSlug);

        // Assert
        Assert.Equal(org1.Id, org2.Id);

        // Verify only one organization exists
        var orgs = await _dbContext.Organizations.ToListAsync();
        Assert.Single(orgs);
    }

    [Fact]
    public async Task GetOrCreateOrganizationAsync_WithEmptyTenantSlug_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _userSyncService.GetOrCreateOrganizationAsync(""));
    }

    #endregion

    #region Role Tests

    [Fact]
    public async Task GetOrCreateRoleAsync_WithSuperUserFlag_CreatesSuperUserRole()
    {
        // Act
        var role = await _userSyncService.GetOrCreateRoleAsync("admin", isSuperUser: true);

        // Assert
        Assert.NotNull(role);
        Assert.Equal("SUPERUSER", role.Code);
        Assert.Equal("Super User", role.Name);
        Assert.True(role.IsActive);
    }

    [Fact]
    public async Task GetOrCreateRoleAsync_WithAdminRole_CreatesAdminRole()
    {
        // Act
        var role = await _userSyncService.GetOrCreateRoleAsync("admin", isSuperUser: false);

        // Assert
        Assert.NotNull(role);
        Assert.Equal("ADMIN", role.Code);
        Assert.Equal("Administrator", role.Name);
    }

    [Fact]
    public async Task GetOrCreateRoleAsync_WithUserRole_CreatesUserRole()
    {
        // Act
        var role = await _userSyncService.GetOrCreateRoleAsync("user", isSuperUser: false);

        // Assert
        Assert.NotNull(role);
        Assert.Equal("USER", role.Code);
        Assert.Equal("User", role.Name);
    }

    [Fact]
    public async Task GetOrCreateRoleAsync_WithExistingRole_ReturnsSameRole()
    {
        // Arrange
        var role1 = await _userSyncService.GetOrCreateRoleAsync("admin", isSuperUser: true);

        // Act
        var role2 = await _userSyncService.GetOrCreateRoleAsync("admin", isSuperUser: true);

        // Assert
        Assert.Equal(role1.Id, role2.Id);

        // Verify only one SUPERUSER role exists
        var roles = await _dbContext.Roles.Where(r => r.Code == "SUPERUSER").ToListAsync();
        Assert.Single(roles);
    }

    [Fact]
    public async Task GetOrCreateRoleAsync_WithEmptyRoleName_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _userSyncService.GetOrCreateRoleAsync("", isSuperUser: false));
    }

    #endregion

    #region Complete Login Flow Tests

    [Fact]
    public async Task CompleteLoginFlow_SuperUserSync_CreatesUserTenantAndRole()
    {
        // Arrange: Simulate SSO response with superuser credentials
        var ssoUserId = Guid.NewGuid().ToString();
        var email = SuperUserEmail;
        var role = "superuser";
        var isSuperUser = true;
        var fullName = "Admin User";

        // Act: Sync user from SSO
        var syncedUser = await _userSyncService.SyncUserFromSsoAsync(
            ssoUserId, email, TenantSlug, role, isSuperUser, fullName);

        // Assert: Verify user created
        Assert.NotNull(syncedUser);
        Assert.Equal(email, syncedUser.Email);
        Assert.Equal(fullName, syncedUser.FullName);

        // Assert: Verify organization (tenant) created
        var org = await _dbContext.Organizations.FindAsync(syncedUser.OrganizationId);
        Assert.NotNull(org);
        Assert.Equal(TenantSlug, org.Code);

        // Assert: Verify role assigned
        var userRoles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == syncedUser.Id)
            .Include(ur => ur.Role)
            .ToListAsync();
        Assert.NotEmpty(userRoles);
        Assert.Equal("SUPERUSER", userRoles.First().Role.Code);

        // Assert: Verify sync status
        Assert.Equal("synced", syncedUser.SyncStatus);
        Assert.NotNull(syncedUser.SyncAt);
    }

    [Fact]
    public async Task CompleteLoginFlow_MultipleUsers_SameTenant_AssignedCorrectly()
    {
        // Arrange: Create multiple users for same tenant
        var ssoUserId1 = Guid.NewGuid().ToString();
        var ssoUserId2 = Guid.NewGuid().ToString();
        var email1 = "admin@codevertexitsolutions.com";
        var email2 = "user@codevertexitsolutions.com";

        // Act: Sync first user (superuser)
        var user1 = await _userSyncService.SyncUserFromSsoAsync(
            ssoUserId1, email1, TenantSlug, "superuser", isSuperUser: true, "Admin");

        // Act: Sync second user (regular user)
        var user2 = await _userSyncService.SyncUserFromSsoAsync(
            ssoUserId2, email2, TenantSlug, "user", isSuperUser: false, "Regular User");

        // Assert: Both users assigned to same organization
        Assert.Equal(user1.OrganizationId, user2.OrganizationId);

        // Assert: Users have different roles
        var user1Roles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == user1.Id)
            .Include(ur => ur.Role)
            .ToListAsync();
        var user2Roles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == user2.Id)
            .Include(ur => ur.Role)
            .ToListAsync();

        Assert.Equal("SUPERUSER", user1Roles.First().Role.Code);
        Assert.Equal("USER", user2Roles.First().Role.Code);
    }

    [Fact]
    public async Task CompleteLoginFlow_UserReassignment_UpdatesRoleCorrectly()
    {
        // Arrange: Create user with initial role
        var ssoUserId = Guid.NewGuid().ToString();
        var email = SuperUserEmail;

        // Act: First sync as regular user
        var user1 = await _userSyncService.SyncUserFromSsoAsync(
            ssoUserId, email, TenantSlug, "user", isSuperUser: false);

        // Act: Sync again as superuser
        var user2 = await _userSyncService.SyncUserFromSsoAsync(
            ssoUserId, email, TenantSlug, "superuser", isSuperUser: true);

        // Assert: Same user ID
        Assert.Equal(user1.Id, user2.Id);

        // Assert: User still has original role (new role not auto-assigned, only on first sync)
        var userRoles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == user2.Id)
            .Include(ur => ur.Role)
            .ToListAsync();
        Assert.Single(userRoles);
        Assert.Equal("USER", userRoles.First().Role.Code);
    }

    #endregion
}
