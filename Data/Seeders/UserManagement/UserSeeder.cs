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
        await SeedTruLoadDemoAdminAsync();
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

    /// <summary>
    /// Seeds a demo admin user for the TruLoad Demo commercial weighing organization.
    /// This user logs in via SSO (auth-api "truload" tenant) in production.
    /// A local account is also seeded here as a fallback for development/testing.
    /// </summary>
    private async Task SeedTruLoadDemoAdminAsync()
    {
        var truloadDemoOrg = await _context.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Code == "TRULOAD-DEMO");

        if (truloadDemoOrg == null)
        {
            Console.WriteLine("⚠ TRULOAD-DEMO organization not found, skipping demo admin seed");
            return;
        }

        var demoStation = await _context.Stations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Code == "DEMO-WB-01");

        // Station Manager role is appropriate for a commercial weighbridge admin
        var stationManagerRole = await _roleManager.FindByNameAsync("Station Manager");
        if (stationManagerRole == null)
        {
            Console.WriteLine("⚠ Station Manager role not found, skipping TruLoad demo admin seed");
            return;
        }

        var demoAdminEmail = "admin@truload.codevertexitsolutions.com";
        var existingAdmin = await _userManager.FindByEmailAsync(demoAdminEmail);

        if (existingAdmin == null)
        {
            var demoAdmin = new ApplicationUser
            {
                Email = demoAdminEmail,
                NormalizedEmail = demoAdminEmail.ToUpper(),
                UserName = demoAdminEmail,
                NormalizedUserName = demoAdminEmail.ToUpper(),
                FullName = "TruLoad Demo Admin",
                PhoneNumber = "+254700000010",
                OrganizationId = truloadDemoOrg.Id,
                StationId = demoStation?.Id,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                TwoFactorEnabled = false,
                LockoutEnabled = false
            };

            var result = await _userManager.CreateAsync(demoAdmin, DefaultPassword);
            if (!result.Succeeded)
            {
                Console.WriteLine($"⚠ Failed to create TruLoad demo admin: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                return;
            }

            var roleResult = await _userManager.AddToRoleAsync(demoAdmin, "Station Manager");
            if (!roleResult.Succeeded)
            {
                Console.WriteLine($"⚠ Failed to assign role to TruLoad demo admin: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
                return;
            }

            Console.WriteLine($"✓ Seeded TruLoad demo admin: {demoAdminEmail} → TRULOAD-DEMO org ({truloadDemoOrg.Name}), station: {demoStation?.Name ?? "none"}");
            Console.WriteLine($"  Password: {DefaultPassword} (DEVELOPMENT ONLY — production login is via SSO)");
        }
        else
        {
            if (existingAdmin.OrganizationId != truloadDemoOrg.Id || (existingAdmin.StationId == null && demoStation != null))
            {
                existingAdmin.OrganizationId = truloadDemoOrg.Id;
                if (demoStation != null) existingAdmin.StationId = demoStation.Id;
                await _userManager.UpdateAsync(existingAdmin);
                Console.WriteLine($"✓ Updated TruLoad demo admin {demoAdminEmail}: org + station linked");
            }
            else
            {
                Console.WriteLine($"✓ TruLoad demo admin {demoAdminEmail} already exists, skipping seed");
            }
        }
    }

}
