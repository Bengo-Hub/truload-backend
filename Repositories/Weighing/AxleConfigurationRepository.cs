using Microsoft.EntityFrameworkCore;
using truload_backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.Weighing.Interfaces;

namespace TruLoad.Backend.Repositories.Weighing;

public class AxleConfigurationRepository : IAxleConfigurationRepository
{
    private readonly TruLoadDbContext _context;
    private readonly ILogger<AxleConfigurationRepository> _logger;

    public AxleConfigurationRepository(
        TruLoadDbContext context,
        ILogger<AxleConfigurationRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<AxleConfiguration>> GetAllAsync(
        bool? isStandard = null,
        string? legalFramework = null,
        int? axleCount = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AxleConfigurations.AsQueryable();

        if (isStandard.HasValue)
        {
            query = query.Where(ac => ac.IsStandard == isStandard.Value);
        }

        if (!string.IsNullOrWhiteSpace(legalFramework))
        {
            query = query.Where(ac => ac.LegalFramework == legalFramework);
        }

        if (axleCount.HasValue)
        {
            query = query.Where(ac => ac.AxleNumber == axleCount.Value);
        }

        if (!includeInactive)
        {
            query = query.Where(ac => ac.IsActive);
        }

        return await query
            .OrderBy(ac => ac.AxleNumber)
            .ThenBy(ac => ac.AxleCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<AxleConfiguration?> GetByIdAsync(
        Guid id,
        bool includeWeightReferences = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AxleConfigurations.AsQueryable();

        if (includeWeightReferences)
        {
            query = query.Include(ac => ac.AxleWeightReferences.OrderBy(wr => wr.AxlePosition));
        }

        return await query.FirstOrDefaultAsync(ac => ac.Id == id, cancellationToken);
    }

    public async Task<AxleConfiguration?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        return await _context.AxleConfigurations
            .FirstOrDefaultAsync(ac => ac.AxleCode == code, cancellationToken);
    }

    public async Task<AxleConfiguration> CreateDerivedConfigAsync(
        AxleConfiguration config,
        CancellationToken cancellationToken = default)
    {
        // Validation
        var (isValid, errors) = await ValidateDerivedConfigAsync(config, cancellationToken);
        if (!isValid)
        {
            throw new InvalidOperationException($"Derived configuration validation failed: {string.Join(", ", errors)}");
        }

        // Ensure it's marked as derived (not standard)
        config.IsStandard = false;
        config.CreatedAt = DateTime.UtcNow;

        _context.AxleConfigurations.Add(config);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created derived axle configuration {AxleCode} with GVW {GVW}kg",
            config.AxleCode, config.GvwPermissibleKg);

        return config;
    }

    public async Task<AxleConfiguration> UpdateDerivedConfigAsync(
        AxleConfiguration config,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.AxleConfigurations.FindAsync(new object[] { config.Id }, cancellationToken);
        if (existing == null)
        {
            throw new KeyNotFoundException($"Axle configuration {config.Id} not found");
        }

        if (existing.IsStandard)
        {
            throw new InvalidOperationException("Cannot modify standard EAC configurations");
        }

        // Validation
        var (isValid, errors) = await ValidateDerivedConfigAsync(config, cancellationToken);
        if (!isValid)
        {
            throw new InvalidOperationException($"Derived configuration validation failed: {string.Join(", ", errors)}");
        }

        // Update fields
        existing.AxleCode = config.AxleCode;
        existing.AxleNumber = config.AxleNumber;
        existing.LegalFramework = config.LegalFramework;
        existing.Description = config.Description;
        existing.GvwPermissibleKg = config.GvwPermissibleKg;
        existing.VisualDiagramUrl = config.VisualDiagramUrl;
        existing.IsActive = config.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated derived axle configuration {AxleCode}", config.AxleCode);

        return existing;
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var config = await _context.AxleConfigurations.FindAsync(new object[] { id }, cancellationToken);
        if (config == null)
        {
            return false;
        }

        if (config.IsStandard)
        {
            throw new InvalidOperationException("Cannot delete standard EAC configurations");
        }

        config.IsActive = false;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Soft deleted axle configuration {AxleCode}", config.AxleCode);

        return true;
    }

    public async Task<bool> CodeExistsAsync(
        string code,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AxleConfigurations.Where(ac => ac.AxleCode == code);

        if (excludeId.HasValue)
        {
            query = query.Where(ac => ac.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<(bool IsValid, List<string> Errors)> ValidateDerivedConfigAsync(
        AxleConfiguration config,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        // Check code uniqueness
        if (await CodeExistsAsync(config.AxleCode, config.Id, cancellationToken))
        {
            errors.Add($"Axle code '{config.AxleCode}' already exists");
        }

        // Validate axle number
        if (config.AxleNumber < 2 || config.AxleNumber > 8)
        {
            errors.Add("Axle number must be between 2 and 8");
        }

        // Validate GVW
        if (config.GvwPermissibleKg <= 0)
        {
            errors.Add("GVW permissible must be greater than 0");
        }

        // Validate legal framework
        if (!new[] { "EAC", "TRAFFIC_ACT", "BOTH" }.Contains(config.LegalFramework))
        {
            errors.Add("Legal framework must be 'EAC', 'TRAFFIC_ACT', or 'BOTH'");
        }

        // Validate required fields
        if (string.IsNullOrWhiteSpace(config.AxleCode))
        {
            errors.Add("Axle code is required");
        }

        if (string.IsNullOrWhiteSpace(config.Description))
        {
            errors.Add("Vehicle configuration description is required");
        }

        return (errors.Count == 0, errors);
    }
}
