using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Data.Repositories.Weighing;
using TruLoad.Backend.DTOs.Weighing;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Models.Financial;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Models.Infrastructure;
using TruLoad.Backend.Services.Interfaces.Financial;
using TruLoad.Backend.Services.Interfaces.Shared;
using TruLoad.Backend.Services.Interfaces.Weighing;
using TruLoad.Backend.Services.Interfaces.Infrastructure;
using STJson = System.Text.Json;

namespace TruLoad.Backend.Services.Implementations.Weighing;

public class CommercialWeighingService : ICommercialWeighingService
{
    private readonly TruLoadDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IDocumentNumberService _documentNumberService;
    private readonly ITreasuryService _treasuryService;
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CommercialWeighingService> _logger;

    public CommercialWeighingService(
        TruLoadDbContext dbContext,
        ITenantContext tenantContext,
        IVehicleRepository vehicleRepository,
        IDocumentNumberService documentNumberService,
        ITreasuryService treasuryService,
        INotificationService notificationService,
        IConfiguration configuration,
        ILogger<CommercialWeighingService> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _vehicleRepository = vehicleRepository;
        _documentNumberService = documentNumberService;
        _treasuryService = treasuryService;
        _notificationService = notificationService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<WeighingTransaction> InitiateCommercialWeighingAsync(
        InitiateCommercialWeighingRequest request,
        Guid userId)
    {
        // Resolve vehicle
        Guid vehicleId;
        string vehicleRegNo;

        if (request.VehicleId.HasValue && request.VehicleId.Value != Guid.Empty)
        {
            vehicleId = request.VehicleId.Value;
            var vehicle = await _vehicleRepository.GetByIdAsync(vehicleId);
            vehicleRegNo = vehicle?.RegNo ?? request.VehicleRegNo?.Trim().ToUpper() ?? string.Empty;
        }
        else if (!string.IsNullOrWhiteSpace(request.VehicleRegNo))
        {
            var normalizedRegNo = request.VehicleRegNo.Trim().ToUpper();
            var existingVehicle = await _vehicleRepository.GetByRegNoAsync(normalizedRegNo);

            if (existingVehicle != null)
            {
                vehicleId = existingVehicle.Id;
            }
            else
            {
                var newVehicle = new Vehicle { RegNo = normalizedRegNo };
                var created = await _vehicleRepository.CreateAsync(newVehicle);
                vehicleId = created.Id;
                _logger.LogInformation("Auto-created vehicle {RegNo} with ID {VehicleId}", normalizedRegNo, vehicleId);
            }
            vehicleRegNo = normalizedRegNo;
        }
        else
        {
            throw new ArgumentException("Either VehicleId or VehicleRegNo must be provided.");
        }

        // Generate ticket number
        var orgId = _tenantContext.OrganizationId;
        var ticketNumber = await _documentNumberService.GenerateNumberAsync(
            orgId, request.StationId, DocumentTypes.WeightTicket,
            vehicleRegNo);

        // Load snapshot data — captured once so historical tickets show correct info even if master data changes
        Driver? snapshotDriver = null;
        Transporter? snapshotTransporter = null;
        Vehicle? snapshotVehicle = null;
        CargoTypes? snapshotCargo = null;
        OriginsDestinations? snapshotOrigin = null;
        OriginsDestinations? snapshotDestination = null;

        // Load vehicle for make/model snapshot
        if (vehicleId != Guid.Empty)
            snapshotVehicle = await _dbContext.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vehicleId);

        if (request.DriverId.HasValue)
            snapshotDriver = await _dbContext.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.DriverId.Value);

