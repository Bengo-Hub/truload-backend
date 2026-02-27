using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Technical;

namespace TruLoad.Backend.Data.Seeders.Technical;

public class AnnualCalibrationSeeder
{
    private readonly TruLoadDbContext _context;

    public AnnualCalibrationSeeder(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync()
    {
        var mobileStation = await _context.Stations
            .FirstOrDefaultAsync(s => s.IsDefault) ?? await _context.Stations
            .FirstOrDefaultAsync(s => s.Code == "NRB-MOBILE-01");

        if (mobileStation == null)
        {
            Console.WriteLine("⚠ No default station found, skipping Annual Calibration seeding.");
            return;
        }

        var kuraOrg = await _context.Organizations
            .FirstOrDefaultAsync(o => o.IsDefault) ?? await _context.Organizations
            .FirstOrDefaultAsync(o => o.Code == "KURA");

        if (kuraOrg == null) return;

        var existingRecord = await _context.Set<AnnualCalibrationRecord>()
            .FirstOrDefaultAsync(c => c.StationId == mobileStation.Id);

        if (existingRecord == null)
        {
            var record = new AnnualCalibrationRecord
            {
                Id = Guid.NewGuid(),
                OrganizationId = kuraOrg.Id,
                StationId = mobileStation.Id,
                CertificateNo = "CAL-CAP513-001",
                IssueDate = DateTime.UtcNow.AddDays(-30),
                ExpiryDate = DateTime.UtcNow.AddDays(335), // 1 year validity
                TargetWeightKg = 18000,
                MaxDeviationKg = 50,
                CertificateFileUrl = "/technical/callibration-certificate.pdf",
                Status = "active"
            };

            _context.Set<AnnualCalibrationRecord>().Add(record);
            await _context.SaveChangesAsync();
            Console.WriteLine($"✓ Seeded Annual Calibration Record for station {mobileStation.Name}");
        }
        else
        {
            Console.WriteLine($"✓ Annual Calibration Record for station {mobileStation.Name} already exists, skipping seed.");
        }
    }
}
