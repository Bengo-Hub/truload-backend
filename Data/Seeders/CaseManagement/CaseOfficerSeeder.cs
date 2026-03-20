using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Models.Identity;

namespace TruLoad.Backend.Data.Seeders.CaseManagement;

/// <summary>
/// Seeds default complainant officers and case managers for axle load enforcement organizations.
/// Each enforcement org (KURA, KERRA, KENHA) gets:
///   - A complainant user: "{ORG} Through The Republic of Kenya"
///   - A case manager user: "Case Manager" tied to the org
/// These are pre-selected by default when escalating cases.
/// Idempotent - safe to run multiple times.
/// </summary>
public class CaseOfficerSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly TruLoadDbContext _context;
    private const string DefaultPassword = "ChangeMe123!";

    public CaseOfficerSeeder(
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
        // Enforcement organizations that need complainant and case manager accounts
        var enforcementOrgCodes = new[] { "KURA", "KERRA", "KENHA" };

        foreach (var orgCode in enforcementOrgCodes)
        {
            var org = await _context.Organizations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Code == orgCode);

            if (org == null)
            {
                Console.WriteLine($"⚠ Organization {orgCode} not found, skipping case officer seed");
                continue;
            }

            // Get HQ station for this org
            var hqStation = await _context.Stations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.OrganizationId == org.Id && s.IsHq);

            // Get default station if no HQ
            var station = hqStation ?? await _context.Stations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.OrganizationId == org.Id && s.IsDefault);

            await SeedComplainantAsync(org.Id, org.Code, org.Name, station?.Id);
            await SeedCaseManagerAsync(org.Id, org.Code, org.Name, station?.Id);
        }
    }

    /// <summary>
    /// Seeds a complainant officer account for the org.
    /// Name format: "{ORG_NAME} Through The Republic of Kenya"
    /// Email format: "complainant@{org_code_lower}.truload.local"
    /// </summary>
    private async Task SeedComplainantAsync(Guid orgId, string orgCode, string orgName, Guid? stationId)
    {
        var email = $"complainant@{orgCode.ToLowerInvariant()}.truload.local";
        var existingUser = await _userManager.FindByEmailAsync(email);

        if (existingUser == null)
        {
            var user = new ApplicationUser
            {
                Email = email,
                NormalizedEmail = email.ToUpper(),
                UserName = email,
                NormalizedUserName = email.ToUpper(),
                FullName = $"{orgName} Through The Republic of Kenya",
                PhoneNumber = $"+254700000{orgCode.GetHashCode() % 100:D2}",
                OrganizationId = orgId,
                StationId = stationId,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                TwoFactorEnabled = false,
                LockoutEnabled = false
            };

            var result = await _userManager.CreateAsync(user, DefaultPassword);
            if (!result.Succeeded)
            {
                Console.WriteLine($"⚠ Failed to create complainant for {orgCode}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                return;
            }

            // Assign Enforcement Officer role
            var roleName = "Enforcement Officer";
            if (await _roleManager.FindByNameAsync(roleName) != null)
            {
                await _userManager.AddToRoleAsync(user, roleName);
            }

            Console.WriteLine($"✓ Seeded complainant officer for {orgCode}: {user.FullName} ({email})");
        }
        else
        {
            // Update name if changed
            var expectedName = $"{orgName} Through The Republic of Kenya";
            if (existingUser.FullName != expectedName)
            {
                existingUser.FullName = expectedName;
                await _userManager.UpdateAsync(existingUser);
                Console.WriteLine($"✓ Updated complainant name for {orgCode}: {expectedName}");
            }
        }
    }

    /// <summary>
    /// Seeds a case manager account and CaseManager record for the org.
    /// Name format: "{ORG_CODE} Case Manager"
    /// Email format: "casemanager@{org_code_lower}.truload.local"
    /// </summary>
    private async Task SeedCaseManagerAsync(Guid orgId, string orgCode, string orgName, Guid? stationId)
    {
        var email = $"casemanager@{orgCode.ToLowerInvariant()}.truload.local";
        var existingUser = await _userManager.FindByEmailAsync(email);
        ApplicationUser caseManagerUser;

        if (existingUser == null)
        {
            caseManagerUser = new ApplicationUser
            {
                Email = email,
                NormalizedEmail = email.ToUpper(),
                UserName = email,
                NormalizedUserName = email.ToUpper(),
                FullName = $"{orgCode} Case Manager",
                PhoneNumber = $"+254700001{orgCode.GetHashCode() % 100:D2}",
                OrganizationId = orgId,
                StationId = stationId,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                TwoFactorEnabled = false,
                LockoutEnabled = false
            };

            var result = await _userManager.CreateAsync(caseManagerUser, DefaultPassword);
            if (!result.Succeeded)
            {
                Console.WriteLine($"⚠ Failed to create case manager for {orgCode}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                return;
            }

            // Assign Station Manager role (has case management permissions)
            var roleName = "Station Manager";
            if (await _roleManager.FindByNameAsync(roleName) != null)
            {
                await _userManager.AddToRoleAsync(caseManagerUser, roleName);
            }

            Console.WriteLine($"✓ Seeded case manager user for {orgCode}: {caseManagerUser.FullName} ({email})");
        }
        else
        {
            caseManagerUser = existingUser;
        }

        // Ensure CaseManager entity record exists for this user
        var existingCm = await _context.CaseManagers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(cm => cm.UserId == caseManagerUser.Id && cm.RoleType == "case_manager");

        if (existingCm == null)
        {
            var caseManager = new CaseManager
            {
                Id = Guid.NewGuid(),
                UserId = caseManagerUser.Id,
                RoleType = "case_manager",
                Specialization = "Axle Load Enforcement",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.CaseManagers.Add(caseManager);
            await _context.SaveChangesAsync();
            Console.WriteLine($"✓ Created CaseManager record for {orgCode} case manager (UserId: {caseManagerUser.Id})");
        }
        else
        {
            Console.WriteLine($"✓ CaseManager record already exists for {orgCode} case manager");
        }
    }
}
