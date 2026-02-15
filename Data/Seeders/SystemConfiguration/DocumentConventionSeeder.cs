using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models.System;

namespace TruLoad.Backend.Data.Seeders.SystemConfiguration;

/// <summary>
/// Seeds default document naming conventions for all document types.
/// Idempotent - safe to run multiple times.
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
        // Get the first organization (or skip if none exist yet)
        var org = await _context.Organizations.FirstOrDefaultAsync();
        if (org == null) return;

        var conventions = new[]
        {
            new DocumentConvention
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                DocumentType = DocumentTypes.WeightTicket,
                DisplayName = "Weight Ticket",
                Prefix = "",
                IncludeStationCode = true,
                IncludeBound = true,
                IncludeDate = true,
                DateFormat = "yyyyMMdd",
                IncludeVehicleReg = true,
                SequencePadding = 4,
                Separator = "-",
                IsActive = true
            },
            new DocumentConvention
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                DocumentType = DocumentTypes.Invoice,
                DisplayName = "Invoice",
                Prefix = "INV",
                IncludeStationCode = false,
                IncludeBound = false,
                IncludeDate = true,
                DateFormat = "ddMMyy",
                IncludeVehicleReg = false,
                SequencePadding = 4,
                Separator = "-",
                IsActive = true
            },
            new DocumentConvention
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                DocumentType = DocumentTypes.Receipt,
                DisplayName = "Receipt",
                Prefix = "RCP",
                IncludeStationCode = false,
                IncludeBound = false,
                IncludeDate = true,
                DateFormat = "ddMMyy",
                IncludeVehicleReg = false,
                SequencePadding = 4,
                Separator = "-",
                IsActive = true
            },
            new DocumentConvention
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                DocumentType = DocumentTypes.ChargeSheet,
                DisplayName = "Charge Sheet",
                Prefix = "CS",
                IncludeStationCode = true,
                IncludeBound = false,
                IncludeDate = true,
                DateFormat = "yyyyMMdd",
                IncludeVehicleReg = false,
                SequencePadding = 4,
                Separator = "-",
                IsActive = true
            },
            new DocumentConvention
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                DocumentType = DocumentTypes.ComplianceCertificate,
                DisplayName = "Compliance Certificate",
                Prefix = "CC",
                IncludeStationCode = true,
                IncludeBound = false,
                IncludeDate = true,
                DateFormat = "yyyyMMdd",
                IncludeVehicleReg = false,
                SequencePadding = 4,
                Separator = "-",
                IsActive = true
            },
            new DocumentConvention
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                DocumentType = DocumentTypes.ProhibitionOrder,
                DisplayName = "Prohibition Order",
                Prefix = "PO",
                IncludeStationCode = true,
                IncludeBound = false,
                IncludeDate = true,
                DateFormat = "yyyyMMdd",
                IncludeVehicleReg = false,
                SequencePadding = 4,
                Separator = "-",
                IsActive = true
            },
            new DocumentConvention
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                DocumentType = DocumentTypes.SpecialRelease,
                DisplayName = "Special Release Certificate",
                Prefix = "SR",
                IncludeStationCode = true,
                IncludeBound = false,
                IncludeDate = true,
                DateFormat = "yyyyMMdd",
                IncludeVehicleReg = false,
                SequencePadding = 4,
                Separator = "-",
                IsActive = true
            },
            new DocumentConvention
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                DocumentType = DocumentTypes.LoadCorrectionMemo,
                DisplayName = "Load Correction Memo",
                Prefix = "LCM",
                IncludeStationCode = true,
                IncludeBound = false,
                IncludeDate = true,
                DateFormat = "yyyyMMdd",
                IncludeVehicleReg = false,
                SequencePadding = 4,
                Separator = "-",
                IsActive = true
            },
            new DocumentConvention
            {
                Id = Guid.NewGuid(),
                OrganizationId = org.Id,
                DocumentType = DocumentTypes.CourtMinutes,
                DisplayName = "Court Minutes",
                Prefix = "CM",
                IncludeStationCode = true,
                IncludeBound = false,
                IncludeDate = true,
                DateFormat = "yyyyMMdd",
                IncludeVehicleReg = false,
                SequencePadding = 4,
                Separator = "-",
                IsActive = true
            },
        };

        foreach (var convention in conventions)
        {
            var exists = await _context.DocumentConventions.AnyAsync(c =>
                c.OrganizationId == convention.OrganizationId &&
                c.DocumentType == convention.DocumentType);

            if (!exists)
            {
                _context.DocumentConventions.Add(convention);
            }
        }

        await _context.SaveChangesAsync();
    }
}
