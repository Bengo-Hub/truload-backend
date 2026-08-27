using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Data.Repositories.Weighing;
using TruLoad.Backend.DTOs.Shared;
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
using TruLoad.Backend.Services.Interfaces.System;
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
    private readonly ISettingsService _settingsService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CommercialWeighingService> _logger;

    private const int DefaultPendingWeighingThresholdHours = 8;
    private const decimal DefaultTareDriftAnomalyThresholdPercent = 5m;

    public CommercialWeighingService(
        TruLoadDbContext dbContext,
        ITenantContext tenantContext,
        IVehicleRepository vehicleRepository,
        IDocumentNumberService documentNumberService,
        ITreasuryService treasuryService,
        INotificationService notificationService,
        ISettingsService settingsService,
        IConfiguration configuration,
        ILogger<CommercialWeighingService> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _vehicleRepository = vehicleRepository;
        _documentNumberService = documentNumberService;
        _treasuryService = treasuryService;
        _notificationService = notificationService;
        _settingsService = settingsService;
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
            // Default assumption at initiation - no weight has been captured yet. Overwritten to
            // "Manual" by CaptureFirstWeightAsync/CaptureSecondWeightAsync if the operator ends up
            // hand-entering a weight (e.g. scale fault mid-capture); otherwise stays "TruConnect"
            // to reflect the normal live-scale two-pass flow.
            CaptureSource = "TruConnect",
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

        ApplyManualEntryIfRequested(transaction, request.IsManualEntry, request.ManualEntryJustification);

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

        // Tare anomaly detection (Phase 7 MVP) - compare this session's newly measured tare against
        // the vehicle's PRIOR stored tare before RecordTareWeightAsync overwrites it further below.
        // Informational only - does not block completion (unlike ToleranceExceeded).
        await FlagTareAnomalyIfDriftedAsync(transaction, tareWeightKg);

        ApplyManualEntryIfRequested(transaction, request.IsManualEntry, request.ManualEntryJustification);

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
            $"Measured during commercial weighing {transaction.TicketNumber}",
            recordedByUserId: transaction.WeighedByUserId);

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
            // "Preset Tare" per tare-management.md - a supervisor manually entering a tare weight
            // requires a recorded justification for audit purposes.
            EnsureJustificationForPresetTare(request.Justification);

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

        // Tare anomaly detection (Phase 7 MVP) - only meaningful for "preset" (a supervisor
        // asserting a NEW tare value via OverrideTareWeightKg). The "stored"/default-fallback
        // branches above reuse an already-known vehicle tare with nothing new to compare.
        if (tareSource == "preset")
        {
            await FlagTareAnomalyIfDriftedAsync(transaction, tareWeightKg);
        }

        // Record the preset-tare justification for audit purposes. Reuses Remarks (no dedicated
        // column) - same pattern UpdateQualityDeductionAsync uses to append an audit note.
        if (tareSource == "preset" && !string.IsNullOrWhiteSpace(request.Justification))
        {
            transaction.Remarks = string.IsNullOrEmpty(transaction.Remarks)
                ? $"Preset tare justification: {request.Justification}"
                : $"{transaction.Remarks}; Preset tare justification: {request.Justification}";
        }

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
                ?? "https://books.codevertexafrica.com/pay";
            dto.TreasuryPaymentUrl = !string.IsNullOrWhiteSpace(invoice.TreasuryIntentId)
                ? $"{payPortalBase}?intent_id={invoice.TreasuryIntentId}"
                : null;
        }

        return dto;
    }

    public async Task<VehicleTareHistory?> RecordTareWeightAsync(
        Guid vehicleId,
        int tareWeightKg,
        Guid? stationId,
        string source = "measured",
        string? notes = null,
        Guid? recordedByUserId = null,
        bool setAsDefault = true)
    {
        var vehicle = await _dbContext.Vehicles.FindAsync(vehicleId);
        if (vehicle == null)
        {
            _logger.LogWarning("Cannot record tare weight: vehicle {VehicleId} not found", vehicleId);
            return null;
        }

        if (setAsDefault)
        {
            vehicle.LastTareWeightKg = tareWeightKg;
            vehicle.LastTareWeighedAt = DateTime.UtcNow;
            vehicle.UpdatedAt = DateTime.UtcNow;
        }

        string? recordedByName = null;
        if (recordedByUserId.HasValue)
        {
            recordedByName = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == recordedByUserId.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync();
        }

        var history = new VehicleTareHistory
        {
            VehicleId = vehicleId,
            TareWeightKg = tareWeightKg,
            WeighedAt = DateTime.UtcNow,
            StationId = stationId,
            OrganizationId = _tenantContext.OrganizationId,
            Source = source,
            Notes = notes,
            RecordedByUserId = recordedByUserId,
            RecordedByName = recordedByName
        };

        _dbContext.VehicleTareHistory.Add(history);
        // SaveChanges is called by the caller or at end of operation

        _logger.LogInformation(
            "Recorded tare weight for vehicle {VehicleId}: {TareKg}kg ({Source}), setAsDefault={SetAsDefault}",
            vehicleId, tareWeightKg, source, setAsDefault);

        return history;
    }

    /// <summary>
    /// Records a new tare weight history entry for a vehicle (Tare Register "Record Tare" dialog).
    /// Unlike RecordTareWeightAsync (a shared building block used mid-transaction elsewhere and left
    /// to the caller to persist), this method validates the vehicle up front and saves itself, since
    /// it is the sole action of the request it backs.
    /// </summary>
    public async Task<VehicleTareHistoryDto> RecordTareHistoryEntryAsync(RecordTareHistoryRequest request, Guid? recordedByUserId)
    {
        if (request.Source != "measured" && request.Source != "manual")
        {
            throw new InvalidOperationException("Source must be 'measured' or 'manual'.");
        }

        // A manually punched-in tare weight (not read off the scale) is the same "preset" style
        // entry Stage A already gated behind a required justification on UseStoredTareAsync - reuse
        // that rule here via the shared helper, using Notes as the justification text (the frontend's
        // Record Tare dialog has no separate justification field).
        if (request.Source == "manual")
        {
            EnsureJustificationForPresetTare(request.Notes);
        }

        var vehicle = await _dbContext.Vehicles.FindAsync(request.VehicleId);
        if (vehicle == null)
        {
            throw new KeyNotFoundException($"Vehicle {request.VehicleId} not found.");
        }

        // Tare anomaly detection (Phase 7 MVP) - evaluated against the vehicle's PRIOR stored tare
        // BEFORE RecordTareWeightAsync below overwrites it (when SetAsDefault is true, the default).
        // This entry point isn't tied to any live WeighingTransaction (it's the standalone Tare
        // Register "Record Tare" dialog), so the flag is anchored on the VehicleTareHistory row
        // itself rather than a transaction - see Stage C report for this anchoring decision.
        var (isTareAnomaly, tareAnomalyReason) = await EvaluateTareDriftAnomalyAsync(request.VehicleId, request.TareWeightKg);

        var history = await RecordTareWeightAsync(
            request.VehicleId,
            request.TareWeightKg,
            stationId: null,
            source: request.Source,
            notes: request.Notes,
            recordedByUserId: recordedByUserId,
            setAsDefault: request.SetAsDefault);

        if (isTareAnomaly && history != null)
        {
            history.TareAnomalyFlaggedAt = DateTime.UtcNow;
            history.TareAnomalyReason = tareAnomalyReason;
            _logger.LogWarning("Tare anomaly flagged for vehicle tare history {HistoryId} (vehicle {VehicleId}): {Reason}",
                history.Id, request.VehicleId, tareAnomalyReason);
        }

        await _dbContext.SaveChangesAsync();

        // history is guaranteed non-null: the vehicle existence check above already passed.
        return new VehicleTareHistoryDto
        {
            Id = history!.Id,
            VehicleId = history.VehicleId,
            VehicleRegNo = vehicle.RegNo,
            TareWeightKg = history.TareWeightKg,
            WeighedAt = history.WeighedAt,
            StationId = history.StationId,
            StationName = null,
            Source = history.Source,
            Notes = history.Notes,
            RecordedByUserId = history.RecordedByUserId,
            RecordedByName = history.RecordedByName,
            TareAnomalyFlaggedAt = history.TareAnomalyFlaggedAt,
            TareAnomalyReason = history.TareAnomalyReason
        };
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

        // Load the cargo type's quality-deduction rules (moisture target / foreign matter limit),
        // if any. Cargo types with neither configured keep the flat-kg entry path working exactly
        // as before.
        CargoTypes? cargoType = null;
        if (transaction.CargoId.HasValue)
        {
            cargoType = await _dbContext.CargoTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == transaction.CargoId.Value);
        }

        var hasQualityRules = cargoType != null &&
            (cargoType.MoistureTargetPercent.HasValue || cargoType.ForeignMatterLimitPercent.HasValue);
        var hasActualMeasurements = request.ActualMoisturePercent.HasValue || request.ActualForeignMatterPercent.HasValue;

        int qualityDeductionKg;
        decimal? moistureDeductionKg = null;
        decimal? foreignMatterDeductionKg = null;

        if (hasQualityRules && hasActualMeasurements)
        {
            // setup.md documented formulas — authoritative over any caller-supplied flat kg once
            // actual measurements are supplied against a cargo type with quality rules configured.
            var netWeightKg = transaction.NetWeightKg.Value;

            if (request.ActualMoisturePercent.HasValue && cargoType!.MoistureTargetPercent.HasValue &&
                request.ActualMoisturePercent.Value > cargoType.MoistureTargetPercent.Value)
            {
                moistureDeductionKg = netWeightKg * (request.ActualMoisturePercent.Value - cargoType.MoistureTargetPercent.Value) / 100m;
            }

            if (request.ActualForeignMatterPercent.HasValue && cargoType!.ForeignMatterLimitPercent.HasValue &&
                request.ActualForeignMatterPercent.Value > cargoType.ForeignMatterLimitPercent.Value)
            {
                foreignMatterDeductionKg = netWeightKg * request.ActualForeignMatterPercent.Value / 100m;
            }

            var computedDeductionKg = (moistureDeductionKg ?? 0) + (foreignMatterDeductionKg ?? 0);
            qualityDeductionKg = (int)Math.Round(computedDeductionKg, MidpointRounding.AwayFromZero);
        }
        else
        {
            // Fallback: no quality-deduction rules configured for this cargo type, or no actual
            // measurements supplied — use the caller-supplied flat kg value as before.
            qualityDeductionKg = request.QualityDeductionKg;
        }

        transaction.QualityDeductionKg = qualityDeductionKg;
        transaction.AdjustedNetWeightKg = transaction.NetWeightKg.Value - qualityDeductionKg;
        transaction.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            transaction.Remarks = string.IsNullOrEmpty(transaction.Remarks)
                ? $"Quality deduction: {request.Reason}"
                : $"{transaction.Remarks}; Quality deduction: {request.Reason}";
        }

        // Persist the actual measured percentages (and per-type breakdown) into the existing
        // IndustryMetadata JSON column - no new dedicated columns - so a later ticket view/report
        // can show which measurement drove the deduction. Which type(s) applied is derived from
        // these stored values (moisture/foreignMatter when their *DeductionKg is non-null and > 0,
        // "manual" otherwise) rather than stored as a separate enum.
        if (hasActualMeasurements)
        {
            transaction.IndustryMetadata = MergeIndustryMetadata(transaction.IndustryMetadata, new
            {
                qualityDeduction = new
                {
                    actualMoisturePercent = request.ActualMoisturePercent,
                    actualForeignMatterPercent = request.ActualForeignMatterPercent,
                    moistureDeductionKg,
                    foreignMatterDeductionKg
                }
            });
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Quality deduction updated for {TransactionId}: {DeductionKg}kg (moisture={MoistureKg}, fm={FmKg}), adjusted net={AdjustedKg}kg",
            transactionId, qualityDeductionKg, moistureDeductionKg, foreignMatterDeductionKg, transaction.AdjustedNetWeightKg);

        _ = SendQualityDeductionNotificationAsync(transaction, qualityDeductionKg, request.Reason);

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
            Notes = h.Notes,
            RecordedByUserId = h.RecordedByUserId,
            RecordedByName = h.RecordedByName,
            TareAnomalyFlaggedAt = h.TareAnomalyFlaggedAt,
            TareAnomalyReason = h.TareAnomalyReason
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

    public async Task<List<CommercialWeighingResultDto>> GetPendingByPlateAsync(string vehicleRegNo, int? thresholdHours = null)
    {
        var orgId = _tenantContext.OrganizationId;
        var effectiveThresholdHours = thresholdHours ?? await _settingsService.GetSettingValueAsync(
            SettingKeys.CommercialPendingWeighingThresholdHours, DefaultPendingWeighingThresholdHours);
        var cutoff = DateTime.UtcNow.AddHours(-effectiveThresholdHours);
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

            // Invoice numbers are org-wide (no station code in the convention → a per-station
            // sequence could collide across stations on the same day).
            var invoiceNo = await _documentNumberService.GenerateNumberAsync(
                org.Id, null, DocumentTypes.Invoice);

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
    /// Enforces the "Preset Tare" justification rule (tare-management.md): a manually punched-in
    /// tare weight - not read off the scale - requires a recorded justification for audit purposes.
    /// Shared by UseStoredTareAsync (OverrideTareWeightKg) and RecordTareWeightAsync/the tare-history
    /// endpoint (Source == "manual") rather than duplicating the check in each caller.
    /// </summary>
    private static void EnsureJustificationForPresetTare(string? justification)
    {
        if (string.IsNullOrWhiteSpace(justification))
        {
            throw new InvalidOperationException(
                "Justification is required when providing a manual override (preset) tare weight.");
        }
    }

    /// <summary>
    /// Tare anomaly detection (Phase 7 MVP, tare-management.md's "drift vs. stored tare" rule only -
    /// vehicle-class range checks and rapid-change alerts are explicitly out of scope). Compares a
    /// newly measured/asserted tare against the vehicle's CURRENT stored tare (vehicle.LastTareWeightKg)
    /// using the shared TareDriftHelper.ComputeTareDriftPercent - the same calculation the Tare Weight
    /// Audit / Tare Verification reports already use - against the configurable
    /// commercial.tare_drift_anomaly_threshold_percent setting (default 5%).
    /// Returns (false, null) when the vehicle has no prior stored tare to compare against (nothing to
    /// detect drift from yet) or the drift is within threshold. Callers must invoke this BEFORE
    /// RecordTareWeightAsync (which overwrites vehicle.LastTareWeightKg), so the comparison is always
    /// against the prior value, not the one about to be recorded.
    /// </summary>
    private async Task<(bool IsAnomaly, string? Reason)> EvaluateTareDriftAnomalyAsync(Guid vehicleId, int newTareKg)
    {
        var vehicle = await _dbContext.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vehicleId);
        if (vehicle?.LastTareWeightKg == null || vehicle.LastTareWeightKg.Value <= 0)
            return (false, null);

        var thresholdPercent = await _settingsService.GetSettingValueAsync(
            SettingKeys.CommercialTareDriftAnomalyThresholdPercent, DefaultTareDriftAnomalyThresholdPercent);

        var driftPercent = TareDriftHelper.ComputeTareDriftPercent(newTareKg, vehicle.LastTareWeightKg.Value);
        if (driftPercent <= thresholdPercent)
            return (false, null);

        var reason = $"Measured tare differs from stored tare by {driftPercent:F1}% (threshold {thresholdPercent:F0}%)";
        return (true, reason);
    }

    /// <summary>
    /// Evaluates <see cref="EvaluateTareDriftAnomalyAsync"/> and, if anomalous, stamps
    /// TareAnomalyFlaggedAt/TareAnomalyReason on the transaction. Informational only - does not
    /// change ControlStatus or block completion (unlike ToleranceExceeded).
    /// </summary>
    private async Task FlagTareAnomalyIfDriftedAsync(WeighingTransaction transaction, int newTareKg)
    {
        var (isAnomaly, reason) = await EvaluateTareDriftAnomalyAsync(transaction.VehicleId, newTareKg);
        if (!isAnomaly) return;

        transaction.TareAnomalyFlaggedAt = DateTime.UtcNow;
        transaction.TareAnomalyReason = reason;
        _logger.LogWarning("Tare anomaly flagged for transaction {TransactionId}: {Reason}", transaction.Id, reason);
    }

    /// <summary>
    /// Records a manual weight entry on the transaction: validates the required justification,
    /// stamps CaptureSource = "Manual", and appends the justification to Remarks for audit purposes
    /// (docs: "Scale fault during capture"). The <c>manual_weight_override</c> permission itself is
    /// enforced by the controller before this is ever reached. No-op when <paramref name="isManualEntry"/>
    /// is false - CaptureSource keeps whatever it already was ("TruConnect" from initiation, for the
    /// normal live-scale capture path).
    /// </summary>
    private static void ApplyManualEntryIfRequested(WeighingTransaction transaction, bool isManualEntry, string? justification)
    {
        if (!isManualEntry) return;

        if (string.IsNullOrWhiteSpace(justification))
        {
            throw new InvalidOperationException(
                "Justification is required when entering a manual weight (scale/TruConnect unavailable).");
        }

        transaction.CaptureSource = "Manual";
        transaction.Remarks = string.IsNullOrEmpty(transaction.Remarks)
            ? $"Manual weight entry: {justification}"
            : $"{transaction.Remarks}; Manual weight entry: {justification}";
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

    /// <summary>
    /// Reads back the "qualityDeduction" sub-object UpdateQualityDeductionAsync writes into
    /// IndustryMetadata, and derives which deduction type(s) applied from the stored values
    /// (rather than a separate enum column - see UpdateQualityDeductionAsync).
    /// </summary>
    private static (decimal? ActualMoisturePercent, decimal? ActualForeignMatterPercent,
        decimal? MoistureDeductionKg, decimal? ForeignMatterDeductionKg, List<string> AppliedTypes)
        ParseQualityDeductionMetadata(string? industryMetadata, int? qualityDeductionKg)
    {
        decimal? actualMoisturePercent = null, actualForeignMatterPercent = null;
        decimal? moistureDeductionKg = null, foreignMatterDeductionKg = null;

        if (!string.IsNullOrEmpty(industryMetadata))
        {
            try
            {
                var doc = STJson.JsonDocument.Parse(industryMetadata);
                if (doc.RootElement.TryGetProperty("qualityDeduction", out var qd))
                {
                    if (qd.TryGetProperty("actualMoisturePercent", out var amp) && amp.ValueKind == STJson.JsonValueKind.Number)
                        actualMoisturePercent = amp.GetDecimal();
                    if (qd.TryGetProperty("actualForeignMatterPercent", out var afmp) && afmp.ValueKind == STJson.JsonValueKind.Number)
                        actualForeignMatterPercent = afmp.GetDecimal();
                    if (qd.TryGetProperty("moistureDeductionKg", out var mdk) && mdk.ValueKind == STJson.JsonValueKind.Number)
                        moistureDeductionKg = mdk.GetDecimal();
                    if (qd.TryGetProperty("foreignMatterDeductionKg", out var fmdk) && fmdk.ValueKind == STJson.JsonValueKind.Number)
                        foreignMatterDeductionKg = fmdk.GetDecimal();
                }
            }
            catch { /* malformed/legacy metadata - fall through with nulls */ }
        }

        var appliedTypes = new List<string>();
        if (moistureDeductionKg is > 0) appliedTypes.Add("moisture");
        if (foreignMatterDeductionKg is > 0) appliedTypes.Add("foreignMatter");
        if (appliedTypes.Count == 0 && qualityDeductionKg is > 0) appliedTypes.Add("manual");

        return (actualMoisturePercent, actualForeignMatterPercent, moistureDeductionKg, foreignMatterDeductionKg, appliedTypes);
    }

    private static CommercialWeighingResultDto MapToCommercialResultDto(WeighingTransaction transaction)
    {
        var qualityDeduction = ParseQualityDeductionMetadata(transaction.IndustryMetadata, transaction.QualityDeductionKg);

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
            ActualMoisturePercent = qualityDeduction.ActualMoisturePercent,
            ActualForeignMatterPercent = qualityDeduction.ActualForeignMatterPercent,
            MoistureDeductionKg = qualityDeduction.MoistureDeductionKg,
            ForeignMatterDeductionKg = qualityDeduction.ForeignMatterDeductionKg,
            QualityDeductionTypesApplied = qualityDeduction.AppliedTypes,

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

            TareAnomalyFlaggedAt = transaction.TareAnomalyFlaggedAt,
            TareAnomalyReason = transaction.TareAnomalyReason,
            TareAnomalyResolvedByUserId = transaction.TareAnomalyResolvedByUserId,
            TareAnomalyResolvedAt = transaction.TareAnomalyResolvedAt,
            TareAnomalyResolution = transaction.TareAnomalyResolution,

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

    public async Task<CommercialWeighingResultDto> RejectToleranceExceptionAsync(Guid transactionId, string? reason, Guid rejectedByUserId)
    {
        var transaction = await GetTransactionOrThrowAsync(transactionId);
        EnsureCommercialMode(transaction);

        if (!transaction.ToleranceExceeded)
            throw new InvalidOperationException("This transaction did not exceed tolerance; there is no exception to reject.");

        if (transaction.ToleranceExceptionApproved)
            throw new InvalidOperationException("This tolerance exception has already been approved and cannot be rejected.");

        if (transaction.VoidedAt.HasValue)
            throw new InvalidOperationException("This transaction has already been voided.");

        // Reuses the Void state transition/fields — a rejected tolerance exception is, in effect,
        // a supervisor-voided transaction, with the reason recorded via the existing VoidReason
        // column rather than a new one.
        var rejectionReason = string.IsNullOrWhiteSpace(reason)
            ? "Tolerance exception rejected by supervisor."
            : $"Tolerance exception rejected: {reason}";

        transaction.VoidedAt = DateTime.UtcNow;
        transaction.VoidReason = rejectionReason;
        transaction.VoidedByUserId = rejectedByUserId;
        transaction.ControlStatus = "Voided";
        transaction.CaptureStatus = "voided";
        transaction.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Tolerance exception rejected for transaction {TransactionId} by user {UserId}: {Reason}",
            transactionId, rejectedByUserId, rejectionReason);

        return MapToCommercialResultDto(transaction);
    }

    // ============================================================================
    // Tare Anomaly Detection - Supervisor Resolution (Phase 7 MVP)
    // ============================================================================

    public async Task<CommercialWeighingResultDto> ApproveTareAnomalyAsync(Guid transactionId, Guid approvedByUserId)
    {
        var transaction = await GetTransactionOrThrowAsync(transactionId);
        EnsureCommercialMode(transaction);

        if (!transaction.TareAnomalyFlaggedAt.HasValue)
            throw new InvalidOperationException("This transaction has no flagged tare anomaly to approve.");
        if (transaction.TareAnomalyResolvedAt.HasValue)
            throw new InvalidOperationException("This tare anomaly has already been resolved.");

        // TareAnomalyFlaggedAt/Reason are left untouched (permanent audit trail) - mirrors
        // ToleranceExceeded/ToleranceExceptionApproved above, where the original flag persists and a
        // separate resolved-at timestamp/outcome field indicates how it was resolved.
        transaction.TareAnomalyResolvedByUserId = approvedByUserId;
        transaction.TareAnomalyResolvedAt = DateTime.UtcNow;
        transaction.TareAnomalyResolution = "Approved";
        transaction.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Tare anomaly approved for transaction {TransactionId} by user {UserId}", transactionId, approvedByUserId);

        return MapToCommercialResultDto(transaction);
    }

    public async Task<CommercialWeighingResultDto> RejectTareAnomalyAsync(Guid transactionId, string? reason, Guid rejectedByUserId)
    {
        var transaction = await GetTransactionOrThrowAsync(transactionId);
        EnsureCommercialMode(transaction);

        if (!transaction.TareAnomalyFlaggedAt.HasValue)
            throw new InvalidOperationException("This transaction has no flagged tare anomaly to reject.");
        if (transaction.TareAnomalyResolvedAt.HasValue)
            throw new InvalidOperationException("This tare anomaly has already been resolved.");

        // Deliberately does NOT reopen/unwind the transaction's capture state or change the already-
        // recorded TareWeightKg/NetWeightKg (which would also ripple into fees/invoice already
        // generated) - see Stage C report for this data-integrity judgment call. Rejecting simply
        // records that a supervisor reviewed and dismissed the flag; the expectation per
        // tare-management.md is the vehicle's tare gets re-verified on its next visit.
        transaction.TareAnomalyResolvedByUserId = rejectedByUserId;
        transaction.TareAnomalyResolvedAt = DateTime.UtcNow;
        transaction.TareAnomalyResolution = string.IsNullOrWhiteSpace(reason) ? "Rejected" : $"Rejected: {reason}";
        transaction.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Tare anomaly rejected for transaction {TransactionId} by user {UserId}", transactionId, rejectedByUserId);

        return MapToCommercialResultDto(transaction);
    }

    public async Task<CommercialWeighingResultDto> OverrideTareAnomalyAsync(Guid transactionId, OverrideTareAnomalyRequest request, Guid overriddenByUserId)
    {
        var transaction = await GetTransactionOrThrowAsync(transactionId);
        EnsureCommercialMode(transaction);

        if (!transaction.TareAnomalyFlaggedAt.HasValue)
            throw new InvalidOperationException("This transaction has no flagged tare anomaly to override.");
        if (transaction.TareAnomalyResolvedAt.HasValue)
            throw new InvalidOperationException("This tare anomaly has already been resolved.");

        // Reuses the same required-justification rule UseStoredTareAsync enforces for preset tare.
        EnsureJustificationForPresetTare(request.Justification);

        // Corrects the vehicle's stored tare going forward (and logs a VehicleTareHistory audit
        // entry via the shared helper). Deliberately does NOT retroactively rewrite this
        // transaction's already-recorded TareWeightKg/NetWeightKg/fees/invoice - same data-integrity
        // reasoning as Reject above (see Stage C report).
        await RecordTareWeightAsync(
            transaction.VehicleId,
            request.CorrectedTareWeightKg,
            transaction.StationId,
            source: "manual",
            notes: $"Tare anomaly override for ticket {transaction.TicketNumber}: {request.Justification}",
            recordedByUserId: overriddenByUserId,
            setAsDefault: true);

        transaction.TareAnomalyResolvedByUserId = overriddenByUserId;
        transaction.TareAnomalyResolvedAt = DateTime.UtcNow;
        transaction.TareAnomalyResolution = $"Overridden: corrected tare {request.CorrectedTareWeightKg}kg — {request.Justification}";
        transaction.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation(
            "Tare anomaly overridden for transaction {TransactionId} by user {UserId}: corrected tare {TareKg}kg",
            transactionId, overriddenByUserId, request.CorrectedTareWeightKg);

        return MapToCommercialResultDto(transaction);
    }

    public async Task<PagedResponse<TareAnomalyDto>> GetFlaggedTareAnomaliesAsync(Guid? stationId, int pageNumber, int pageSize)
    {
        var orgId = _tenantContext.OrganizationId;

        var txQuery = _dbContext.WeighingTransactions
            .AsNoTracking()
            .Include(t => t.Station)
            .Where(t =>
                t.OrganizationId == orgId &&
                t.WeighingMode == "commercial" &&
                t.VoidedAt == null &&
                t.TareAnomalyFlaggedAt != null &&
                t.TareAnomalyResolvedAt == null);
        if (stationId.HasValue)
            txQuery = txQuery.Where(t => t.StationId == stationId.Value);

        var txAnomalies = await txQuery
            .OrderByDescending(t => t.TareAnomalyFlaggedAt)
            .Take(500)
            .Select(t => new TareAnomalyDto
            {
                AnchorType = "WeighingTransaction",
                Id = t.Id,
                VehicleId = t.VehicleId,
                VehicleRegNo = t.VehicleRegNumber,
                TicketNumber = t.TicketNumber,
                FlaggedAt = t.TareAnomalyFlaggedAt,
                Reason = t.TareAnomalyReason,
                TareWeightKg = t.TareWeightKg,
                StationId = t.StationId,
                StationName = t.Station != null ? t.Station.Name : null
            })
            .ToListAsync();

        var historyQuery = _dbContext.VehicleTareHistory
            .AsNoTracking()
            .Include(h => h.Vehicle)
            .Include(h => h.Station)
            .Where(h =>
                h.OrganizationId == orgId &&
                h.TareAnomalyFlaggedAt != null &&
                h.TareAnomalyResolvedAt == null);
        if (stationId.HasValue)
            historyQuery = historyQuery.Where(h => h.StationId == stationId.Value);

        var historyAnomalies = await historyQuery
            .OrderByDescending(h => h.TareAnomalyFlaggedAt)
            .Take(500)
            .Select(h => new TareAnomalyDto
            {
                AnchorType = "VehicleTareHistory",
                Id = h.Id,
                VehicleId = h.VehicleId,
                VehicleRegNo = h.Vehicle != null ? h.Vehicle.RegNo : null,
                TicketNumber = null,
                FlaggedAt = h.TareAnomalyFlaggedAt,
                Reason = h.TareAnomalyReason,
                TareWeightKg = h.TareWeightKg,
                StationId = h.StationId,
                StationName = h.Station != null ? h.Station.Name : null
            })
            .ToListAsync();

        var combined = txAnomalies.Concat(historyAnomalies)
            .OrderByDescending(a => a.FlaggedAt)
            .ToList();

        var total = combined.Count;
        var page = combined.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        return PagedResponse<TareAnomalyDto>.Create(page, total, pageNumber, pageSize);
    }
}
