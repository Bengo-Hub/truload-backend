using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Data.Seeders.UserManagement;

/// <summary>
/// Seeds user management base data: roles, organizations, departments, stations, work shifts
/// Idempotent - safe to run multiple times
/// </summary>
public class UserManagementSeeder
{
    private readonly TruLoadDbContext _context;

    public UserManagementSeeder(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync()
    {
        // Note: Roles are now seeded by RoleSeeder in DatabaseSeeder
        // This ensures SUPERUSER and all 7 roles are properly created
        
        await SeedOrganizationsAsync();
        await SeedDepartmentsAsync();
        await SeedStationsAsync();
        await SeedWorkShiftsAsync();
    }

    private async Task SeedOrganizationsAsync()
    {
        var organizations = new[]
        {
            // Platform owner organization — linked to platform admin (admin@codevertexitsolutions.com)
            new Organization
            {
                Id = Guid.NewGuid(),
                Code = "CODEVERTEX",
                Name = "CodeVertex IT Solutions",
                OrgType = "Private",
                ContactEmail = "admin@codevertexitsolutions.com",
                ContactPhone = "+254-700-000000",
                Address = "Nairobi, Kenya",
                PrimaryColor = "#5B1C4D",
                SecondaryColor = "#ea8022",
                LogoUrl = "/images/logos/codevertex-logo.png",
                PlatformLogoUrl = "/images/logos/codevertex-logo.png",
                LoginPageImageUrl = "/images/background-images/login-background-image.png",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Organization
            {
                Id = Guid.NewGuid(),
                Code = "KENHA",
                Name = "Kenya National Highways Authority",
                OrgType = "Government",
                TenantType = "AxleLoadEnforcement",
                ContactEmail = "info@kenha.go.ke",
                ContactPhone = "+254-20-1234567",
                Address = "KENHA Headquarters, Blue Shield Towers, Hospital Road, Upper Hill, Nairobi",
                PrimaryColor = "#1a5276",
                SecondaryColor = "#d4ac0d",
                LogoUrl = "/images/logos/kenha-logo.png",
                PlatformLogoUrl = "/images/logos/kenha-logo.png",
                LoginPageImageUrl = "/images/background-images/login-background-image.png",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Organization
            {
                Id = Guid.NewGuid(),
                Code = "KURA",
                Name = "Kenya Urban Roads Authority",
                OrgType = "Government",
                TenantType = "AxleLoadEnforcement",
                ContactEmail = "info@kura.go.ke",
                ContactPhone = "+254-20-7654321",
                Address = "KURA Headquarters, Mombasa Road, Nairobi",
                PrimaryColor = "#0a9f3d",
                SecondaryColor = "#1a1a2e",
                LogoUrl = "/images/logos/kura-logo.png",
                PlatformLogoUrl = "/images/logos/kuraweigh-logo.png",
                LoginPageImageUrl = "/images/background-images/login-background-image.png",
                IsDefault = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Organization
            {
                Id = Guid.NewGuid(),
                Code = "KERRA",
                Name = "Kenya Rural Roads Authority",
                OrgType = "Government",
                TenantType = "AxleLoadEnforcement",
                ContactEmail = "info@kerra.go.ke",
                ContactPhone = "+254-20-9876543",
                Address = "KERRA Headquarters, Mombasa Road, Nairobi",
                PrimaryColor = "#2e7d32",
                SecondaryColor = "#f57c00",
                LogoUrl = "/images/logos/court-of-arms-kenya.png",
                PlatformLogoUrl = "/images/logos/court-of-arms-kenya.png",
                LoginPageImageUrl = "/images/background-images/login-background-image.png",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            // Demo commercial weighing organization — TenantType = CommercialWeighing
            // SsoTenantSlug matches the auth-api "truload" tenant for PKCE/SSO login
            new Organization
            {
                Id = Guid.NewGuid(),
                Code = "TRULOAD-DEMO",
                Name = "TruLoad Demo Weighbridge",
                OrgType = "Private",
                TenantType = "CommercialWeighing",
                SsoTenantSlug = "truload",
                PaymentGateway = "treasury",
                CommercialWeighingFeeKes = 500m,
                EnabledModulesJson = "[\"dashboard\",\"weighing\",\"reporting\",\"users\",\"setup_weighing_metadata\",\"setup_settings\",\"financial_invoices\",\"financial_receipts\"]",
                ContactEmail = "admin@truload.codevertexitsolutions.com",
                ContactPhone = "+254700000010",
                Address = "Nairobi, Kenya",
                PrimaryColor = "#0cbd4a",
                SecondaryColor = "#067a2e",
                LogoUrl = "/truload-logo.svg",
                PlatformLogoUrl = "/truload-logo.svg",
                LoginPageImageUrl = "/images/background-images/login-background-image.png",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        foreach (var org in organizations)
        {
            var existing = await _context.Organizations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Code == org.Code);

            if (existing == null)
            {
                await _context.Organizations.AddAsync(org);
                Console.WriteLine($"✓ Seeded organization: {org.Name} ({org.Code})");
            }
            else
            {
                // Always sync brand settings from seed data (fill any null fields)
                var updated = false;
                if (string.IsNullOrEmpty(existing.PrimaryColor) && !string.IsNullOrEmpty(org.PrimaryColor))
                {
                    existing.PrimaryColor = org.PrimaryColor;
                    updated = true;
                }
                if (string.IsNullOrEmpty(existing.SecondaryColor) && !string.IsNullOrEmpty(org.SecondaryColor))
                {
                    existing.SecondaryColor = org.SecondaryColor;
                    updated = true;
                }
                if (string.IsNullOrEmpty(existing.LogoUrl) && !string.IsNullOrEmpty(org.LogoUrl))
                {
                    existing.LogoUrl = org.LogoUrl;
                    updated = true;
                }
                if (string.IsNullOrEmpty(existing.PlatformLogoUrl) && !string.IsNullOrEmpty(org.PlatformLogoUrl))
                {
                    existing.PlatformLogoUrl = org.PlatformLogoUrl;
                    updated = true;
                }
                if (string.IsNullOrEmpty(existing.LoginPageImageUrl) && !string.IsNullOrEmpty(org.LoginPageImageUrl))
                {
                    existing.LoginPageImageUrl = org.LoginPageImageUrl;
                    updated = true;
                }
                // Sync EnabledModulesJson: always update from seed for commercial tenants
                // to ensure new modules (e.g. financial_invoices, financial_receipts) are picked up
                if (!string.IsNullOrEmpty(org.EnabledModulesJson) && org.EnabledModulesJson != existing.EnabledModulesJson)
                {
                    existing.EnabledModulesJson = org.EnabledModulesJson;
                    updated = true;
                }
                if (updated)
                {
                    existing.UpdatedAt = DateTime.UtcNow;
                    Console.WriteLine($"✓ Updated brand settings for {org.Name} ({org.Code}): logo={existing.LogoUrl}, platform={existing.PlatformLogoUrl}, colors={existing.PrimaryColor}/{existing.SecondaryColor}");
                }
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedDepartmentsAsync()
    {
        // Get organization for department seeding
        var kura = await _context.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Code == "KURA");
        if (kura == null) return;

        var departments = new[]
        {
            new Department
            {
                Id = Guid.NewGuid(),
                Code = "WEIGHBRIDGE",
                Name = "Weighbridge Operations",
                Description = "Manages weighbridge stations and weighing operations",
                OrganizationId = kura.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Department
            {
                Id = Guid.NewGuid(),
                Code = "ENFORCEMENT",
                Name = "Enforcement & Compliance",
                Description = "Handles violations, prosecutions, and compliance enforcement",
                OrganizationId = kura.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        foreach (var dept in departments)
        {
            var existing = await _context.Departments
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.Code == dept.Code && d.OrganizationId == dept.OrganizationId);
            
            if (existing == null)
            {
                await _context.Departments.AddAsync(dept);
                Console.WriteLine($"✓ Seeded department: {dept.Name} ({dept.Code})");
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedStationsAsync()
    {
        var orgs = await _context.Organizations.IgnoreQueryFilters().Where(o => o.IsActive).ToListAsync();
        if (orgs.Count == 0) return;

        foreach (var org in orgs)
        {
            // Ensure one HQ station per organisation (Code must be globally unique, so use {OrgCode}-HQ)
            var hqCode = $"{org.Code}-HQ";
            var existingHq = await _context.Stations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.OrganizationId == org.Id && s.IsHq);
            if (existingHq == null)
            {
                var byCode = await _context.Stations.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Code == hqCode);
                if (byCode == null)
                {
                    await _context.Stations.AddAsync(new Station
                    {
                        Id = Guid.NewGuid(),
                        Code = hqCode,
                        Name = $"{org.Name} Headquarters",
                        StationType = "weigh_bridge",
                        Location = org.Address,
                        OrganizationId = org.Id,
                        IsDefault = false,
                        IsHq = true,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                    Console.WriteLine($"✓ Seeded HQ station for {org.Name} ({org.Code})");
                }
            }
            else if (!existingHq.IsHq)
            {
                existingHq.IsHq = true;
                existingHq.UpdatedAt = DateTime.UtcNow;
                Console.WriteLine($"✓ Updated station {existingHq.Code} as HQ for {org.Name}");
            }
        }

        await _context.SaveChangesAsync();

        var kura = await _context.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Code == "KURA");
        if (kura == null) return;

        var stations = new List<Station>();

        // KURA Stations (default enforcement tenant - users are linked to KURA by default)
        if (kura != null)
        {
            stations.AddRange(new[]
            {
                new Station
                {
                    Id = Guid.NewGuid(),
                    Code = "NRB-MOBILE-01",
                    Name = "Nairobi Mobile Unit 01",
                    StationType = "Mobile",
                    Location = "Nairobi, Kenya (Mobile)",
                    Latitude = -1.286389m,
                    Longitude = 36.817223m,
                    SupportsBidirectional = true,
                    BoundACode = "A",
                    BoundBCode = "B",
                    OrganizationId = kura.Id,
                    IsDefault = true,
                    IsHq = false,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            });
        }

        // TRULOAD-DEMO station: demo commercial weighbridge (fixed platform scale)
        var truloadDemo = await _context.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Code == "TRULOAD-DEMO");
        if (truloadDemo != null)
        {
            stations.Add(new Station
            {
                Id = Guid.NewGuid(),
                Code = "DEMO-WB-01",
                Name = "Demo Weighbridge Station 01",
                StationType = "weigh_bridge",
                Location = "Nairobi, Kenya",
                Latitude = -1.286389m,
                Longitude = 36.817223m,
                SupportsBidirectional = false,
                OrganizationId = truloadDemo.Id,
                IsDefault = true,
                IsHq = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        foreach (var station in stations)
        {
            var existing = await _context.Stations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Code == station.Code);

            if (existing == null)
            {
                await _context.Stations.AddAsync(station);
                Console.WriteLine($"✓ Seeded station: {station.Name} ({station.Code}) for {(station.OrganizationId == kura?.Id ? "KURA" : "KENHA")}");
            }
            else
            {
                // Update existing station with latest configuration (ensure default station is set for calibration/users)
                var updated = false;

                if (existing.OrganizationId != station.OrganizationId)
                {
                    existing.OrganizationId = station.OrganizationId;
                    updated = true;
                }

                if (!existing.IsDefault && station.IsDefault)
                {
                    existing.IsDefault = true;
                    updated = true;
                }

                // Update bidirectional settings
                if (existing.SupportsBidirectional != station.SupportsBidirectional ||
                    existing.BoundACode != station.BoundACode ||
                    existing.BoundBCode != station.BoundBCode)
                {
                    existing.SupportsBidirectional = station.SupportsBidirectional;
                    existing.BoundACode = station.BoundACode;
                    existing.BoundBCode = station.BoundBCode;
                    updated = true;
                }

                if (updated)
                {
                    existing.UpdatedAt = DateTime.UtcNow;
                    Console.WriteLine($"✓ Updated station: {station.Name} ({station.Code}) - bidirectional: {station.SupportsBidirectional}" + (existing.IsDefault ? ", set as default" : ""));
                }
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedWorkShiftsAsync()
    {
        var shifts = new[]
        {
            new WorkShift
            {
                Id = Guid.NewGuid(),
                Name = "Morning Shift",
                Code = "MORNING",
                Description = "Morning shift: 6:00 AM - 2:00 PM",
                TotalHoursPerWeek = 40m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new WorkShift
            {
                Id = Guid.NewGuid(),
                Name = "Night Shift",
                Code = "NIGHT",
                Description = "Night shift: 10:00 PM - 6:00 AM",
                TotalHoursPerWeek = 40m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        foreach (var shift in shifts)
        {
            var existing = await _context.WorkShifts
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Code == shift.Code);
            
            if (existing == null)
            {
                await _context.WorkShifts.AddAsync(shift);
                Console.WriteLine($"✓ Seeded work shift: {shift.Name} ({shift.Code})");
            }
        }

        await _context.SaveChangesAsync();
    }
}
