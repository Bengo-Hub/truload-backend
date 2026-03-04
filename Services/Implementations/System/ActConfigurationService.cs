using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TruLoad.Backend.Common.Constants;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.System;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Services.Interfaces.System;

namespace TruLoad.Backend.Services.Implementations.System;

/// <summary>
/// Service for managing act configuration data (Traffic Act, EAC Act).
/// Provides read access to fee schedules, tolerances, and demerit points per legal framework.
/// Uses in-memory caching with 30-minute TTL for reference data.
/// </summary>
public class ActConfigurationService : IActConfigurationService
{
    private readonly TruLoadDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ActConfigurationService> _logger;

    private const string CacheKeyPrefix = "ActConfig_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public ActConfigurationService(
        TruLoadDbContext context,
        IMemoryCache cache,
        ISettingsService settingsService,
        ILogger<ActConfigurationService> logger)
    {
        _context = context;
        _cache = cache;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<List<ActDefinitionDto>> GetAllActsAsync(CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}AllActs";

        if (_cache.TryGetValue(cacheKey, out List<ActDefinitionDto>? cached) && cached != null)
            return cached;

        var defaultActCode = await _settingsService.GetSettingValueAsync(
            SettingKeys.DefaultActCode, "TRAFFIC_ACT", ct);

        var acts = await _context.ActDefinitions
            .AsNoTracking()
            .Where(a => a.IsActive && a.DeletedAt == null)
            .OrderBy(a => a.Code)
            .Select(a => MapToActDto(a, defaultActCode))
            .ToListAsync(ct);

        _cache.Set(cacheKey, acts, CacheDuration);
        return acts;
    }

    public async Task<ActDefinitionDto?> GetActByIdAsync(Guid id, CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}Act_{id}";

        if (_cache.TryGetValue(cacheKey, out ActDefinitionDto? cached))
            return cached;

        var defaultActCode = await _settingsService.GetSettingValueAsync(
            SettingKeys.DefaultActCode, "TRAFFIC_ACT", ct);

        var act = await _context.ActDefinitions
            .AsNoTracking()
            .Where(a => a.Id == id && a.IsActive && a.DeletedAt == null)
            .FirstOrDefaultAsync(ct);

        if (act == null) return null;

        var dto = MapToActDto(act, defaultActCode);
        _cache.Set(cacheKey, dto, CacheDuration);
        return dto;
    }

    public async Task<ActConfigurationDto?> GetActConfigurationAsync(Guid actId, CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}FullConfig_{actId}";

        if (_cache.TryGetValue(cacheKey, out ActConfigurationDto? cached))
            return cached;

        var actDto = await GetActByIdAsync(actId, ct);
        if (actDto == null) return null;

        var legalFramework = GetLegalFramework(actDto.ActType);

        var feeSchedules = await GetFeeSchedulesAsync(legalFramework, ct);
        var axleTypeFees = await GetAxleTypeFeeSchedulesAsync(legalFramework, ct);
        var tolerances = await GetToleranceSettingsAsync(legalFramework, ct);
        var demeritPoints = await GetDemeritPointSchedulesAsync(legalFramework, ct);

        var config = new ActConfigurationDto
        {
            Act = actDto,
            FeeSchedules = feeSchedules,
            AxleTypeFeeSchedules = axleTypeFees,
            ToleranceSettings = tolerances,
            DemeritPointSchedules = demeritPoints
        };

        _cache.Set(cacheKey, config, CacheDuration);
        return config;
    }

    public async Task<ActDefinitionDto?> GetDefaultActAsync(CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}DefaultAct";

        if (_cache.TryGetValue(cacheKey, out ActDefinitionDto? cached))
            return cached;

        var defaultActCode = await _settingsService.GetSettingValueAsync(
            SettingKeys.DefaultActCode, "TRAFFIC_ACT", ct);

        var act = await _context.ActDefinitions
            .AsNoTracking()
            .Where(a => a.Code == defaultActCode && a.IsActive && a.DeletedAt == null)
            .FirstOrDefaultAsync(ct);

        if (act == null) return null;

        var dto = MapToActDto(act, defaultActCode);
        _cache.Set(cacheKey, dto, CacheDuration);
        return dto;
    }

    public async Task<ActDefinitionDto> SetDefaultActAsync(Guid actId, Guid userId, CancellationToken ct = default)
    {
        var act = await _context.ActDefinitions
            .Where(a => a.Id == actId && a.IsActive && a.DeletedAt == null)
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Act definition with ID '{actId}' not found");

        await _settingsService.UpdateSettingAsync(
            SettingKeys.DefaultActCode, act.Code, userId, ct);

        InvalidateCache();

        _logger.LogInformation("Default act set to {ActCode} by user {UserId}", act.Code, userId);
        return MapToActDto(act, act.Code);
    }

    public async Task<List<AxleFeeScheduleDto>> GetFeeSchedulesAsync(string legalFramework, CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}FeeSchedules_{legalFramework}";

        if (_cache.TryGetValue(cacheKey, out List<AxleFeeScheduleDto>? cached) && cached != null)
            return cached;

        var schedules = await _context.AxleFeeSchedules
            .AsNoTracking()
            .Where(f => f.IsActive &&
                (f.LegalFramework == legalFramework || f.LegalFramework == BrandingConstants.LegalFramework.Both))
            .OrderBy(f => f.FeeType)
            .ThenBy(f => f.OverloadMinKg)
            .Select(f => new AxleFeeScheduleDto
            {
                Id = f.Id,
                LegalFramework = f.LegalFramework,
                FeeType = f.FeeType,
                OverloadMinKg = f.OverloadMinKg,
                OverloadMaxKg = f.OverloadMaxKg,
                FeePerKgUsd = f.FeePerKgUsd,
                FlatFeeUsd = f.FlatFeeUsd,
                DemeritPoints = f.DemeritPoints,
                PenaltyDescription = f.PenaltyDescription,
                EffectiveFrom = f.EffectiveFrom,
                EffectiveTo = f.EffectiveTo,
                IsActive = f.IsActive
            })
            .ToListAsync(ct);

        _cache.Set(cacheKey, schedules, CacheDuration);
        return schedules;
    }

    public async Task<List<AxleTypeOverloadFeeScheduleDto>> GetAxleTypeFeeSchedulesAsync(string legalFramework, CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}AxleTypeFees_{legalFramework}";

        if (_cache.TryGetValue(cacheKey, out List<AxleTypeOverloadFeeScheduleDto>? cached) && cached != null)
            return cached;

        var schedules = await _context.AxleTypeOverloadFeeSchedules
            .AsNoTracking()
            .Where(f => f.IsActive && f.DeletedAt == null &&
                (f.LegalFramework == legalFramework || f.LegalFramework == BrandingConstants.LegalFramework.Both))
            .OrderBy(f => f.OverloadMinKg)
            .Select(f => new AxleTypeOverloadFeeScheduleDto
            {
                Id = f.Id,
                OverloadMinKg = f.OverloadMinKg,
                OverloadMaxKg = f.OverloadMaxKg,
                SteeringAxleFeeUsd = f.SteeringAxleFeeUsd,
                SingleDriveAxleFeeUsd = f.SingleDriveAxleFeeUsd,
                TandemAxleFeeUsd = f.TandemAxleFeeUsd,
                TridemAxleFeeUsd = f.TridemAxleFeeUsd,
                QuadAxleFeeUsd = f.QuadAxleFeeUsd,
                LegalFramework = f.LegalFramework,
                EffectiveFrom = f.EffectiveFrom,
                EffectiveTo = f.EffectiveTo,
                IsActive = f.IsActive
            })
            .ToListAsync(ct);

        _cache.Set(cacheKey, schedules, CacheDuration);
        return schedules;
    }

    public async Task<List<ToleranceSettingDto>> GetToleranceSettingsAsync(string legalFramework, CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}Tolerances_{legalFramework}";

        if (_cache.TryGetValue(cacheKey, out List<ToleranceSettingDto>? cached) && cached != null)
            return cached;

        var tolerances = await _context.ToleranceSettings
            .AsNoTracking()
            .Where(t => t.IsActive &&
                (t.LegalFramework == legalFramework || t.LegalFramework == BrandingConstants.LegalFramework.Both))
            .OrderBy(t => t.Code)
            .Select(t => new ToleranceSettingDto
            {
                Id = t.Id,
                Code = t.Code,
                Name = t.Name,
                LegalFramework = t.LegalFramework,
                TolerancePercentage = t.TolerancePercentage,
                ToleranceKg = t.ToleranceKg,
                AppliesTo = t.AppliesTo,
                Description = t.Description,
                EffectiveFrom = t.EffectiveFrom,
                EffectiveTo = t.EffectiveTo,
                IsActive = t.IsActive
            })
            .ToListAsync(ct);

        _cache.Set(cacheKey, tolerances, CacheDuration);
        return tolerances;
    }

    public async Task<ToleranceSettingDto?> UpdateToleranceSettingAsync(Guid id, UpdateToleranceSettingRequest request, CancellationToken ct = default)
    {
        var entity = await _context.ToleranceSettings
            .Where(t => t.Id == id)
            .FirstOrDefaultAsync(ct);

        if (entity == null) return null;

        if (request.TolerancePercentage.HasValue)
            entity.TolerancePercentage = request.TolerancePercentage.Value;
        if (request.ToleranceKg.HasValue)
            entity.ToleranceKg = request.ToleranceKg.Value;
        if (request.Description != null)
            entity.Description = request.Description;
        if (request.IsActive.HasValue)
            entity.IsActive = request.IsActive.Value;

        entity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        // Invalidate tolerance caches for this framework and BOTH
        var frameworks = new[] { entity.LegalFramework, BrandingConstants.LegalFramework.Both };
        foreach (var fw in frameworks.Distinct())
        {
            _cache.Remove($"{CacheKeyPrefix}Tolerances_{fw}");
        }
        foreach (var act in await _context.ActDefinitions.Where(a => a.IsActive && a.DeletedAt == null).Select(a => a.Id).ToListAsync(ct))
            _cache.Remove($"{CacheKeyPrefix}FullConfig_{act}");

        _logger.LogInformation("Tolerance setting {Id} updated", id);

        return new ToleranceSettingDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            LegalFramework = entity.LegalFramework,
            TolerancePercentage = entity.TolerancePercentage,
            ToleranceKg = entity.ToleranceKg,
            AppliesTo = entity.AppliesTo,
            Description = entity.Description,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo,
            IsActive = entity.IsActive
        };
    }

    public async Task<List<DemeritPointScheduleDto>> GetDemeritPointSchedulesAsync(string legalFramework, CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}DemeritPoints_{legalFramework}";

        if (_cache.TryGetValue(cacheKey, out List<DemeritPointScheduleDto>? cached) && cached != null)
            return cached;

        var schedules = await _context.DemeritPointSchedules
            .AsNoTracking()
            .Where(d => d.IsActive && d.DeletedAt == null &&
                (d.LegalFramework == legalFramework || d.LegalFramework == BrandingConstants.LegalFramework.Both))
            .OrderBy(d => d.ViolationType)
            .ThenBy(d => d.OverloadMinKg)
            .Select(d => new DemeritPointScheduleDto
            {
                Id = d.Id,
                ViolationType = d.ViolationType,
                OverloadMinKg = d.OverloadMinKg,
                OverloadMaxKg = d.OverloadMaxKg,
                Points = d.Points,
                LegalFramework = d.LegalFramework,
                EffectiveFrom = d.EffectiveFrom,
                IsActive = d.IsActive
            })
            .ToListAsync(ct);

        _cache.Set(cacheKey, schedules, CacheDuration);
        return schedules;
    }

    public async Task<ActConfigurationSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}Summary";

        if (_cache.TryGetValue(cacheKey, out ActConfigurationSummaryDto? cached) && cached != null)
            return cached;

        var defaultAct = await GetDefaultActAsync(ct);

        var totalActs = await _context.ActDefinitions
            .AsNoTracking()
            .CountAsync(a => a.IsActive && a.DeletedAt == null, ct);

        var totalFeeSchedules = await _context.AxleFeeSchedules
            .AsNoTracking()
            .CountAsync(f => f.IsActive, ct);

        var totalTolerances = await _context.ToleranceSettings
            .AsNoTracking()
            .CountAsync(t => t.IsActive, ct);

        var totalDemeritSchedules = await _context.DemeritPointSchedules
            .AsNoTracking()
            .CountAsync(d => d.IsActive && d.DeletedAt == null, ct);

        var summary = new ActConfigurationSummaryDto
        {
            TotalActs = totalActs,
            DefaultActCode = defaultAct?.Code ?? "TRAFFIC_ACT",
            DefaultActName = defaultAct?.Name ?? "Kenya Traffic Act Cap 403",
            DefaultCurrency = defaultAct?.ChargingCurrency ?? "KES",
            TotalFeeSchedules = totalFeeSchedules,
            TotalToleranceSettings = totalTolerances,
            TotalDemeritSchedules = totalDemeritSchedules
        };

        _cache.Set(cacheKey, summary, CacheDuration);
        return summary;
    }

    public void InvalidateCache()
    {
        _logger.LogDebug("Invalidating act configuration cache");
        // Remove all known cache keys by prefix pattern
        // IMemoryCache doesn't support prefix removal, so we clear known keys
        var keys = new[]
        {
            $"{CacheKeyPrefix}AllActs",
            $"{CacheKeyPrefix}DefaultAct",
            $"{CacheKeyPrefix}Summary",
            $"{CacheKeyPrefix}FeeSchedules_{BrandingConstants.LegalFramework.EAC}",
            $"{CacheKeyPrefix}FeeSchedules_{BrandingConstants.LegalFramework.TrafficAct}",
            $"{CacheKeyPrefix}FeeSchedules_{BrandingConstants.LegalFramework.Both}",
            $"{CacheKeyPrefix}AxleTypeFees_{BrandingConstants.LegalFramework.EAC}",
            $"{CacheKeyPrefix}AxleTypeFees_{BrandingConstants.LegalFramework.TrafficAct}",
            $"{CacheKeyPrefix}AxleTypeFees_{BrandingConstants.LegalFramework.Both}",
            $"{CacheKeyPrefix}Tolerances_{BrandingConstants.LegalFramework.EAC}",
            $"{CacheKeyPrefix}Tolerances_{BrandingConstants.LegalFramework.TrafficAct}",
            $"{CacheKeyPrefix}Tolerances_{BrandingConstants.LegalFramework.Both}",
            $"{CacheKeyPrefix}DemeritPoints_{BrandingConstants.LegalFramework.EAC}",
            $"{CacheKeyPrefix}DemeritPoints_{BrandingConstants.LegalFramework.TrafficAct}",
            $"{CacheKeyPrefix}DemeritPoints_{BrandingConstants.LegalFramework.Both}",
        };

        foreach (var key in keys)
            _cache.Remove(key);
    }

    /// <summary>
    /// Maps ActType to the LegalFramework code used in fee schedules/tolerances.
    /// ActType "Traffic" → "TRAFFIC_ACT", ActType "EAC" → "EAC"
    /// </summary>
    private static string GetLegalFramework(string actType)
    {
        return actType switch
        {
            "Traffic" => BrandingConstants.LegalFramework.TrafficAct,
            "EAC" => BrandingConstants.LegalFramework.EAC,
            _ => actType
        };
    }

    private static ActDefinitionDto MapToActDto(ActDefinition act, string defaultActCode)
    {
        return new ActDefinitionDto
        {
            Id = act.Id,
            Code = act.Code,
            Name = act.Name,
            ActType = act.ActType,
            FullName = act.FullName,
            Description = act.Description,
            EffectiveDate = act.EffectiveDate,
            ChargingCurrency = act.ChargingCurrency,
            IsDefault = act.Code == defaultActCode,
            IsActive = act.IsActive,
            CreatedAt = act.CreatedAt,
            UpdatedAt = act.UpdatedAt
        };
    }
}
