using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.CaseManagement;

namespace TruLoad.Backend.Repositories.CaseManagement;

public class CaseRegisterRepository : ICaseRegisterRepository
{
    private readonly TruLoadDbContext _context;

    public CaseRegisterRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<CaseRegister?> GetByIdAsync(Guid id)
    {
        return await _context.CaseRegisters
            .AsNoTracking()
            .Include(c => c.ViolationType)
            .Include(c => c.ActDefinition)
            .Include(c => c.DispositionType)
            .Include(c => c.CaseStatus)
            .Include(c => c.CaseManager)
            .Include(c => c.Subfiles)
            .Include(c => c.SpecialReleases)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<CaseRegister?> GetByCaseNoAsync(string caseNo)
    {
        return await _context.CaseRegisters
            .AsNoTracking()
            .Include(c => c.ViolationType)
            .Include(c => c.ActDefinition)
            .Include(c => c.DispositionType)
            .Include(c => c.CaseStatus)
            .Include(c => c.CaseManager)
            .FirstOrDefaultAsync(c => c.CaseNo == caseNo);
    }

    public async Task<CaseRegister?> GetByWeighingIdAsync(Guid weighingId)
    {
        return await _context.CaseRegisters
            .AsNoTracking()
            .Include(c => c.ViolationType)
            .Include(c => c.CaseStatus)
            .FirstOrDefaultAsync(c => c.WeighingId == weighingId);
    }

    public async Task<CaseRegister?> GetByProhibitionOrderIdAsync(Guid prohibitionOrderId)
    {
        return await _context.CaseRegisters
            .AsNoTracking()
            .Include(c => c.ViolationType)
            .Include(c => c.CaseStatus)
            .FirstOrDefaultAsync(c => c.ProhibitionOrderId == prohibitionOrderId);
    }

    public async Task<IEnumerable<CaseRegister>> GetAllAsync(int pageNumber = 1, int pageSize = 50)
    {
        return await _context.CaseRegisters
            .AsNoTracking()
            .Include(c => c.ViolationType)
            .Include(c => c.CaseStatus)
            .Include(c => c.DispositionType)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IEnumerable<CaseRegister>> SearchAsync(
        string? caseNo = null,
        string? vehicleRegNumber = null,
        Guid? stationId = null,
        Guid? violationTypeId = null,
        Guid? caseStatusId = null,
        Guid? dispositionTypeId = null,
        DateTime? createdFrom = null,
        DateTime? createdTo = null,
        bool? escalatedToCaseManager = null,
        Guid? caseManagerId = null,
        int pageNumber = 1,
        int pageSize = 50)
    {
        var query = _context.CaseRegisters
            .AsNoTracking()
            .Include(c => c.Weighing)
            .Include(c => c.ViolationType)
            .Include(c => c.CaseStatus)
            .Include(c => c.DispositionType)
            .Include(c => c.CaseManager)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(caseNo))
            query = query.Where(c => c.CaseNo.Contains(caseNo));

        if (stationId.HasValue)
            query = query.Where(c => c.Weighing != null && c.Weighing.StationId == stationId.Value);

        if (violationTypeId.HasValue)
            query = query.Where(c => c.ViolationTypeId == violationTypeId.Value);

        if (caseStatusId.HasValue)
            query = query.Where(c => c.CaseStatusId == caseStatusId.Value);

        if (dispositionTypeId.HasValue)
            query = query.Where(c => c.DispositionTypeId == dispositionTypeId.Value);

        if (createdFrom.HasValue)
            query = query.Where(c => c.CreatedAt >= createdFrom.Value);

        if (createdTo.HasValue)
            query = query.Where(c => c.CreatedAt <= createdTo.Value);

        if (escalatedToCaseManager.HasValue)
            query = query.Where(c => c.EscalatedToCaseManager == escalatedToCaseManager.Value);

        if (caseManagerId.HasValue)
            query = query.Where(c => c.CaseManagerId == caseManagerId.Value);

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetTotalCountAsync()
    {
        return await _context.CaseRegisters.AsNoTracking().CountAsync();
    }

    public async Task<int> GetCountByStatusAsync(Guid caseStatusId)
    {
        return await _context.CaseRegisters
            .AsNoTracking()
            .Where(c => c.CaseStatusId == caseStatusId)
            .CountAsync();
    }

    public async Task<int> GetCountByDispositionAsync(Guid dispositionTypeId)
    {
        return await _context.CaseRegisters
            .AsNoTracking()
            .Where(c => c.DispositionTypeId == dispositionTypeId)
            .CountAsync();
    }

    public async Task<CaseRegister> CreateAsync(CaseRegister caseRegister)
    {
        caseRegister.CreatedAt = DateTime.UtcNow;
        caseRegister.UpdatedAt = DateTime.UtcNow;

        _context.CaseRegisters.Add(caseRegister);
        await _context.SaveChangesAsync();
        return caseRegister;
    }

    public async Task<CaseRegister> UpdateAsync(CaseRegister caseRegister)
    {
        caseRegister.UpdatedAt = DateTime.UtcNow;

        _context.CaseRegisters.Update(caseRegister);
        await _context.SaveChangesAsync();
        return caseRegister;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var caseRegister = await GetByIdAsync(id);
        if (caseRegister == null) return false;

        _context.CaseRegisters.Remove(caseRegister);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<string> GenerateNextCaseNumberAsync(string stationPrefix)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"{stationPrefix}-{year}-";

        var lastCaseNo = await _context.CaseRegisters
            .Where(c => c.CaseNo.StartsWith(prefix))
            .OrderByDescending(c => c.CaseNo)
            .Select(c => c.CaseNo)
            .FirstOrDefaultAsync();

        if (lastCaseNo == null)
            return $"{prefix}00001";

        var lastNumber = int.Parse(lastCaseNo.Split('-').Last());
        var nextNumber = lastNumber + 1;
        return $"{prefix}{nextNumber:D5}";
    }

    public async Task<bool> CaseNumberExistsAsync(string caseNo)
    {
        return await _context.CaseRegisters.AnyAsync(c => c.CaseNo == caseNo);
    }
}
