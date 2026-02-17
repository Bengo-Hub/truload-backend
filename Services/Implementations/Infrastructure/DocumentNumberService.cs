using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Services.Interfaces.Infrastructure;

namespace TruLoad.Backend.Services.Implementations.Infrastructure;

/// <summary>
/// Centralized document number generation service.
/// Provides atomic, concurrency-safe document numbering following configurable conventions.
/// Adapted from ERP DocumentNumberService pattern for TruLoad document types.
/// </summary>
public class DocumentNumberService : IDocumentNumberService
{
    private readonly TruLoadDbContext _context;
    private readonly ILogger<DocumentNumberService> _logger;

    public DocumentNumberService(TruLoadDbContext context, ILogger<DocumentNumberService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> GenerateNumberAsync(
        Guid organizationId,
        Guid? stationId,
        string documentType,
        string? vehicleReg = null,
        string? bound = null)
    {
        const int maxRetries = 3;

        // Get convention for this document type
        var convention = await _context.DocumentConventions
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.OrganizationId == organizationId &&
                c.DocumentType == documentType &&
                c.IsActive);

        // Fallback to default convention if none configured
        convention ??= GetDefaultConvention(documentType);

        // Resolve station code and bound code
        string? stationCode = null;
        string? boundCode = null;

        if (stationId.HasValue && (convention.IncludeStationCode || convention.IncludeBound))
        {
            var station = await _context.Stations
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == stationId.Value);

            if (station != null)
            {
                stationCode = station.Code;

                if (convention.IncludeBound && !string.IsNullOrEmpty(bound))
                {
                    boundCode = bound.ToUpperInvariant() switch
                    {
                        "A" => !string.IsNullOrEmpty(station.BoundACode) ? station.BoundACode : "A",
                        "B" => !string.IsNullOrEmpty(station.BoundBCode) ? station.BoundBCode : "B",
                        _ => bound
                    };
                }
            }
        }

        // Retry loop for concurrency conflicts on sequence increment
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var sequence = await GetOrCreateSequenceAsync(organizationId, stationId, documentType, convention.ResetFrequency);

                sequence.CurrentSequence++;
                sequence.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var number = BuildDocumentNumber(convention, sequence.CurrentSequence, stationCode, boundCode, vehicleReg);

                _logger.LogInformation(
                    "Generated document number: {DocumentNumber} for type {DocumentType}, org {OrgId}, station {StationId}",
                    number, documentType, organizationId, stationId);

