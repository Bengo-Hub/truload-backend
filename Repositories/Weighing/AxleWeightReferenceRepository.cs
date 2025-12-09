using Microsoft.EntityFrameworkCore;
using truload_backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Weighing.Interfaces;

namespace TruLoad.Backend.Repositories.Weighing;

public class AxleWeightReferenceRepository : IAxleWeightReferenceRepository
{
    private readonly TruLoadDbContext _context;
    private readonly ILogger<AxleWeightReferenceRepository> _logger;

    public AxleWeightReferenceRepository(
        TruLoadDbContext context,
        ILogger<AxleWeightReferenceRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AxleWeightReference?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.AxleWeightReferences
            .Include(awr => awr.AxleGroup)
            .Include(awr => awr.TyreType)
            .FirstOrDefaultAsync(awr => awr.Id == id, cancellationToken);
    }

    public async Task<List<AxleWeightReference>> GetByConfigurationIdAsync(
        Guid configurationId,
        bool includeRelations = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AxleWeightReferences
            .Where(awr => awr.AxleConfigurationId == configurationId);

        if (includeRelations)
        {
            query = query
                .Include(awr => awr.AxleGroup)
                .Include(awr => awr.TyreType);
        }

        return await query
            .OrderBy(awr => awr.AxlePosition)
            .ToListAsync(cancellationToken);
    }

    public async Task<AxleWeightReference?> GetByPositionAsync(
        Guid configurationId,
        int position,
        CancellationToken cancellationToken = default)
    {
        return await _context.AxleWeightReferences
            .Include(awr => awr.AxleGroup)
            .Include(awr => awr.TyreType)
            .FirstOrDefaultAsync(
                awr => awr.AxleConfigurationId == configurationId && awr.AxlePosition == position,
                cancellationToken);
    }

    public async Task<AxleWeightReference> CreateAsync(
        AxleWeightReference reference,
        CancellationToken cancellationToken = default)
    {
        // Verify parent configuration exists
        var config = await _context.AxleConfigurations.FindAsync(
            new object[] { reference.AxleConfigurationId },
            cancellationToken);

        if (config == null)
        {
            throw new KeyNotFoundException($"Axle configuration {reference.AxleConfigurationId} not found");
        }

        _context.AxleWeightReferences.Add(reference);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created weight reference for configuration {ConfigId} at position {Position}",
            reference.AxleConfigurationId,
            reference.AxlePosition);

        return reference;
    }

    public async Task<AxleWeightReference> UpdateAsync(
        AxleWeightReference reference,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.AxleWeightReferences.FindAsync(
            new object[] { reference.Id },
            cancellationToken);

        if (existing == null)
        {
            throw new KeyNotFoundException($"Weight reference {reference.Id} not found");
        }

        // Update fields
        existing.AxlePosition = reference.AxlePosition;
        existing.AxleLegalWeightKg = reference.AxleLegalWeightKg;
        existing.AxleGroupId = reference.AxleGroupId;
        existing.AxleGrouping = reference.AxleGrouping;
        existing.TyreTypeId = reference.TyreTypeId;
        existing.IsActive = reference.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated weight reference {RefId} for configuration {ConfigId}",
            reference.Id,
            reference.AxleConfigurationId);

        return existing;
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var reference = await _context.AxleWeightReferences.FindAsync(
            new object[] { id },
            cancellationToken);

        if (reference == null)
        {
            return false;
        }

        _context.AxleWeightReferences.Remove(reference);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted weight reference {RefId}", id);
        return true;
    }

    public async Task<(bool IsValid, List<string> Errors)> ValidateAsync(
        AxleWeightReference reference,
        AxleConfiguration parentConfig,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        // Validate position is within axle count
        if (reference.AxlePosition < 1 || reference.AxlePosition > parentConfig.AxleNumber)
        {
            errors.Add($"Axle position must be between 1 and {parentConfig.AxleNumber}");
        }

        // Check if position already exists (exclude current if updating)
        if (await PositionExistsAsync(
            reference.AxleConfigurationId,
            reference.AxlePosition,
            reference.Id,
            cancellationToken))
        {
            errors.Add($"Position {reference.AxlePosition} already has a weight reference");
        }

        // Validate axle weight
        if (reference.AxleLegalWeightKg <= 0)
        {
            errors.Add("Axle legal weight must be greater than 0 kg");
        }

        if (reference.AxleLegalWeightKg > parentConfig.GvwPermissibleKg)
        {
            errors.Add($"Axle weight ({reference.AxleLegalWeightKg} kg) cannot exceed GVW ({parentConfig.GvwPermissibleKg} kg)");
        }

        // Validate grouping format (A, B, C, or D)
        if (!new[] { "A", "B", "C", "D" }.Contains(reference.AxleGrouping))
        {
            errors.Add("Axle grouping must be 'A', 'B', 'C', or 'D'");
        }

        // Verify AxleGroup exists if provided
        if (reference.AxleGroupId != Guid.Empty)
        {
            var axleGroup = await _context.AxleGroups.FindAsync(
                new object[] { reference.AxleGroupId },
                cancellationToken);

            if (axleGroup == null)
            {
                errors.Add($"Axle group {reference.AxleGroupId} not found");
            }
        }

        // Verify TyreType exists if provided
        if (reference.TyreTypeId.HasValue && reference.TyreTypeId.Value != Guid.Empty)
        {
            var tyreType = await _context.TyreTypes.FindAsync(
                new object[] { reference.TyreTypeId.Value },
                cancellationToken);

            if (tyreType == null)
            {
                errors.Add($"Tyre type {reference.TyreTypeId} not found");
            }
        }

        return (errors.Count == 0, errors);
    }

    public async Task<bool> PositionExistsAsync(
        Guid configurationId,
        int position,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AxleWeightReferences
            .Where(awr => awr.AxleConfigurationId == configurationId && awr.AxlePosition == position);

        if (excludeId.HasValue)
        {
            query = query.Where(awr => awr.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<int> GetCountByConfigurationAsync(
        Guid configurationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AxleWeightReferences
            .Where(awr => awr.AxleConfigurationId == configurationId)
            .CountAsync(cancellationToken);
    }
}
