using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Services.Interfaces.CaseManagement;

namespace TruLoad.Backend.Services.Implementations.CaseManagement;

/// <summary>
/// Service implementation for court hearing management.
/// Handles scheduling, adjournment, and completion of court hearings.
/// </summary>
public class CourtHearingService : ICourtHearingService
{
    private readonly TruLoadDbContext _context;

    public CourtHearingService(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<CourtHearingDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var hearing = await _context.CourtHearings
            .Include(h => h.CaseRegister)
            .Include(h => h.HearingType)
            .Include(h => h.HearingStatus)
            .Include(h => h.HearingOutcome)
            .FirstOrDefaultAsync(h => h.Id == id && h.DeletedAt == null, ct);

        return hearing == null ? null : MapToDto(hearing);
    }

    public async Task<IEnumerable<CourtHearingDto>> GetByCaseIdAsync(Guid caseRegisterId, CancellationToken ct = default)
    {
        var hearings = await _context.CourtHearings
            .Include(h => h.CaseRegister)
            .Include(h => h.HearingType)
            .Include(h => h.HearingStatus)
            .Include(h => h.HearingOutcome)
            .Where(h => h.CaseRegisterId == caseRegisterId && h.DeletedAt == null)
            .OrderByDescending(h => h.HearingDate)
            .ToListAsync(ct);

        return hearings.Select(MapToDto);
    }

    public async Task<CourtHearingDto?> GetNextScheduledAsync(Guid caseRegisterId, CancellationToken ct = default)
    {
        // Get "Scheduled" status
        var scheduledStatus = await _context.HearingStatuses
            .FirstOrDefaultAsync(s => s.Code == "SCHEDULED", ct);

        if (scheduledStatus == null)
            return null;

        var hearing = await _context.CourtHearings
            .Include(h => h.CaseRegister)
            .Include(h => h.HearingType)
            .Include(h => h.HearingStatus)
            .Include(h => h.HearingOutcome)
            .Where(h => h.CaseRegisterId == caseRegisterId
                && h.HearingStatusId == scheduledStatus.Id
                && h.HearingDate >= DateTime.UtcNow.Date
                && h.DeletedAt == null)
            .OrderBy(h => h.HearingDate)
            .FirstOrDefaultAsync(ct);

        return hearing == null ? null : MapToDto(hearing);
    }

    public async Task<IEnumerable<CourtHearingDto>> GetByCourtAsync(Guid courtId, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var hearings = await _context.CourtHearings
            .Include(h => h.CaseRegister)
            .Include(h => h.HearingType)
            .Include(h => h.HearingStatus)
            .Include(h => h.HearingOutcome)
            .Where(h => h.CourtId == courtId
                && h.HearingDate >= fromDate
                && h.HearingDate <= toDate
                && h.DeletedAt == null)
            .OrderBy(h => h.HearingDate)
            .ToListAsync(ct);

        return hearings.Select(MapToDto);
    }

    public async Task<IEnumerable<CourtHearingDto>> SearchAsync(CourtHearingSearchCriteria criteria, CancellationToken ct = default)
    {
        var query = _context.CourtHearings
            .Include(h => h.CaseRegister)
            .Include(h => h.HearingType)
            .Include(h => h.HearingStatus)
            .Include(h => h.HearingOutcome)
            .Where(h => h.DeletedAt == null)
            .AsQueryable();

        if (criteria.CaseRegisterId.HasValue)
            query = query.Where(h => h.CaseRegisterId == criteria.CaseRegisterId.Value);

        if (criteria.CourtId.HasValue)
            query = query.Where(h => h.CourtId == criteria.CourtId.Value);

        if (criteria.HearingTypeId.HasValue)
            query = query.Where(h => h.HearingTypeId == criteria.HearingTypeId.Value);

        if (criteria.HearingStatusId.HasValue)
            query = query.Where(h => h.HearingStatusId == criteria.HearingStatusId.Value);

        if (criteria.HearingDateFrom.HasValue)
            query = query.Where(h => h.HearingDate >= criteria.HearingDateFrom.Value);

        if (criteria.HearingDateTo.HasValue)
            query = query.Where(h => h.HearingDate <= criteria.HearingDateTo.Value);

        var hearings = await query
            .OrderByDescending(h => h.HearingDate)
            .Skip(criteria.Skip)
            .Take(criteria.PageSize)
            .ToListAsync(ct);

        return hearings.Select(MapToDto);
    }

