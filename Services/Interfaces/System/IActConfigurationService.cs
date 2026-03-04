using TruLoad.Backend.DTOs.System;

namespace TruLoad.Backend.Services.Interfaces.System;

/// <summary>
/// Service interface for managing act configuration data (Traffic Act, EAC Act).
/// Provides read access to fee schedules, tolerances, and demerit points per legal framework.
/// </summary>
public interface IActConfigurationService
{
    /// <summary>
    /// Gets all act definitions with their default status.
    /// </summary>
    Task<List<ActDefinitionDto>> GetAllActsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets a single act definition by ID.
    /// </summary>
    Task<ActDefinitionDto?> GetActByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets the full configuration for an act (fee schedules, tolerances, demerit points).
    /// </summary>
    Task<ActConfigurationDto?> GetActConfigurationAsync(Guid actId, CancellationToken ct = default);

    /// <summary>
    /// Gets the current default act definition.
    /// </summary>
    Task<ActDefinitionDto?> GetDefaultActAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets the default act by updating the compliance.default_act_code setting.
    /// </summary>
    Task<ActDefinitionDto> SetDefaultActAsync(Guid actId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets fee schedules filtered by legal framework.
    /// </summary>
    Task<List<AxleFeeScheduleDto>> GetFeeSchedulesAsync(string legalFramework, CancellationToken ct = default);

    /// <summary>
    /// Gets axle-type-specific overload fee schedules filtered by legal framework.
    /// </summary>
    Task<List<AxleTypeOverloadFeeScheduleDto>> GetAxleTypeFeeSchedulesAsync(string legalFramework, CancellationToken ct = default);

    /// <summary>
    /// Gets tolerance settings filtered by legal framework.
    /// </summary>
    Task<List<ToleranceSettingDto>> GetToleranceSettingsAsync(string legalFramework, CancellationToken ct = default);

    /// <summary>
    /// Gets demerit point schedules filtered by legal framework.
    /// </summary>
    Task<List<DemeritPointScheduleDto>> GetDemeritPointSchedulesAsync(string legalFramework, CancellationToken ct = default);

    /// <summary>
    /// Gets a summary of the acts configuration for dashboard display.
    /// </summary>
    Task<ActConfigurationSummaryDto> GetSummaryAsync(CancellationToken ct = default);

    /// <summary>
    /// Invalidates all cached act configuration data.
    /// </summary>
    void InvalidateCache();

    /// <summary>
    /// Updates a tolerance setting by id. Invalidates tolerance cache.
    /// </summary>
    Task<ToleranceSettingDto?> UpdateToleranceSettingAsync(Guid id, UpdateToleranceSettingRequest request, CancellationToken ct = default);
}
