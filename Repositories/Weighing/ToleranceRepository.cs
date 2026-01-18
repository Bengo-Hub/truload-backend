using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Weighing.Interfaces;
using TruLoad.Backend.Data;

namespace TruLoad.Backend.Repositories.Weighing;

/// <summary>
/// Repository implementation for ToleranceSetting entity with regulatory compliance lookup
/// Supports Kenya Traffic Act Cap 403 and EAC Act 2016 tolerance rules
/// </summary>
public class ToleranceRepository : IToleranceRepository
{
    private readonly TruLoadDbContext _context;

    public ToleranceRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<List<ToleranceSetting>> GetAllByFrameworkAsync(
        string legalFramework,
        CancellationToken cancellationToken = default)
    {
        return await _context.ToleranceSettings
            .Where(t => t.LegalFramework == legalFramework.ToUpper() || t.LegalFramework == "BOTH")
            .Where(t => t.IsActive)
            .OrderBy(t => t.AppliesTo)
            .ToListAsync(cancellationToken);
    }

    public async Task<ToleranceSetting?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        return await _context.ToleranceSettings
            .Where(t => t.Code == code.ToUpper())
            .Where(t => t.IsActive)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ToleranceSetting?> GetToleranceAsync(
        string legalFramework,
        string appliesTo,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow.Date;

        return await _context.ToleranceSettings
            .Where(t => (t.LegalFramework == legalFramework.ToUpper() || t.LegalFramework == "BOTH"))
            .Where(t => t.AppliesTo == appliesTo.ToUpper() || t.AppliesTo == "BOTH")
            .Where(t => t.IsActive)
            .Where(t => t.EffectiveFrom <= now && (t.EffectiveTo == null || t.EffectiveTo >= now))
            .OrderByDescending(t => t.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> CalculateToleranceKgAsync(
        string legalFramework,
        string appliesTo,
        int permissibleWeightKg,
        CancellationToken cancellationToken = default)
    {
        var toleranceSetting = await GetToleranceAsync(legalFramework, appliesTo, cancellationToken);

        if (toleranceSetting == null)
        {
            // Default to 0 kg tolerance if no setting found (strict enforcement)
            return 0;
        }

        // Check if fixed kg tolerance is specified
        if (toleranceSetting.ToleranceKg.HasValue && toleranceSetting.ToleranceKg.Value > 0)
        {
            return toleranceSetting.ToleranceKg.Value;
        }

        // Otherwise calculate from percentage
        if (toleranceSetting.TolerancePercentage > 0)
        {
            return (int)Math.Round(permissibleWeightKg * (toleranceSetting.TolerancePercentage / 100m));
        }

        // Default to 0 if neither percentage nor fixed kg is set
        return 0;
    }

    public async Task<List<ToleranceSetting>> GetAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow.Date;

        return await _context.ToleranceSettings
            .Where(t => t.IsActive)
            .Where(t => t.EffectiveFrom <= now && (t.EffectiveTo == null || t.EffectiveTo >= now))
            .OrderBy(t => t.LegalFramework)
            .ThenBy(t => t.AppliesTo)
            .ToListAsync(cancellationToken);
    }
}
