using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Repositories.CaseManagement;
using TruLoad.Backend.Services.Interfaces.CaseManagement;

namespace TruLoad.Backend.Services.Implementations.CaseManagement;

public class CaseRegisterService : ICaseRegisterService
{
    private readonly ICaseRegisterRepository _caseRegisterRepository;
    private readonly TruLoadDbContext _context;

    public CaseRegisterService(
        ICaseRegisterRepository caseRegisterRepository,
        TruLoadDbContext context)
    {
        _caseRegisterRepository = caseRegisterRepository;
        _context = context;
    }

    public async Task<CaseRegisterDto?> GetByIdAsync(Guid id)
    {
        var caseRegister = await _caseRegisterRepository.GetByIdAsync(id);
        return caseRegister == null ? null : MapToDto(caseRegister);
    }

    public async Task<CaseRegisterDto?> GetByCaseNoAsync(string caseNo)
    {
        var caseRegister = await _caseRegisterRepository.GetByCaseNoAsync(caseNo);
        return caseRegister == null ? null : MapToDto(caseRegister);
    }

    public async Task<CaseRegisterDto?> GetByWeighingIdAsync(Guid weighingId)
    {
        var caseRegister = await _caseRegisterRepository.GetByWeighingIdAsync(weighingId);
        return caseRegister == null ? null : MapToDto(caseRegister);
    }

    public async Task<IEnumerable<CaseRegisterDto>> SearchCasesAsync(CaseSearchCriteria criteria)
    {
        var cases = await _caseRegisterRepository.SearchAsync(
            caseNo: criteria.CaseNo,
            vehicleRegNumber: criteria.VehicleRegNumber,
            violationTypeId: criteria.ViolationTypeId,
            caseStatusId: criteria.CaseStatusId,
            dispositionTypeId: criteria.DispositionTypeId,
            createdFrom: criteria.CreatedFrom,
            createdTo: criteria.CreatedTo,
            escalatedToCaseManager: criteria.EscalatedToCaseManager,
            caseManagerId: criteria.CaseManagerId,
            pageNumber: criteria.PageNumber,
            pageSize: criteria.PageSize);

        return cases.Select(MapToDto);
    }

