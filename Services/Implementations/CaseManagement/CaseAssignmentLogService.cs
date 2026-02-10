using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Services.Interfaces.CaseManagement;

namespace TruLoad.Backend.Services.Implementations.CaseManagement;

/// <summary>
/// Service implementation for IO assignment audit trail.
/// Tracks chain of custody - history of which IOs were assigned to cases.
/// </summary>
public class CaseAssignmentLogService : ICaseAssignmentLogService
{
    private readonly TruLoadDbContext _context;

    public CaseAssignmentLogService(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<CaseAssignmentLogDto>> GetByCaseIdAsync(Guid caseRegisterId, CancellationToken ct = default)
    {
        var logs = await _context.CaseAssignmentLogs
            .Include(l => l.CaseRegister)
            .Include(l => l.PreviousOfficer)
            .Include(l => l.NewOfficer)
            .Include(l => l.AssignedBy)
            .Where(l => l.CaseRegisterId == caseRegisterId && l.DeletedAt == null)
            .OrderByDescending(l => l.AssignedAt)
            .ToListAsync(ct);

        return logs.Select(MapToDto);
    }

    public async Task<CaseAssignmentLogDto?> GetCurrentAssignmentAsync(Guid caseRegisterId, CancellationToken ct = default)
    {
        var log = await _context.CaseAssignmentLogs
            .Include(l => l.CaseRegister)
            .Include(l => l.PreviousOfficer)
            .Include(l => l.NewOfficer)
            .Include(l => l.AssignedBy)
            .FirstOrDefaultAsync(l => l.CaseRegisterId == caseRegisterId
                && l.IsCurrent == true
                && l.DeletedAt == null, ct);

        return log == null ? null : MapToDto(log);
    }

    public async Task<CaseAssignmentLogDto> LogAssignmentAsync(Guid caseRegisterId, LogAssignmentRequest request, Guid assignedById, CancellationToken ct = default)
    {
        // Verify case exists
        var caseRegister = await _context.CaseRegisters.FindAsync(new object[] { caseRegisterId }, ct)
            ?? throw new InvalidOperationException($"Case {caseRegisterId} not found");

        Guid? previousOfficerId = null;

        // Find current assignment and mark as no longer current
        var currentAssignment = await _context.CaseAssignmentLogs
            .FirstOrDefaultAsync(l => l.CaseRegisterId == caseRegisterId
                && l.IsCurrent == true
                && l.DeletedAt == null, ct);

        if (currentAssignment != null)
        {
            currentAssignment.IsCurrent = false;
            currentAssignment.UpdatedAt = DateTime.UtcNow;
            previousOfficerId = currentAssignment.NewOfficerId;
        }

        // Create new assignment log
        var newLog = new CaseAssignmentLog
        {
            Id = Guid.NewGuid(),
            CaseRegisterId = caseRegisterId,
            PreviousOfficerId = previousOfficerId,
            NewOfficerId = request.NewOfficerId,
            AssignedById = assignedById,
            AssignmentType = request.AssignmentType,
            Reason = request.Reason,
            AssignedAt = DateTime.UtcNow,
            IsCurrent = true,
            OfficerRank = request.OfficerRank,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.CaseAssignmentLogs.Add(newLog);

        // Update the case register's investigating officer
        caseRegister.InvestigatingOfficerId = request.NewOfficerId;
        caseRegister.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        // Reload with navigation properties
        var saved = await _context.CaseAssignmentLogs
            .Include(l => l.CaseRegister)
            .Include(l => l.PreviousOfficer)
            .Include(l => l.NewOfficer)
            .Include(l => l.AssignedBy)
            .FirstOrDefaultAsync(l => l.Id == newLog.Id, ct);

        return MapToDto(saved!);
    }

    private CaseAssignmentLogDto MapToDto(CaseAssignmentLog log)
    {
        return new CaseAssignmentLogDto
        {
            Id = log.Id,
            CaseRegisterId = log.CaseRegisterId,
            CaseNo = log.CaseRegister?.CaseNo,
            PreviousOfficerId = log.PreviousOfficerId,
            PreviousOfficerName = log.PreviousOfficer?.FullName,
            NewOfficerId = log.NewOfficerId,
            NewOfficerName = log.NewOfficer?.FullName,
            AssignedById = log.AssignedById,
            AssignedByName = log.AssignedBy?.FullName,
            AssignmentType = log.AssignmentType,
            Reason = log.Reason,
            AssignedAt = log.AssignedAt,
            IsCurrent = log.IsCurrent,
            OfficerRank = log.OfficerRank
        };
    }
}
