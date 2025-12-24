using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Auth;
using truload_backend.Data;
using Xunit;
using FluentAssertions;

namespace truload_backend.Tests.Unit.Repositories;

/// <summary>
/// Unit tests for PermissionRepository.
/// Uses in-memory database for fast, isolated testing.
/// </summary>
public class PermissionRepositoryTests
{
    private TruLoadDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<TruLoadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new TruLoadDbContext(options);
    }

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsPermission()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            Code = "test.read",
            Name = "Test Read",
            Category = "Test",
            Description = "Test permission",
            IsActive = true
        };
        context.Permissions.Add(permission);
        await context.SaveChangesAsync();

        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetByIdAsync(permission.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Code.Should().Be("test.read");
        result.Name.Should().Be("Test Read");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WithEmptyGuid_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetByIdAsync(Guid.Empty);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByCodeAsync Tests

    [Fact]
    public async Task GetByCodeAsync_WithValidCode_ReturnsPermission()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            Code = "weighing.create",
            Name = "Create Weighing",
            Category = "Weighing",
            IsActive = true
        };
        context.Permissions.Add(permission);
        await context.SaveChangesAsync();

        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetByCodeAsync("weighing.create");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(permission.Id);
        result.Name.Should().Be("Create Weighing");
    }

    [Fact]
    public async Task GetByCodeAsync_WithNonExistentCode_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetByCodeAsync("nonexistent.code");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByCodeAsync_WithNullCode_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetByCodeAsync(null!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByCodeAsync_WithEmptyCode_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetByCodeAsync(string.Empty);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByCategoryAsync Tests

    [Fact]
    public async Task GetByCategoryAsync_WithValidCategory_ReturnsPermissionsInCategory()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var perm1 = new Permission { Id = Guid.NewGuid(), Code = "weighing.create", Name = "Create", Category = "Weighing", IsActive = true };
        var perm2 = new Permission { Id = Guid.NewGuid(), Code = "weighing.read", Name = "Read", Category = "Weighing", IsActive = true };
        var perm3 = new Permission { Id = Guid.NewGuid(), Code = "user.create", Name = "Create User", Category = "User", IsActive = true };

        context.Permissions.AddRange(perm1, perm2, perm3);
        await context.SaveChangesAsync();

        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetByCategoryAsync("Weighing");

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(p => p.Category.Should().Be("Weighing"));
    }

    [Fact]
    public async Task GetByCategoryAsync_WithNonExistentCategory_ReturnsEmpty()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetByCategoryAsync("NonExistent");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByCategoryAsync_WithNullCategory_ReturnsEmpty()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetByCategoryAsync(null!);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllPermissions()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var perm1 = new Permission { Id = Guid.NewGuid(), Code = "perm1", Name = "Permission 1", Category = "Cat1", IsActive = true };
        var perm2 = new Permission { Id = Guid.NewGuid(), Code = "perm2", Name = "Permission 2", Category = "Cat2", IsActive = false };

        context.Permissions.AddRange(perm1, perm2);
        await context.SaveChangesAsync();

        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_WithEmptyDatabase_ReturnsEmpty()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsSortedByCategory()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        context.Permissions.AddRange(
            new Permission { Id = Guid.NewGuid(), Code = "z", Name = "Z", Category = "Zebra", IsActive = true },
            new Permission { Id = Guid.NewGuid(), Code = "a", Name = "A", Category = "Apple", IsActive = true }
        );
        await context.SaveChangesAsync();

        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetAllAsync();
        var resultList = result.ToList();

        // Assert
        for (int i = 0; i < resultList.Count - 1; i++)
        {
            resultList[i].Category.CompareTo(resultList[i + 1].Category).Should().BeLessThanOrEqualTo(0);
        }
    }

    #endregion

    #region GetActiveAsync Tests

    [Fact]
    public async Task GetActiveAsync_ReturnsOnlyActivePermissions()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var active = new Permission { Id = Guid.NewGuid(), Code = "active", Name = "Active", Category = "Test", IsActive = true };
        var inactive = new Permission { Id = Guid.NewGuid(), Code = "inactive", Name = "Inactive", Category = "Test", IsActive = false };

        context.Permissions.AddRange(active, inactive);
        await context.SaveChangesAsync();

        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetActiveAsync();

        // Assert
        result.Should().HaveCount(1);
        result.First().IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetActiveAsync_WithNoActivePermissions_ReturnsEmpty()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        context.Permissions.Add(new Permission { Id = Guid.NewGuid(), Code = "inactive", Name = "Inactive", Category = "Test", IsActive = false });
        await context.SaveChangesAsync();

        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetActiveAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetForRoleAsync Tests

    [Fact]
    public async Task GetForRoleAsync_WithValidRoleId_ReturnsAssignedPermissions()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var roleId = Guid.NewGuid();
        var perm1 = new Permission { Id = Guid.NewGuid(), Code = "perm1", Name = "Permission 1", Category = "Test", IsActive = true };
        var perm2 = new Permission { Id = Guid.NewGuid(), Code = "perm2", Name = "Permission 2", Category = "Test", IsActive = true };

        context.Permissions.AddRange(perm1, perm2);
        context.RolePermissions.AddRange(
            new RolePermission { RoleId = roleId, PermissionId = perm1.Id, AssignedAt = DateTime.UtcNow },
            new RolePermission { RoleId = roleId, PermissionId = perm2.Id, AssignedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetForRoleAsync(roleId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(p => p.Id == perm1.Id);
        result.Should().Contain(p => p.Id == perm2.Id);
    }

    [Fact]
    public async Task GetForRoleAsync_WithNonExistentRoleId_ReturnsEmpty()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.GetForRoleAsync(Guid.NewGuid());

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WithValidPermission_PersistsToDatabase()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            Code = "new.permission",
            Name = "New Permission",
            Category = "Test",
            Description = "A new test permission",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.CreateAsync(permission);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(permission.Id);

        var saved = await context.Permissions.FirstOrDefaultAsync(p => p.Id == permission.Id);
        saved.Should().NotBeNull();
        saved!.Code.Should().Be("new.permission");
    }

    [Fact]
    public async Task CreateAsync_WithNullPermission_ThrowsArgumentNullException()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new PermissionRepository(context);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.CreateAsync(null!));
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WithValidPermission_UpdatesDatabase()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var permission = new Permission { Id = Guid.NewGuid(), Code = "test", Name = "Test", Category = "Test", IsActive = true };
        context.Permissions.Add(permission);
        await context.SaveChangesAsync();

        permission.Name = "Updated Name";
        permission.IsActive = false;

        var repository = new PermissionRepository(context);

        // Act
        await repository.UpdateAsync(permission);

        // Assert
        var updated = await context.Permissions.FirstOrDefaultAsync(p => p.Id == permission.Id);
        updated!.Name.Should().Be("Updated Name");
        updated.IsActive.Should().BeFalse();
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_WithValidId_RemovesFromDatabase()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var permission = new Permission { Id = Guid.NewGuid(), Code = "test", Name = "Test", Category = "Test", IsActive = true };
        context.Permissions.Add(permission);
        await context.SaveChangesAsync();

        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.DeleteAsync(permission.Id);

        // Assert
        result.Should().BeTrue();
        var deleted = await context.Permissions.FirstOrDefaultAsync(p => p.Id == permission.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_ReturnsFalse()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.DeleteAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ExistsByCodeAsync Tests

    [Fact]
    public async Task ExistsByCodeAsync_WithExistingCode_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var permission = new Permission { Id = Guid.NewGuid(), Code = "existing", Name = "Existing", Category = "Test", IsActive = true };
        context.Permissions.Add(permission);
        await context.SaveChangesAsync();

        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.ExistsByCodeAsync("existing");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByCodeAsync_WithNonExistentCode_ReturnsFalse()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.ExistsByCodeAsync("nonexistent");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region CountAsync Tests

    [Fact]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        context.Permissions.AddRange(
            new Permission { Id = Guid.NewGuid(), Code = "p1", Name = "P1", Category = "Test", IsActive = true },
            new Permission { Id = Guid.NewGuid(), Code = "p2", Name = "P2", Category = "Test", IsActive = true },
            new Permission { Id = Guid.NewGuid(), Code = "p3", Name = "P3", Category = "Test", IsActive = true }
        );
        await context.SaveChangesAsync();

        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.CountAsync();

        // Assert
        result.Should().Be(3);
    }

    [Fact]
    public async Task CountAsync_WithEmptyDatabase_ReturnsZero()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var repository = new PermissionRepository(context);

        // Act
        var result = await repository.CountAsync();

        // Assert
        result.Should().Be(0);
    }

    #endregion
}