                return number;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex,
                    "Concurrency conflict on attempt {Attempt}/{MaxRetries} for {DocumentType}",
                    attempt, maxRetries, documentType);

                if (attempt == maxRetries)
                    throw;

                // Detach conflicting entities so they're re-fetched on next attempt
                foreach (var entry in _context.ChangeTracker.Entries()
                    .Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted))
                {
                    entry.State = EntityState.Detached;
                }

                await Task.Delay(50 * attempt);
            }
        }

        throw new InvalidOperationException("Failed to generate document number after retries");
    }

    public async Task<string> PreviewNextNumberAsync(
        Guid organizationId,
        Guid? stationId,
        string documentType,
        string? stationCode = null,
        string? vehicleReg = null,
        string? bound = null)
    {
        var convention = await _context.DocumentConventions
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.OrganizationId == organizationId &&
                c.DocumentType == documentType &&
                c.IsActive);

        convention ??= GetDefaultConvention(documentType);

        // Resolve bound code from station if needed
        string? boundCode = bound;
        if (stationId.HasValue && convention.IncludeBound && !string.IsNullOrEmpty(bound))
        {
            var station = await _context.Stations
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == stationId.Value);

            if (station != null)
            {
                stationCode ??= station.Code;
                boundCode = bound.ToUpperInvariant() switch
                {
                    "A" => !string.IsNullOrEmpty(station.BoundACode) ? station.BoundACode : "A",
                    "B" => !string.IsNullOrEmpty(station.BoundBCode) ? station.BoundBCode : "B",
                    _ => bound
                };
            }
        }

        // Get current sequence without incrementing
        var sequence = await _context.DocumentSequences
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.OrganizationId == organizationId &&
                s.StationId == stationId &&
                s.DocumentType == documentType);

        var nextSeq = (sequence?.CurrentSequence ?? 0) + 1;

        // Check if reset is due
        if (sequence != null && ShouldResetSequence(sequence.ResetFrequency, sequence.LastResetDate))
        {
            nextSeq = 1;
        }

        return BuildDocumentNumber(convention, nextSeq, stationCode, boundCode, vehicleReg);
    }

    private async Task<DocumentSequence> GetOrCreateSequenceAsync(
        Guid organizationId,
        Guid? stationId,
        string documentType,
        string resetFrequency)
    {
        var sequence = await _context.DocumentSequences
            .FirstOrDefaultAsync(s =>
                s.OrganizationId == organizationId &&
                s.StationId == stationId &&
                s.DocumentType == documentType);

        if (sequence == null)
        {
            sequence = new DocumentSequence
            {
                OrganizationId = organizationId,
                StationId = stationId,
                DocumentType = documentType,
                CurrentSequence = 0,
                ResetFrequency = resetFrequency,
                LastResetDate = DateTime.UtcNow
            };
            _context.DocumentSequences.Add(sequence);
            await _context.SaveChangesAsync();
        }
        else if (ShouldResetSequence(sequence.ResetFrequency, sequence.LastResetDate))
        {
            _logger.LogInformation(
                "Resetting sequence for {DocumentType} (frequency: {Frequency}, last reset: {LastReset})",
                documentType, sequence.ResetFrequency, sequence.LastResetDate);
            sequence.CurrentSequence = 0;
            sequence.LastResetDate = DateTime.UtcNow;
        }

        return sequence;
    }

    private static bool ShouldResetSequence(string resetFrequency, DateTime lastResetDate)
    {
        var now = DateTime.UtcNow;
        return resetFrequency.ToLowerInvariant() switch
        {
            "daily" => lastResetDate.Date < now.Date,
            "monthly" => lastResetDate.Year < now.Year || lastResetDate.Month < now.Month,
            "yearly" => lastResetDate.Year < now.Year,
            "never" => false,
            _ => false
        };
    }

    private static string BuildDocumentNumber(
        DocumentConvention convention,
        int sequenceNumber,
        string? stationCode,
        string? boundCode,
        string? vehicleReg)
    {
        var sep = convention.Separator;
        var parts = new List<string>();

        // Prefix (e.g., "INV", "RCP")
        if (!string.IsNullOrEmpty(convention.Prefix))
        {
            parts.Add(convention.Prefix);
        }

        // Station code (e.g., "NRBM01")
        if (convention.IncludeStationCode && !string.IsNullOrEmpty(stationCode))
        {
            // If we also have a bound code, concatenate station+bound as one part
            if (convention.IncludeBound && !string.IsNullOrEmpty(boundCode))
            {
                parts.Add($"{stationCode}{boundCode}");
            }
            else
            {
                parts.Add(stationCode);
            }
        }
        else if (convention.IncludeBound && !string.IsNullOrEmpty(boundCode))
        {
            parts.Add(boundCode);
        }

        // Date (e.g., "20260214")
        if (convention.IncludeDate)
        {
            var dateStr = DateTime.UtcNow.ToString(convention.DateFormat);
            parts.Add(dateStr);
        }

        // Sequence number (e.g., "0001")
        var seqStr = sequenceNumber.ToString().PadLeft(convention.SequencePadding, '0');
        parts.Add(seqStr);

        // Vehicle registration (e.g., "KDG606L")
        if (convention.IncludeVehicleReg && !string.IsNullOrEmpty(vehicleReg))
        {
            parts.Add(vehicleReg.Trim().ToUpperInvariant().Replace(" ", ""));
        }

        return string.Join(sep, parts);
    }

    private static DocumentConvention GetDefaultConvention(string documentType)
    {
        return documentType switch
        {
            DocumentTypes.WeightTicket => new DocumentConvention
            {
                DocumentType = DocumentTypes.WeightTicket,
                Prefix = "",
                IncludeStationCode = true,
                IncludeBound = true,
                IncludeDate = true,
                DateFormat = "yyyyMMdd",
                IncludeVehicleReg = true,
                SequencePadding = 4,
                Separator = "-",
                ResetFrequency = "daily"
            },
            DocumentTypes.Invoice => new DocumentConvention
            {
                DocumentType = DocumentTypes.Invoice,
                Prefix = "INV",
                IncludeStationCode = false,
                IncludeBound = false,
                IncludeDate = true,
                DateFormat = "ddMMyy",
                IncludeVehicleReg = false,
                SequencePadding = 4,
                Separator = "-",
                ResetFrequency = "never"
            },
            DocumentTypes.Receipt => new DocumentConvention
            {
                DocumentType = DocumentTypes.Receipt,
                Prefix = "RCP",
                IncludeStationCode = false,
                IncludeBound = false,
                IncludeDate = true,
                DateFormat = "ddMMyy",
                IncludeVehicleReg = false,
                SequencePadding = 4,
                Separator = "-",
                ResetFrequency = "never"
            },
            _ => new DocumentConvention
            {
                DocumentType = documentType,
                Prefix = documentType[..Math.Min(3, documentType.Length)].ToUpperInvariant(),
                IncludeStationCode = true,
                IncludeBound = false,
                IncludeDate = true,
                DateFormat = "yyyyMMdd",
                IncludeVehicleReg = false,
                SequencePadding = 4,
                Separator = "-",
                ResetFrequency = "monthly"
            }
        };
    }

}
