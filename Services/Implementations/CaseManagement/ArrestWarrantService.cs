using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Services.Interfaces.CaseManagement;

namespace TruLoad.Backend.Services.Implementations.CaseManagement;

/// <summary>
/// Service implementation for arrest warrant management.
/// Handles issuance, execution, and dropping of warrants.
/// </summary>
public class ArrestWarrantService : IArrestWarrantService
{
    private readonly TruLoadDbContext _context;

    public ArrestWarrantService(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<ArrestWarrantDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var warrant = await _context.ArrestWarrants
            .Include(w => w.CaseRegister)
            .Include(w => w.WarrantStatus)
            .FirstOrDefaultAsync(w => w.Id == id && w.DeletedAt == null, ct);

        return warrant == null ? null : MapToDto(warrant);
    }

    public async Task<IEnumerable<ArrestWarrantDto>> GetByCaseIdAsync(Guid caseRegisterId, CancellationToken ct = default)
    {
        var warrants = await _context.ArrestWarrants
            .Include(w => w.CaseRegister)
            .Include(w => w.WarrantStatus)
            .Where(w => w.CaseRegisterId == caseRegisterId && w.DeletedAt == null)
            .OrderByDescending(w => w.IssuedAt)
            .ToListAsync(ct);

        return warrants.Select(MapToDto);
    }

    public async Task<IEnumerable<ArrestWarrantDto>> SearchAsync(ArrestWarrantSearchCriteria criteria, CancellationToken ct = default)
    {
        var query = _context.ArrestWarrants
            .Include(w => w.CaseRegister)
            .Include(w => w.WarrantStatus)
            .Where(w => w.DeletedAt == null)
            .AsQueryable();

        if (criteria.CaseRegisterId.HasValue)
            query = query.Where(w => w.CaseRegisterId == criteria.CaseRegisterId.Value);

        if (criteria.WarrantStatusId.HasValue)
            query = query.Where(w => w.WarrantStatusId == criteria.WarrantStatusId.Value);

        if (!string.IsNullOrWhiteSpace(criteria.AccusedName))
            query = query.Where(w => w.AccusedName.Contains(criteria.AccusedName));

        if (criteria.IssuedFrom.HasValue)
            query = query.Where(w => w.IssuedAt >= criteria.IssuedFrom.Value);

        if (criteria.IssuedTo.HasValue)
            query = query.Where(w => w.IssuedAt <= criteria.IssuedTo.Value);

        var warrants = await query
            .OrderByDescending(w => w.IssuedAt)
            .Skip(criteria.Skip)
            .Take(criteria.PageSize)
            .ToListAsync(ct);

        return warrants.Select(MapToDto);
    }

    public async Task<ArrestWarrantDto> CreateAsync(CreateArrestWarrantRequest request, Guid userId, CancellationToken ct = default)
    {
        // Verify case exists
        var caseRegister = await _context.CaseRegisters.FindAsync(new object[] { request.CaseRegisterId }, ct)
            ?? throw new InvalidOperationException($"Case {request.CaseRegisterId} not found");

        // Look up ISSUED warrant status
        var issuedStatus = await _context.WarrantStatuses
            .FirstOrDefaultAsync(s => s.Code == "ISSUED", ct)
            ?? throw new InvalidOperationException("ISSUED warrant status not found");

        // Auto-generate warrant number: WAR-{year}-{sequence}
        var year = DateTime.UtcNow.Year;
        var startOfYear = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endOfYear = new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        var warrantCountThisYear = await _context.ArrestWarrants
            .CountAsync(w => w.IssuedAt >= startOfYear && w.IssuedAt <= endOfYear, ct);

        var sequence = (warrantCountThisYear + 1).ToString().PadLeft(5, '0');
        var warrantNo = $"WAR-{year}-{sequence}";

        var warrant = new ArrestWarrant
        {
            Id = Guid.NewGuid(),
            CaseRegisterId = request.CaseRegisterId,
            WarrantNo = warrantNo,
            IssuedBy = request.IssuedBy,
            AccusedName = request.AccusedName,
            AccusedIdNo = request.AccusedIdNo,
            OffenceDescription = request.OffenceDescription,
            WarrantStatusId = issuedStatus.Id,
            IssuedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ArrestWarrants.Add(warrant);
        await _context.SaveChangesAsync(ct);

        // Reload with navigation properties
        return (await GetByIdAsync(warrant.Id, ct))!;
    }

    public async Task<ArrestWarrantDto> ExecuteAsync(Guid id, ExecuteWarrantRequest request, CancellationToken ct = default)
    {
        var warrant = await _context.ArrestWarrants.FindAsync(new object[] { id }, ct)
            ?? throw new InvalidOperationException($"Warrant {id} not found");

        if (warrant.DeletedAt != null)
            throw new InvalidOperationException("Cannot execute a deleted warrant");

        // Look up EXECUTED warrant status
        var executedStatus = await _context.WarrantStatuses
            .FirstOrDefaultAsync(s => s.Code == "EXECUTED", ct)
            ?? throw new InvalidOperationException("EXECUTED warrant status not found");

        warrant.WarrantStatusId = executedStatus.Id;
        warrant.ExecutedAt = DateTime.UtcNow;
        warrant.ExecutionDetails = request.ExecutionDetails;
        warrant.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return (await GetByIdAsync(id, ct))!;
    }

    public async Task<ArrestWarrantDto> DropAsync(Guid id, DropWarrantRequest request, CancellationToken ct = default)
    {
        var warrant = await _context.ArrestWarrants.FindAsync(new object[] { id }, ct)
            ?? throw new InvalidOperationException($"Warrant {id} not found");

        if (warrant.DeletedAt != null)
            throw new InvalidOperationException("Cannot drop a deleted warrant");

        // Look up DROPPED warrant status
        var droppedStatus = await _context.WarrantStatuses
            .FirstOrDefaultAsync(s => s.Code == "DROPPED", ct)
            ?? throw new InvalidOperationException("DROPPED warrant status not found");

        warrant.WarrantStatusId = droppedStatus.Id;
        warrant.DroppedAt = DateTime.UtcNow;
        warrant.DroppedReason = request.DroppedReason;
        warrant.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return (await GetByIdAsync(id, ct))!;
    }

    private ArrestWarrantDto MapToDto(ArrestWarrant warrant)
    {
        return new ArrestWarrantDto
        {
            Id = warrant.Id,
            CaseRegisterId = warrant.CaseRegisterId,
            CaseNo = warrant.CaseRegister?.CaseNo,
            WarrantNo = warrant.WarrantNo,
            IssuedBy = warrant.IssuedBy,
            AccusedName = warrant.AccusedName,
            AccusedIdNo = warrant.AccusedIdNo,
            OffenceDescription = warrant.OffenceDescription,
            WarrantStatusId = warrant.WarrantStatusId,
            WarrantStatusName = warrant.WarrantStatus?.Name,
            IssuedAt = warrant.IssuedAt,
            ExecutedAt = warrant.ExecutedAt,
            DroppedAt = warrant.DroppedAt,
            ExecutionDetails = warrant.ExecutionDetails,
            DroppedReason = warrant.DroppedReason,
            CreatedAt = warrant.CreatedAt
        };
    }
}
