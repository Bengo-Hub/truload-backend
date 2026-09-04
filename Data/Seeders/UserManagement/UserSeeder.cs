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
    // confirmed to have zero real references before deletion. The codevertex-demo convention that
    // replaced them (SeedCommercialDemoTenantAdminAsync/SeedCommercialDemoStaffAsync) was itself
    // retired 2026-09-03: Services/Background/AuthDemoSyncService.cs now consumes auth-api's
    // "auth" NATS stream and is the sole source of truth for CODEVERTEX-DEMO's commercial-weighing
    // demo staff (commercial.operator@/commercial.finance@demo.codevertexafrica.com), so the two
    // local hand-maintained seed methods would only drift against it, not add anything it doesn't
    // already cover. NOTE: admin@demo.codevertexafrica.com (the demo tenant admin) is genuinely
    // NOT covered by the syncer — auth-api's seedDemoTenantAdmin publishes it with the generic
    // role "admin", which AuthDemoSyncService's RoleMap deliberately excludes (same
    // ambiguous-role-name guard that prevents cross-vertical demo leaks, see the class of bug
    // documented in [[hospital-demo-tenant-leak-and-fleet-backfill-cleanup-2026-08-30]]). That
    // account is left as a known gap, not silently papered over — see the TruLoad Phase 5b/5c
    // audit notes for the follow-up decision.
    // gadmin@masterspace.co.ke was investigated and deliberately KEPT (see SeedSuperUserAsync) —
    // it has real linked KURA production data (a live prosecution case, thousands of audit log
    // rows) and now serves as KURA's own service-level superuser, distinct from the platform-wide
    // SSO superuser wired in AuthController.SsoExchange.
    public async Task SeedAsync()
    {
        await SeedPlatformOwnerAsync();
        await SeedSuperUserAsync();
        await SeedMiddlewareServiceUserAsync();
        await SeedMiddlewareDemoServiceUserAsync();
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
    /// Seeds the dedicated TruConnect demo/training service account
    /// (middleware-demo@truconnect.local), linked to CODEVERTEX-DEMO org / DEMO-WB-01 station.
    /// Deliberately a SEPARATE account from SeedMiddlewareServiceUserAsync's
    /// middleware@truconnect.local (which stays linked to the live KURA org) — so a single
    /// TruConnect device can never be simultaneously "live" and "demo": Settings' Demo/Training
    /// Mode toggle points at this account, never the live one. See AuthDemoSyncService.cs for the
    /// codevertex-demo auth-api personas this pairs with.
    /// </summary>
    private async Task SeedMiddlewareDemoServiceUserAsync()
    {
        var middlewareRole = await _roleManager.FindByNameAsync("Middleware Service");
        if (middlewareRole == null)
        {
            Console.WriteLine("⚠ MIDDLEWARE_SERVICE role not found, skipping middleware demo user seed");
            return;
        }

        var truloadDemoOrg = await _context.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Code == "CODEVERTEX-DEMO");

        if (truloadDemoOrg == null)
        {
            Console.WriteLine("⚠ CODEVERTEX-DEMO organization not found, skipping middleware demo user seed");
            return;
        }

        var demoStation = await _context.Stations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Code == "DEMO-WB-01");

        const string middlewareDemoEmail = "middleware-demo@truconnect.local";
        var existingUser = await _userManager.FindByEmailAsync(middlewareDemoEmail);

        if (existingUser == null)
        {
            var middlewareDemoUser = new ApplicationUser
            {
                Email = middlewareDemoEmail,
                NormalizedEmail = middlewareDemoEmail.ToUpper(),
                UserName = middlewareDemoEmail,
                NormalizedUserName = middlewareDemoEmail.ToUpper(),
                FullName = "TruConnect Demo/Training Middleware",
                OrganizationId = truloadDemoOrg.Id,
                StationId = demoStation?.Id,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                TwoFactorEnabled = false,
                LockoutEnabled = false
            };

            var result = await _userManager.CreateAsync(middlewareDemoUser, DefaultPassword);
            if (!result.Succeeded)
            {
                Console.WriteLine($"⚠ Failed to create middleware demo user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                return;
            }

            var roleResult = await _userManager.AddToRoleAsync(middlewareDemoUser, "Middleware Service");
            if (!roleResult.Succeeded)
            {
                Console.WriteLine($"⚠ Failed to assign role to middleware demo user: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
                return;
            }

            Console.WriteLine($"✓ Seeded middleware demo service user: {middlewareDemoEmail} → CODEVERTEX-DEMO org with MIDDLEWARE_SERVICE role");
            Console.WriteLine($"  Password: {DefaultPassword} (DEVELOPMENT ONLY - change in production!)");
        }
        else
        {
            var updated = false;
            if (existingUser.OrganizationId != truloadDemoOrg.Id)
            {
                existingUser.OrganizationId = truloadDemoOrg.Id;
                updated = true;
            }
            if (existingUser.StationId == null && demoStation != null)
            {
                existingUser.StationId = demoStation.Id;
                updated = true;
            }
            if (updated)
            {
                await _userManager.UpdateAsync(existingUser);
                Console.WriteLine($"✓ Updated middleware demo user {middlewareDemoEmail}: linked to CODEVERTEX-DEMO org/station");
            }
            else
            {
                Console.WriteLine($"✓ Middleware demo service user {middlewareDemoEmail} already exists, skipping seed");
            }
        }
    }

}
