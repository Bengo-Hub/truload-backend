using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.DTOs.Yard;

namespace TruLoad.Backend.Services.Interfaces.Yard;

/// <summary>
/// Service interface for vehicle tag operations.
/// Handles tagging vehicles with alerts, warnings, or flags for monitoring.
/// </summary>
public interface IVehicleTagService
{
    /// <summary>
    /// Get a vehicle tag by its ID.
    /// </summary>
    Task<VehicleTagDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Search vehicle tags with filters and pagination.
    /// </summary>
    Task<PagedResponse<VehicleTagDto>> SearchAsync(SearchVehicleTagsRequest request, CancellationToken ct = default);

    /// <summary>
    /// Check if a vehicle has any open tags.
    /// </summary>
    Task<List<VehicleTagDto>> CheckVehicleTagsAsync(string regNo, CancellationToken ct = default);

    /// <summary>
    /// Create a new vehicle tag.
    /// </summary>
    Task<VehicleTagDto> CreateAsync(CreateVehicleTagRequest request, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Close a vehicle tag.
    /// </summary>
    Task<VehicleTagDto> CloseAsync(Guid id, CloseVehicleTagRequest request, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get all tag categories.
    /// </summary>
    Task<List<TagCategoryDto>> GetCategoriesAsync(CancellationToken ct = default);

    /// <summary>
    /// Get tag statistics.
    /// </summary>
    Task<VehicleTagStatisticsDto> GetStatisticsAsync(CancellationToken ct = default);
}

/// <summary>
/// Vehicle tag statistics summary.
/// </summary>
public class VehicleTagStatisticsDto
{
    public int TotalOpen { get; set; }
    public int ClosedToday { get; set; }
    public int CreatedToday { get; set; }
    public Dictionary<string, int> ByCategory { get; set; } = new();
}
