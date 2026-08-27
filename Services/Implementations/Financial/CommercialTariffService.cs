using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Financial;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Models.Financial;
using TruLoad.Backend.Services.Interfaces.Financial;

namespace TruLoad.Backend.Services.Implementations.Financial;

public class CommercialTariffService : ICommercialTariffService
{
    private readonly TruLoadDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CommercialTariffService> _logger;

    public CommercialTariffService(
        TruLoadDbContext dbContext,
        ITenantContext tenantContext,
        ILogger<CommercialTariffService> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<List<CommercialTariffRuleDto>> GetAllAsync(CancellationToken ct = default)
    {
        var orgId = _tenantContext.OrganizationId;
        var rules = await _dbContext.CommercialTariffRules
            .AsNoTracking()
            .Include(r => r.Transporter)
            .Where(r => r.OrganizationId == orgId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return rules.Select(MapToDto).ToList();
    }

    public async Task<CommercialTariffRuleDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var orgId = _tenantContext.OrganizationId;
        var rule = await _dbContext.CommercialTariffRules
            .AsNoTracking()
            .Include(r => r.Transporter)
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId, ct);

        return rule == null ? null : MapToDto(rule);
    }

    public async Task<CommercialTariffRuleDto> CreateAsync(CreateCommercialTariffRuleRequest request, CancellationToken ct = default)
    {
        var orgId = _tenantContext.OrganizationId;

        var rule = new CommercialTariffRule
        {
            OrganizationId = orgId,
            TransporterId = request.TransporterId,
            VehicleType = request.VehicleType,
            AxleCountMin = request.AxleCountMin,
            AxleCountMax = request.AxleCountMax,
            WeightBracketMinKg = request.WeightBracketMinKg,
            WeightBracketMaxKg = request.WeightBracketMaxKg,
            FeeKes = request.FeeKes,
            EffectiveFrom = request.EffectiveFrom ?? DateTime.UtcNow,
            EffectiveTo = request.EffectiveTo,
            Label = request.Label
        };

        _dbContext.CommercialTariffRules.Add(rule);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created commercial tariff rule {RuleId} ({FeeKes} KES) for org {OrgId}", rule.Id, rule.FeeKes, orgId);

        return await GetByIdAsync(rule.Id, ct) ?? MapToDto(rule);
    }

    public async Task<CommercialTariffRuleDto?> UpdateAsync(Guid id, UpdateCommercialTariffRuleRequest request, CancellationToken ct = default)
    {
        var orgId = _tenantContext.OrganizationId;
        var rule = await _dbContext.CommercialTariffRules
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId, ct);
        if (rule == null) return null;

        if (request.TransporterId.HasValue) rule.TransporterId = request.TransporterId;
        if (request.VehicleType != null) rule.VehicleType = request.VehicleType;
        if (request.AxleCountMin.HasValue) rule.AxleCountMin = request.AxleCountMin;
        if (request.AxleCountMax.HasValue) rule.AxleCountMax = request.AxleCountMax;
        if (request.WeightBracketMinKg.HasValue) rule.WeightBracketMinKg = request.WeightBracketMinKg;
        if (request.WeightBracketMaxKg.HasValue) rule.WeightBracketMaxKg = request.WeightBracketMaxKg;
        if (request.FeeKes.HasValue) rule.FeeKes = request.FeeKes.Value;
        if (request.EffectiveFrom.HasValue) rule.EffectiveFrom = request.EffectiveFrom.Value;
        if (request.EffectiveTo.HasValue) rule.EffectiveTo = request.EffectiveTo;
        if (request.Label != null) rule.Label = request.Label;
        if (request.IsActive.HasValue) rule.IsActive = request.IsActive.Value;
        rule.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        return await GetByIdAsync(rule.Id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var orgId = _tenantContext.OrganizationId;
        var rule = await _dbContext.CommercialTariffRules
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == orgId, ct);
        if (rule == null) return false;

        _dbContext.CommercialTariffRules.Remove(rule);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    private static CommercialTariffRuleDto MapToDto(CommercialTariffRule r) => new()
    {
        Id = r.Id,
        TransporterId = r.TransporterId,
        TransporterName = r.Transporter?.Name,
        VehicleType = r.VehicleType,
        AxleCountMin = r.AxleCountMin,
        AxleCountMax = r.AxleCountMax,
        WeightBracketMinKg = r.WeightBracketMinKg,
        WeightBracketMaxKg = r.WeightBracketMaxKg,
        FeeKes = r.FeeKes,
        EffectiveFrom = r.EffectiveFrom,
        EffectiveTo = r.EffectiveTo,
        Label = r.Label,
        IsActive = r.IsActive,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt
    };
}