    public async Task<CourtHearingDto> ScheduleHearingAsync(Guid caseRegisterId, CreateCourtHearingRequest request, Guid userId, CancellationToken ct = default)
    {
        // Verify case exists
        var caseRegister = await _context.CaseRegisters.FindAsync(new object[] { caseRegisterId }, ct)
            ?? throw new InvalidOperationException($"Case {caseRegisterId} not found");

        // Get "Scheduled" status
        var scheduledStatus = await _context.HearingStatuses
            .FirstOrDefaultAsync(s => s.Code == "SCHEDULED", ct)
            ?? throw new InvalidOperationException("SCHEDULED hearing status not found");

        var hearing = new CourtHearing
        {
            Id = Guid.NewGuid(),
            CaseRegisterId = caseRegisterId,
            CourtId = request.CourtId,
            HearingDate = request.HearingDate,
            HearingTime = request.HearingTime,
            HearingTypeId = request.HearingTypeId,
            HearingStatusId = scheduledStatus.Id,
            PresidingOfficer = request.PresidingOfficer,
            MinuteNotes = request.MinuteNotes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.CourtHearings.Add(hearing);
        await _context.SaveChangesAsync(ct);

        // Reload with navigation properties
        return (await GetByIdAsync(hearing.Id, ct))!;
    }

    public async Task<CourtHearingDto> UpdateHearingAsync(Guid id, UpdateCourtHearingRequest request, Guid userId, CancellationToken ct = default)
    {
        var hearing = await _context.CourtHearings.FindAsync(new object[] { id }, ct)
            ?? throw new InvalidOperationException($"Hearing {id} not found");

        if (hearing.DeletedAt != null)
            throw new InvalidOperationException("Cannot update a deleted hearing");

        if (request.CourtId.HasValue)
            hearing.CourtId = request.CourtId;

        if (request.HearingDate.HasValue)
            hearing.HearingDate = request.HearingDate.Value;

        if (request.HearingTime.HasValue)
            hearing.HearingTime = request.HearingTime;

        if (request.HearingTypeId.HasValue)
            hearing.HearingTypeId = request.HearingTypeId;

        if (!string.IsNullOrWhiteSpace(request.PresidingOfficer))
            hearing.PresidingOfficer = request.PresidingOfficer;

        if (!string.IsNullOrWhiteSpace(request.MinuteNotes))
            hearing.MinuteNotes = request.MinuteNotes;

        hearing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return (await GetByIdAsync(id, ct))!;
    }

    public async Task<CourtHearingDto> AdjournHearingAsync(Guid id, AdjournHearingRequest request, Guid userId, CancellationToken ct = default)
    {
        var hearing = await _context.CourtHearings.FindAsync(new object[] { id }, ct)
            ?? throw new InvalidOperationException($"Hearing {id} not found");

        if (hearing.DeletedAt != null)
            throw new InvalidOperationException("Cannot adjourn a deleted hearing");

        // Get "Adjourned" status
        var adjournedStatus = await _context.HearingStatuses
            .FirstOrDefaultAsync(s => s.Code == "ADJOURNED", ct)
            ?? throw new InvalidOperationException("ADJOURNED hearing status not found");

        // Get "Adjourned" outcome
        var adjournedOutcome = await _context.HearingOutcomes
            .FirstOrDefaultAsync(o => o.Code == "ADJOURNED", ct);

        hearing.HearingStatusId = adjournedStatus.Id;
        hearing.HearingOutcomeId = adjournedOutcome?.Id;
        hearing.AdjournmentReason = request.AdjournmentReason;
        hearing.NextHearingDate = request.NextHearingDate;
        hearing.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.MinuteNotes))
        {
            hearing.MinuteNotes = string.IsNullOrWhiteSpace(hearing.MinuteNotes)
                ? request.MinuteNotes
                : $"{hearing.MinuteNotes}\n\n--- Adjournment ---\n{request.MinuteNotes}";
        }

        await _context.SaveChangesAsync(ct);

        // Auto-create the next hearing if next date specified
        if (request.NextHearingDate > DateTime.MinValue)
        {
            var nextHearingRequest = new CreateCourtHearingRequest
            {
                CourtId = hearing.CourtId,
                HearingDate = request.NextHearingDate,
                HearingTypeId = hearing.HearingTypeId,
                PresidingOfficer = hearing.PresidingOfficer,
                MinuteNotes = $"Continued from hearing on {hearing.HearingDate:d}. Adjournment reason: {request.AdjournmentReason}"
            };

            await ScheduleHearingAsync(hearing.CaseRegisterId, nextHearingRequest, userId, ct);
        }

