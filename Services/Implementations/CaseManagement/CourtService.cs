using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Services.Interfaces.CaseManagement;

namespace TruLoad.Backend.Services.Implementations.CaseManagement;

/// <summary>
/// Service implementation for court registry management.
/// Handles CRUD operations for court master data.
/// </summary>
public class CourtService : ICourtService
{
    private readonly TruLoadDbContext _context;

    public CourtService(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<CourtDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var court = await _context.Courts
            .FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, ct);

        return court == null ? null : MapToDto(court);
    }

    public async Task<CourtDto?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var court = await _context.Courts
            .FirstOrDefaultAsync(c => c.Code == code && c.DeletedAt == null, ct);

        return court == null ? null : MapToDto(court);
    }

    public async Task<IEnumerable<CourtDto>> SearchAsync(CourtSearchCriteria criteria, CancellationToken ct = default)
    {
        var query = _context.Courts
            .Where(c => c.DeletedAt == null)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(criteria.Code))
            query = query.Where(c => c.Code == criteria.Code);

        if (!string.IsNullOrWhiteSpace(criteria.Name))
            query = query.Where(c => c.Name.Contains(criteria.Name));

        if (!string.IsNullOrWhiteSpace(criteria.CourtType))
            query = query.Where(c => c.CourtType == criteria.CourtType);

        if (criteria.IsActive.HasValue)
            query = query.Where(c => c.IsActive == criteria.IsActive.Value);

        var courts = await query
            .OrderBy(c => c.Name)
            .Skip(criteria.Skip)
            .Take(criteria.PageSize)
            .ToListAsync(ct);

        return courts.Select(MapToDto);
    }

    public async Task<CourtDto> CreateAsync(CreateCourtRequest request, CancellationToken ct = default)
    {
        var court = new Court
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            Name = request.Name,
            Location = request.Location,
            CourtType = request.CourtType,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Courts.Add(court);
        await _context.SaveChangesAsync(ct);

        return MapToDto(court);
    }

    public async Task<CourtDto> UpdateAsync(Guid id, UpdateCourtRequest request, CancellationToken ct = default)
    {
        var court = await _context.Courts.FindAsync(new object[] { id }, ct)
            ?? throw new InvalidOperationException($"Court {id} not found");

        if (court.DeletedAt != null)
            throw new InvalidOperationException("Cannot update a deleted court");

        if (!string.IsNullOrWhiteSpace(request.Name))
            court.Name = request.Name;

        if (!string.IsNullOrWhiteSpace(request.Location))
            court.Location = request.Location;

        if (!string.IsNullOrWhiteSpace(request.CourtType))
            court.CourtType = request.CourtType;

        if (request.IsActive.HasValue)
            court.IsActive = request.IsActive.Value;

        court.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return MapToDto(court);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var court = await _context.Courts.FindAsync(new object[] { id }, ct);
        if (court == null)
            return false;

        court.DeletedAt = DateTime.UtcNow;
        court.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return true;
    }

    private CourtDto MapToDto(Court court)
    {
        return new CourtDto
        {
            Id = court.Id,
            Code = court.Code,
            Name = court.Name,
            Location = court.Location,
            CourtType = court.CourtType,
            IsActive = court.IsActive,
            CreatedAt = court.CreatedAt,
            UpdatedAt = court.UpdatedAt
        };
    }
}