        if (request.TransporterId.HasValue)
            snapshotTransporter = await _dbContext.Transporters.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.TransporterId.Value);

        if (request.CargoId.HasValue)
            snapshotCargo = await _dbContext.CargoTypes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CargoId.Value);

        if (request.OriginId.HasValue)
            snapshotOrigin = await _dbContext.OriginsDestinations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == request.OriginId.Value);

        if (request.DestinationId.HasValue)
            snapshotDestination = await _dbContext.OriginsDestinations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == request.DestinationId.Value);

        var transaction = new WeighingTransaction
        {
            TicketNumber = ticketNumber,
            StationId = request.StationId,
            WeighedByUserId = userId,
            VehicleId = vehicleId,
            VehicleRegNumber = vehicleRegNo,
            DriverId = request.DriverId,
            TransporterId = request.TransporterId,
            OriginId = request.OriginId,
            DestinationId = request.DestinationId,
            CargoId = request.CargoId,
            ConsignmentNo = request.ConsignmentNo,
            OrderReference = request.OrderReference,
            ExpectedNetWeightKg = request.ExpectedNetWeightKg,
            SealNumbers = request.SealNumbers,
            TrailerRegNo = request.TrailerRegNo,
            Remarks = request.Remarks,
            IndustryMetadata = request.IndustryMetadata,
            WeighingMode = "commercial",
            WeighingType = "static",
            ControlStatus = "Pending",
            CaptureStatus = "pending",
            CaptureSource = "frontend",
            WeighedAt = DateTime.UtcNow,
            OrganizationId = orgId,
            WeighingScaleType = request.WeighingScaleType ?? "multideck",
            SnapshotDriverName = snapshotDriver != null ? $"{snapshotDriver.FullNames} {snapshotDriver.Surname}".Trim() : null,
            SnapshotTransporterName = snapshotTransporter?.Name,
            SnapshotVehicleMake = snapshotVehicle?.Make,
            SnapshotVehicleModel = snapshotVehicle?.Model,
            SnapshotCargoTypeName = snapshotCargo?.Name,
            SnapshotOriginName = snapshotOrigin?.Name,
            SnapshotDestinationName = snapshotDestination?.Name,
        };

        _dbContext.WeighingTransactions.Add(transaction);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Commercial weighing initiated: {TicketNumber}, Station: {StationId}, Vehicle: {VehicleRegNo}",
            ticketNumber, request.StationId, vehicleRegNo);

        return transaction;
    }

    public async Task<WeighingTransaction> CaptureFirstWeightAsync(
        Guid transactionId,
        CaptureFirstWeightRequest request)
    {
        var transaction = await GetTransactionOrThrowAsync(transactionId);
        EnsureCommercialMode(transaction);

        if (transaction.FirstWeightKg.HasValue)
        {
            throw new InvalidOperationException("First weight has already been captured for this transaction.");
        }

        transaction.FirstWeightKg = request.WeightKg;
        transaction.FirstWeightType = request.WeightType;
        transaction.FirstWeightAt = DateTime.UtcNow;
        transaction.CaptureStatus = "first_weight_captured";
        transaction.UpdatedAt = DateTime.UtcNow;

        // Store per-deck/axle weights in IndustryMetadata JSON (commercial doesn't use weighing_axles enforcement schema)
        if (request.AxleWeights != null && request.AxleWeights.Count > 0)
        {
            var meta = MergeIndustryMetadata(transaction.IndustryMetadata, new { firstPassWeights = request.AxleWeights });
            transaction.IndustryMetadata = meta;
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "First weight captured for {TransactionId}: {WeightKg}kg ({WeightType})",
            transactionId, request.WeightKg, request.WeightType);

        return transaction;
    }

    public async Task<WeighingTransaction> CaptureSecondWeightAsync(
        Guid transactionId,
        CaptureSecondWeightRequest request)
    {
        var transaction = await GetTransactionOrThrowAsync(transactionId);
        EnsureCommercialMode(transaction);

        if (!transaction.FirstWeightKg.HasValue)
        {
            throw new InvalidOperationException("First weight must be captured before capturing second weight.");
        }

        if (transaction.SecondWeightKg.HasValue)
        {
            throw new InvalidOperationException("Second weight has already been captured for this transaction.");
        }

        // Auto-determine second weight type (opposite of first)
        var secondWeightType = transaction.FirstWeightType == "tare" ? "gross" : "tare";

        transaction.SecondWeightKg = request.WeightKg;
        transaction.SecondWeightType = secondWeightType;
        transaction.SecondWeightAt = DateTime.UtcNow;

        // Resolve tare and gross
        int tareWeightKg, grossWeightKg;
        if (transaction.FirstWeightType == "tare")
        {
            tareWeightKg = transaction.FirstWeightKg.Value;
            grossWeightKg = request.WeightKg;
        }
        else
        {
            tareWeightKg = request.WeightKg;
            grossWeightKg = transaction.FirstWeightKg.Value;
        }

        transaction.TareWeightKg = tareWeightKg;
        transaction.GrossWeightKg = grossWeightKg;
        transaction.NetWeightKg = grossWeightKg - tareWeightKg;
        transaction.TareSource = "measured";
        transaction.GvwMeasuredKg = grossWeightKg;

        // Allow operator to provide/override expected net weight at capture time
        if (request.ExpectedNetWeightKg.HasValue)
            transaction.ExpectedNetWeightKg = request.ExpectedNetWeightKg.Value;

        // Calculate discrepancy if expected weight provided
        if (transaction.ExpectedNetWeightKg.HasValue && transaction.NetWeightKg.HasValue)
        {
            transaction.WeightDiscrepancyKg = transaction.NetWeightKg.Value - transaction.ExpectedNetWeightKg.Value;
        }

        // Check commercial tolerance
        await CheckCommercialToleranceAsync(transaction);

        transaction.ControlStatus = transaction.ToleranceExceeded ? "ToleranceExceeded" : "Complete";
        transaction.CaptureStatus = "captured";
        transaction.ProcessingTimeSeconds = (int)(DateTime.UtcNow - transaction.WeighedAt).TotalSeconds;
        transaction.UpdatedAt = DateTime.UtcNow;

        // Store per-deck/axle weights for second pass in IndustryMetadata JSON
        if (request.AxleWeights != null && request.AxleWeights.Count > 0)
        {
            var meta = MergeIndustryMetadata(transaction.IndustryMetadata, new { secondPassWeights = request.AxleWeights });
            transaction.IndustryMetadata = meta;
        }

        // Update vehicle tare if tare was measured in this session
        await RecordTareWeightAsync(
            transaction.VehicleId,
            tareWeightKg,
            transaction.StationId,
            "measured",
            $"Measured during commercial weighing {transaction.TicketNumber}");

        await _dbContext.SaveChangesAsync();

        // Create commercial weighing invoice (idempotent)
        await CreateCommercialInvoiceAsync(transaction);

        _logger.LogInformation(
            "Second weight captured for {TransactionId}: {WeightKg}kg ({WeightType}). Net={NetKg}kg",
            transactionId, request.WeightKg, secondWeightType, transaction.NetWeightKg);

        _ = SendCompletionNotificationsAsync(transaction);

        return transaction;
    }

    public async Task<WeighingTransaction> UseStoredTareAsync(
        Guid transactionId,
        UseStoredTareRequest request)
    {
        var transaction = await GetTransactionOrThrowAsync(transactionId);
        EnsureCommercialMode(transaction);

        if (!transaction.FirstWeightKg.HasValue)
        {
            throw new InvalidOperationException("First weight (gross) must be captured before using stored tare.");
        }

        if (transaction.FirstWeightType == "tare")
        {
            throw new InvalidOperationException("First weight was captured as tare. Use CaptureSecondWeight to capture gross weight instead.");
        }

        // Resolve tare weight
        int tareWeightKg;
        string tareSource;

        if (request.OverrideTareWeightKg.HasValue)
        {
            tareWeightKg = request.OverrideTareWeightKg.Value;
            tareSource = "preset";
        }
        else
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(transaction.VehicleId);
            if (vehicle == null)
            {
                throw new InvalidOperationException("Vehicle not found.");
            }

            // Prefer last measured tare, fall back to default
            if (vehicle.LastTareWeightKg.HasValue)
            {
                tareWeightKg = vehicle.LastTareWeightKg.Value;
                tareSource = "stored";

                // Check tare expiry (honouring org-level grace period)
                if (vehicle.LastTareWeighedAt.HasValue)
                {
                    var expiryDays = vehicle.TareExpiryDays ?? 90;

                    // Load org-level grace period so soft-expired tares are not hard-blocked immediately
                    var orgGraceDays = 0;
                    var orgId = _tenantContext.OrganizationId;
                    if (orgId != Guid.Empty)
                    {
                        var org = await _dbContext.Organizations
                            .AsNoTracking()
                            .FirstOrDefaultAsync(o => o.Id == orgId);
                        if (org != null)
                            orgGraceDays = org.TareGracePeriodDays;
                    }

                    var effectiveExpiryDays = expiryDays + orgGraceDays;
                    if (vehicle.LastTareWeighedAt.Value.AddDays(effectiveExpiryDays) < DateTime.UtcNow)
                    {
                        var daysElapsed = (DateTime.UtcNow - vehicle.LastTareWeighedAt.Value).Days;
                        _logger.LogWarning(
                            "Stored tare for vehicle {VehicleId} expired ({ExpiryDays} days + {GraceDays} grace = {Effective} effective). " +
                            "Last measured: {LastTareAt}. Days elapsed: {DaysElapsed}",
                            transaction.VehicleId, expiryDays, orgGraceDays, effectiveExpiryDays,
                            vehicle.LastTareWeighedAt.Value, daysElapsed);
                        throw new InvalidOperationException(
                            $"Stored tare for this vehicle expired {daysElapsed} days ago " +
                            $"(expiry: {expiryDays} days, grace: {orgGraceDays} days). " +
                            "Re-weigh the empty vehicle or provide a manual override tare weight.");
                    }
                }
            }
            else if (vehicle.DefaultTareWeightKg.HasValue)
            {
                tareWeightKg = vehicle.DefaultTareWeightKg.Value;
                tareSource = "preset";
            }
            else
            {
                throw new InvalidOperationException(
                    $"No stored or default tare weight found for vehicle {vehicle.RegNo}. Please capture tare weight on the scale.");
            }
        }

        var grossWeightKg = transaction.FirstWeightKg.Value;

        transaction.SecondWeightKg = tareWeightKg;
        transaction.SecondWeightType = "tare";
        transaction.SecondWeightAt = DateTime.UtcNow;
        transaction.TareWeightKg = tareWeightKg;
        transaction.GrossWeightKg = grossWeightKg;
        transaction.NetWeightKg = grossWeightKg - tareWeightKg;
        transaction.TareSource = tareSource;
        transaction.GvwMeasuredKg = grossWeightKg;

        // Calculate discrepancy if expected weight provided
        if (transaction.ExpectedNetWeightKg.HasValue && transaction.NetWeightKg.HasValue)
        {
            transaction.WeightDiscrepancyKg = transaction.NetWeightKg.Value - transaction.ExpectedNetWeightKg.Value;
        }

        // Check commercial tolerance
        await CheckCommercialToleranceAsync(transaction);

        transaction.ControlStatus = transaction.ToleranceExceeded ? "ToleranceExceeded" : "Complete";
        transaction.CaptureStatus = "captured";
        transaction.ProcessingTimeSeconds = (int)(DateTime.UtcNow - transaction.WeighedAt).TotalSeconds;
        transaction.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        // Create commercial weighing invoice (idempotent)
        await CreateCommercialInvoiceAsync(transaction);

        _logger.LogInformation(
            "Stored tare used for {TransactionId}: tare={TareKg}kg ({Source}). Net={NetKg}kg",
            transactionId, tareWeightKg, tareSource, transaction.NetWeightKg);

        _ = SendCompletionNotificationsAsync(transaction);

        return transaction;
    }

    public async Task<CommercialWeighingResultDto> GetCommercialResultAsync(Guid transactionId)
    {
        var transaction = await _dbContext.WeighingTransactions
            .AsNoTracking()
            .Include(t => t.Vehicle)
            .Include(t => t.Driver)
            .Include(t => t.Transporter)
            .Include(t => t.WeighedByUser)
            .Include(t => t.Station)
            .Include(t => t.Origin)
            .Include(t => t.Destination)
            .Include(t => t.Cargo)
            .FirstOrDefaultAsync(t => t.Id == transactionId);

        if (transaction == null)
            throw new KeyNotFoundException($"Weighing transaction {transactionId} not found");

        var dto = MapToCommercialResultDto(transaction);

        // Attach invoice data so the frontend can show payment status / open the treasury modal
        var invoice = await _dbContext.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.WeighingId == transactionId && i.InvoiceType == "commercial_weighing_fee");

        if (invoice != null)
        {
            dto.InvoiceNo = invoice.InvoiceNo;
            dto.InvoiceStatus = invoice.Status;
            dto.InvoiceAmountKes = invoice.AmountDue;
            dto.TreasuryIntentId = invoice.TreasuryIntentId;
            var payPortalBase = _configuration["Treasury:PayPortalBaseUrl"]
                ?? "https://books.codevertexitsolutions.com/pay";
            dto.TreasuryPaymentUrl = !string.IsNullOrWhiteSpace(invoice.TreasuryIntentId)
                ? $"{payPortalBase}?intent_id={invoice.TreasuryIntentId}"
                : null;
        }

        return dto;
    }

    public async Task RecordTareWeightAsync(
        Guid vehicleId,
        int tareWeightKg,
        Guid? stationId,
        string source = "measured",
        string? notes = null)
    {
        var vehicle = await _dbContext.Vehicles.FindAsync(vehicleId);
        if (vehicle == null)
        {
            _logger.LogWarning("Cannot record tare weight: vehicle {VehicleId} not found", vehicleId);
            return;
        }

        vehicle.LastTareWeightKg = tareWeightKg;
        vehicle.LastTareWeighedAt = DateTime.UtcNow;
        vehicle.UpdatedAt = DateTime.UtcNow;

        var history = new VehicleTareHistory
        {
            VehicleId = vehicleId,
            TareWeightKg = tareWeightKg,
            WeighedAt = DateTime.UtcNow,
            StationId = stationId,
            OrganizationId = _tenantContext.OrganizationId,
            Source = source,
            Notes = notes
        };

        _dbContext.VehicleTareHistory.Add(history);
        // SaveChanges is called by the caller or at end of operation

        _logger.LogInformation(
            "Recorded tare weight for vehicle {VehicleId}: {TareKg}kg ({Source})",
            vehicleId, tareWeightKg, source);
    }

    public async Task<WeighingTransaction> UpdateQualityDeductionAsync(
        Guid transactionId,
        UpdateQualityDeductionRequest request)
    {
        var transaction = await GetTransactionOrThrowAsync(transactionId);
        EnsureCommercialMode(transaction);

        if (!transaction.NetWeightKg.HasValue)
        {
            throw new InvalidOperationException("Net weight must be calculated before applying quality deductions.");
        }

        transaction.QualityDeductionKg = request.QualityDeductionKg;
        transaction.AdjustedNetWeightKg = transaction.NetWeightKg.Value - request.QualityDeductionKg;
        transaction.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            transaction.Remarks = string.IsNullOrEmpty(transaction.Remarks)
                ? $"Quality deduction: {request.Reason}"
                : $"{transaction.Remarks}; Quality deduction: {request.Reason}";
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Quality deduction updated for {TransactionId}: {DeductionKg}kg, adjusted net={AdjustedKg}kg",
            transactionId, request.QualityDeductionKg, transaction.AdjustedNetWeightKg);

        _ = SendQualityDeductionNotificationAsync(transaction, request.QualityDeductionKg, request.Reason);

        return transaction;
    }

    private Task SendQualityDeductionNotificationAsync(WeighingTransaction transaction, int deductionKg, string? reason)
    {
        return Task.Run(async () =>
        {
            try
            {
                if (!transaction.TransporterId.HasValue) return;

                var transporter = await _dbContext.Transporters
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == transaction.TransporterId.Value);
                if (transporter == null || string.IsNullOrWhiteSpace(transporter.Email)) return;

                var data = new Dictionary<string, object>
                {
                    ["ticket_number"] = transaction.TicketNumber ?? transaction.Id.ToString(),
                    ["vehicle_plate"] = transaction.VehicleRegNumber,
                    ["net_weight_kg"] = transaction.NetWeightKg ?? 0,
                    ["deduction_kg"] = deductionKg,
                    ["adjusted_net_kg"] = transaction.AdjustedNetWeightKg ?? 0,
                    ["reason"] = reason ?? string.Empty,
                };

                await _notificationService.SendWorkflowEmailAsync(
                    workflowKey: "qualityDeductionApplied",
                    templateName: "truload/quality_deduction_applied",
                    primaryRecipientEmail: transporter.Email,
                    primaryRecipientName: transporter.Name ?? "Transporter",
                    templateData: data,
                    subject: $"[TruLoad] Quality Deduction Applied — {transaction.TicketNumber ?? transaction.Id.ToString()}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CommercialWeighing] Failed to send quality deduction notification for {Id}", transaction.Id);
            }
        });
    }

    public async Task<List<VehicleTareHistoryDto>> GetVehicleTareHistoryAsync(Guid vehicleId)
    {
        var orgId = _tenantContext.OrganizationId;
        var history = await _dbContext.VehicleTareHistory
            .AsNoTracking()
            .Include(h => h.Vehicle)
            .Include(h => h.Station)
            .Where(h => h.VehicleId == vehicleId &&
                        (orgId == Guid.Empty || h.OrganizationId == orgId))
            .OrderByDescending(h => h.WeighedAt)
            .ToListAsync();

        return history.Select(h => new VehicleTareHistoryDto
        {
            Id = h.Id,
            VehicleId = h.VehicleId,
            VehicleRegNo = h.Vehicle?.RegNo,
            TareWeightKg = h.TareWeightKg,
            WeighedAt = h.WeighedAt,
            StationId = h.StationId,
            StationName = h.Station?.Name,
            Source = h.Source,
            Notes = h.Notes
        }).ToList();
    }

    public async Task<List<CommercialToleranceSettingDto>> GetCommercialToleranceSettingsAsync()
    {
        var orgId = _tenantContext.OrganizationId;

        var settings = await _dbContext.CommercialToleranceSettings
            .AsNoTracking()
            .Include(s => s.CargoType)
            .Where(s => s.OrganizationId == orgId && s.IsActive)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();

        return settings.Select(s => new CommercialToleranceSettingDto
        {
            Id = s.Id,
            ToleranceType = s.ToleranceType,
            ToleranceValue = s.ToleranceValue,
            MaxToleranceKg = s.MaxToleranceKg,
            CargoTypeId = s.CargoTypeId,
            CargoTypeName = s.CargoType?.Name,
            Description = s.Description
        }).ToList();
    }

    public async Task<CommercialToleranceSettingDto> CreateCommercialToleranceSettingAsync(CommercialToleranceSettingDto dto)
    {
        var orgId = _tenantContext.OrganizationId;

        var setting = new CommercialToleranceSetting
        {
            OrganizationId = orgId,
            StationId = _tenantContext.StationId,
            ToleranceType = dto.ToleranceType,
            ToleranceValue = dto.ToleranceValue,
            MaxToleranceKg = dto.MaxToleranceKg,
            CargoTypeId = dto.CargoTypeId,
            Description = dto.Description
        };

        _dbContext.CommercialToleranceSettings.Add(setting);
        await _dbContext.SaveChangesAsync();

        dto.Id = setting.Id;
        return dto;
    }

    public async Task<CommercialToleranceSettingDto> UpdateCommercialToleranceSettingAsync(Guid id, CommercialToleranceSettingDto dto)
    {
        var setting = await _dbContext.CommercialToleranceSettings.FindAsync(id);
        if (setting == null)
            throw new KeyNotFoundException($"Commercial tolerance setting {id} not found");

        setting.ToleranceType = dto.ToleranceType;
        setting.ToleranceValue = dto.ToleranceValue;
        setting.MaxToleranceKg = dto.MaxToleranceKg;
        setting.CargoTypeId = dto.CargoTypeId;
        setting.Description = dto.Description;
        setting.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        dto.Id = setting.Id;
        return dto;
    }

    public async Task DeleteCommercialToleranceSettingAsync(Guid id)
    {
        var orgId = _tenantContext.OrganizationId;
        var setting = await _dbContext.CommercialToleranceSettings
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == orgId);

        if (setting == null)
            throw new KeyNotFoundException($"Commercial tolerance setting {id} not found for this organisation");

        _dbContext.CommercialToleranceSettings.Remove(setting);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<CommercialWeighingResultDto> VoidCommercialWeighingAsync(
        Guid transactionId,
        VoidCommercialWeighingRequest request,
        Guid voidedByUserId)
    {
        var transaction = await GetTransactionOrThrowAsync(transactionId);
        EnsureCommercialMode(transaction);

        if (transaction.VoidedAt.HasValue)
            throw new InvalidOperationException("This transaction has already been voided.");

        if (transaction.ControlStatus == "Complete" && transaction.SecondWeightKg.HasValue)
            throw new InvalidOperationException("Cannot void a completed weighing. Contact a supervisor for corrections.");

        transaction.VoidedAt = DateTime.UtcNow;
        transaction.VoidReason = request.Reason;
        transaction.VoidedByUserId = voidedByUserId;
        transaction.ControlStatus = "Voided";
        transaction.CaptureStatus = "voided";
        transaction.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Commercial weighing {TransactionId} voided by {UserId}: {Reason}",
            transactionId, voidedByUserId, request.Reason);

        return await GetCommercialResultAsync(transactionId);
    }

    public async Task<List<CommercialWeighingResultDto>> GetPendingCommercialTransactionsAsync(Guid stationId)
    {
        var orgId = _tenantContext.OrganizationId;

        var transactions = await _dbContext.WeighingTransactions
            .AsNoTracking()
            .Include(t => t.Vehicle)
            .Include(t => t.Driver)
            .Include(t => t.Transporter)
            .Include(t => t.Cargo)
            .Include(t => t.Origin)
            .Include(t => t.Destination)
            .Where(t =>
                t.OrganizationId == orgId &&
                t.StationId == stationId &&
                t.WeighingMode == "commercial" &&
                t.CaptureStatus == "first_weight_captured" &&
                t.VoidedAt == null)
            .OrderByDescending(t => t.FirstWeightAt)
            .Take(20)
            .ToListAsync();

        return transactions.Select(MapToCommercialResultDto).ToList();
    }

    public async Task<List<CommercialWeighingResultDto>> GetPendingByPlateAsync(string vehicleRegNo, int thresholdHours = 8)
    {
        var orgId = _tenantContext.OrganizationId;
        var cutoff = DateTime.UtcNow.AddHours(-thresholdHours);
        var regNo = vehicleRegNo.Trim().ToUpperInvariant();

        var transactions = await _dbContext.WeighingTransactions
            .AsNoTracking()
            .Include(t => t.Vehicle)
            .Include(t => t.Driver)
            .Include(t => t.Transporter)
            .Include(t => t.Cargo)
            .Include(t => t.Origin)
            .Include(t => t.Destination)
            .Where(t =>
                t.OrganizationId == orgId &&
                t.WeighingMode == "commercial" &&
                t.CaptureStatus == "first_weight_captured" &&
                t.VoidedAt == null &&
                t.Vehicle != null && t.Vehicle.RegNo == regNo &&
                t.FirstWeightAt.HasValue && t.FirstWeightAt.Value >= cutoff)
            .OrderByDescending(t => t.FirstWeightAt)
            .Take(5)
            .ToListAsync();

        return transactions.Select(MapToCommercialResultDto).ToList();
    }

    // ============================================================================
    // Private Helpers
    // ============================================================================

    /// <summary>
    /// Creates a flat-fee commercial weighing invoice when weighing completes.
    /// Idempotent — silently skips if invoice already exists.
    /// </summary>
    private async Task CreateCommercialInvoiceAsync(WeighingTransaction transaction)
    {
        try
        {
            var existing = await _dbContext.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.WeighingId == transaction.Id && i.InvoiceType == "commercial_weighing_fee");
            if (existing != null) return;

            var org = await _dbContext.Organizations
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == transaction.OrganizationId);
            if (org == null) return;

            // Facility-owned scales do not charge per-transaction fees
            if (org.WeighingBusinessModel == "FacilityOwnedScale" || org.CommercialWeighingFeeKes <= 0)
            {
                _logger.LogInformation(
                    "Skipping invoice creation for transaction {TransactionId} — FacilityOwnedScale or zero fee",
                    transaction.Id);
                return;
            }

            var invoiceNo = await _documentNumberService.GenerateNumberAsync(
                org.Id, transaction.StationId, DocumentTypes.Invoice);

            var invoice = new Invoice
            {
                InvoiceNo = invoiceNo,
                WeighingId = transaction.Id,
                AmountDue = org.CommercialWeighingFeeKes,
                Currency = "KES",
                Status = "pending",
                InvoiceType = "commercial_weighing_fee",
                GeneratedAt = DateTime.UtcNow,
                OrganizationId = org.Id,
                StationId = transaction.StationId
            };

            _dbContext.Invoices.Add(invoice);
            await _dbContext.SaveChangesAsync();

            if (org.PaymentGateway == "treasury" && !string.IsNullOrWhiteSpace(org.SsoTenantSlug))
            {
                try
                {
                    var intent = await _treasuryService.CreatePaymentIntentAsync(
                        org.SsoTenantSlug,
                        org.CommercialWeighingFeeKes,
                        invoice.Id.ToString(),
                        $"Weighing fee — ticket {transaction.TicketNumber}");

                    invoice.TreasuryIntentId = intent.IntentId;
                    invoice.TreasuryIntentStatus = intent.Status;
                    await _dbContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to create treasury payment intent for invoice {InvoiceNo}. Invoice saved as pending.",
                        invoiceNo);
                }
            }

            _logger.LogInformation(
                "Created commercial invoice {InvoiceNo} ({Amount} KES) for weighing {TransactionId}",
                invoiceNo, org.CommercialWeighingFeeKes, transaction.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create commercial invoice for transaction {TransactionId}. Manual intervention required.",
                transaction.Id);
        }
    }

    private async Task<WeighingTransaction> GetTransactionOrThrowAsync(Guid transactionId)
    {
        var transaction = await _dbContext.WeighingTransactions
            .Include(t => t.Vehicle)
            .FirstOrDefaultAsync(t => t.Id == transactionId);

        if (transaction == null)
            throw new KeyNotFoundException($"Weighing transaction {transactionId} not found");

        return transaction;
    }

    private static void EnsureCommercialMode(WeighingTransaction transaction)
    {
        if (transaction.WeighingMode != "commercial")
        {
            throw new InvalidOperationException(
                $"Transaction {transaction.Id} is not a commercial weighing (mode: {transaction.WeighingMode}).");
        }
    }

    /// <summary>
    /// Checks commercial tolerance settings for the transaction's organization and cargo type.
    /// Sets ToleranceApplied and related fields on the transaction.
    /// </summary>
    private async Task CheckCommercialToleranceAsync(WeighingTransaction transaction)
    {
        if (!transaction.NetWeightKg.HasValue || !transaction.ExpectedNetWeightKg.HasValue)
            return;

        var orgId = transaction.OrganizationId;
        var discrepancy = Math.Abs(transaction.WeightDiscrepancyKg ?? 0);

        // Find matching tolerance: cargo-specific first, then org-wide fallback
        var tolerance = await _dbContext.CommercialToleranceSettings
            .AsNoTracking()
            .Where(s => s.OrganizationId == orgId && s.IsActive)
            .Where(s => s.CargoTypeId == transaction.CargoId || s.CargoTypeId == null)
            .OrderByDescending(s => s.CargoTypeId.HasValue) // Prefer cargo-specific
            .FirstOrDefaultAsync();

        if (tolerance == null)
            return;

        int toleranceKg;
        if (tolerance.ToleranceType == "percentage")
        {
            toleranceKg = (int)Math.Round(transaction.ExpectedNetWeightKg.Value * tolerance.ToleranceValue / 100m);
            if (tolerance.MaxToleranceKg.HasValue && toleranceKg > tolerance.MaxToleranceKg.Value)
            {
                toleranceKg = tolerance.MaxToleranceKg.Value;
            }
        }
        else
        {
            toleranceKg = (int)tolerance.ToleranceValue;
        }

        transaction.ToleranceApplied = true;
        transaction.GvwToleranceKg = toleranceKg;
        transaction.GvwToleranceDisplay = tolerance.ToleranceType == "percentage"
            ? $"{tolerance.ToleranceValue:0.##}%"
            : $"{toleranceKg:N0} kg";

        transaction.ToleranceExceeded = discrepancy > toleranceKg;

        if (transaction.ToleranceExceeded)
        {
            _logger.LogWarning(
                "Commercial tolerance exceeded for {TransactionId}: discrepancy={DiscrepancyKg}kg, tolerance={ToleranceKg}kg",
                transaction.Id, discrepancy, toleranceKg);
        }
    }

    private Task SendCompletionNotificationsAsync(WeighingTransaction transaction)
    {
        return Task.Run(async () =>
        {
            try
            {
                var org = await _dbContext.Organizations
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(o => o.Id == transaction.OrganizationId);

                var station = await _dbContext.Stations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == transaction.StationId);

                var templateData = new Dictionary<string, object>
                {
                    ["ticket_number"] = transaction.TicketNumber ?? transaction.Id.ToString(),
                    ["vehicle_plate"] = transaction.VehicleRegNumber,
                    ["gross_weight_kg"] = transaction.GrossWeightKg ?? 0,
                    ["tare_weight_kg"] = transaction.TareWeightKg ?? 0,
                    ["net_weight_kg"] = transaction.NetWeightKg ?? 0,
                    ["station_name"] = station?.Name ?? "Unknown Station",
                    ["org_name"] = org?.Name ?? string.Empty,
                    ["weighed_at"] = (transaction.SecondWeightAt ?? DateTime.UtcNow).ToString("yyyy-MM-dd HH:mm"),
                };

                if (transaction.TransporterId.HasValue)
                {
                    var transporter = await _dbContext.Transporters
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Id == transaction.TransporterId.Value);
                    if (transporter != null && !string.IsNullOrWhiteSpace(transporter.Email))
                    {
                        await _notificationService.SendWorkflowEmailAsync(
                            workflowKey: "weighingTicketReady",
                            templateName: "truload/weight_ticket",
                            primaryRecipientEmail: transporter.Email,
                            primaryRecipientName: transporter.Name ?? "Transporter",
                            templateData: templateData,
                            subject: $"[TruLoad] Weight Ticket — {transaction.TicketNumber ?? transaction.Id.ToString()}");
                    }
                }

                var discrepancy = Math.Abs(transaction.WeightDiscrepancyKg ?? 0);

                if (transaction.ToleranceExceeded && !transaction.ToleranceExceptionApproved && org != null)
                {
                    var managerRoleNames = new[] { "Commercial Weighing Manager", "Station Manager" };
                    var managers = await _dbContext.Users
                        .AsNoTracking()
                        .Where(u =>
                            u.OrganizationId == org.Id &&
                            u.DeletedAt == null &&
                            !string.IsNullOrEmpty(u.Email) &&
                            _dbContext.UserRoles
                                .Join(_dbContext.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
                                .Where(x => x.UserId == u.Id && managerRoleNames.Contains(x.Name))
                                .Any())
                        .Select(u => new { u.Email, u.FullName })
                        .ToListAsync();

                    var alertData = new Dictionary<string, object>(templateData)
                    {
                        ["discrepancy_kg"] = discrepancy,
                        ["expected_net_kg"] = transaction.ExpectedNetWeightKg ?? 0,
                        ["tolerance_display"] = transaction.GvwToleranceDisplay ?? $"{transaction.GvwToleranceKg} kg",
                    };

                    var subject = $"[TruLoad] Tolerance Exception — {transaction.TicketNumber ?? transaction.Id.ToString()}";
                    // First manager gets the workflow email (includes group defaults + CC in prefs)
                    var first = managers.FirstOrDefault();
                    if (first != null)
                    {
                        await _notificationService.SendWorkflowEmailAsync(
                            workflowKey: "toleranceExceptionRaised",
                            templateName: "truload/tolerance_exception_alert",
                            primaryRecipientEmail: first.Email!,
                            primaryRecipientName: first.FullName ?? "Manager",
                            templateData: alertData,
                            subject: subject);
                    }
                    // Remaining managers each get a direct email
                    foreach (var manager in managers.Skip(1))
                    {
                        await _notificationService.SendEmailAsync(
                            templateName: "truload/tolerance_exception_alert",
                            recipientEmail: manager.Email!,
                            recipientName: manager.FullName ?? "Manager",
                            templateData: alertData,
                            subject: subject);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CommercialWeighing] Failed to send completion notifications for transaction {Id}", transaction.Id);
            }
        });
    }

    private static List<CommercialAxleWeightDto> ParsePassWeights(string? industryMetadata, string passKey)
    {
        if (string.IsNullOrEmpty(industryMetadata)) return new();
        try
        {
            var doc = STJson.JsonDocument.Parse(industryMetadata);
            if (!doc.RootElement.TryGetProperty(passKey, out var arr)) return new();
            var result = new List<CommercialAxleWeightDto>();
            int axle = 1;
            foreach (var item in arr.EnumerateArray())
                result.Add(new CommercialAxleWeightDto { AxleNumber = axle++, WeightKg = item.GetInt32(), Pass = passKey == "firstPassWeights" ? "first" : "second" });
            return result;
        }
        catch { return new(); }
    }

    private static CommercialWeighingResultDto MapToCommercialResultDto(WeighingTransaction transaction)
    {
        return new CommercialWeighingResultDto
        {
            Id = transaction.Id,
            TicketNumber = transaction.TicketNumber,
            ControlStatus = transaction.ControlStatus,
            WeighingMode = transaction.WeighingMode,
            WeighingScaleType = transaction.WeighingScaleType,

            VehicleId = transaction.VehicleId,
            VehicleRegNumber = transaction.VehicleRegNumber,
            VehicleMake = transaction.SnapshotVehicleMake ?? transaction.Vehicle?.Make,
            VehicleModel = transaction.SnapshotVehicleModel ?? transaction.Vehicle?.Model,
            TrailerRegNo = transaction.TrailerRegNo,

            DriverId = transaction.DriverId,
            DriverName = transaction.SnapshotDriverName ?? (transaction.Driver != null ? $"{transaction.Driver.FullNames} {transaction.Driver.Surname}".Trim() : null),
            TransporterId = transaction.TransporterId,
            TransporterName = transaction.SnapshotTransporterName ?? transaction.Transporter?.Name,
            WeighedByUserName = transaction.WeighedByUser?.FullName,

            StationId = transaction.StationId ?? Guid.Empty,
            StationName = transaction.Station?.Name,

            FirstWeightKg = transaction.FirstWeightKg,
            FirstWeightType = transaction.FirstWeightType,
            FirstWeightAt = transaction.FirstWeightAt,
            SecondWeightKg = transaction.SecondWeightKg,
            SecondWeightType = transaction.SecondWeightType,
            SecondWeightAt = transaction.SecondWeightAt,

            TareWeightKg = transaction.TareWeightKg,
            GrossWeightKg = transaction.GrossWeightKg,
            NetWeightKg = transaction.NetWeightKg,
            TareSource = transaction.TareSource,

            QualityDeductionKg = transaction.QualityDeductionKg,
            AdjustedNetWeightKg = transaction.AdjustedNetWeightKg,

            ConsignmentNo = transaction.ConsignmentNo,
            OrderReference = transaction.OrderReference,
            ExpectedNetWeightKg = transaction.ExpectedNetWeightKg,
            WeightDiscrepancyKg = transaction.WeightDiscrepancyKg,
            SealNumbers = transaction.SealNumbers,
            Remarks = transaction.Remarks,

            OriginId = transaction.OriginId,
            SourceLocation = transaction.SnapshotOriginName ?? transaction.Origin?.Name,
            DestinationId = transaction.DestinationId,
            DestinationLocation = transaction.SnapshotDestinationName ?? transaction.Destination?.Name,
            CargoId = transaction.CargoId,
            CargoType = transaction.SnapshotCargoTypeName ?? transaction.Cargo?.Name,

            ToleranceExceeded = transaction.ToleranceExceeded,
            ToleranceDisplay = transaction.GvwToleranceDisplay,
            ToleranceExceptionApproved = transaction.ToleranceExceptionApproved,
            ToleranceExceptionApprovedBy = transaction.ToleranceExceptionApprovedBy,
            ToleranceExceptionApprovedAt = transaction.ToleranceExceptionApprovedAt,

            FirstPassAxles = ParsePassWeights(transaction.IndustryMetadata, "firstPassWeights"),
            SecondPassAxles = ParsePassWeights(transaction.IndustryMetadata, "secondPassWeights"),

            IndustryMetadata = transaction.IndustryMetadata,
            WeighedAt = transaction.WeighedAt,
            CreatedAt = transaction.CreatedAt,
            VoidedAt = transaction.VoidedAt,
            VoidReason = transaction.VoidReason,
        };
    }

    private static string MergeIndustryMetadata(string? existingJson, object mergeData)
    {
        var existing = string.IsNullOrEmpty(existingJson)
            ? new Dictionary<string, object?>()
            : STJson.JsonSerializer.Deserialize<Dictionary<string, object?>>(existingJson)
              ?? new Dictionary<string, object?>();

        var mergeJson = STJson.JsonSerializer.Serialize(mergeData);
        var mergeDict = STJson.JsonSerializer.Deserialize<Dictionary<string, object?>>(mergeJson)
                        ?? new Dictionary<string, object?>();

        foreach (var kvp in mergeDict)
            existing[kvp.Key] = kvp.Value;

        return STJson.JsonSerializer.Serialize(existing);
    }

    public async Task<CommercialWeighingResultDto> ApproveToleranceExceptionAsync(Guid transactionId, Guid approvedByUserId)
    {
        var transaction = await GetTransactionOrThrowAsync(transactionId);
        EnsureCommercialMode(transaction);

        transaction.ToleranceExceptionApproved = true;
        transaction.ToleranceExceptionApprovedBy = approvedByUserId;
        transaction.ToleranceExceptionApprovedAt = DateTime.UtcNow;
        transaction.ControlStatus = "Complete";

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Tolerance exception approved for transaction {TransactionId} by user {UserId}", transactionId, approvedByUserId);

        return MapToCommercialResultDto(transaction);
    }
}
