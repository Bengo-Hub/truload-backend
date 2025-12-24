using Xunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using TruLoad.Backend.Authorization.Handlers;
using TruLoad.Backend.Authorization.Requirements;
using TruLoad.Backend.Models;
using TruLoad.Backend.Services.Interfaces;
using TruLoad.Backend.Services.Interfaces.Authorization;
using TruLoad.Backend.Services.Implementations.Authorization;
using System.Security.Claims;

namespace TruLoad.Backend.Tests.Unit.Authorization;

/// <summary>
/// Unit tests for authorization policies and handlers.
/// Tests PermissionRequirement, PermissionRequirementHandler, and PermissionVerificationService.
/// </summary>
public class PermissionAuthorizationTests
{
    private readonly Mock<IPermissionService> _mockPermissionService;
    private readonly Mock<ILogger<PermissionRequirementHandler>> _mockLogger;
    private readonly PermissionRequirementHandler _handler;
    private readonly PermissionVerificationService _verificationService;

    public PermissionAuthorizationTests()
    {
        _mockPermissionService = new Mock<IPermissionService>();
        _mockLogger = new Mock<ILogger<PermissionRequirementHandler>>();
        _handler = new PermissionRequirementHandler(
            new PermissionVerificationService(_mockPermissionService.Object, new Mock<ILogger<PermissionVerificationService>>().Object),
            _mockLogger.Object);
        _verificationService = new PermissionVerificationService(
            _mockPermissionService.Object,
            new Mock<ILogger<PermissionVerificationService>>().Object);
    }

    #region PermissionRequirement Tests

    [Fact]
    public void PermissionRequirement_SinglePermissionCode_CreatedSuccessfully()
    {
        // Arrange & Act
        var requirement = new PermissionRequirement("user.create");

        // Assert
        Assert.NotNull(requirement);
        Assert.Single(requirement.PermissionCodes);
        Assert.Contains("user.create", requirement.PermissionCodes);
        Assert.Equal(PermissionRequirementType.All, requirement.RequirementType);
    }

    [Fact]
    public void PermissionRequirement_MultiplePermissionCodes_CreatedWithAnyType()
    {
        // Arrange & Act
        var codes = new[] { "user.create", "user.update" };
        var requirement = new PermissionRequirement(codes, PermissionRequirementType.Any);

        // Assert
        Assert.NotNull(requirement);
        Assert.Equal(2, requirement.PermissionCodes.Count());
        Assert.Equal(PermissionRequirementType.Any, requirement.RequirementType);
    }

    [Fact]
    public void PermissionRequirement_MultiplePermissionCodes_CreatedWithAllType()
    {
        // Arrange & Act
        var codes = new[] { "user.create", "user.approve" };
        var requirement = new PermissionRequirement(codes, PermissionRequirementType.All);

        // Assert
        Assert.NotNull(requirement);
        Assert.Equal(2, requirement.PermissionCodes.Count());
        Assert.Equal(PermissionRequirementType.All, requirement.RequirementType);
    }

