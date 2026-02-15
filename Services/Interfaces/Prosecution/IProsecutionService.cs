using TruLoad.Backend.DTOs.Prosecution;
using TruLoad.Backend.DTOs.Shared;

namespace TruLoad.Backend.Services.Interfaces.Prosecution;

/// <summary>
/// Service interface for prosecution case management.
/// Handles charge calculation, case creation, and status tracking.
/// </summary>
public interface IProsecutionService
{
    /// <summary>
    /// Get prosecution case by ID
    /// </summary>
    Task<ProsecutionCaseDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Get prosecution case by case register ID
    /// </summary>
    Task<ProsecutionCaseDto?> GetByCaseIdAsync(Guid caseRegisterId, CancellationToken ct = default);

    /// <summary>
    /// Search prosecution cases with filters
    /// </summary>
    Task<PagedResponse<ProsecutionCaseDto>> SearchAsync(ProsecutionSearchCriteria criteria, CancellationToken ct = default);

    /// <summary>
    /// Calculate charges for a weighing transaction
    /// </summary>
    Task<ChargeCalculationResult> CalculateChargesAsync(Guid weighingId, string legalFramework, CancellationToken ct = default);

    /// <summary>
    /// Create prosecution case from case register
    /// </summary>
    Task<ProsecutionCaseDto> CreateFromCaseAsync(Guid caseRegisterId, CreateProsecutionRequest request, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Update prosecution case
    /// </summary>
    Task<ProsecutionCaseDto> UpdateAsync(Guid id, UpdateProsecutionRequest request, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Delete prosecution case (soft delete)
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Get prosecution statistics
    /// </summary>
    Task<ProsecutionStatisticsDto> GetStatisticsAsync(CancellationToken ct = default);

    /// <summary>
    /// Generate certificate number for prosecution
    /// </summary>
    Task<string> GenerateCertificateNumberAsync(CancellationToken ct = default);
}
