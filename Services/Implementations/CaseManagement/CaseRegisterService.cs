using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.DTOs.Shared;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Repositories.CaseManagement;
using TruLoad.Backend.Services.Interfaces.CaseManagement;
using TruLoad.Backend.Services.Interfaces.Shared;

namespace TruLoad.Backend.Services.Implementations.CaseManagement;

public class CaseRegisterService : ICaseRegisterService
{
    private readonly ICaseRegisterRepository _caseRegisterRepository;
    private readonly INotificationService _notificationService;
    private readonly IBackgroundNotificationDispatcher _backgroundNotifications;
    private readonly ITenantContext _tenantContext;
    private readonly TruLoadDbContext _context;
    private readonly ILogger<CaseRegisterService> _logger;

    public CaseRegisterService(
        ICaseRegisterRepository caseRegisterRepository,
        INotificationService notificationService,
        IBackgroundNotificationDispatcher backgroundNotifications,
        ITenantContext tenantContext,
        TruLoadDbContext context,
        ILogger<CaseRegisterService> logger)
    {
        _caseRegisterRepository = caseRegisterRepository;
        _notificationService = notificationService;
        _backgroundNotifications = backgroundNotifications;
        _tenantContext = tenantContext;
        _context = context;
        _logger = logger;
    }

    /// <summary>Tenant slug captured from the request for off-request notification dispatch.</summary>
    private string? TenantSlug => _tenantContext.OrganizationCode?.ToLowerInvariant();

    public async Task<CaseRegisterDto?> GetByIdAsync(Guid id)
    {
        var caseRegister = await _caseRegisterRepository.GetByIdAsync(id);
        if (caseRegister == null) return null;
        var dto = MapToDto(caseRegister);
        await EnrichProsecutionStatusAsync(new[] { dto });
        return dto;
    }