    [Fact]
    public void PermissionRequirement_NullOrWhitespaceCode_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new PermissionRequirement(null!));
        Assert.Throws<ArgumentException>(() => new PermissionRequirement(""));
        Assert.Throws<ArgumentException>(() => new PermissionRequirement("   "));
    }

    [Fact]
    public void PermissionRequirement_EmptyCodeCollection_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            new PermissionRequirement(Array.Empty<string>(), PermissionRequirementType.Any));
    }

    #endregion

    #region PermissionVerificationService Tests

    [Fact]
    public async Task UserHasPermissionAsync_UserWithPermission_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var permissionCode = "user.create";

        var httpContext = CreateHttpContextWithClaims(userId, roleId);
        var permissions = new[] { new Permission { Id = Guid.NewGuid(), Code = permissionCode, Name = "Create User" } };

        _mockPermissionService
            .Setup(x => x.GetPermissionsForRoleAsync(roleId, default))
            .ReturnsAsync(permissions);

        // Act
        var result = await _verificationService.UserHasPermissionAsync(httpContext, permissionCode);

        // Assert
        Assert.True(result);
        _mockPermissionService.Verify(x => x.GetPermissionsForRoleAsync(roleId, default), Times.Once);
    }

    [Fact]
    public async Task UserHasPermissionAsync_UserWithoutPermission_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var requestedCode = "user.create";
        var availableCode = "user.view";

        var httpContext = CreateHttpContextWithClaims(userId, roleId);
        var permissions = new[] { new Permission { Id = Guid.NewGuid(), Code = availableCode, Name = "View User" } };

        _mockPermissionService
            .Setup(x => x.GetPermissionsForRoleAsync(roleId, default))
            .ReturnsAsync(permissions);

        // Act
        var result = await _verificationService.UserHasPermissionAsync(httpContext, requestedCode);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UserHasPermissionAsync_NoRoleInClaims_ReturnsFalse()
    {
        // Arrange
        var httpContext = CreateHttpContextWithClaims(Guid.NewGuid(), null);

        // Act
        var result = await _verificationService.UserHasPermissionAsync(httpContext, "user.create");

        // Assert
        Assert.False(result);
        _mockPermissionService.Verify(x => x.GetPermissionsForRoleAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task UserHasAnyPermissionAsync_UserWithOneRequiredPermission_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var requiredCodes = new[] { "user.create", "user.update" };

        var httpContext = CreateHttpContextWithClaims(userId, roleId);
        var permissions = new[] { new Permission { Id = Guid.NewGuid(), Code = "user.update", Name = "Update User" } };

        _mockPermissionService
            .Setup(x => x.GetPermissionsForRoleAsync(roleId, default))
            .ReturnsAsync(permissions);

        // Act
        var result = await _verificationService.UserHasAnyPermissionAsync(httpContext, requiredCodes);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task UserHasAnyPermissionAsync_UserWithoutAnyRequiredPermission_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var requiredCodes = new[] { "user.create", "user.update" };

        var httpContext = CreateHttpContextWithClaims(userId, roleId);
        var permissions = new[] { new Permission { Id = Guid.NewGuid(), Code = "user.view", Name = "View User" } };

        _mockPermissionService
            .Setup(x => x.GetPermissionsForRoleAsync(roleId, default))
            .ReturnsAsync(permissions);

        // Act
        var result = await _verificationService.UserHasAnyPermissionAsync(httpContext, requiredCodes);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UserHasAllPermissionsAsync_UserWithAllRequiredPermissions_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var requiredCodes = new[] { "user.create", "user.approve" };

        var httpContext = CreateHttpContextWithClaims(userId, roleId);
        var permissions = new[]
        {
            new Permission { Id = Guid.NewGuid(), Code = "user.create", Name = "Create User" },
            new Permission { Id = Guid.NewGuid(), Code = "user.approve", Name = "Approve User" }
        };

        _mockPermissionService
            .Setup(x => x.GetPermissionsForRoleAsync(roleId, default))
            .ReturnsAsync(permissions);

        // Act
        var result = await _verificationService.UserHasAllPermissionsAsync(httpContext, requiredCodes);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task UserHasAllPermissionsAsync_UserMissingOnePermission_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var requiredCodes = new[] { "user.create", "user.approve" };

        var httpContext = CreateHttpContextWithClaims(userId, roleId);
        var permissions = new[] { new Permission { Id = Guid.NewGuid(), Code = "user.create", Name = "Create User" } };

        _mockPermissionService
            .Setup(x => x.GetPermissionsForRoleAsync(roleId, default))
            .ReturnsAsync(permissions);

        // Act
        var result = await _verificationService.UserHasAllPermissionsAsync(httpContext, requiredCodes);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetUserPermissionsAsync_PermissionsRetrieved_CachedInRequestItems()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithClaims(userId, roleId);
        var permissions = new[]
        {
            new Permission { Id = Guid.NewGuid(), Code = "user.create", Name = "Create User" },
            new Permission { Id = Guid.NewGuid(), Code = "user.view", Name = "View User" }
        };

        _mockPermissionService
            .Setup(x => x.GetPermissionsForRoleAsync(roleId, default))
            .ReturnsAsync(permissions);

        // Act
        var result1 = await _verificationService.GetUserPermissionsAsync(httpContext);
        var result2 = await _verificationService.GetUserPermissionsAsync(httpContext); // Should use cache

        // Assert
        Assert.Equal(2, result1.Count());
        Assert.Equal(result1, result2);
        _mockPermissionService.Verify(x => x.GetPermissionsForRoleAsync(roleId, default), Times.Once); // Called only once
    }

    [Fact]
    public async Task GetUserPermissionsAsync_InvalidRoleId_ReturnsEmptyEnumerable()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithClaims(userId, "invalid-guid");

        // Act
        var result = await _verificationService.GetUserPermissionsAsync(httpContext);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetUserIdFromClaims_ValidClaims_ReturnsUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var httpContext = CreateHttpContextWithClaims(userId, null);

        // Act
        var result = _verificationService.GetUserIdFromClaims(httpContext);

        // Assert
        Assert.Equal(userId.ToString(), result);
    }

    [Fact]
    public void GetUserIdFromClaims_NoClaims_ReturnsNull()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();

        // Act
        var result = _verificationService.GetUserIdFromClaims(httpContext);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region PermissionRequirementHandler Tests

    [Fact]
    public async Task HandleRequirementAsync_AuthenticatedUserWithPermission_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var permissionCode = "user.create";

        var httpContext = CreateHttpContextWithClaims(userId, roleId);
        var requirement = new PermissionRequirement(permissionCode);
        var permissions = new[] { new Permission { Id = Guid.NewGuid(), Code = permissionCode, Name = "Create User" } };

        _mockPermissionService
            .Setup(x => x.GetPermissionsForRoleAsync(roleId, default))
            .ReturnsAsync(permissions);

        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            CreatePrincipalWithClaims(userId, roleId),
            httpContext);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_AuthenticatedUserWithoutPermission_Fails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var permissionCode = "user.create";

        var httpContext = CreateHttpContextWithClaims(userId, roleId);
        var requirement = new PermissionRequirement(permissionCode);
        var permissions = new[] { new Permission { Id = Guid.NewGuid(), Code = "user.view", Name = "View User" } };

        _mockPermissionService
            .Setup(x => x.GetPermissionsForRoleAsync(roleId, default))
            .ReturnsAsync(permissions);

        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            CreatePrincipalWithClaims(userId, roleId),
            httpContext);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_UnauthenticatedUser_Fails()
    {
        // Arrange
        var requirement = new PermissionRequirement("user.create");
        var httpContext = new DefaultHttpContext();
        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            new ClaimsPrincipal(),
            httpContext);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_AnyPermissionLogic_SucceedsWithOneMatch()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var permissionCodes = new[] { "user.create", "user.update" };

        var httpContext = CreateHttpContextWithClaims(userId, roleId);
        var requirement = new PermissionRequirement(permissionCodes, PermissionRequirementType.Any);
        var permissions = new[] { new Permission { Id = Guid.NewGuid(), Code = "user.update", Name = "Update User" } };

        _mockPermissionService
            .Setup(x => x.GetPermissionsForRoleAsync(roleId, default))
            .ReturnsAsync(permissions);

        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            CreatePrincipalWithClaims(userId, roleId),
            httpContext);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirementAsync_AllPermissionLogic_SucceedsWithAllMatches()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var permissionCodes = new[] { "user.create", "user.approve" };

        var httpContext = CreateHttpContextWithClaims(userId, roleId);
        var requirement = new PermissionRequirement(permissionCodes, PermissionRequirementType.All);
        var permissions = new[]
        {
            new Permission { Id = Guid.NewGuid(), Code = "user.create", Name = "Create User" },
            new Permission { Id = Guid.NewGuid(), Code = "user.approve", Name = "Approve User" }
        };

        _mockPermissionService
            .Setup(x => x.GetPermissionsForRoleAsync(roleId, default))
            .ReturnsAsync(permissions);

        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            CreatePrincipalWithClaims(userId, roleId),
            httpContext);

        // Act
        await _handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    #endregion

    #region Helper Methods

    private DefaultHttpContext CreateHttpContextWithClaims(Guid? userId, Guid? roleId)
    {
        var claims = new List<Claim>();
        if (userId.HasValue)
            claims.Add(new Claim("auth_service_user_id", userId.Value.ToString()));
        if (roleId.HasValue)
            claims.Add(new Claim("role_id", roleId.Value.ToString()));

        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext();
        httpContext.User = principal;
        return httpContext;
    }

    private DefaultHttpContext CreateHttpContextWithClaims(Guid userId, string? roleId)
    {
        var claims = new List<Claim> { new Claim("auth_service_user_id", userId.ToString()) };
        if (!string.IsNullOrEmpty(roleId))
            claims.Add(new Claim("role_id", roleId));

        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext();
        httpContext.User = principal;
        return httpContext;
    }

    private ClaimsPrincipal CreatePrincipalWithClaims(Guid userId, Guid roleId)
    {
        var claims = new List<Claim>
        {
            new Claim("auth_service_user_id", userId.ToString()),
            new Claim("role_id", roleId.ToString())
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    #endregion
}
