using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Models.Weighing;
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

    // Demo/dev accounts retired 2026-08-27 and hard-deleted directly from both the shared
    // truload and dedicated kuraweigh databases (via kubectl/psql): admin@truload.codevertexafrica.com,
    // supervisor/operator/finance/auditor@truload.codevertexafrica.com,
    // manager/officer@enforcement.truload.codevertexafrica.com, and the SAVANNAH-HAULAGE demo
    // transporter + its 3 portal accounts (admin/manager/viewer@savannahhaulage.co.ke) — all
    // confirmed to have zero real references before deletion. Replaced by the codevertex-demo
    // convention below (SeedCommercialDemoTenantAdminAsync/SeedCommercialDemoStaffAsync).
    // gadmin@masterspace.co.ke was investigated and deliberately KEPT (see SeedSuperUserAsync) —
    // it has real linked KURA production data (a live prosecution case, thousands of audit log
    // rows) and now serves as KURA's own service-level superuser, distinct from the platform-wide
    // SSO superuser wired in AuthController.SsoExchange.
    public async Task SeedAsync()
    {
        await SeedPlatformOwnerAsync();
        await SeedSuperUserAsync();
        await SeedMiddlewareServiceUserAsync();
        await SeedCommercialDemoTenantAdminAsync();
        await SeedCommercialDemoStaffAsync();
    }

    /// <summary>
    /// Seeds the platform owner account (admin@codevertexafrica.com) linked to CODEVERTEX org.
    /// This is the primary platform admin — similar to how ordering-backend and other Go services
    /// sync the platform owner from auth-api.
    /// </summary>
    private async Task SeedPlatformOwnerAsync()
    {
        var codevertexOrg = await _context.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Code == "CODEVERTEX");

        if (codevertexOrg == null)
        {
            Console.WriteLine("⚠ CODEVERTEX organization not found, skipping platform owner seed");
            return;
        }

        var superuserRole = await _roleManager.FindByNameAsync("Superuser");
        if (superuserRole == null)
        {
            throw new InvalidOperationException("SUPERUSER role not found. Ensure RoleSeeder runs before UserSeeder.");
        }

        // Get or create an HQ station for the platform owner
        var codevertexHq = await _context.Stations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.OrganizationId == codevertexOrg.Id && s.IsHq);

        var platformAdminEmail = "admin@codevertexafrica.com";
        var existingAdmin = await _userManager.FindByEmailAsync(platformAdminEmail);

        if (existingAdmin == null)
        {
            var platformAdmin = new ApplicationUser
            {
                Email = platformAdminEmail,
                NormalizedEmail = platformAdminEmail.ToUpper(),
                UserName = platformAdminEmail,
                NormalizedUserName = platformAdminEmail.ToUpper(),
                FullName = "Platform Administrator",
                PhoneNumber = "+254700000001",
                OrganizationId = codevertexOrg.Id,
                StationId = codevertexHq?.Id,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                TwoFactorEnabled = false,
                LockoutEnabled = false
            };

            var result = await _userManager.CreateAsync(platformAdmin, DefaultPassword);
            if (!result.Succeeded)
            {
                Console.WriteLine($"⚠ Failed to create platform admin: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                return;
            }

            var roleResult = await _userManager.AddToRoleAsync(platformAdmin, "Superuser");
            if (!roleResult.Succeeded)
            {
                Console.WriteLine($"⚠ Failed to assign role to platform admin: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
                return;
            }

            Console.WriteLine($"✓ Seeded platform owner: {platformAdminEmail} → CODEVERTEX org with SUPERUSER role");
            Console.WriteLine($"  Password: {DefaultPassword} (DEVELOPMENT ONLY - change in production!)");
        }
        else
        {
            // Ensure linked to CODEVERTEX org
            if (existingAdmin.OrganizationId != codevertexOrg.Id)
            {
                existingAdmin.OrganizationId = codevertexOrg.Id;
                if (codevertexHq != null) existingAdmin.StationId = codevertexHq.Id;
                await _userManager.UpdateAsync(existingAdmin);
                Console.WriteLine($"✓ Updated platform admin {platformAdminEmail}: linked to CODEVERTEX org");
            }
            else
            {
                Console.WriteLine($"✓ Platform admin {platformAdminEmail} already exists, skipping seed");
            }
        }
    }

    private async Task SeedSuperUserAsync()
    {
        // Link gadmin@masterspace.co.ke to KURA organization (default enforcement tenant)
        var kuraOrg = await _context.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Code == "KURA")
            ?? await _context.Organizations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.IsDefault);

        if (kuraOrg == null)
        {
            throw new InvalidOperationException("KURA organization not found. Ensure UserManagementSeeder runs before UserSeeder.");
        }

        // Get the HQ station for KURA
        var hqStation = await _context.Stations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.OrganizationId == kuraOrg.Id && s.IsHq);

        // Check if SUPERUSER role exists
        var superuserRole = await _roleManager.FindByNameAsync("Superuser");

        if (superuserRole == null)
        {
            throw new InvalidOperationException("SUPERUSER role not found. Ensure RoleSeeder runs before UserSeeder.");
        }

        // Seed superuser: gadmin@masterspace.co.ke — linked to kura org
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
                StationId = hqStation?.Id,
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

            Console.WriteLine($"✓ Seeded superuser: {superUserEmail} linked to {kuraOrg.Name} ({kuraOrg.Code}) and {hqStation?.Name ?? "no station"} with SUPERUSER role");
            Console.WriteLine($"  Password: {DefaultPassword} (DEVELOPMENT ONLY - change in production!)");
        }
        else
        {
            // Update to link to KURA org if currently linked elsewhere
            var updated = false;
            if (existingSuperUser.OrganizationId != kuraOrg.Id)
            {
                existingSuperUser.OrganizationId = kuraOrg.Id;
                updated = true;
            }
            if (existingSuperUser.StationId == null && hqStation != null)
            {
                existingSuperUser.StationId = hqStation.Id;
                updated = true;
            }
            if (updated)
            {
                await _userManager.UpdateAsync(existingSuperUser);
                Console.WriteLine($"✓ Updated superuser {superUserEmail}: linked to {kuraOrg.Name} ({kuraOrg.Code})");
            }
            else
            {
                Console.WriteLine($"✓ Superuser {superUserEmail} already exists, skipping seed");
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
    /// Seeds/repairs admin@demo.codevertexafrica.com as the TRULOAD-DEMO (commercial weighing)
    /// tenant admin. This is the SAME account used platform-wide as the codevertex-demo tenant
    /// admin (auth-api's seedDemoTenantAdmin) — replaces the old truload-only
    /// admin@truload.codevertexafrica.com fallback. Production login is via SSO
    /// (Organization.SsoTenantSlug == "codevertex-demo"); this local account is a dev/test
    /// fallback and self-heals org/role on every run, same pattern as before.
    /// </summary>
    private async Task SeedCommercialDemoTenantAdminAsync()
    {
        var truloadDemoOrg = await _context.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Code == "TRULOAD-DEMO");

        if (truloadDemoOrg == null)
        {
            Console.WriteLine("⚠ TRULOAD-DEMO organization not found, skipping commercial demo tenant admin seed");
            return;
        }

        var demoStation = await _context.Stations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Code == "DEMO-WB-01");

        // Commercial Weighing Manager role is the appropriate admin for a commercial weighbridge
        var commercialManagerRole = await _roleManager.FindByNameAsync("Commercial Weighing Manager");
        if (commercialManagerRole == null)
        {
            Console.WriteLine("⚠ Commercial Weighing Manager role not found, skipping commercial demo tenant admin seed");
            return;
        }

        const string demoAdminEmail = "admin@demo.codevertexafrica.com";
        var existingAdmin = await _userManager.FindByEmailAsync(demoAdminEmail);

        if (existingAdmin == null)
        {
            var demoAdmin = new ApplicationUser
            {
                Email = demoAdminEmail,
                NormalizedEmail = demoAdminEmail.ToUpper(),
                UserName = demoAdminEmail,
                NormalizedUserName = demoAdminEmail.ToUpper(),
                FullName = "Demo Admin",
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
                Console.WriteLine($"⚠ Failed to create commercial demo tenant admin: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                return;
            }

            var roleResult = await _userManager.AddToRoleAsync(demoAdmin, "Commercial Weighing Manager");
            if (!roleResult.Succeeded)
            {
                Console.WriteLine($"⚠ Failed to assign role to commercial demo tenant admin: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
                return;
            }

            Console.WriteLine($"✓ Seeded commercial demo tenant admin: {demoAdminEmail} → TRULOAD-DEMO org ({truloadDemoOrg.Name}), role: Commercial Weighing Manager");
            Console.WriteLine($"  Password: {DefaultPassword} (DEVELOPMENT ONLY — production login is via SSO)");
        }
        else
        {
            var updated = false;
            if (existingAdmin.OrganizationId != truloadDemoOrg.Id)
            {
                existingAdmin.OrganizationId = truloadDemoOrg.Id;
                updated = true;
            }
            if (existingAdmin.StationId == null && demoStation != null)
            {
                existingAdmin.StationId = demoStation.Id;
                updated = true;
            }
            if (updated)
            {
                await _userManager.UpdateAsync(existingAdmin);
                Console.WriteLine($"✓ Updated commercial demo tenant admin {demoAdminEmail}: org + station linked");
            }

            // Repair role: remove any stale roles and ensure Commercial Weighing Manager is assigned
            var currentRoles = await _userManager.GetRolesAsync(existingAdmin);
            if (!currentRoles.Contains("Commercial Weighing Manager"))
            {
                if (currentRoles.Any())
                    await _userManager.RemoveFromRolesAsync(existingAdmin, currentRoles);
                await _userManager.AddToRoleAsync(existingAdmin, "Commercial Weighing Manager");
                Console.WriteLine($"✓ Repaired role for {demoAdminEmail}: removed [{string.Join(", ", currentRoles)}], assigned Commercial Weighing Manager");
            }
            else
            {
                Console.WriteLine($"✓ Commercial demo tenant admin {demoAdminEmail} already exists with correct role, skipping seed");
            }
        }
    }

    /// <summary>
    /// Seeds a small curated set of commercial-weighing demo staff under codevertex-demo,
    /// mirroring auth-api's erpDemoStaff convention (one demo user per role that matters for
    /// the use case, not a full role roster). Deliberately does NOT reintroduce enforcement-side
    /// demo staff (retired SeedEnforcementDemoUsersAsync) — out of scope for the commercial
    /// weighing demo sync.
    /// </summary>
    private async Task SeedCommercialDemoStaffAsync()
    {
        var truloadDemoOrg = await _context.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Code == "TRULOAD-DEMO");

        if (truloadDemoOrg == null)
        {
            Console.WriteLine("⚠ TRULOAD-DEMO organization not found, skipping commercial demo staff seed");
            return;
        }

        var demoStation = await _context.Stations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Code == "DEMO-WB-01");

        var demoUsers = new[]
        {
            new
            {
                Email = "commercial.operator@demo.codevertexafrica.com",
                FullName = "Demo Weighbridge Operator",
                Phone = "+254700000013",
                RoleName = "Commercial Weighing Operator",
                Label = "commercial weighing operator"
            },
            new
            {
                Email = "commercial.finance@demo.codevertexafrica.com",
                FullName = "Demo Finance Officer",
                Phone = "+254700000014",
                RoleName = "Commercial Finance",
                Label = "commercial finance"
            }
        };

        foreach (var userData in demoUsers)
        {
            var role = await _roleManager.FindByNameAsync(userData.RoleName);
            if (role == null)
            {
                Console.WriteLine($"⚠ Role '{userData.RoleName}' not found, skipping {userData.Label} seed");
                continue;
            }

            var existing = await _userManager.FindByEmailAsync(userData.Email);
            if (existing != null)
            {
                // Repair role if the user has wrong roles from a prior seed run
                var currentRoles = await _userManager.GetRolesAsync(existing);
                if (!currentRoles.Contains(userData.RoleName))
                {
                    if (currentRoles.Any())
                        await _userManager.RemoveFromRolesAsync(existing, currentRoles);
                    await _userManager.AddToRoleAsync(existing, userData.RoleName);
                    Console.WriteLine($"✓ Repaired role for {userData.Email}: removed [{string.Join(", ", currentRoles)}], assigned {userData.RoleName}");
                }
                else
                {
                    Console.WriteLine($"✓ Demo {userData.Label} {userData.Email} already exists with correct role, skipping seed");
                }
                continue;
            }

            var user = new ApplicationUser
            {
                Email = userData.Email,
                NormalizedEmail = userData.Email.ToUpper(),
                UserName = userData.Email,
                NormalizedUserName = userData.Email.ToUpper(),
                FullName = userData.FullName,
                PhoneNumber = userData.Phone,
                OrganizationId = truloadDemoOrg.Id,
                StationId = demoStation?.Id,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                TwoFactorEnabled = false,
                LockoutEnabled = false
            };

            var createResult = await _userManager.CreateAsync(user, DefaultPassword);
            if (!createResult.Succeeded)
            {
                Console.WriteLine($"⚠ Failed to create {userData.Label}: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                continue;
            }

            var roleResult = await _userManager.AddToRoleAsync(user, userData.RoleName);
            if (!roleResult.Succeeded)
            {
                Console.WriteLine($"⚠ Failed to assign {userData.RoleName} to {userData.Email}: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
                continue;
            }

            Console.WriteLine($"✓ Seeded {userData.Label}: {userData.Email} → TRULOAD-DEMO org, role: {userData.RoleName}");
            Console.WriteLine($"  Password: {DefaultPassword} (DEVELOPMENT ONLY)");
        }
    }

}
