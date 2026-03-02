using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models.System;

namespace TruLoad.Backend.Data.Seeders.SystemConfiguration;

/// <summary>
/// Seeds initial document sequences for the first organization so the document sequences list is populated.
/// Sequences are also created on first use by DocumentNumberService; this seed ensures defaults exist (e.g. weight_ticket, reweigh_ticket).
/// Idempotent - safe to run multiple times.
/// </summary>
public class DocumentSequenceSeeder
{
    private readonly TruLoadDbContext _context;

    public DocumentSequenceSeeder(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync()
    {
        var org = await _context.Organizations.FirstOrDefaultAsync();
        if (org == null) return;

        var types = new[] { DocumentTypes.WeightTicket, DocumentTypes.ReweighTicket };

        foreach (var documentType in types)
        {
            var exists = await _context.DocumentSequences.AnyAsync(s =>
                s.OrganizationId == org.Id &&
                s.StationId == null &&
                s.DocumentType == documentType);

            if (!exists)
            {
                _context.DocumentSequences.Add(new DocumentSequence
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = org.Id,
                    StationId = null,
                    DocumentType = documentType,
                    CurrentSequence = 0,
                    ResetFrequency = "daily",
                    LastResetDate = DateTime.UtcNow,
                });
            }
        }

        await _context.SaveChangesAsync();
    }
}
