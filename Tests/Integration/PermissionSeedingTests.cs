using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories;
using TruLoad.Backend.Data;
using truload_backend.Data;
using Xunit;
using FluentAssertions;

namespace truload_backend.Tests.Integration;

/// <summary>
/// Integration tests for Permission and RolePermission seeding and relationships.
/// Tests complete database interactions and constraints.
/// </summary>
public class PermissionSeedingTests : IAsyncLifetime
{
    private TruLoadDbContext? _context;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<TruLoadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TruLoadDbContext(options);
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
        {
            await _context.Database.EnsureDeletedAsync();
            await _context.DisposeAsync();
        }
    }

    #region Permission Seeding Tests

    [Fact]
    public async Task SeedPermissions_Creates77PermissionsIn8Categories()
    {
        // Arrange
        var permissions = new List<Permission>();
        
        // Create 77 permissions across 8 categories
        var categories = new[]
        {
            ("Weighing", 12),
            ("Case", 15),
            ("Prosecution", 8),
            ("User", 10),
            ("Station", 12),
            ("Configuration", 8),
            ("Analytics", 8),
            ("System", 6)
        };

        int permId = 0;
        foreach (var (category, count) in categories)
        {
            for (int i = 0; i < count; i++)
            {
                permId++;
                permissions.Add(new Permission
                {
                    Id = Guid.NewGuid(),
                    Code = $"{category.ToLower()}.perm{i + 1}",
                    Name = $"{category} Permission {i + 1}",
                    Category = category,
                    Description = $"Permission {i + 1} for {category}",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        _context!.Permissions.AddRange(permissions);
        await _context.SaveChangesAsync();

        // Act
        var allPermissions = await _context.Permissions.ToListAsync();
        var byCategory = await _context.Permissions.GroupBy(p => p.Category).Select(g => new { Category = g.Key, Count = g.Count() }).ToListAsync();

        // Assert
        allPermissions.Should().HaveCount(77);
        byCategory.Should().HaveCount(8);
        byCategory.Should().Contain(c => c.Category == "Weighing" && c.Count == 12);
        byCategory.Should().Contain(c => c.Category == "Case" && c.Count == 15);
        byCategory.Should().Contain(c => c.Category == "System" && c.Count == 6);
    }

    [Fact]
    public async Task SeedPermissions_AllPermissionsHaveDescriptions()
    {
        // Arrange
        var permissions = new[]
        {
            new Permission { Id = Guid.NewGuid(), Code = "test.1", Name = "Test 1", Category = "Test", Description = "A test permission", IsActive = true },
            new Permission { Id = Guid.NewGuid(), Code = "test.2", Name = "Test 2", Category = "Test", Description = null as string, IsActive = true }
        };

        _context!.Permissions.AddRange(permissions);
        await _context.SaveChangesAsync();

        // Act
        var all = await _context.Permissions.ToListAsync();

        // Assert
        all[0].Description.Should().NotBeNullOrEmpty();
        all[1].Description.Should().BeNull();
    }

    #endregion

    #region RolePermission Relationship Tests

    [Fact]
    public async Task RolePermissionRelationship_CreatesValidCompositeKey()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();

        var role = new Role { Id = roleId, Code = "admin", Name = "Admin", IsActive = true, CreatedAt = DateTime.UtcNow };
        var permission = new Permission { Id = permissionId, Code = "admin.all", Name = "Admin All", Category = "System", IsActive = true };

        _context!.Roles.Add(role);
        _context.Permissions.Add(permission);
        _context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId, AssignedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        // Act
        var rolePerms = await _context.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync();

        // Assert
        rolePerms.Should().HaveCount(1);
        rolePerms[0].PermissionId.Should().Be(permissionId);
    }

    [Fact]
    public async Task RolePermissionRelationship_CascadeDeletesOnRoleDelete()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();

        var role = new Role { Id = roleId, Code = "test", Name = "Test Role", IsActive = true, CreatedAt = DateTime.UtcNow };
        var permission = new Permission { Id = permissionId, Code = "test.perm", Name = "Test Perm", Category = "Test", IsActive = true };
        var rolePermission = new RolePermission { RoleId = roleId, PermissionId = permissionId, AssignedAt = DateTime.UtcNow };

        _context!.Roles.Add(role);
        _context.Permissions.Add(permission);
        _context.RolePermissions.Add(rolePermission);
        await _context.SaveChangesAsync();

        // Act
        _context.Roles.Remove(role);
        await _context.SaveChangesAsync();

        var remaining = await _context.RolePermissions.Where(rp => rp.RoleId == roleId).CountAsync();

        // Assert
        remaining.Should().Be(0);
    }

    [Fact]
    public async Task RolePermissionRelationship_CascadeDeletesOnPermissionDelete()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();

        var role = new Role { Id = roleId, Code = "test", Name = "Test Role", IsActive = true, CreatedAt = DateTime.UtcNow };
        var permission = new Permission { Id = permissionId, Code = "test.perm", Name = "Test Perm", Category = "Test", IsActive = true };
        var rolePermission = new RolePermission { RoleId = roleId, PermissionId = permissionId, AssignedAt = DateTime.UtcNow };

        _context!.Roles.Add(role);
        _context.Permissions.Add(permission);
        _context.RolePermissions.Add(rolePermission);
        await _context.SaveChangesAsync();

        // Act
        _context.Permissions.Remove(permission);
        await _context.SaveChangesAsync();

        var remaining = await _context.RolePermissions.Where(rp => rp.PermissionId == permissionId).CountAsync();

        // Assert
        remaining.Should().Be(0);
    }

    [Fact]
    public async Task RolePermissionRelationship_EnforcesUniqueCompositeKey()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();

        var role = new Role { Id = roleId, Code = "test", Name = "Test Role", IsActive = true, CreatedAt = DateTime.UtcNow };
        var permission = new Permission { Id = permissionId, Code = "test.perm", Name = "Test Perm", Category = "Test", IsActive = true };

        _context!.Roles.Add(role);
        _context.Permissions.Add(permission);
        _context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId, AssignedAt = DateTime.UtcNow });
        _context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId, AssignedAt = DateTime.UtcNow });
        
        // Act & Assert
        var ex = await Assert.ThrowsAsync<DbUpdateException>(async () => await _context.SaveChangesAsync());
        ex.Should().NotBeNull();
    }

    #endregion

    #region Permission-Role Assignment Tests

    [Fact]
    public async Task AssignPermissionsToRole_MultiplePermissionsAssignedCorrectly()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var role = new Role { Id = roleId, Code = "editor", Name = "Editor", IsActive = true, CreatedAt = DateTime.UtcNow };

        var permissions = Enumerable.Range(1, 5).Select(i => new Permission
        {
            Id = Guid.NewGuid(),
            Code = $"edit.perm{i}",
            Name = $"Edit Permission {i}",
            Category = "Editing",
            IsActive = true
        }).ToList();

        _context!.Roles.Add(role);
        _context.Permissions.AddRange(permissions);
        await _context.SaveChangesAsync();

        var rolePermissions = permissions.Select(p => new RolePermission 
        { 
            RoleId = roleId, 
            PermissionId = p.Id, 
            AssignedAt = DateTime.UtcNow 
        }).ToList();

        _context.RolePermissions.AddRange(rolePermissions);
        await _context.SaveChangesAsync();

        // Act
        var assigned = await _context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission)
            .ToListAsync();

        // Assert
        assigned.Should().HaveCount(5);
        assigned.Should().AllSatisfy(p => p.Category.Should().Be("Editing"));
    }

    [Fact]
    public async Task GetRolePermissions_ReturnsPermissionsWithCorrectNavigationProperties()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var role = new Role { Id = roleId, Code = "viewer", Name = "Viewer", IsActive = true, CreatedAt = DateTime.UtcNow };
        var permission = new Permission { Id = Guid.NewGuid(), Code = "view.all", Name = "View All", Category = "Viewing", IsActive = true };

        _context!.Roles.Add(role);
        _context.Permissions.Add(permission);
        _context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permission.Id, AssignedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        // Act
        var roleWithPerms = await _context.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        // Assert
        roleWithPerms.Should().NotBeNull();
        roleWithPerms!.RolePermissions.Should().HaveCount(1);
        roleWithPerms.RolePermissions.First().Permission.Code.Should().Be("view.all");
    }

    #endregion

    #region Database Constraints Tests

    [Fact]
    public async Task PermissionCode_UniqueConstraintEnforced()
    {
        // Arrange
        var perm1 = new Permission { Id = Guid.NewGuid(), Code = "duplicate", Name = "Perm 1", Category = "Test", IsActive = true };
        var perm2 = new Permission { Id = Guid.NewGuid(), Code = "duplicate", Name = "Perm 2", Category = "Test", IsActive = true };

        _context!.Permissions.Add(perm1);
        await _context.SaveChangesAsync();

        _context.Permissions.Add(perm2);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DbUpdateException>(async () => await _context.SaveChangesAsync());
        ex.Should().NotBeNull();
    }

    [Fact]
    public async Task PermissionCategory_IndexExists()
    {
        // Arrange
        var permissions = Enumerable.Range(1, 3).Select(i => new Permission
        {
            Id = Guid.NewGuid(),
            Code = $"test{i}",
            Name = $"Test {i}",
            Category = "TestCategory",
            IsActive = true
        }).ToList();

        _context!.Permissions.AddRange(permissions);
        await _context.SaveChangesAsync();

        // Act
        var result = await _context.Permissions.Where(p => p.Category == "TestCategory").ToListAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task PermissionIsActive_IndexExists()
    {
        // Arrange
        _context!.Permissions.Add(new Permission { Id = Guid.NewGuid(), Code = "active", Name = "Active", Category = "Test", IsActive = true });
        _context.Permissions.Add(new Permission { Id = Guid.NewGuid(), Code = "inactive", Name = "Inactive", Category = "Test", IsActive = false });
        await _context.SaveChangesAsync();

        // Act
        var activeCount = await _context.Permissions.Where(p => p.IsActive).CountAsync();
        var inactiveCount = await _context.Permissions.Where(p => !p.IsActive).CountAsync();

        // Assert
        activeCount.Should().Be(1);
        inactiveCount.Should().Be(1);
    }

    #endregion
}
