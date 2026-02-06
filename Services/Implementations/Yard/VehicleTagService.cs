using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.DTOs.Yard;
using TruLoad.Backend.Models.Yard;
using TruLoad.Backend.Services.Interfaces.Yard;
using TruLoad.Backend.Services.Interfaces.CaseManagement;
using TruLoad.Backend.DTOs.CaseManagement;

namespace TruLoad.Backend.Services.Implementations.Yard;

/// <summary>
/// Service implementation for vehicle tag operations.
/// Manual tags are linked to case register for violation tracking.
/// </summary>
public class VehicleTagService : IVehicleTagService
{
    private readonly TruLoadDbContext _context;
    private readonly ICaseRegisterService _caseRegisterService;
    private readonly ILogger<VehicleTagService> _logger;

    public VehicleTagService(
        TruLoadDbContext context,
        ICaseRegisterService caseRegisterService,
        ILogger<VehicleTagService> logger)
    {
        _context = context;
        _caseRegisterService = caseRegisterService;
        _logger = logger;
    }

    public async Task<VehicleTagDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tag = await _context.VehicleTags
            .Include(t => t.TagCategory)
            .Include(t => t.CreatedBy)
            .Include(t => t.ClosedBy)
            .FirstOrDefaultAsync(t => t.Id == id && t.DeletedAt == null, ct);

