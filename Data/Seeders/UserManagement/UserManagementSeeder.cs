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
            new Organization
            {
                Id = Guid.NewGuid(),
                Code = "KENHA",
                Name = "Kenya National Highways Authority",
                OrgType = "Government",
                ContactEmail = "info@kenha.go.ke",
                ContactPhone = "+254-20-1234567",
                Address = "KENHA Headquarters, Blue Shield Towers, Hospital Road, Upper Hill, Nairobi",
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
                ContactEmail = "info@kura.go.ke",
                ContactPhone = "+254-20-7654321",
                Address = "KURA Headquarters, Mombasa Road, Nairobi",
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
                ContactEmail = "info@kerra.go.ke",
                ContactPhone = "+254-20-9876543",
                Address = "KERRA Headquarters, Mombasa Road, Nairobi",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Organization
            {
                Id = Guid.NewGuid(),
                Code = "MSS",
                Name = "Masterspace Solutions Ltd",
                OrgType = "Private",
                ContactEmail = "info@masterspace.co.ke",
                ContactPhone = "+254-722-123456",
                Address = "Nairobi, Kenya",
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
        var kura = await _context.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Code == "KURA");

        if (kura == null) return;

        var stations = new List<Station>();

        // KURA Stations (default tenant - users are linked to KURA by default)
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
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
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
                // Update existing station with latest configuration
                var updated = false;

                if (existing.OrganizationId != station.OrganizationId)
                {
                    existing.OrganizationId = station.OrganizationId;
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
                    Console.WriteLine($"✓ Updated station: {station.Name} ({station.Code}) - bidirectional: {station.SupportsBidirectional}");
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