    /// <summary>
    /// Batch-populates HasProsecution/ProsecutionStatus for the given DTOs using a single
    /// query against prosecution_cases (one-to-one with case_register), avoiding N+1.
    /// </summary>
    private async Task EnrichProsecutionStatusAsync(IReadOnlyCollection<CaseRegisterDto> dtos)
    {
        if (dtos.Count == 0) return;
        var caseIds = dtos.Select(d => d.Id).ToList();
        var prosecutions = await _context.ProsecutionCases
            .AsNoTracking()
            .Where(p => caseIds.Contains(p.CaseRegisterId) && p.DeletedAt == null)
            .Select(p => new { p.CaseRegisterId, p.Status })
            .ToListAsync();
        var byCase = prosecutions
            .GroupBy(p => p.CaseRegisterId)
            .ToDictionary(g => g.Key, g => g.First().Status);
        foreach (var dto in dtos)
        {
            if (byCase.TryGetValue(dto.Id, out var status))
            {
                dto.HasProsecution = true;
                dto.ProsecutionStatus = status;
            }
        }
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

    public async Task<PagedResponse<CaseRegisterDto>> SearchCasesAsync(CaseSearchCriteria criteria)
    {
        // Build filtered query for count
        var countQuery = _context.CaseRegisters
            .AsNoTracking()
            .Where(c => c.DeletedAt == null)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(criteria.GeneralSearch))
        {
            var gs = criteria.GeneralSearch.Trim().Replace(" ", "");
            countQuery = countQuery
                .Include(c => c.Weighing).ThenInclude(w => w!.Vehicle)
                .Where(c => c.CaseNo.Contains(criteria.GeneralSearch.Trim()) ||
                    (c.Weighing != null && c.Weighing.Vehicle != null &&
                     EF.Functions.ILike(c.Weighing.Vehicle.RegNo.Replace(" ", ""), $"%{gs}%")));
        }
        if (!string.IsNullOrWhiteSpace(criteria.CaseNo))
            countQuery = countQuery.Where(c => c.CaseNo.Contains(criteria.CaseNo));
        if (!string.IsNullOrWhiteSpace(criteria.VehicleRegNumber))
        {
            var normalizedReg = criteria.VehicleRegNumber.Replace(" ", "");
            countQuery = countQuery
                .Include(c => c.Weighing).ThenInclude(w => w!.Vehicle)
                .Where(c => c.Weighing != null && c.Weighing.Vehicle != null &&
                     EF.Functions.ILike(c.Weighing.Vehicle.RegNo.Replace(" ", ""), $"%{normalizedReg}%"));
        }
        if (criteria.StationId.HasValue)
            countQuery = countQuery.Where(c => c.Weighing != null && c.Weighing.StationId == criteria.StationId.Value);
        if (criteria.ViolationTypeId.HasValue)
            countQuery = countQuery.Where(c => c.ViolationTypeId == criteria.ViolationTypeId.Value);
        if (criteria.CaseStatusId.HasValue)
            countQuery = countQuery.Where(c => c.CaseStatusId == criteria.CaseStatusId.Value);
        if (criteria.DispositionTypeId.HasValue)
            countQuery = countQuery.Where(c => c.DispositionTypeId == criteria.DispositionTypeId.Value);
        if (criteria.CreatedFrom.HasValue)
        {
            var from = DateTime.SpecifyKind(criteria.CreatedFrom.Value, DateTimeKind.Utc);
            countQuery = countQuery.Where(c => c.CreatedAt >= from);
        }
        if (criteria.CreatedTo.HasValue)
        {
            var to = DateTime.SpecifyKind(criteria.CreatedTo.Value.Date.AddDays(1), DateTimeKind.Utc);
            countQuery = countQuery.Where(c => c.CreatedAt < to);
        }
        if (criteria.EscalatedToCaseManager.HasValue)
            countQuery = countQuery.Where(c => c.EscalatedToCaseManager == criteria.EscalatedToCaseManager.Value);
        if (criteria.CaseManagerId.HasValue)
            countQuery = countQuery.Where(c => c.CaseManagerId == criteria.CaseManagerId.Value);

        var totalCount = await countQuery.CountAsync();

        // Get paginated items from repository
        var cases = await _caseRegisterRepository.SearchAsync(
            generalSearch: criteria.GeneralSearch,
            caseNo: criteria.CaseNo,
            vehicleRegNumber: criteria.VehicleRegNumber,
            stationId: criteria.StationId,
            violationTypeId: criteria.ViolationTypeId,
            caseStatusId: criteria.CaseStatusId,
            dispositionTypeId: criteria.DispositionTypeId,
            createdFrom: criteria.CreatedFrom,
            createdTo: criteria.CreatedTo,
            escalatedToCaseManager: criteria.EscalatedToCaseManager,
            caseManagerId: criteria.CaseManagerId,
            pageNumber: criteria.PageNumber,
            pageSize: criteria.PageSize);

        var items = cases.Select(MapToDto).ToList();
        await EnrichProsecutionStatusAsync(items);

        return PagedResponse<CaseRegisterDto>.Create(
            items,
            totalCount,
            criteria.PageNumber,
            criteria.PageSize);
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
            SubcountyId = request.SubcountyId,
            CaseStatusId = openStatus.Id,
            DispositionTypeId = pendingDisposition.Id,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _caseRegisterRepository.CreateAsync(caseRegister);

        // NOTIFY: Case Created
        await _notificationService.SendInternalNotificationAsync(
            userId,
            "New Case Created",
            $"Case {caseNo} has been created for vehicle {request.VehicleId}.",
            "info",
            $"/cases/{created.Id}");

        if (!string.IsNullOrEmpty(user?.Email))
        {
            // Dispatch off-request with a fresh DI scope (request scope is disposed before this runs).
            _backgroundNotifications.DispatchWorkflowEmail(
                TenantSlug,
                "caseCreated",
                "truload/case_created",
                user.Email,
                user.FullName ?? "Officer",
                new Dictionary<string, object>
                {
                    ["case_no"] = caseNo,
                    ["station_name"] = user.Station?.Name ?? "Unknown Station",
                    ["violation_details"] = request.ViolationDetails ?? string.Empty
                });
        }

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

        // Build violation details based on actual violation type
        // Traffic Act: axle overloads alone yield Warning (no GVW charge); only GVW overload creates Overloaded status
        string violationDetails = weighing.OverloadKg > 0
            ? $"GVW Overload: {weighing.OverloadKg:N0} kg above effective GVW limit. Control Status: {weighing.ControlStatus}"
            : $"Axle group overload detected; GVW within permissible tolerance. Control Status: {weighing.ControlStatus}";

        // Create case register entry
        var request = new CreateCaseRegisterRequest
        {
            WeighingId = weighingId,
            ProhibitionOrderId = prohibitionOrder?.Id,
            VehicleId = weighing.VehicleId,
            DriverId = weighing.DriverId,
            ViolationTypeId = overloadViolationType.Id,
            ViolationDetails = violationDetails,
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

        if (!string.IsNullOrWhiteSpace(request.ObExtractFileUrl))
            caseRegister.ObExtractFileUrl = request.ObExtractFileUrl;

        if (!string.IsNullOrWhiteSpace(request.CourtCaseNo))
            caseRegister.CourtCaseNo = request.CourtCaseNo;

        if (!string.IsNullOrWhiteSpace(request.PoliceCaseFileNo))
            caseRegister.PoliceCaseFileNo = request.PoliceCaseFileNo;

        if (request.CourtId.HasValue)
            caseRegister.CourtId = request.CourtId;

        if (request.DispositionTypeId.HasValue)
            caseRegister.DispositionTypeId = request.DispositionTypeId;

        if (request.CaseManagerId.HasValue)
            caseRegister.CaseManagerId = request.CaseManagerId;

        if (request.ProsecutorId.HasValue)
            caseRegister.ProsecutorId = request.ProsecutorId;

        if (request.ComplainantOfficerId.HasValue)
            caseRegister.ComplainantOfficerId = request.ComplainantOfficerId;

        if (request.DetentionStationId.HasValue)
            caseRegister.DetentionStationId = request.DetentionStationId;

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
        caseRegister.CaseStatus = null!; // Detach stale navigation to prevent EF conflict
        caseRegister.DispositionTypeId = request.DispositionTypeId;
        caseRegister.DispositionType = null!; // Detach stale navigation
        caseRegister.ClosingReason = request.ClosingReason;
        caseRegister.ClosedAt = DateTime.UtcNow;
        caseRegister.ClosedById = userId;
        caseRegister.UpdatedAt = DateTime.UtcNow;

        var updated = await _caseRegisterRepository.UpdateAsync(caseRegister);

        // NOTIFY: Case Closed
        await _notificationService.SendInternalNotificationAsync(
            caseRegister.CreatedById.GetValueOrDefault(), 
            "Case Closed",
            $"Case {caseRegister.CaseNo} has been closed by {userId}.",
            "success",
            $"/cases/{id}");

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

        // Verify case manager exists — lookup by entity ID first, then by UserId
        // Frontend may send either the CaseManager entity ID or the User ID
        var caseManager = await _context.CaseManagers.FindAsync(caseManagerId);
        if (caseManager == null)
        {
            // Try looking up by UserId (frontend sends user ID from the escalation form)
            caseManager = await _context.CaseManagers
                .FirstOrDefaultAsync(cm => cm.UserId == caseManagerId && cm.RoleType == "case_manager" && cm.IsActive);
            if (caseManager == null)
            {
                // Auto-create a CaseManager record for this user if they exist
                var userExists = await _context.Users.AnyAsync(u => u.Id == caseManagerId);
                if (!userExists)
                    throw new InvalidOperationException($"Case manager {caseManagerId} not found");

                caseManager = new CaseManager
                {
                    Id = Guid.NewGuid(),
                    UserId = caseManagerId,
                    RoleType = "case_manager",
                    Specialization = "General",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.CaseManagers.Add(caseManager);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Auto-created CaseManager record for user {UserId}", caseManagerId);
            }
        }

        // Get "Escalated" status
        var escalatedStatus = await _context.CaseStatuses
            .FirstOrDefaultAsync(cs => cs.Code == "ESCALATED")
            ?? throw new InvalidOperationException("ESCALATED status not found");

        caseRegister.EscalatedToCaseManager = true;
        caseRegister.CaseManagerId = caseManager.Id;
        caseRegister.CaseStatusId = escalatedStatus.Id;

        var updated = await _caseRegisterRepository.UpdateAsync(caseRegister);

        // NOTIFY: Case Escalated (notify the user, not the CaseManager entity)
        await _notificationService.SendInternalNotificationAsync(
            caseManager.UserId,
            "Case Escalated to You",
            $"Case {caseRegister.CaseNo} has been escalated to you by {userId}.",
            "warning",
            $"/cases/{id}");

        // Resolve the manager's email in-scope, then dispatch off-request with a fresh DI scope.
        var manager = await _context.Users.AsNoTracking()
            .Where(u => u.Id == caseManager.UserId && !string.IsNullOrEmpty(u.Email))
            .Select(u => new { u.Email, u.FullName })
            .FirstOrDefaultAsync();
        if (manager != null)
        {
            _backgroundNotifications.DispatchWorkflowEmail(
                TenantSlug,
                "caseEscalated",
                "truload/case_escalated",
                manager.Email!,
                manager.FullName ?? "Case Manager",
                new Dictionary<string, object>
                {
                    ["case_no"] = caseRegister.CaseNo
                });
        }

        return MapToDto(updated);
    }

    public async Task<List<string>> ResendCaseNotificationsAsync(Guid caseId, Guid userId)
    {
        var sent = new List<string>();
        var caseEntity = await _caseRegisterRepository.GetByIdAsync(caseId)
            ?? throw new InvalidOperationException($"Case {caseId} not found");

        // Recipient: the creating officer, else the requesting user.
        var recipientUserId = caseEntity.CreatedById ?? userId;
        var recipient = await _context.Users.AsNoTracking()
            .Where(u => u.Id == recipientUserId && !string.IsNullOrEmpty(u.Email))
            .Select(u => new { u.Email, u.FullName })
            .FirstOrDefaultAsync();
        if (recipient == null)
            throw new InvalidOperationException("No recipient email found for this case's officer.");

        var tenantSlug = TenantSlug;
        var caseNo = caseEntity.CaseNo;
        var recipientName = recipient.FullName ?? "Officer";

        // Resolve weighing + station (for overload alert and case station name).
        string stationName = "Unknown Station";
        var weighing = caseEntity.WeighingId.HasValue
            ? await _context.WeighingTransactions.AsNoTracking()
                .Where(w => w.Id == caseEntity.WeighingId.Value)
                .Select(w => new { w.VehicleRegNumber, w.OverloadKg, w.TicketNumber, w.StationId })
                .FirstOrDefaultAsync()
            : null;
        var resolvedStationId = weighing?.StationId ?? caseEntity.StationId;
        if (resolvedStationId.HasValue)
        {
            stationName = await _context.Stations.AsNoTracking()
                .Where(s => s.Id == resolvedStationId.Value)
                .Select(s => s.Name).FirstOrDefaultAsync() ?? "Unknown Station";
        }

        // 1) Overload alert (only when there is a weighing).
        if (weighing != null)
        {
            if (await _notificationService.SendWorkflowEmailAsync(
                    "overloadAlert", "truload/overload_alert", recipient.Email!, recipientName,
                    new Dictionary<string, object>
                    {
                        ["vehicle_reg"] = weighing.VehicleRegNumber,
                        ["overload_kg"] = weighing.OverloadKg,
                        ["ticket_no"] = weighing.TicketNumber,
                        ["station_name"] = stationName,
                        ["case_no"] = caseNo
                    }, tenantSlug: tenantSlug))
                sent.Add("overloadAlert");
        }

        // 2) Case created.
        if (await _notificationService.SendWorkflowEmailAsync(
                "caseCreated", "truload/case_created", recipient.Email!, recipientName,
                new Dictionary<string, object>
                {
                    ["case_no"] = caseNo,
                    ["station_name"] = stationName,
                    ["violation_details"] = caseEntity.ViolationDetails ?? string.Empty
                }, tenantSlug: tenantSlug))
            sent.Add("caseCreated");

        // 3) Invoice issued (only when an invoice exists for this case).
        var invoice = await _context.Invoices.AsNoTracking()
            .Where(i => i.DeletedAt == null &&
                (i.CaseRegisterId == caseId || (i.ProsecutionCase != null && i.ProsecutionCase.CaseRegisterId == caseId)))
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new { i.InvoiceNo, i.AmountDue, i.Currency })
            .FirstOrDefaultAsync();
        if (invoice != null)
        {
            if (await _notificationService.SendWorkflowEmailAsync(
                    "invoiceIssued", "truload/invoice_issued", recipient.Email!, recipientName,
                    new Dictionary<string, object>
                    {
                        ["invoice_no"] = invoice.InvoiceNo,
                        ["amount_due"] = invoice.AmountDue.ToString("N2"),
                        ["currency"] = invoice.Currency,
                        ["due_date"] = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd")
                    }, tenantSlug: tenantSlug))
                sent.Add("invoiceIssued");
        }

        _logger.LogInformation("Resent {Count} workflow email(s) for case {CaseNo} to {Email}: {Workflows}",
            sent.Count, caseNo, recipient.Email, string.Join(", ", sent));
        return sent;
    }

    public async Task<CaseRegisterDto> AssignInvestigatingOfficerAsync(Guid id, Guid officerId, Guid assignedById)
    {
        var caseRegister = await _caseRegisterRepository.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Case {id} not found");

        caseRegister.InvestigatingOfficerId = officerId;
        caseRegister.InvestigatingOfficerAssignedById = assignedById;
        caseRegister.InvestigatingOfficerAssignedAt = DateTime.UtcNow;

        var updated = await _caseRegisterRepository.UpdateAsync(caseRegister);

        // NOTIFY: Officer Assigned
        await _notificationService.SendInternalNotificationAsync(
            officerId,
            "Case Assigned to You",
            $"You have been assigned as the investigating officer for Case {caseRegister.CaseNo} by officer ID {assignedById}.",
            "info",
            $"/cases/{id}");

        return MapToDto(updated);
    }

    public async Task<CaseStatisticsDto> GetCaseStatisticsAsync(DateTime? dateFrom = null, DateTime? dateTo = null, Guid? stationId = null)
    {
        var cases = _context.CaseRegisters.AsNoTracking().Where(c => c.DeletedAt == null);

        if (stationId.HasValue)
            cases = cases.Where(c => c.Weighing != null && c.Weighing.StationId == stationId.Value);
        if (dateFrom.HasValue)
        {
            var from = DateTime.SpecifyKind(dateFrom.Value, DateTimeKind.Utc);
            cases = cases.Where(c => c.CreatedAt >= from);
        }
        if (dateTo.HasValue)
        {
            var to = DateTime.SpecifyKind(dateTo.Value.Date.AddDays(1), DateTimeKind.Utc);
            cases = cases.Where(c => c.CreatedAt < to);
        }

        var total = await cases.CountAsync();

        // Get counts by status code for reliable matching
        var statusCounts = await cases
            .Include(c => c.CaseStatus)
            .GroupBy(c => c.CaseStatus!.Code)
            .Select(g => new { StatusCode = g.Key, Count = g.Count() })
            .ToListAsync();

        var open = statusCounts.Where(s => s.StatusCode == "OPEN").Sum(s => s.Count);
        var closed = statusCounts.Where(s => s.StatusCode == "CLOSED").Sum(s => s.Count);
        var escalated = await cases.CountAsync(c => c.EscalatedToCaseManager);

        return new CaseStatisticsDto
        {
            TotalCases = total,
            OpenCases = open,
            EscalatedCases = escalated,
            ClosedCases = closed
        };
    }

    public async Task<bool> DeleteCaseAsync(Guid id)
    {
        return await _caseRegisterRepository.DeleteAsync(id);
    }

    private CaseRegisterDto MapToDto(CaseRegister caseRegister)
    {
        var weighing = caseRegister.Weighing;
        var driverName = weighing?.Driver != null
            ? $"{weighing.Driver.FullNames} {weighing.Driver.Surname}".Trim()
            : null;
        var driverLicenseNo = weighing?.Driver?.DrivingLicenseNo;
        var driverIdNumber = weighing?.Driver?.IdNumber;
        var driverPhoneNumber = weighing?.Driver?.PhoneNumber;
        var vehicleRegNumber = weighing?.VehicleRegNumber ?? string.Empty;
        var transporterName = weighing?.Transporter?.Name;

        // Resolve names that aren't loaded via navigation properties
        string? caseManagerName = null;
        if (caseRegister.CaseManager != null)
        {
            var cmUser = _context.Users.AsNoTracking().FirstOrDefault(u => u.Id == caseRegister.CaseManager.UserId);
            caseManagerName = cmUser?.FullName;
        }

        string? investigatingOfficerName = null;
        if (caseRegister.InvestigatingOfficerId.HasValue)
        {
            var ioUser = _context.Users.AsNoTracking().FirstOrDefault(u => u.Id == caseRegister.InvestigatingOfficerId);
            investigatingOfficerName = ioUser?.FullName;
        }

        string? courtName = null;
        if (caseRegister.CourtId.HasValue)
        {
            var court = _context.Courts.AsNoTracking().FirstOrDefault(c => c.Id == caseRegister.CourtId);
            courtName = court?.Name;
        }

        string? createdByName = null;
        if (caseRegister.CreatedById.HasValue)
        {
            var createdByUser = _context.Users.AsNoTracking().FirstOrDefault(u => u.Id == caseRegister.CreatedById);
            createdByName = createdByUser?.FullName;
        }

        string? closedByName = null;
        if (caseRegister.ClosedById.HasValue)
        {
            var closedByUser = _context.Users.AsNoTracking().FirstOrDefault(u => u.Id == caseRegister.ClosedById);
            closedByName = closedByUser?.FullName;
        }

        return new CaseRegisterDto
        {
            Id = caseRegister.Id,
            CaseNo = caseRegister.CaseNo,
            WeighingId = caseRegister.WeighingId,
            YardEntryId = caseRegister.YardEntryId,
            ProhibitionOrderId = caseRegister.ProhibitionOrderId,
            VehicleId = caseRegister.VehicleId,
            VehicleRegNumber = vehicleRegNumber,
            DriverId = caseRegister.DriverId,
            DriverName = driverName,
            DriverLicenseNo = driverLicenseNo,
            DriverIdNumber = driverIdNumber,
            DriverPhoneNumber = driverPhoneNumber,
            ViolationTypeId = caseRegister.ViolationTypeId,
            ViolationType = caseRegister.ViolationType?.Name ?? string.Empty,
            ViolationDetails = caseRegister.ViolationDetails,
            ActId = caseRegister.ActId,
            ActName = caseRegister.ActDefinition?.Name,
            DriverNtacNo = caseRegister.DriverNtacNo,
            TransporterNtacNo = caseRegister.TransporterNtacNo,
            TransporterName = transporterName,
            ObNo = caseRegister.ObNo,
            ObExtractFileUrl = caseRegister.ObExtractFileUrl,
            CourtCaseNo = caseRegister.CourtCaseNo,
            PoliceCaseFileNo = caseRegister.PoliceCaseFileNo,
            CourtId = caseRegister.CourtId,
            CourtName = courtName,
            DispositionTypeId = caseRegister.DispositionTypeId,
            DispositionType = caseRegister.DispositionType?.Name,
            CaseStatusId = caseRegister.CaseStatusId,
            CaseStatus = caseRegister.CaseStatus?.Name ?? string.Empty,
            EscalatedToCaseManager = caseRegister.EscalatedToCaseManager,
            CaseManagerId = caseRegister.CaseManagerId,
            CaseManagerName = caseManagerName,
            ProsecutorId = caseRegister.ProsecutorId,
            ComplainantOfficerId = caseRegister.ComplainantOfficerId,
            ComplainantOfficerName = caseRegister.ComplainantOfficer?.FullName,
            DetentionStationId = caseRegister.DetentionStationId,
            DetentionStationName = caseRegister.DetentionStation?.Name,
            InvestigatingOfficerId = caseRegister.InvestigatingOfficerId,
            InvestigatingOfficerName = investigatingOfficerName,
            CreatedById = caseRegister.CreatedById,
            CreatedByName = createdByName,
            CreatedAt = caseRegister.CreatedAt,
            ClosedAt = caseRegister.ClosedAt,
            ClosedById = caseRegister.ClosedById,
            ClosedByName = closedByName,
            ClosingReason = caseRegister.ClosingReason,
            UpdatedAt = caseRegister.UpdatedAt
        };
    }
}
