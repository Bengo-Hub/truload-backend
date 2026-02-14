using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Identity;

namespace TruLoad.Backend.Tests.Integration.Helpers;

/// <summary>
/// Helper for creating and seeding ApplicationUser instances in integration tests.
/// Provides consistent defaults so individual tests only override the properties they care about.
/// </summary>
public static class TestUserHelper
{
    /// <summary>
    /// Creates an in-memory ApplicationUser with sensible defaults.
    /// The user is NOT persisted to any DbContext; call SeedTestUser for that.
    /// </summary>
    public static ApplicationUser CreateTestUser(
        string email = "test@example.com",
        string firstName = "Test",
        string lastName = "User")
    {
        var userId = Guid.NewGuid();

        return new ApplicationUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            FullName = $"{firstName} {lastName}",
            StationId = null,
            OrganizationId = null,
            DepartmentId = null,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates an ApplicationUser, saves it to the database, and assigns the specified role.
    /// The role must already exist in the context (e.g., via TestDbContextFactory.SeedBaseData).
    /// </summary>
    /// <param name="context">The DbContext to persist the user into.</param>
    /// <param name="email">Email address (also used as UserName).</param>
    /// <param name="roleName">
    /// Display name of the role to assign (e.g., "System Admin", "Superuser", "Enforcement Officer").
    /// Must match an existing role's Name column.
    /// </param>
    /// <returns>The persisted ApplicationUser with its role assignment saved.</returns>
    public static async Task<ApplicationUser> SeedTestUser(
        TruLoadDbContext context,
        string email = "test@example.com",
        string roleName = "System Admin")
    {
        var user = CreateTestUser(email);

        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Look up the role by Name
        var role = await context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
        if (role != null)
        {
            context.UserRoles.Add(new IdentityUserRole<Guid>
            {
                UserId = user.Id,
                RoleId = role.Id
            });
            await context.SaveChangesAsync();
        }

        return user;
    }
}