        return tag == null ? null : MapToDto(tag);
    }

    public async Task<PagedResponse<VehicleTagDto>> SearchAsync(SearchVehicleTagsRequest request, CancellationToken ct = default)
    {
        var query = _context.VehicleTags
            .Include(t => t.TagCategory)
            .Include(t => t.CreatedBy)
            .Include(t => t.ClosedBy)
            .Where(t => t.DeletedAt == null)
            .AsQueryable();

        // Vehicle registration search
        if (!string.IsNullOrWhiteSpace(request.RegNo))
        {
            query = query.Where(t => t.RegNo.Contains(request.RegNo));
        }

        // Status filter
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(t => t.Status == request.Status);
        }

        // Tag type filter
        if (!string.IsNullOrWhiteSpace(request.TagType))
        {
            query = query.Where(t => t.TagType == request.TagType);
        }

        // Category filter
        if (request.TagCategoryId.HasValue)
        {
            query = query.Where(t => t.TagCategoryId == request.TagCategoryId.Value);
        }

        // Station filter
        if (!string.IsNullOrWhiteSpace(request.StationCode))
        {
            query = query.Where(t => t.StationCode == request.StationCode);
        }

        // Date range
        if (request.FromDate.HasValue)
        {
            query = query.Where(t => t.OpenedAt >= request.FromDate.Value);
        }
        if (request.ToDate.HasValue)
        {
            query = query.Where(t => t.OpenedAt <= request.ToDate.Value);
        }

        var totalCount = await query.CountAsync(ct);

        // Sorting
        query = request.SortOrder?.ToLower() == "asc"
            ? query.OrderBy(t => EF.Property<object>(t, request.SortBy ?? "OpenedAt"))
            : query.OrderByDescending(t => EF.Property<object>(t, request.SortBy ?? "OpenedAt"));

        // Pagination
        var items = await query
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return PagedResponse<VehicleTagDto>.Create(items.Select(MapToDto).ToList(), totalCount, request.PageNumber, request.PageSize);
    }

    public async Task<List<VehicleTagDto>> CheckVehicleTagsAsync(string regNo, CancellationToken ct = default)
    {
        var tags = await _context.VehicleTags
            .Include(t => t.TagCategory)
            .Where(t => t.RegNo == regNo && t.Status == "open" && t.DeletedAt == null)
            .ToListAsync(ct);

        return tags.Select(MapToDto).ToList();
    }

    public async Task<VehicleTagDto> CreateAsync(CreateVehicleTagRequest request, Guid userId, CancellationToken ct = default)
    {
        // Verify category exists
        var category = await _context.TagCategories
            .FirstOrDefaultAsync(c => c.Id == request.TagCategoryId, ct)
            ?? throw new InvalidOperationException("Tag category not found");

        // Look up vehicle by registration number
        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.RegNo == request.RegNo.ToUpper(), ct);

        var tag = new VehicleTag
        {
            Id = Guid.NewGuid(),
            RegNo = request.RegNo.ToUpper(),
            TagType = request.TagType,
            TagCategoryId = request.TagCategoryId,
            Reason = request.Reason,
            StationCode = request.StationCode,
            Status = "open",
            TagPhotoPath = request.TagPhotoPath,
            EffectiveTimePeriod = request.EffectiveDays.HasValue
                ? TimeSpan.FromDays(request.EffectiveDays.Value)
                : null,
            CreatedById = userId,
            OpenedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.VehicleTags.AddAsync(tag, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created vehicle tag {TagId} for vehicle {RegNo}, type {TagType} by user {UserId}",
            tag.Id, request.RegNo, request.TagType, userId);

        // For MANUAL tags, create a case register entry to track as violation
        // Automatic tags (from system rules) have optional case linking
        if (request.TagType?.ToLower() == "manual" && request.CreateCase)
        {
            try
            {
                // Get TAG violation type
                var tagViolationType = await _context.ViolationTypes
                    .FirstOrDefaultAsync(vt => vt.Code == "TAG" || vt.Code == "TAGGED_VEHICLE", ct);

                if (tagViolationType != null && vehicle != null)
                {
                    var caseRequest = new CreateCaseRegisterRequest
                    {
                        VehicleId = vehicle.Id,
                        ViolationTypeId = tagViolationType.Id,
                        ViolationDetails = $"Vehicle tagged: {category.Name}. Reason: {request.Reason}"
                    };

                    var caseRegister = await _caseRegisterService.CreateCaseAsync(caseRequest, userId);

                    // Link tag to case register
                    tag.CaseRegisterId = caseRegister.Id;
                    await _context.SaveChangesAsync(ct);

                    _logger.LogInformation("Created case {CaseNo} for manual tag {TagId}",
                        caseRegister.CaseNo, tag.Id);
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail tag creation if case creation fails
                _logger.LogWarning(ex, "Failed to create case register for manual tag {TagId}", tag.Id);
            }
        }

        // Reload with includes for DTO mapping
        tag = await _context.VehicleTags
            .Include(t => t.TagCategory)
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == tag.Id, ct);

        return MapToDto(tag!);
    }

    public async Task<VehicleTagDto> CloseAsync(Guid id, CloseVehicleTagRequest request, Guid userId, CancellationToken ct = default)
    {
        var tag = await _context.VehicleTags
            .Include(t => t.TagCategory)
            .FirstOrDefaultAsync(t => t.Id == id && t.DeletedAt == null, ct)
            ?? throw new KeyNotFoundException($"Vehicle tag {id} not found");

        if (tag.Status == "closed")
            throw new InvalidOperationException("This tag has already been closed");

        tag.Status = "closed";
        tag.ClosedById = userId;
        tag.ClosedReason = request.ClosedReason;
        tag.ClosedAt = DateTime.UtcNow;
        tag.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Closed vehicle tag {TagId} by user {UserId}", id, userId);

        // Reload with all includes
        tag = await _context.VehicleTags
            .Include(t => t.TagCategory)
            .Include(t => t.CreatedBy)
            .Include(t => t.ClosedBy)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        return MapToDto(tag!);
    }

    public async Task<List<TagCategoryDto>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var categories = await _context.TagCategories
            .Where(c => c.DeletedAt == null)
            .OrderBy(c => c.Name)
            .Select(c => new TagCategoryDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                Description = c.Description,
                IsActive = c.IsActive
            })
            .ToListAsync(ct);

        return categories;
    }

    public async Task<VehicleTagStatisticsDto> GetStatisticsAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;

        var query = _context.VehicleTags.Where(t => t.DeletedAt == null);

        var stats = new VehicleTagStatisticsDto
        {
            TotalOpen = await query.CountAsync(t => t.Status == "open", ct),
            ClosedToday = await query.CountAsync(t => t.Status == "closed" && t.ClosedAt.HasValue && t.ClosedAt.Value.Date == today, ct),
            CreatedToday = await query.CountAsync(t => t.OpenedAt.Date == today, ct),
            ByCategory = new Dictionary<string, int>()
        };

        // Get counts by category
        var categoryStats = await query
            .Where(t => t.Status == "open")
            .GroupBy(t => t.TagCategory!.Name)
            .Select(g => new { Category = g.Key ?? "Unknown", Count = g.Count() })
            .ToListAsync(ct);

        foreach (var cat in categoryStats)
        {
            stats.ByCategory[cat.Category] = cat.Count;
        }

        return stats;
    }

    private static VehicleTagDto MapToDto(VehicleTag tag)
    {
        return new VehicleTagDto
        {
            Id = tag.Id,
            RegNo = tag.RegNo,
            TagType = tag.TagType,
            TagCategoryId = tag.TagCategoryId,
            TagCategoryCode = tag.TagCategory?.Code ?? "",
            TagCategoryName = tag.TagCategory?.Name ?? "",
            Reason = tag.Reason,
            StationCode = tag.StationCode,
            Status = tag.Status,
            TagPhotoPath = tag.TagPhotoPath,
            EffectiveTimePeriod = tag.EffectiveTimePeriod,
            CreatedById = tag.CreatedById,
            CreatedByName = tag.CreatedBy?.FullName,
            ClosedById = tag.ClosedById,
            ClosedByName = tag.ClosedBy?.FullName,
            ClosedReason = tag.ClosedReason,
            OpenedAt = tag.OpenedAt,
            ClosedAt = tag.ClosedAt,
            Exported = tag.Exported,
            CaseRegisterId = tag.CaseRegisterId,
            CreatedAt = tag.CreatedAt,
            UpdatedAt = tag.UpdatedAt
        };
    }
}
