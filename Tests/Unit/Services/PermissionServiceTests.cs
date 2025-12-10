using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Auth.Interfaces;
using TruLoad.Backend.Services.Implementations;
using truload_backend.Data;
using Xunit;
using FluentAssertions;
using System.Text.Json;

namespace truload_backend.Tests.Unit.Services;

/// <summary>
/// Unit tests for PermissionService.
/// Tests caching behavior and service methods with mocked cache and repository.
/// </summary>
public class PermissionServiceTests
{
    private TruLoadDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<TruLoadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new TruLoadDbContext(options);
    }

    #region GetPermissionByIdAsync Tests

    [Fact]
    public async Task GetPermissionByIdAsync_WithValidId_ReturnsPermission()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var permission = new Permission { Id = Guid.NewGuid(), Code = "test", Name = "Test", Category = "Test", IsActive = true };
        context.Permissions.Add(permission);
        await context.SaveChangesAsync();

        var mockCache = new Mock<IDistributedCache>();
        var repository = new PermissionRepository(context);
        var service = new PermissionService(repository, mockCache.Object);

        // Act
        var result = await service.GetPermissionByIdAsync(permission.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(permission.Id);
    }

    [Fact]
    public async Task GetPermissionByIdAsync_WithEmptyGuid_ReturnsNull()
    {
        // Arrange
        var mockCache = new Mock<IDistributedCache>();
        var mockRepository = new Mock<PermissionRepository>(new Mock<TruLoadDbContext>().Object);
        var service = new PermissionService(mockRepository.Object, mockCache.Object);

        // Act
        var result = await service.GetPermissionByIdAsync(Guid.Empty);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetPermissionByCodeAsync Tests

    [Fact]
    public async Task GetPermissionByCodeAsync_OnCacheHit_ReturnsCachedPermission()
    {
        // Arrange
        var permission = new Permission { Id = Guid.NewGuid(), Code = "test.code", Name = "Test", Category = "Test", IsActive = true };
        var cacheKey = "perm:code:test.code";
        var cachedJson = JsonSerializer.Serialize(permission);

        var mockCache = new Mock<IDistributedCache>();
        mockCache.Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedJson);

        var mockRepository = new Mock<IPermissionRepository>();
        var service = new PermissionService(mockRepository.Object, mockCache.Object);

        // Act
        var result = await service.GetPermissionByCodeAsync("test.code");

        // Assert
        result.Should().NotBeNull();
        result!.Code.Should().Be("test.code");
        mockRepository.Verify(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPermissionByCodeAsync_OnCacheMiss_FetchesFromRepository()
    {
        // Arrange
        var permission = new Permission { Id = Guid.NewGuid(), Code = "test.code", Name = "Test", Category = "Test", IsActive = true };
        var cacheKey = "perm:code:test.code";

        var mockCache = new Mock<IDistributedCache>();
        mockCache.Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        mockCache.Setup(c => c.SetStringAsync(cacheKey, It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockRepository = new Mock<IPermissionRepository>();
        mockRepository.Setup(r => r.GetByCodeAsync("test.code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(permission);

        var service = new PermissionService(mockRepository.Object, mockCache.Object);

        // Act
        var result = await service.GetPermissionByCodeAsync("test.code");

        // Assert
        result.Should().NotBeNull();
        result!.Code.Should().Be("test.code");
        mockRepository.Verify(r => r.GetByCodeAsync("test.code", It.IsAny<CancellationToken>()), Times.Once);
        mockCache.Verify(c => c.SetStringAsync(cacheKey, It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPermissionByCodeAsync_WithNullCode_ReturnsNull()
    {
        // Arrange
        var mockCache = new Mock<IDistributedCache>();
        var mockRepository = new Mock<IPermissionRepository>();
        var service = new PermissionService(mockRepository.Object, mockCache.Object);

        // Act
        var result = await service.GetPermissionByCodeAsync(null!);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetPermissionsByCategoryAsync Tests

    [Fact]
    public async Task GetPermissionsByCategoryAsync_OnCacheHit_ReturnsCachedList()
    {
        // Arrange
        var permissions = new List<Permission>
        {
            new Permission { Id = Guid.NewGuid(), Code = "p1", Name = "P1", Category = "Weighing", IsActive = true },
            new Permission { Id = Guid.NewGuid(), Code = "p2", Name = "P2", Category = "Weighing", IsActive = true }
        };
        var cacheKey = "perm:category:Weighing";
        var cachedJson = JsonSerializer.Serialize(permissions);

        var mockCache = new Mock<IDistributedCache>();
        mockCache.Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedJson);

        var mockRepository = new Mock<IPermissionRepository>();
        var service = new PermissionService(mockRepository.Object, mockCache.Object);

        // Act
        var result = await service.GetPermissionsByCategoryAsync("Weighing");

        // Assert
        result.Should().HaveCount(2);
        mockRepository.Verify(r => r.GetByCategoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPermissionsByCategoryAsync_OnCacheMiss_FetchesFromRepository()
    {
        // Arrange
        var permissions = new List<Permission>
        {
            new Permission { Id = Guid.NewGuid(), Code = "p1", Name = "P1", Category = "Weighing", IsActive = true }
        };
        var cacheKey = "perm:category:Weighing";

        var mockCache = new Mock<IDistributedCache>();
        mockCache.Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        mockCache.Setup(c => c.SetStringAsync(cacheKey, It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockRepository = new Mock<IPermissionRepository>();
        mockRepository.Setup(r => r.GetByCategoryAsync("Weighing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);

        var service = new PermissionService(mockRepository.Object, mockCache.Object);

        // Act
        var result = await service.GetPermissionsByCategoryAsync("Weighing");

        // Assert
        result.Should().HaveCount(1);
        mockRepository.Verify(r => r.GetByCategoryAsync("Weighing", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetAllActivePermissionsAsync Tests

    [Fact]
    public async Task GetAllActivePermissionsAsync_OnCacheHit_ReturnsCachedList()
    {
        // Arrange
        var permissions = new List<Permission>
        {
            new Permission { Id = Guid.NewGuid(), Code = "p1", Name = "P1", Category = "Test", IsActive = true }
        };
        var cachedJson = JsonSerializer.Serialize(permissions);

        var mockCache = new Mock<IDistributedCache>();
        mockCache.Setup(c => c.GetStringAsync("perm:active:all", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedJson);

        var mockRepository = new Mock<IPermissionRepository>();
        var service = new PermissionService(mockRepository.Object, mockCache.Object);

        // Act
        var result = await service.GetAllActivePermissionsAsync();

        // Assert
        result.Should().HaveCount(1);
        mockRepository.Verify(r => r.GetActiveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAllActivePermissionsAsync_OnCacheMiss_FetchesFromRepository()
    {
        // Arrange
        var permissions = new List<Permission>
        {
            new Permission { Id = Guid.NewGuid(), Code = "p1", Name = "P1", Category = "Test", IsActive = true }
        };

        var mockCache = new Mock<IDistributedCache>();
        mockCache.Setup(c => c.GetStringAsync("perm:active:all", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        mockCache.Setup(c => c.SetStringAsync("perm:active:all", It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockRepository = new Mock<IPermissionRepository>();
        mockRepository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);

        var service = new PermissionService(mockRepository.Object, mockCache.Object);

        // Act
        var result = await service.GetAllActivePermissionsAsync();

        // Assert
        result.Should().HaveCount(1);
        mockRepository.Verify(r => r.GetActiveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetAllPermissionsAsync Tests

    [Fact]
    public async Task GetAllPermissionsAsync_OnCacheHit_ReturnsCachedList()
    {
        // Arrange
        var permissions = new List<Permission>
        {
            new Permission { Id = Guid.NewGuid(), Code = "active", Name = "Active", Category = "Test", IsActive = true },
            new Permission { Id = Guid.NewGuid(), Code = "inactive", Name = "Inactive", Category = "Test", IsActive = false }
        };
        var cachedJson = JsonSerializer.Serialize(permissions);

        var mockCache = new Mock<IDistributedCache>();
        mockCache.Setup(c => c.GetStringAsync("perm:all", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedJson);

        var mockRepository = new Mock<IPermissionRepository>();
        var service = new PermissionService(mockRepository.Object, mockCache.Object);

        // Act
        var result = await service.GetAllPermissionsAsync();

        // Assert
        result.Should().HaveCount(2);
        mockRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region GetPermissionsForRoleAsync Tests

    [Fact]
    public async Task GetPermissionsForRoleAsync_OnCacheMiss_FetchesAndCaches()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var permissions = new List<Permission>
        {
            new Permission { Id = Guid.NewGuid(), Code = "p1", Name = "P1", Category = "Test", IsActive = true }
        };
        var cacheKey = $"perm:role:{roleId}";

        var mockCache = new Mock<IDistributedCache>();
        mockCache.Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        mockCache.Setup(c => c.SetStringAsync(cacheKey, It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockRepository = new Mock<IPermissionRepository>();
        mockRepository.Setup(r => r.GetForRoleAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);

        var service = new PermissionService(mockRepository.Object, mockCache.Object);

        // Act
        var result = await service.GetPermissionsForRoleAsync(roleId);

        // Assert
        result.Should().HaveCount(1);
        mockCache.Verify(c => c.SetStringAsync(cacheKey, It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region UserHasPermissionAsync Tests

    [Fact]
    public async Task UserHasPermissionAsync_WithActivePermission_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var permission = new Permission { Id = Guid.NewGuid(), Code = "test.read", Name = "Test Read", Category = "Test", IsActive = true };
        var cachedJson = JsonSerializer.Serialize(permission);

        var mockCache = new Mock<IDistributedCache>();
        mockCache.Setup(c => c.GetStringAsync("perm:code:test.read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedJson);

        var mockRepository = new Mock<IPermissionRepository>();
        var service = new PermissionService(mockRepository.Object, mockCache.Object);

        // Act
        var result = await service.UserHasPermissionAsync(userId, "test.read");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task UserHasPermissionAsync_WithInactivePermission_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var permission = new Permission { Id = Guid.NewGuid(), Code = "test.read", Name = "Test Read", Category = "Test", IsActive = false };
        var cachedJson = JsonSerializer.Serialize(permission);

        var mockCache = new Mock<IDistributedCache>();
        mockCache.Setup(c => c.GetStringAsync("perm:code:test.read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedJson);

        var mockRepository = new Mock<IPermissionRepository>();
        var service = new PermissionService(mockRepository.Object, mockCache.Object);

        // Act
        var result = await service.UserHasPermissionAsync(userId, "test.read");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UserHasPermissionAsync_WithNullPermissionCode_ReturnsFalse()
    {
        // Arrange
        var mockCache = new Mock<IDistributedCache>();
        var mockRepository = new Mock<IPermissionRepository>();
        var service = new PermissionService(mockRepository.Object, mockCache.Object);

        // Act
        var result = await service.UserHasPermissionAsync(Guid.NewGuid(), null!);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region RoleHasPermissionAsync Tests

    [Fact]
    public async Task RoleHasPermissionAsync_WithAssignedActivePermission_ReturnsTrue()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var permissions = new List<Permission>
        {
            new Permission { Id = Guid.NewGuid(), Code = "test.write", Name = "Test Write", Category = "Test", IsActive = true }
        };
        var cachedJson = JsonSerializer.Serialize(permissions);
        var cacheKey = $"perm:role:{roleId}";

        var mockCache = new Mock<IDistributedCache>();
        mockCache.Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedJson);

        var mockRepository = new Mock<IPermissionRepository>();
        var service = new PermissionService(mockRepository.Object, mockCache.Object);

        // Act
        var result = await service.RoleHasPermissionAsync(roleId, "test.write");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RoleHasPermissionAsync_WithUnassignedPermission_ReturnsFalse()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var permissions = Enumerable.Empty<Permission>();
        var cacheKey = $"perm:role:{roleId}";

        var mockCache = new Mock<IDistributedCache>();
        mockCache.Setup(c => c.GetStringAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        mockCache.Setup(c => c.SetStringAsync(cacheKey, It.IsAny<string>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockRepository = new Mock<IPermissionRepository>();
        mockRepository.Setup(r => r.GetForRoleAsync(roleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);

        var service = new PermissionService(mockRepository.Object, mockCache.Object);

        // Act
        var result = await service.RoleHasPermissionAsync(roleId, "nonexistent.perm");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Cache Invalidation Tests

    [Fact]
    public async Task InvalidatePermissionCacheAsync_RemovesCodeAndGlobalCaches()
    {
        // Arrange
        var mockCache = new Mock<IDistributedCache>();
        mockCache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockRepository = new Mock<IPermissionRepository>();
        var service = new PermissionService(mockRepository.Object, mockCache.Object);

        // Act
        await service.InvalidatePermissionCacheAsync("test.code");

        // Assert
        mockCache.Verify(c => c.RemoveAsync("perm:code:test.code", It.IsAny<CancellationToken>()), Times.Once);
        mockCache.Verify(c => c.RemoveAsync("perm:active:all", It.IsAny<CancellationToken>()), Times.Once);
        mockCache.Verify(c => c.RemoveAsync("perm:all", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidateAllPermissionCacheAsync_RemovesGlobalCaches()
    {
        // Arrange
        var mockCache = new Mock<IDistributedCache>();
        mockCache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockRepository = new Mock<IPermissionRepository>();
        var service = new PermissionService(mockRepository.Object, mockCache.Object);

        // Act
        await service.InvalidateAllPermissionCacheAsync();

        // Assert
        mockCache.Verify(c => c.RemoveAsync("perm:active:all", It.IsAny<CancellationToken>()), Times.Once);
        mockCache.Verify(c => c.RemoveAsync("perm:all", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
