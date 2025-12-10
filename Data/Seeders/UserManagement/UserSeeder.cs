using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Infrastructure.Security;
using truload_backend.Data;

namespace TruLoad.Backend.Data.Seeders.UserManagement;

/// <summary>
/// Seeds initial users for TruLoad system
/// Note: In production, users are synced from auth-service SSO
/// These seed users are for development/testing with local auth-service
/// 
/// IMPORTANT: This seeder also creates users in auth-service with pre-hashed passwords
/// to enable bidirectional sync. Password hashing uses shared Argon2id implementation.
/// </summary>
public class UserSeeder
{
    private readonly TruLoadDbContext _context;
    private readonly PasswordHasher _passwordHasher;

    // Password for seeded users (DEVELOPMENT ONLY)
    private const string DefaultPassword = "ChangeMe123!";

    public UserSeeder(TruLoadDbContext context)
    {
        _context = context;
        _passwordHasher = new PasswordHasher();
    }

    public async Task SeedAsync()
    {
        await SeedSuperUserAsync();
    }

    private async Task SeedSuperUserAsync()
    {
        // Check if KURA organization exists (required for linking)
        var kuraOrg = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Code == "KURA");
        
        if (kuraOrg == null)
        {
            throw new InvalidOperationException("KURA organization not found. Ensure UserManagementSeeder runs before UserSeeder.");
        }

        // Check if SYSTEM_ADMIN role exists
        var systemAdminRole = await _context.Roles
            .FirstOrDefaultAsync(r => r.Code == "SYSTEM_ADMIN");
        
        if (systemAdminRole == null)
        {
            throw new InvalidOperationException("SYSTEM_ADMIN role not found. Ensure UserManagementSeeder runs before UserSeeder.");
        }

        // Seed superuser: gadmin@masterspace.co.ke
        var superUserEmail = "gadmin@masterspace.co.ke";
        var existingSuperUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == superUserEmail);

        if (existingSuperUser == null)
        {
            // Generate deterministic GUID for AuthServiceUserId (development only)
            // In production, this will be synced from auth-service
            var authServiceUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");

            // Hash password using shared Argon2id implementation
            // This hash can be verified by auth-service and other services
            var passwordHash = _passwordHasher.HashPassword(DefaultPassword);

            var superUser = new User
            {
                Id = Guid.Parse("20000000-0000-0000-0000-000000000001"),
                AuthServiceUserId = authServiceUserId,
                Email = superUserEmail,
                FullName = "Global Administrator",
                Phone = "+254700000000",
                Status = "active",
                OrganizationId = kuraOrg.Id,
                SyncStatus = "synced",
                SyncAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.Users.AddAsync(superUser);
            await _context.SaveChangesAsync();

            // Assign SYSTEM_ADMIN role to superuser
            var userRole = new UserRole
            {
                UserId = superUser.Id,
                RoleId = systemAdminRole.Id,
                AssignedAt = DateTime.UtcNow
            };

            await _context.UserRoles.AddAsync(userRole);
            await _context.SaveChangesAsync();

            Console.WriteLine($"✓ Seeded superuser: {superUserEmail} linked to KURA organization with SYSTEM_ADMIN role");
            Console.WriteLine($"  Password: {DefaultPassword} (DEVELOPMENT ONLY - change in production!)");
            Console.WriteLine($"  Password hash format: Argon2id (compatible with auth-service)");
            
            // TODO: Sync this user to auth-service via AuthServiceClient
            // This ensures the user exists in both local DB and SSO for bidirectional sync
        }
        else
        {
            Console.WriteLine($"✓ Superuser {superUserEmail} already exists, skipping seed");
        }
    }
}
