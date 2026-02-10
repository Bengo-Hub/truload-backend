using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Services.Interfaces.CaseManagement;

namespace TruLoad.Backend.Services.Implementations.CaseManagement;

/// <summary>
/// Service implementation for load correction memo queries.
/// Read-only — memos are auto-created by the weighing/receipt workflow.
/// </summary>
public class LoadCorrectionMemoService : ILoadCorrectionMemoService
{
    private readonly TruLoadDbContext _context;

    public LoadCorrectionMemoService(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<LoadCorrectionMemoDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var memo = await _context.LoadCorrectionMemos
            .Include(m => m.CaseRegister)
            .Include(m => m.Weighing)
            .Include(m => m.IssuedBy)
            .FirstOrDefaultAsync(m => m.Id == id && m.DeletedAt == null, ct);

        return memo == null ? null : MapToDto(memo);
    }

    public async Task<IEnumerable<LoadCorrectionMemoDto>> GetByCaseIdAsync(Guid caseRegisterId, CancellationToken ct = default)
    {
        var memos = await _context.LoadCorrectionMemos
            .Include(m => m.CaseRegister)
            .Include(m => m.Weighing)
            .Include(m => m.IssuedBy)
            .Where(m => m.CaseRegisterId == caseRegisterId && m.DeletedAt == null)
            .OrderByDescending(m => m.IssuedAt)
            .ToListAsync(ct);

        return memos.Select(MapToDto);
    }

    public async Task<LoadCorrectionMemoDto?> GetByWeighingIdAsync(Guid weighingId, CancellationToken ct = default)
    {
        var memo = await _context.LoadCorrectionMemos
            .Include(m => m.CaseRegister)
            .Include(m => m.Weighing)
            .Include(m => m.IssuedBy)
            .FirstOrDefaultAsync(m => m.WeighingId == weighingId && m.DeletedAt == null, ct);

        return memo == null ? null : MapToDto(memo);
    }

    private LoadCorrectionMemoDto MapToDto(LoadCorrectionMemo memo)
    {
        return new LoadCorrectionMemoDto
        {
            Id = memo.Id,
            MemoNo = memo.MemoNo,
            CaseRegisterId = memo.CaseRegisterId,
            CaseNo = memo.CaseRegister?.CaseNo,
            WeighingId = memo.WeighingId,
            WeighingTicketNo = memo.Weighing?.TicketNumber,
            OverloadKg = memo.OverloadKg,
            RedistributionType = memo.RedistributionType,
            ReweighScheduledAt = memo.ReweighScheduledAt,
            ReweighWeighingId = memo.ReweighWeighingId,
            ComplianceAchieved = memo.ComplianceAchieved,
            ReliefTruckRegNumber = memo.ReliefTruckRegNumber,
            ReliefTruckEmptyWeightKg = memo.ReliefTruckEmptyWeightKg,
            IssuedById = memo.IssuedById,
            IssuedByName = memo.IssuedBy?.FullName,
            IssuedAt = memo.IssuedAt,
            CreatedAt = memo.CreatedAt
        };
    }
}
