using System.ComponentModel.DataAnnotations;

namespace TruLoad.Backend.DTOs.Shared;

/// <summary>
/// Base class for paginated search/list requests.
/// All search DTOs should inherit from this to ensure consistent pagination.
/// </summary>
public abstract class PagedRequest
{
    /// <summary>
    /// Page number (1-indexed). Default: 1.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Number of items per page. Default: 50. Maximum: 100.
    /// </summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// Computed skip value for LINQ queries. Do not bind from query string.
    /// </summary>
    public int Skip => (PageNumber - 1) * PageSize;
}
