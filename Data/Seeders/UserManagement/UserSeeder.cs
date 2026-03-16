using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Data.Seeders.UserManagement;

/// <summary>
/// Seeds initial users for TruLoad system using ASP.NET Core Identity
/// These seed users are for development/testing
/// 
/// IMPORTANT: Password is managed by UserManager's password hasher (Identity's default)
/// </summary>
public class UserSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly TruLoadDbContext _context;

    // Password for seeded users (DEVELOPMENT ONLY)
    private const string DefaultPassword = "ChangeMe123!";

    public UserSeeder(
        UserManager<ApplicationUser> userManager, 
        RoleManager<ApplicationRole> roleManager,
        TruLoadDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
    }

    public async Task SeedAsync()
    {
        await SeedSuperUserAsync();
        await SeedMiddlewareServiceUserAsync();
        await SeedMiddlewareOperatorUserAsync();
    }

    private async Task SeedSuperUserAsync()
    {
        // Check if KURA organization exists (required for linking - KURA is the default tenant)
        var kuraOrg = await _context.Organizations
            .FirstOrDefaultAsync(o => o.IsDefault) ?? await _context.Organizations
            .FirstOrDefaultAsync(o => o.Code == "KURA");

        if (kuraOrg == null)
        {
            throw new InvalidOperationException("Default or KURA organization not found. Ensure UserManagementSeeder runs before UserSeeder.");
        }

        // Get the first mobile station to link to the user (NRB-MOBILE-01)
        var mobileStation = await _context.Stations
            .FirstOrDefaultAsync(s => s.IsDefault) ?? await _context.Stations
            .FirstOrDefaultAsync(s => s.Code == "NRB-MOBILE-01");

        // Check if SUPERUSER role exists
        var superuserRole = await _roleManager.FindByNameAsync("Superuser");

        if (superuserRole == null)
        {
            throw new InvalidOperationException("SUPERUSER role not found. Ensure RoleSeeder runs before UserSeeder.");
        }

        // Seed superuser: gadmin@masterspace.co.ke
        var superUserEmail = "gadmin@masterspace.co.ke";
        var existingSuperUser = await _userManager.FindByEmailAsync(superUserEmail);

        if (existingSuperUser == null)
        {
            var superUser = new ApplicationUser
            {
                Email = superUserEmail,
                NormalizedEmail = superUserEmail.ToUpper(),
                UserName = superUserEmail,
                NormalizedUserName = superUserEmail.ToUpper(),
                FullName = "Global Administrator",
                PhoneNumber = "+254700000000",
                OrganizationId = kuraOrg.Id,
                StationId = mobileStation?.Id,  // Link to mobile station
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                TwoFactorEnabled = false,
                LockoutEnabled = false
            };

            var result = await _userManager.CreateAsync(superUser, DefaultPassword);
            if (!result.Succeeded)
            {
                throw new Exception($"Failed to create superuser: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            // Assign SUPERUSER role to superuser
            var roleResult = await _userManager.AddToRoleAsync(superUser, "Superuser");
            if (!roleResult.Succeeded)
            {
                throw new Exception($"Failed to assign role to superuser: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
            }

            Console.WriteLine($"✓ Seeded superuser: {superUserEmail} linked to KURA organization and {mobileStation?.Name ?? "no station"} with SUPERUSER role");
            Console.WriteLine($"  Password: {DefaultPassword} (DEVELOPMENT ONLY - change in production!)");
        }
        else
        {
            // Update existing user to link station if not already linked
            if (existingSuperUser.StationId == null && mobileStation != null)
            {
                existingSuperUser.StationId = mobileStation.Id;
                existingSuperUser.OrganizationId = kuraOrg.Id;
                await _userManager.UpdateAsync(existingSuperUser);
                Console.WriteLine($"✓ Updated superuser {superUserEmail} to link station {mobileStation.Name}");
            }
            else
            {
                Console.WriteLine($"✓ Superuser {superUserEmail} already exists with station link, skipping seed");
            }
        }
    }

    private async Task SeedMiddlewareServiceUserAsync()
    {
        // Check if MIDDLEWARE_SERVICE role exists
        var middlewareRole = await _roleManager.FindByNameAsync("Middleware Service");

        if (middlewareRole == null)
        {
            Console.WriteLine("⚠ MIDDLEWARE_SERVICE role not found, skipping middleware user seed");
            return;
        }

        // Get KURA organization for linking
        var kuraOrg = await _context.Organizations
            .FirstOrDefaultAsync(o => o.IsDefault) ?? await _context.Organizations
            .FirstOrDefaultAsync(o => o.Code == "KURA");

        if (kuraOrg == null)
        {
            Console.WriteLine("⚠ Default or KURA organization not found, skipping middleware user seed");
            return;
        }

        // Get the default mobile station
        var mobileStation = await _context.Stations
            .FirstOrDefaultAsync(s => s.IsDefault) ?? await _context.Stations
            .FirstOrDefaultAsync(s => s.Code == "NRB-MOBILE-01");

        // Seed middleware service user: middleware@truconnect.local
        var middlewareEmail = "middleware@truconnect.local";
        var existingUser = await _userManager.FindByEmailAsync(middlewareEmail);

        if (existingUser == null)
        {
            var middlewareUser = new ApplicationUser
            {
                Email = middlewareEmail,
                NormalizedEmail = middlewareEmail.ToUpper(),
                UserName = middlewareEmail,
                NormalizedUserName = middlewareEmail.ToUpper(),
                FullName = "TruConnect Middleware",
                OrganizationId = kuraOrg.Id,
                StationId = mobileStation?.Id,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                TwoFactorEnabled = false,
                LockoutEnabled = false
            };

            var result = await _userManager.CreateAsync(middlewareUser, DefaultPassword);
            if (!result.Succeeded)
            {
                throw new Exception($"Failed to create middleware user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            var roleResult = await _userManager.AddToRoleAsync(middlewareUser, "Middleware Service");
            if (!roleResult.Succeeded)
            {
                throw new Exception($"Failed to assign role to middleware user: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
            }

            Console.WriteLine($"✓ Seeded middleware service user: {middlewareEmail} with MIDDLEWARE_SERVICE role");
            Console.WriteLine($"  Password: {DefaultPassword} (DEVELOPMENT ONLY - change in production!)");
        }
        else
        {
            // Update existing user to link station if not already linked
            if (existingUser.StationId == null && mobileStation != null)
            {
                existingUser.StationId = mobileStation.Id;
                existingUser.OrganizationId = kuraOrg.Id;
                await _userManager.UpdateAsync(existingUser);
                Console.WriteLine($"✓ Updated middleware user {middlewareEmail} to link station {mobileStation.Name}");
            }
            else
            {
                Console.WriteLine($"✓ Middleware service user {middlewareEmail} already exists, skipping seed");
            }
        }
    }

    private async Task SeedMiddlewareOperatorUserAsync()
    {
        // Check if MIDDLEWARE_OPERATOR role exists
        var operatorRole = await _roleManager.FindByNameAsync("Middleware Operator");

        if (operatorRole == null)
        {
            Console.WriteLine("⚠ MIDDLEWARE_OPERATOR role not found, skipping operator user seed");
            return;
        }

        // Get KURA organization
        var kuraOrg = await _context.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Code == "KURA");

        if (kuraOrg == null) return;

        // Seed operator user: user@truconnect.com
        var operatorEmail = "user@truconnect.com";
        var existingUser = await _userManager.FindByEmailAsync(operatorEmail);

        if (existingUser == null)
        {
            // Use specific password as requested: User@1234
            var operatorUser = new ApplicationUser
            {
                Email = operatorEmail,
                NormalizedEmail = operatorEmail.ToUpper(),
                UserName = operatorEmail,
                NormalizedUserName = operatorEmail.ToUpper(),
                FullName = "TruConnect Operator",
                OrganizationId = kuraOrg.Id,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                TwoFactorEnabled = false,
                LockoutEnabled = false
            };

            var result = await _userManager.CreateAsync(operatorUser, "User@1234");
            if (!result.Succeeded)
            {
                throw new Exception($"Failed to create operator user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            var roleResult = await _userManager.AddToRoleAsync(operatorUser, "Middleware Operator");
            if (!roleResult.Succeeded)
            {
                throw new Exception($"Failed to assign role to operator user: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
            }

            Console.WriteLine($"✓ Seeded middleware operator user: {operatorEmail} with MIDDLEWARE_OPERATOR role");
        }
    }
}
