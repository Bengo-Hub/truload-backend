using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models.System;

namespace TruLoad.Backend.Data.Seeders.SystemConfiguration;

/// <summary>
/// Seeds default document naming conventions for all document types.
/// Uses DocumentSeedDefinitions so conventions stay aligned with document sequences (same types, matching ResetFrequency).
/// DocumentNumberService links convention and sequence by OrganizationId + DocumentType when generating numbers.
/// Idempotent - safe to run multiple times. Seeds for all organizations (like DocumentSequenceSeeder).
/// </summary>
public class DocumentConventionSeeder
{
    private readonly TruLoadDbContext _context;

    public DocumentConventionSeeder(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync()
    {
        var orgs = await _context.Organizations.ToListAsync();
        if (orgs.Count == 0) return;

        foreach (var org in orgs)
        {
            foreach (var entry in DocumentSeedDefinitions.All)
            {
                var exists = await _context.DocumentConventions.AnyAsync(c =>
                    c.OrganizationId == org.Id &&
                    c.DocumentType == entry.DocumentType);

                if (!exists)
                {
                    _context.DocumentConventions.Add(new DocumentConvention
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = org.Id,
                        DocumentType = entry.DocumentType,
                        DisplayName = entry.DisplayName,
                        Prefix = entry.Prefix,
                        IncludeStationCode = entry.IncludeStationCode,
                        IncludeBound = entry.IncludeBound,
                        IncludeDate = entry.IncludeDate,
                        DateFormat = entry.DateFormat,
                        IncludeVehicleReg = entry.IncludeVehicleReg,
                        SequencePadding = entry.SequencePadding,
                        Separator = entry.Separator,
                        ResetFrequency = entry.ResetFrequency,
                        IsActive = true
                    });
                }
            }
        }

        await _context.SaveChangesAsync();
    }
}
