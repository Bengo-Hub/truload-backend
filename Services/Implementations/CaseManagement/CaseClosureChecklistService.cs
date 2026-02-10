using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Services.Interfaces.CaseManagement;

namespace TruLoad.Backend.Services.Implementations.CaseManagement;

/// <summary>
/// Service implementation for case closure checklist management.
/// Handles checklist updates, review requests, and approval/rejection workflow.
/// </summary>
public class CaseClosureChecklistService : ICaseClosureChecklistService
{
    private readonly TruLoadDbContext _context;

    public CaseClosureChecklistService(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<CaseClosureChecklistDto?> GetByCaseIdAsync(Guid caseRegisterId, CancellationToken ct = default)
    {
        var checklist = await _context.CaseClosureChecklists
            .Include(c => c.CaseRegister)
            .Include(c => c.ClosureType)
            .Include(c => c.LegalSection)
            .Include(c => c.ReviewStatus)
            .FirstOrDefaultAsync(c => c.CaseRegisterId == caseRegisterId && c.DeletedAt == null, ct);

        return checklist == null ? null : await MapToDtoAsync(checklist, ct);
    }

    public async Task<CaseClosureChecklistDto> CreateOrUpdateAsync(Guid caseRegisterId, UpdateChecklistRequest request, Guid userId, CancellationToken ct = default)
    {
        var checklist = await _context.CaseClosureChecklists
            .FirstOrDefaultAsync(c => c.CaseRegisterId == caseRegisterId && c.DeletedAt == null, ct);

        if (checklist == null)
        {
            // Verify case exists
            _ = await _context.CaseRegisters.FindAsync(new object[] { caseRegisterId }, ct)
                ?? throw new InvalidOperationException($"Case {caseRegisterId} not found");

            checklist = new CaseClosureChecklist
            {
                Id = Guid.NewGuid(),
                CaseRegisterId = caseRegisterId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.CaseClosureChecklists.Add(checklist);
        }

        // Update only non-null fields
        if (request.ClosureTypeId.HasValue)
            checklist.ClosureTypeId = request.ClosureTypeId;

        if (request.LegalSectionId.HasValue)
            checklist.LegalSectionId = request.LegalSectionId;

        if (request.SubfileAComplete.HasValue)
            checklist.SubfileAComplete = request.SubfileAComplete.Value;

        if (request.SubfileBComplete.HasValue)
            checklist.SubfileBComplete = request.SubfileBComplete.Value;

        if (request.SubfileCComplete.HasValue)
            checklist.SubfileCComplete = request.SubfileCComplete.Value;

        if (request.SubfileDComplete.HasValue)
            checklist.SubfileDComplete = request.SubfileDComplete.Value;

        if (request.SubfileEComplete.HasValue)
            checklist.SubfileEComplete = request.SubfileEComplete.Value;

        if (request.SubfileFComplete.HasValue)
            checklist.SubfileFComplete = request.SubfileFComplete.Value;

        if (request.SubfileGComplete.HasValue)
            checklist.SubfileGComplete = request.SubfileGComplete.Value;

        if (request.SubfileHComplete.HasValue)
            checklist.SubfileHComplete = request.SubfileHComplete.Value;

        if (request.SubfileIComplete.HasValue)
            checklist.SubfileIComplete = request.SubfileIComplete.Value;

        if (request.SubfileJComplete.HasValue)
            checklist.SubfileJComplete = request.SubfileJComplete.Value;

        // Recalculate AllSubfilesVerified
        checklist.AllSubfilesVerified =
            checklist.SubfileAComplete &&
            checklist.SubfileBComplete &&
            checklist.SubfileCComplete &&
            checklist.SubfileDComplete &&
            checklist.SubfileEComplete &&
            checklist.SubfileFComplete &&
            checklist.SubfileGComplete &&
            checklist.SubfileHComplete &&
            checklist.SubfileIComplete &&
            checklist.SubfileJComplete;

        checklist.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return (await GetByCaseIdAsync(caseRegisterId, ct))!;
    }

    public async Task<CaseClosureChecklistDto> RequestReviewAsync(Guid caseRegisterId, RequestReviewRequest request, Guid userId, CancellationToken ct = default)
    {
        var checklist = await _context.CaseClosureChecklists
            .FirstOrDefaultAsync(c => c.CaseRegisterId == caseRegisterId && c.DeletedAt == null, ct)
            ?? throw new InvalidOperationException($"Checklist for case {caseRegisterId} not found");

        var requestedStatus = await _context.CaseReviewStatuses
            .FirstOrDefaultAsync(s => s.Code == "REQUESTED", ct)
            ?? throw new InvalidOperationException("REQUESTED review status not found");

        checklist.ReviewStatusId = requestedStatus.Id;
        checklist.ReviewRequestedAt = DateTime.UtcNow;
        checklist.ReviewRequestedById = userId;
        checklist.ReviewNotes = request.ReviewNotes;
        checklist.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return (await GetByCaseIdAsync(caseRegisterId, ct))!;
    }

    public async Task<CaseClosureChecklistDto> ApproveReviewAsync(Guid caseRegisterId, ReviewDecisionRequest request, Guid userId, CancellationToken ct = default)
    {
        var checklist = await _context.CaseClosureChecklists
            .FirstOrDefaultAsync(c => c.CaseRegisterId == caseRegisterId && c.DeletedAt == null, ct)
            ?? throw new InvalidOperationException($"Checklist for case {caseRegisterId} not found");

        var approvedStatus = await _context.CaseReviewStatuses
            .FirstOrDefaultAsync(s => s.Code == "APPROVED", ct)
            ?? throw new InvalidOperationException("APPROVED review status not found");

        checklist.ReviewStatusId = approvedStatus.Id;
        checklist.ApprovedById = userId;
        checklist.ApprovedAt = DateTime.UtcNow;
        checklist.ReviewNotes = request.ReviewNotes;
        checklist.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return (await GetByCaseIdAsync(caseRegisterId, ct))!;
    }

    public async Task<CaseClosureChecklistDto> RejectReviewAsync(Guid caseRegisterId, ReviewDecisionRequest request, Guid userId, CancellationToken ct = default)
    {
        var checklist = await _context.CaseClosureChecklists
            .FirstOrDefaultAsync(c => c.CaseRegisterId == caseRegisterId && c.DeletedAt == null, ct)
            ?? throw new InvalidOperationException($"Checklist for case {caseRegisterId} not found");

        var rejectedStatus = await _context.CaseReviewStatuses
            .FirstOrDefaultAsync(s => s.Code == "REJECTED", ct)
            ?? throw new InvalidOperationException("REJECTED review status not found");

        checklist.ReviewStatusId = rejectedStatus.Id;
        checklist.ReviewNotes = request.ReviewNotes;
        checklist.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return (await GetByCaseIdAsync(caseRegisterId, ct))!;
    }

    private string? GetUserName(Guid? userId)
    {
        if (!userId.HasValue) return null;
        var user = _context.Users.Find(userId.Value);
        return user?.FullName;
    }

    private async Task<CaseClosureChecklistDto> MapToDtoAsync(CaseClosureChecklist checklist, CancellationToken ct = default)
    {
        return new CaseClosureChecklistDto
        {
            Id = checklist.Id,
            CaseRegisterId = checklist.CaseRegisterId,
            CaseNo = checklist.CaseRegister?.CaseNo,
            ClosureTypeId = checklist.ClosureTypeId,
            ClosureTypeName = checklist.ClosureType?.Name,
            LegalSectionId = checklist.LegalSectionId,
            LegalSectionTitle = checklist.LegalSection?.Title,
            SubfileAComplete = checklist.SubfileAComplete,
            SubfileBComplete = checklist.SubfileBComplete,
            SubfileCComplete = checklist.SubfileCComplete,
            SubfileDComplete = checklist.SubfileDComplete,
            SubfileEComplete = checklist.SubfileEComplete,
            SubfileFComplete = checklist.SubfileFComplete,
            SubfileGComplete = checklist.SubfileGComplete,
            SubfileHComplete = checklist.SubfileHComplete,
            SubfileIComplete = checklist.SubfileIComplete,
            SubfileJComplete = checklist.SubfileJComplete,
            AllSubfilesVerified = checklist.AllSubfilesVerified,
            ReviewStatusId = checklist.ReviewStatusId,
            ReviewStatusName = checklist.ReviewStatus?.Name,
            ReviewRequestedAt = checklist.ReviewRequestedAt,
            ReviewRequestedById = checklist.ReviewRequestedById,
            ReviewRequestedByName = GetUserName(checklist.ReviewRequestedById),
            ReviewNotes = checklist.ReviewNotes,
            ApprovedById = checklist.ApprovedById,
            ApprovedByName = GetUserName(checklist.ApprovedById),
            ApprovedAt = checklist.ApprovedAt,
            VerifiedById = checklist.VerifiedById,
            VerifiedByName = GetUserName(checklist.VerifiedById),
            VerifiedAt = checklist.VerifiedAt,
            CreatedAt = checklist.CreatedAt,
            UpdatedAt = checklist.UpdatedAt
        };
    }
}
