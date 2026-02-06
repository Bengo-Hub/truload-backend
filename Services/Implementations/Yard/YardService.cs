using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.DTOs.Yard;
using TruLoad.Backend.Models.Yard;
using TruLoad.Backend.Services.Interfaces.Yard;

namespace TruLoad.Backend.Services.Implementations.Yard;

/// <summary>
/// Service implementation for yard entry management.
/// </summary>
public class YardService : IYardService
{
    private readonly TruLoadDbContext _context;
    private readonly ILogger<YardService> _logger;

    private static readonly string[] ValidStatuses = { "pending", "processing", "released", "escalated" };

    public YardService(TruLoadDbContext context, ILogger<YardService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<YardEntryDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _context.YardEntries
            .Include(y => y.Station)
            .Include(y => y.Weighing)
            .FirstOrDefaultAsync(y => y.Id == id && y.DeletedAt == null, ct);

        return entry == null ? null : MapToDto(entry);
    }

    public async Task<YardEntryDto?> GetByWeighingIdAsync(Guid weighingId, CancellationToken ct = default)
    {
        var entry = await _context.YardEntries
            .Include(y => y.Station)
            .Include(y => y.Weighing)
            .FirstOrDefaultAsync(y => y.WeighingId == weighingId && y.DeletedAt == null, ct);

        return entry == null ? null : MapToDto(entry);
    }

    public async Task<PagedResponse<YardEntryDto>> SearchAsync(SearchYardEntriesRequest request, Guid? tenantStationId, CancellationToken ct = default)
    {
        var query = _context.YardEntries
            .Include(y => y.Station)
            .Include(y => y.Weighing)
            .Where(y => y.DeletedAt == null)
            .AsQueryable();

        // Apply station filter (from tenant context or request)
        var stationId = request.StationId ?? tenantStationId;
        if (stationId.HasValue)
        {
            query = query.Where(y => y.StationId == stationId.Value);
        }

        // Status filter
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(y => y.Status == request.Status);
        }