        return (await GetByIdAsync(id, ct))!;
    }

    public async Task<CourtHearingDto> CompleteHearingAsync(Guid id, CompleteHearingRequest request, Guid userId, CancellationToken ct = default)
    {
        var hearing = await _context.CourtHearings.FindAsync(new object[] { id }, ct)
            ?? throw new InvalidOperationException($"Hearing {id} not found");

        if (hearing.DeletedAt != null)
            throw new InvalidOperationException("Cannot complete a deleted hearing");

        // Get "Completed" status
        var completedStatus = await _context.HearingStatuses
            .FirstOrDefaultAsync(s => s.Code == "COMPLETED", ct)
            ?? throw new InvalidOperationException("COMPLETED hearing status not found");

        hearing.HearingStatusId = completedStatus.Id;
        hearing.HearingOutcomeId = request.HearingOutcomeId;
        hearing.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.MinuteNotes))
        {
            hearing.MinuteNotes = string.IsNullOrWhiteSpace(hearing.MinuteNotes)
                ? request.MinuteNotes
                : $"{hearing.MinuteNotes}\n\n--- Outcome ---\n{request.MinuteNotes}";
        }

        // Add sentence details if convicted
        if (!string.IsNullOrWhiteSpace(request.SentenceDetails))
        {
            hearing.MinuteNotes = string.IsNullOrWhiteSpace(hearing.MinuteNotes)
                ? $"Sentence: {request.SentenceDetails}"
                : $"{hearing.MinuteNotes}\nSentence: {request.SentenceDetails}";

            if (request.FineAmount.HasValue)
            {
                hearing.MinuteNotes += $"\nFine Amount: {request.FineAmount.Value:C}";
            }
        }

        if (request.NextHearingDate.HasValue)
        {
            hearing.NextHearingDate = request.NextHearingDate.Value;
        }

        await _context.SaveChangesAsync(ct);

        return (await GetByIdAsync(id, ct))!;
    }

    public async Task<bool> DeleteHearingAsync(Guid id, CancellationToken ct = default)
    {
        var hearing = await _context.CourtHearings.FindAsync(new object[] { id }, ct);
        if (hearing == null)
            return false;

        hearing.DeletedAt = DateTime.UtcNow;
        hearing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Dictionary<string, int>> GetHearingStatisticsAsync(DateTime? dateFrom = null, DateTime? dateTo = null, Guid? stationId = null, CancellationToken ct = default)
    {
        var stats = new Dictionary<string, int>();

        var query = _context.CourtHearings
            .AsNoTracking()
            .Where(h => h.DeletedAt == null);

        if (stationId.HasValue)
            query = query.Where(h => h.CaseRegister != null && h.CaseRegister.Weighing != null && h.CaseRegister.Weighing.StationId == stationId.Value);
        if (dateFrom.HasValue)
        {
            var from = DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc);
            query = query.Where(h => h.HearingDate >= from);
        }
        if (dateTo.HasValue)
        {
            var to = DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc);
            query = query.Where(h => h.HearingDate <= to);
        }

        // Single grouped query to avoid N+1
        var statusCounts = await query
            .GroupBy(h => h.HearingStatus!.Name)
            .Select(g => new { StatusName = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var total = statusCounts.Sum(sc => sc.Count);
        stats["Total"] = total;
        foreach (var sc in statusCounts)
        {
            if (!string.IsNullOrEmpty(sc.StatusName))
                stats[sc.StatusName] = sc.Count;
        }

        // Upcoming hearings (next 7 days)
        var upcomingQuery = _context.CourtHearings
            .Where(h => h.DeletedAt == null
                && h.HearingDate >= DateTime.UtcNow.Date
                && h.HearingDate <= DateTime.UtcNow.Date.AddDays(7));
        if (stationId.HasValue)
            upcomingQuery = upcomingQuery.Where(h => h.CaseRegister != null && h.CaseRegister.Weighing != null && h.CaseRegister.Weighing.StationId == stationId.Value);
        if (dateFrom.HasValue)
            upcomingQuery = upcomingQuery.Where(h => h.HearingDate >= DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc));
        if (dateTo.HasValue)
            upcomingQuery = upcomingQuery.Where(h => h.HearingDate <= DateTime.SpecifyKind(dateTo.Value, DateTimeKind.Utc));
        var upcomingCount = await upcomingQuery.CountAsync(ct);
        stats["Upcoming7Days"] = upcomingCount;

        return stats;
    }

    private CourtHearingDto MapToDto(CourtHearing hearing)
    {
        // Get court info if available
        var court = hearing.CourtId.HasValue
            ? _context.Courts.Find(hearing.CourtId.Value)
            : null;

        return new CourtHearingDto
        {
            Id = hearing.Id,
            CaseRegisterId = hearing.CaseRegisterId,
            CaseNo = hearing.CaseRegister?.CaseNo ?? string.Empty,
            CourtId = hearing.CourtId,
            CourtName = court?.Name,
            CourtLocation = court?.Location,
            HearingDate = hearing.HearingDate,
            HearingTime = hearing.HearingTime,
            HearingTypeId = hearing.HearingTypeId,
            HearingTypeName = hearing.HearingType?.Name,
            HearingStatusId = hearing.HearingStatusId,
            HearingStatusName = hearing.HearingStatus?.Name,
            HearingOutcomeId = hearing.HearingOutcomeId,
            HearingOutcomeName = hearing.HearingOutcome?.Name,
            MinuteNotes = hearing.MinuteNotes,
            NextHearingDate = hearing.NextHearingDate,
            AdjournmentReason = hearing.AdjournmentReason,
            PresidingOfficer = hearing.PresidingOfficer,
            CreatedAt = hearing.CreatedAt,
            UpdatedAt = hearing.UpdatedAt
        };
    }
}
