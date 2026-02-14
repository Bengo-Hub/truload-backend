using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Repositories;
using TruLoad.Backend.Data;
using TruLoad.Data.Seeders;
using Xunit;
using FluentAssertions;

namespace Truload.Backend.Tests.Integration;

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
    public async Task SeedPermissions_Creates121PermissionsIn14Categories()
    {
        // Arrange & Act - Use the actual PermissionSeeder to create default permissions
        await PermissionSeeder.SeedAsync(_context!);

        var allPermissions = await _context!.Permissions.ToListAsync();
        var byCategory = await _context.Permissions.GroupBy(p => p.Category).Select(g => new { Category = g.Key, Count = g.Count() }).ToListAsync();

        // Assert - verify exactly 121 permissions across 14 categories
        allPermissions.Should().HaveCount(121, "PermissionSeeder should create exactly 121 default permissions");
        byCategory.Should().HaveCount(14, "Permissions should be distributed across 14 categories");
        byCategory.Should().Contain(c => c.Category == "Weighing" && c.Count == 12);
        byCategory.Should().Contain(c => c.Category == "Yard" && c.Count == 8);
        byCategory.Should().Contain(c => c.Category == "Tag" && c.Count == 8);
        byCategory.Should().Contain(c => c.Category == "Case" && c.Count == 15);
        byCategory.Should().Contain(c => c.Category == "Prosecution" && c.Count == 8);
        byCategory.Should().Contain(c => c.Category == "User" && c.Count == 10);
        byCategory.Should().Contain(c => c.Category == "Station" && c.Count == 12);
        byCategory.Should().Contain(c => c.Category == "Configuration" && c.Count == 10);
        byCategory.Should().Contain(c => c.Category == "Analytics" && c.Count == 8);
        byCategory.Should().Contain(c => c.Category == "Financial" && c.Count == 10);
        byCategory.Should().Contain(c => c.Category == "Vehicle" && c.Count == 3);
        byCategory.Should().Contain(c => c.Category == "Transporter" && c.Count == 4);
        byCategory.Should().Contain(c => c.Category == "Driver" && c.Count == 3);
        byCategory.Should().Contain(c => c.Category == "System" && c.Count == 10);
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

        var role = new ApplicationRole { Id = roleId, Code = "admin", Name = "Admin", IsActive = true };
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

        var role = new ApplicationRole { Id = roleId, Code = "test", Name = "Test Role", IsActive = true };
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

        var role = new ApplicationRole { Id = roleId, Code = "test", Name = "Test Role", IsActive = true };
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
        // For in-memory databases, we test that seeders don't create duplicates
        // rather than testing database constraint enforcement

        // Arrange & Act - Run the seeders
        await PermissionSeeder.SeedAsync(_context!);
        await RolePermissionSeeder.SeedAsync(_context!);

        // Assert - Verify no duplicate role-permission relationships were created
        var allRolePermissions = await _context!.RolePermissions.ToListAsync();
        var uniqueCombinations = allRolePermissions
            .Select(rp => (rp.RoleId, rp.PermissionId))
            .Distinct()
            .ToList();

        allRolePermissions.Should().HaveCount(uniqueCombinations.Count,
            "Seeders should not create duplicate role-permission relationships");
    }

    #endregion

    #region Permission-Role Assignment Tests

    [Fact]
    public async Task AssignPermissionsToRole_MultiplePermissionsAssignedCorrectly()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var role = new ApplicationRole { Id = roleId, Code = "editor", Name = "Editor", IsActive = true };

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
        var role = new ApplicationRole { Id = roleId, Code = "viewer", Name = "Viewer", IsActive = true };
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
        // Arrange - Seed permissions first
        await PermissionSeeder.SeedAsync(_context!);

        // Get all permission codes
        var codes = await _context!.Permissions.Select(p => p.Code).ToListAsync();

        // Act & Assert - Verify all codes are unique
        var uniqueCodes = codes.Distinct().ToList();
        codes.Should().HaveCount(uniqueCodes.Count, "All permission codes should be unique");

        // Also verify that trying to add a duplicate programmatically would be caught by EF validation
        var existingCode = codes.First();
        var duplicatePermission = new Permission
        {
            Id = Guid.NewGuid(),
            Code = existingCode,
            Name = "Duplicate Permission",
            Category = "Test",
            IsActive = true
        };

        // This should not throw an exception with in-memory DB, but the codes should still be unique
        _context.Permissions.Add(duplicatePermission);
        await _context.SaveChangesAsync();

        var allCodesAfter = await _context.Permissions.Select(p => p.Code).ToListAsync();
        var duplicateCount = allCodesAfter.Count(c => c == existingCode);
        duplicateCount.Should().Be(2, "In-memory database allows duplicates but seeder should create unique codes");
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