        // Reason filter
        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            query = query.Where(y => y.Reason == request.Reason);
        }

        // Vehicle registration search
        if (!string.IsNullOrWhiteSpace(request.VehicleRegNo))
        {
            query = query.Where(y => y.Weighing != null &&
                y.Weighing.VehicleRegNumber.Contains(request.VehicleRegNo));
        }

        // Date range
        if (request.FromDate.HasValue)
        {
            query = query.Where(y => y.EnteredAt >= request.FromDate.Value);
        }
        if (request.ToDate.HasValue)
        {
            query = query.Where(y => y.EnteredAt <= request.ToDate.Value);
        }

        var totalCount = await query.CountAsync(ct);

        // Sorting
        query = request.SortOrder?.ToLower() == "asc"
            ? query.OrderBy(y => EF.Property<object>(y, request.SortBy ?? "EnteredAt"))
            : query.OrderByDescending(y => EF.Property<object>(y, request.SortBy ?? "EnteredAt"));

        // Pagination
        var items = await query
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return PagedResponse<YardEntryDto>.Create(items.Select(MapToDto).ToList(), totalCount, request.PageNumber, request.PageSize);
    }

    public async Task<YardEntryDto> CreateAsync(CreateYardEntryRequest request, Guid userId, CancellationToken ct = default)
    {
        // Verify weighing transaction exists
        var weighing = await _context.WeighingTransactions
            .FirstOrDefaultAsync(w => w.Id == request.WeighingId, ct)
            ?? throw new InvalidOperationException("Weighing transaction not found");

        // Check if entry already exists for this weighing
        var existingEntry = await _context.YardEntries
            .FirstOrDefaultAsync(y => y.WeighingId == request.WeighingId && y.DeletedAt == null, ct);

        if (existingEntry != null)
            throw new InvalidOperationException("A yard entry already exists for this weighing transaction");

        var entry = new YardEntry
        {
            Id = Guid.NewGuid(),
            WeighingId = request.WeighingId,
            StationId = request.StationId,
            Reason = request.Reason,
            Status = "pending",
            EnteredAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Update weighing transaction to mark as sent to yard
        weighing.IsSentToYard = true;
        weighing.UpdatedAt = DateTime.UtcNow;

        await _context.YardEntries.AddAsync(entry, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created yard entry {EntryId} for weighing {WeighingId}",
            entry.Id, request.WeighingId);

        return MapToDto(entry);
    }

    public async Task<YardEntryDto> ReleaseAsync(Guid id, ReleaseYardEntryRequest request, Guid releasedById, CancellationToken ct = default)
    {
        var entry = await _context.YardEntries
            .Include(y => y.Station)
            .Include(y => y.Weighing)
            .FirstOrDefaultAsync(y => y.Id == id && y.DeletedAt == null, ct)
            ?? throw new KeyNotFoundException($"Yard entry {id} not found");

        if (entry.Status == "released")
            throw new InvalidOperationException("This yard entry has already been released");

        entry.Status = "released";
        entry.ReleasedAt = DateTime.UtcNow;
        entry.UpdatedAt = DateTime.UtcNow;

        // Update weighing transaction
        if (entry.Weighing != null)
        {
            entry.Weighing.IsSentToYard = false;
            entry.Weighing.ControlStatus = "Released";
            entry.Weighing.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Released yard entry {EntryId} by user {UserId} with notes: {Notes}",
            id, releasedById, request.Notes);

        return MapToDto(entry);
    }

    public async Task<YardEntryDto> UpdateStatusAsync(Guid id, string status, Guid updatedById, CancellationToken ct = default)
    {
        if (!ValidStatuses.Contains(status.ToLower()))
            throw new ArgumentException($"Invalid status. Must be one of: {string.Join(", ", ValidStatuses)}");

        var entry = await _context.YardEntries
            .Include(y => y.Station)
            .FirstOrDefaultAsync(y => y.Id == id && y.DeletedAt == null, ct)
            ?? throw new KeyNotFoundException($"Yard entry {id} not found");

        entry.Status = status.ToLower();
        entry.UpdatedAt = DateTime.UtcNow;

        if (status.ToLower() == "released")
        {
            entry.ReleasedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Updated yard entry {EntryId} status to {Status} by user {UserId}",
            id, status, updatedById);

        return MapToDto(entry);
    }

    public async Task<YardStatisticsDto> GetStatisticsAsync(Guid? stationId, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;

        var query = _context.YardEntries.Where(y => y.DeletedAt == null);

        if (stationId.HasValue)
        {
            query = query.Where(y => y.StationId == stationId.Value);
        }

        var stats = await query
            .GroupBy(_ => 1)
            .Select(g => new YardStatisticsDto
            {
                TotalPending = g.Count(y => y.Status == "pending"),
                ReleasedToday = g.Count(y => y.Status == "released" && y.ReleasedAt.HasValue && y.ReleasedAt.Value.Date == today),
                TotalEntriesToday = g.Count(y => y.EnteredAt.Date == today),
                Escalated = g.Count(y => y.Status == "escalated")
            })
            .FirstOrDefaultAsync(ct);

        return stats ?? new YardStatisticsDto();
    }

    private static YardEntryDto MapToDto(YardEntry entry)
    {
        return new YardEntryDto
        {
            Id = entry.Id,
            WeighingId = entry.WeighingId,
            StationId = entry.StationId,
            StationName = entry.Station?.Name ?? "",
            Reason = entry.Reason,
            Status = entry.Status,
            EnteredAt = entry.EnteredAt,
            ReleasedAt = entry.ReleasedAt,
            TicketNumber = entry.Weighing?.TicketNumber,
            VehicleRegNumber = entry.Weighing?.VehicleRegNumber,
            GvwMeasuredKg = entry.Weighing?.GvwMeasuredKg,
            GvwPermissibleKg = entry.Weighing?.GvwPermissibleKg,
            OverloadKg = entry.Weighing?.OverloadKg,
            TotalFeeUsd = entry.Weighing?.TotalFeeUsd,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt
        };
    }
}