    public async Task<CaseRegisterDto> CreateCaseAsync(CreateCaseRegisterRequest request, Guid userId)
    {
        // Get default "Open" status
        var openStatus = await _context.CaseStatuses
            .FirstOrDefaultAsync(cs => cs.Code == "OPEN")
            ?? throw new InvalidOperationException("Default OPEN case status not found");

        // Get default "Pending" disposition
        var pendingDisposition = await _context.DispositionTypes
            .FirstOrDefaultAsync(dt => dt.Code == "PENDING")
            ?? throw new InvalidOperationException("Default PENDING disposition not found");

        // Generate case number - get user's assigned station
        var user = await _context.Users
            .Include(u => u.Station)
            .FirstOrDefaultAsync(u => u.Id == userId);
        var stationPrefix = user?.Station?.Code ?? "CASE";
        var caseNo = await _caseRegisterRepository.GenerateNextCaseNumberAsync(stationPrefix);

        var caseRegister = new CaseRegister
        {
            Id = Guid.NewGuid(),
            CaseNo = caseNo,
            WeighingId = request.WeighingId,
            YardEntryId = request.YardEntryId,
            ProhibitionOrderId = request.ProhibitionOrderId,
            VehicleId = request.VehicleId,
            DriverId = request.DriverId,
            ViolationTypeId = request.ViolationTypeId,
            ViolationDetails = request.ViolationDetails,
            ActId = request.ActId,
            RoadId = request.RoadId,
            CountyId = request.CountyId,
            DistrictId = request.DistrictId,
            SubcountyId = request.SubcountyId,
            CaseStatusId = openStatus.Id,
            DispositionTypeId = pendingDisposition.Id,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _caseRegisterRepository.CreateAsync(caseRegister);
        return MapToDto(created);
    }

    public async Task<CaseRegisterDto> CreateCaseFromWeighingAsync(Guid weighingId, Guid userId)
    {
        // Get weighing transaction
        var weighing = await _context.WeighingTransactions
            .FirstOrDefaultAsync(w => w.Id == weighingId)
            ?? throw new InvalidOperationException($"Weighing transaction {weighingId} not found");

        // Check if case already exists for this weighing
        var existingCase = await _caseRegisterRepository.GetByWeighingIdAsync(weighingId);
        if (existingCase != null)
            throw new InvalidOperationException($"Case already exists for weighing {weighingId}: {existingCase.CaseNo}");

        // Get violation type - "Overload" as default
        var overloadViolationType = await _context.ViolationTypes
            .FirstOrDefaultAsync(vt => vt.Code == "OVERLOAD")
            ?? throw new InvalidOperationException("Overload violation type not found");

        // Get prohibition order if exists (query separately)
        var prohibitionOrder = await _context.ProhibitionOrders
            .FirstOrDefaultAsync(po => po.WeighingId == weighingId);

        // Resolve ActId: use weighing's ActId if set, otherwise fall back to default act from settings
        Guid? actId = weighing.ActId;
        if (!actId.HasValue)
        {
            var defaultActSetting = await _context.ApplicationSettings
                .FirstOrDefaultAsync(s => s.SettingKey == SettingKeys.DefaultActCode);
            var actCode = defaultActSetting?.SettingValue ?? "TRAFFIC_ACT";
            var defaultAct = await _context.ActDefinitions
                .FirstOrDefaultAsync(a => a.Code == actCode);
            actId = defaultAct?.Id;
        }

        // Create case register entry
        var request = new CreateCaseRegisterRequest
        {
            WeighingId = weighingId,
            ProhibitionOrderId = prohibitionOrder?.Id,
            VehicleId = weighing.VehicleId,
            DriverId = weighing.DriverId,
            ViolationTypeId = overloadViolationType.Id,
            ViolationDetails = $"GVW Overload: {weighing.OverloadKg:N0} kg. Control Status: {weighing.ControlStatus}",
            ActId = actId
        };

        return await CreateCaseAsync(request, userId);
    }

    public async Task<CaseRegisterDto> CreateCaseFromProhibitionAsync(Guid prohibitionOrderId, Guid userId)
    {
        var prohibition = await _context.ProhibitionOrders
            .FirstOrDefaultAsync(p => p.Id == prohibitionOrderId)
            ?? throw new InvalidOperationException($"Prohibition order {prohibitionOrderId} not found");

        // Check if case already exists
        var existingCase = await _caseRegisterRepository.GetByProhibitionOrderIdAsync(prohibitionOrderId);
        if (existingCase != null)
            throw new InvalidOperationException($"Case already exists for prohibition {prohibitionOrderId}");

        if (prohibition.WeighingId == Guid.Empty)
            throw new InvalidOperationException("Prohibition order has no associated weighing");

        return await CreateCaseFromWeighingAsync(prohibition.WeighingId, userId);
    }

    public async Task<CaseRegisterDto> UpdateCaseAsync(Guid id, UpdateCaseRegisterRequest request, Guid userId)
    {
        // Use FindAsync to get a tracked entity (avoids AsNoTracking conflicts)
        var caseRegister = await _context.CaseRegisters.FindAsync(id)
            ?? throw new InvalidOperationException($"Case {id} not found");

        // Update fields
        if (!string.IsNullOrWhiteSpace(request.ViolationDetails))
            caseRegister.ViolationDetails = request.ViolationDetails;

        if (!string.IsNullOrWhiteSpace(request.DriverNtacNo))
            caseRegister.DriverNtacNo = request.DriverNtacNo;

        if (!string.IsNullOrWhiteSpace(request.TransporterNtacNo))
            caseRegister.TransporterNtacNo = request.TransporterNtacNo;

        if (!string.IsNullOrWhiteSpace(request.ObNo))
            caseRegister.ObNo = request.ObNo;

        if (request.CourtId.HasValue)
            caseRegister.CourtId = request.CourtId;

        if (request.DispositionTypeId.HasValue)
            caseRegister.DispositionTypeId = request.DispositionTypeId;

        if (request.CaseManagerId.HasValue)
            caseRegister.CaseManagerId = request.CaseManagerId;

        if (request.ProsecutorId.HasValue)
            caseRegister.ProsecutorId = request.ProsecutorId;

        if (request.InvestigatingOfficerId.HasValue)
            caseRegister.InvestigatingOfficerId = request.InvestigatingOfficerId;

        caseRegister.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Re-fetch with includes to populate navigation properties for DTO mapping
        var refreshed = await _caseRegisterRepository.GetByIdAsync(caseRegister.Id)
            ?? throw new InvalidOperationException($"Case {caseRegister.Id} not found after update");
        return MapToDto(refreshed);
    }

    public async Task<CaseRegisterDto> CloseCaseAsync(Guid id, CloseCaseRequest request, Guid userId)
    {
        var caseRegister = await _caseRegisterRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Case {id} not found");

        // State machine validation - check current status
        var currentStatus = caseRegister.CaseStatus?.Code ?? await GetStatusCodeAsync(caseRegister.CaseStatusId);

        // Cases can only be closed from OPEN, INVESTIGATION, or ESCALATED states
        var closableStates = new[] { "OPEN", "INVESTIGATION", "ESCALATED" };
        if (!closableStates.Contains(currentStatus))
        {
            throw new InvalidOperationException(
                $"Cannot close case in status '{currentStatus}'. Cases can only be closed from: {string.Join(", ", closableStates)}");
        }

        // Validate disposition is provided
        if (request.DispositionTypeId == Guid.Empty)
        {
            throw new ArgumentException("Disposition type is required when closing a case");
        }

        // Get "Closed" status
        var closedStatus = await _context.CaseStatuses
            .FirstOrDefaultAsync(cs => cs.Code == "CLOSED")
            ?? throw new InvalidOperationException("CLOSED case status not found");

        caseRegister.CaseStatusId = closedStatus.Id;
        caseRegister.CaseStatus = null; // Detach stale navigation to prevent EF conflict
        caseRegister.DispositionTypeId = request.DispositionTypeId;
        caseRegister.DispositionType = null; // Detach stale navigation
        caseRegister.ClosingReason = request.ClosingReason;
        caseRegister.ClosedAt = DateTime.UtcNow;
        caseRegister.ClosedById = userId;
        caseRegister.UpdatedAt = DateTime.UtcNow;

        var updated = await _caseRegisterRepository.UpdateAsync(caseRegister);
        return MapToDto(updated);
    }

    private async Task<string> GetStatusCodeAsync(Guid statusId)
    {
        var status = await _context.CaseStatuses.FindAsync(statusId);
        return status?.Code ?? "UNKNOWN";
    }

    public async Task<CaseRegisterDto> EscalateToCaseManagerAsync(Guid id, Guid caseManagerId, Guid userId)
    {
        var caseRegister = await _caseRegisterRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Case {id} not found");

        // Verify case manager exists
        var caseManager = await _context.CaseManagers.FindAsync(caseManagerId)
            ?? throw new InvalidOperationException($"Case manager {caseManagerId} not found");

        // Get "Escalated" status
        var escalatedStatus = await _context.CaseStatuses
            .FirstOrDefaultAsync(cs => cs.Code == "ESCALATED")
            ?? throw new InvalidOperationException("ESCALATED status not found");

        caseRegister.EscalatedToCaseManager = true;
        caseRegister.CaseManagerId = caseManagerId;
        caseRegister.CaseStatusId = escalatedStatus.Id;

        var updated = await _caseRegisterRepository.UpdateAsync(caseRegister);
        return MapToDto(updated);
    }

    public async Task<CaseRegisterDto> AssignInvestigatingOfficerAsync(Guid id, Guid officerId, Guid assignedById)
    {
        var caseRegister = await _caseRegisterRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Case {id} not found");

        caseRegister.InvestigatingOfficerId = officerId;
        caseRegister.InvestigatingOfficerAssignedById = assignedById;
        caseRegister.InvestigatingOfficerAssignedAt = DateTime.UtcNow;

        var updated = await _caseRegisterRepository.UpdateAsync(caseRegister);
        return MapToDto(updated);
    }

    public async Task<Dictionary<string, int>> GetCaseStatisticsAsync()
    {
        var total = await _caseRegisterRepository.GetTotalCountAsync();

        var statuses = await _context.CaseStatuses.ToListAsync();
        var stats = new Dictionary<string, int> { { "Total", total } };

        foreach (var status in statuses)
        {
            var count = await _caseRegisterRepository.GetCountByStatusAsync(status.Id);
            stats[status.Name] = count;
        }

        return stats;
    }

    public async Task<bool> DeleteCaseAsync(Guid id)
    {
        return await _caseRegisterRepository.DeleteAsync(id);
    }

    private CaseRegisterDto MapToDto(CaseRegister caseRegister)
    {
        return new CaseRegisterDto
        {
            Id = caseRegister.Id,
            CaseNo = caseRegister.CaseNo,
            WeighingId = caseRegister.WeighingId,
            YardEntryId = caseRegister.YardEntryId,
            ProhibitionOrderId = caseRegister.ProhibitionOrderId,
            VehicleId = caseRegister.VehicleId,
            DriverId = caseRegister.DriverId,
            ViolationTypeId = caseRegister.ViolationTypeId,
            ViolationType = caseRegister.ViolationType?.Name ?? string.Empty,
            ViolationDetails = caseRegister.ViolationDetails,
            ActId = caseRegister.ActId,
            ActName = caseRegister.ActDefinition?.Name,
            DriverNtacNo = caseRegister.DriverNtacNo,
            TransporterNtacNo = caseRegister.TransporterNtacNo,
            ObNo = caseRegister.ObNo,
            CourtId = caseRegister.CourtId,
            DispositionTypeId = caseRegister.DispositionTypeId,
            DispositionType = caseRegister.DispositionType?.Name,
            CaseStatusId = caseRegister.CaseStatusId,
            CaseStatus = caseRegister.CaseStatus?.Name ?? string.Empty,
            EscalatedToCaseManager = caseRegister.EscalatedToCaseManager,
            CaseManagerId = caseRegister.CaseManagerId,
            ProsecutorId = caseRegister.ProsecutorId,
            ComplainantOfficerId = caseRegister.ComplainantOfficerId,
            InvestigatingOfficerId = caseRegister.InvestigatingOfficerId,
            CreatedById = caseRegister.CreatedById,
            CreatedAt = caseRegister.CreatedAt,
            ClosedAt = caseRegister.ClosedAt,
            ClosedById = caseRegister.ClosedById,
            ClosingReason = caseRegister.ClosingReason,
            UpdatedAt = caseRegister.UpdatedAt
        };
    }
}
