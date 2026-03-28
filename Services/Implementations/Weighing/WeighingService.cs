using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Data.Repositories.Weighing;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Services.Interfaces.Weighing;
using TruLoad.Backend.Services.Interfaces.CaseManagement;
using TruLoad.Backend.Services.Interfaces.Yard;
using TruLoad.Backend.Repositories.Weighing.Interfaces;
using TruLoad.Backend.Services.Interfaces.Infrastructure;
using TruLoad.Backend.Data.Repositories.Infrastructure;
using TruLoad.Backend.Models.Infrastructure;
using TruLoad.Backend.DTOs.Weighing;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.DTOs.Yard;
using TruLoad.Backend.Repositories.Infrastructure;
using TruLoad.Backend.Models.Prosecution;
using TruLoad.Backend.Models.Financial;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Services.Interfaces.System;
using TruLoad.Backend.Services.Interfaces.Shared;
using TruLoad.Backend.Services.Interfaces.Financial;
using TruLoad.Backend.Services.Interfaces.Subscription;

namespace TruLoad.Backend.Services.Implementations.Weighing;

public class WeighingService : IWeighingService
{
    private readonly IWeighingRepository _weighingRepository;
    private readonly IAxleConfigurationRepository _axleConfigurationRepository;
    private readonly IPermitRepository _permitRepository;
    private readonly IProhibitionRepository _prohibitionRepository;
    private readonly IToleranceRepository _toleranceRepository;
    private readonly IAxleFeeScheduleRepository _feeScheduleRepository;
    private readonly IPdfService _pdfService;
    private readonly IBlobStorageService _storageService;
    private readonly IDocumentRepository _documentRepository;
    private readonly IAxleGroupAggregationService _axleGroupAggregationService;
    private readonly IScaleTestRepository _scaleTestRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ICaseRegisterService _caseRegisterService;
    private readonly IYardService _yardService;
    private readonly IVehicleTagService _vehicleTagService;
    private readonly TruLoadDbContext _dbContext;
    private readonly ISettingsService _settingsService;
    private readonly IDocumentNumberService _documentNumberService;
    private readonly INotificationService _notificationService;
    private readonly ITreasuryService _treasuryService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<WeighingService> _logger;

    public WeighingService(
        IWeighingRepository weighingRepository,
        IAxleConfigurationRepository axleConfigurationRepository,
        IPermitRepository permitRepository,
        IProhibitionRepository prohibitionRepository,
        IToleranceRepository toleranceRepository,
        IAxleFeeScheduleRepository feeScheduleRepository,
        IPdfService pdfService,
        IBlobStorageService storageService,
        IDocumentRepository documentRepository,
        IAxleGroupAggregationService axleGroupAggregationService,
        IScaleTestRepository scaleTestRepository,
        IVehicleRepository vehicleRepository,
        ICaseRegisterService caseRegisterService,
        IYardService yardService,
        IVehicleTagService vehicleTagService,
        TruLoadDbContext dbContext,
        ISettingsService settingsService,
        IDocumentNumberService documentNumberService,
        INotificationService notificationService,
        ITreasuryService treasuryService,
        ISubscriptionService subscriptionService,
        ILogger<WeighingService> logger)
    {
        _weighingRepository = weighingRepository;
        _axleConfigurationRepository = axleConfigurationRepository;
        _permitRepository = permitRepository;
        _prohibitionRepository = prohibitionRepository;
        _toleranceRepository = toleranceRepository;
        _feeScheduleRepository = feeScheduleRepository;
        _pdfService = pdfService;
        _storageService = storageService;
        _documentRepository = documentRepository;
        _axleGroupAggregationService = axleGroupAggregationService;
        _scaleTestRepository = scaleTestRepository;
        _vehicleRepository = vehicleRepository;
        _caseRegisterService = caseRegisterService;
        _yardService = yardService;
        _vehicleTagService = vehicleTagService;
        _dbContext = dbContext;
        _settingsService = settingsService;
        _documentNumberService = documentNumberService;
        _notificationService = notificationService;
        _treasuryService = treasuryService;
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    /// <summary>
    /// Initiates a weighing transaction with scale test validation.
    /// Generates ticket number via DocumentNumberService using configured conventions.
    /// Per FRD: Scale test must be completed once daily per station/bound before weighing operations.
    /// All required FK fields (vehicleId) must be provided to avoid FK constraint violations on save.
    /// </summary>
    public async Task<WeighingTransaction> InitiateWeighingAsync(
        Guid stationId,
        Guid userId,
        Guid vehicleId,
        string vehicleRegNo,
        string? bound = null,
        Guid? scaleTestId = null,
        Guid? driverId = null,
        Guid? transporterId = null,
        string weighingType = "static",
        Guid? actId = null,
        Guid? roadId = null,
        Guid? subcountyId = null,
        string? locationTown = null,
        string? locationSubcounty = null,
        string? locationCounty = null,
        decimal? locationLat = null,
        decimal? locationLng = null,
        Guid? originId = null,
        Guid? destinationId = null,
        Guid? cargoId = null)
    {
        // Validate scale test requirement
        var isScaleTestRequired = await _settingsService.GetSettingValueAsync(SettingKeys.WeighingScaleTestRequired, false);
        var hasValidScaleTest = await _scaleTestRepository.HasPassedDailyCalibrationalAsync(stationId, bound);

        if (isScaleTestRequired && !hasValidScaleTest)
        {
            _logger.LogWarning(
                "Weighing initiation blocked - no valid scale test for Station {StationId}, Bound {Bound}",
                stationId, bound);
            throw new InvalidOperationException(
                $"Scale test required before weighing. Please complete a passing scale test for this station{(string.IsNullOrEmpty(bound) ? "" : $" (Bound {bound})")} today.");
        }

        // Get today's passing scale test if not provided
        Guid? validScaleTestId = scaleTestId;
        if (!validScaleTestId.HasValue)
        {
            var todaysTest = await _scaleTestRepository.GetTodaysPassingTestAsync(stationId, bound);
            validScaleTestId = todaysTest?.Id;
        }

        // When request did not send driver/transporter/location, copy from last weighing for this vehicle
        var effectiveDriverId = driverId;
        var effectiveTransporterId = transporterId;
        var effectiveRoadId = roadId;
        var effectiveSubcountyId = subcountyId;
        var effectiveLocationTown = locationTown;
        var effectiveLocationSubcounty = locationSubcounty;
        var effectiveLocationCounty = locationCounty;
        var effectiveLocationLat = locationLat;
        var effectiveLocationLng = locationLng;
        var effectiveOriginId = originId;
        var effectiveDestinationId = destinationId;
        var effectiveCargoId = cargoId;

        if (!driverId.HasValue && !transporterId.HasValue && !roadId.HasValue && !subcountyId.HasValue)
        {
            var lastWeighing = await _weighingRepository.GetLastWeighingByVehicleAsync(vehicleId);
            if (lastWeighing != null)
            {
                effectiveDriverId = lastWeighing.DriverId;
                effectiveTransporterId = lastWeighing.TransporterId;
                effectiveRoadId = lastWeighing.RoadId;
                effectiveSubcountyId = lastWeighing.SubcountyId;
                effectiveLocationTown = lastWeighing.LocationTown ?? locationTown;
                effectiveLocationSubcounty = lastWeighing.LocationSubcounty ?? locationSubcounty;
                effectiveLocationCounty = lastWeighing.LocationCounty ?? locationCounty;
                effectiveLocationLat = lastWeighing.LocationLat ?? locationLat;
                effectiveLocationLng = lastWeighing.LocationLng ?? locationLng;
                effectiveOriginId = lastWeighing.OriginId ?? originId;
                effectiveDestinationId = lastWeighing.DestinationId ?? destinationId;
                effectiveCargoId = lastWeighing.CargoId ?? cargoId;
                _logger.LogDebug("Copied driver/transporter/location from last weighing {LastId} for vehicle {VehicleId}", lastWeighing.Id, vehicleId);
            }
        }
        else
        {
            if (!originId.HasValue || !destinationId.HasValue || !cargoId.HasValue)
            {
                var lastWeighing = await _weighingRepository.GetLastWeighingByVehicleAsync(vehicleId);
                if (lastWeighing != null)
                {
                    if (!effectiveOriginId.HasValue) effectiveOriginId = lastWeighing.OriginId;
                    if (!effectiveDestinationId.HasValue) effectiveDestinationId = lastWeighing.DestinationId;
                    if (!effectiveCargoId.HasValue) effectiveCargoId = lastWeighing.CargoId;
                }
            }
        }

        // Generate ticket number via DocumentNumberService
        var orgId = await _dbContext.Stations
            .Where(s => s.Id == stationId)
            .Select(s => s.OrganizationId)
            .FirstOrDefaultAsync();

        var ticketNumber = await _documentNumberService.GenerateNumberAsync(
            orgId, stationId, Models.System.DocumentTypes.WeightTicket,
            vehicleRegNo, bound);

        var transaction = new WeighingTransaction
        {
            TicketNumber = ticketNumber,
            StationId = stationId,
            WeighedByUserId = userId,
            VehicleId = vehicleId,
            VehicleRegNumber = vehicleRegNo,
            DriverId = effectiveDriverId,
            TransporterId = effectiveTransporterId,
            WeighingType = weighingType,
            Bound = bound,
            ScaleTestId = validScaleTestId,
            ActId = actId,
            RoadId = effectiveRoadId,
            SubcountyId = effectiveSubcountyId,
            LocationTown = effectiveLocationTown,
            LocationSubcounty = effectiveLocationSubcounty,
            LocationCounty = effectiveLocationCounty,
            LocationLat = effectiveLocationLat,
            LocationLng = effectiveLocationLng,
            OriginId = effectiveOriginId,
            DestinationId = effectiveDestinationId,
            CargoId = effectiveCargoId,
            ControlStatus = "Pending",
            CaptureStatus = "pending",
            CaptureSource = "frontend",
            WeighedAt = DateTime.UtcNow
        };

        _logger.LogInformation(
            "Weighing transaction initiated: {TicketNumber}, Station: {StationId}, Vehicle: {VehicleId}, Bound: {Bound}, ScaleTest: {ScaleTestId}",
            ticketNumber, stationId, vehicleId, bound, validScaleTestId);

        return await _weighingRepository.CreateTransactionAsync(transaction);
    }

    public async Task<WeighingTransaction> InitiateReweighAsync(Guid originalTransactionId, string? ticketNumber, Guid userId,
        string? reliefTruckRegNumber = null, int? reliefTruckEmptyWeightKg = null)
    {
        var original = await _weighingRepository.GetTransactionByIdAsync(originalTransactionId);
        if (original == null) throw new KeyNotFoundException($"Original weighing transaction {originalTransactionId} not found");

        var maxReweighCycles = await _settingsService.GetSettingValueAsync(SettingKeys.WeighingMaxReweighCycles, 8);
        if (original.ReweighCycleNo >= maxReweighCycles)
        {
            throw new InvalidOperationException($"Maximum reweigh cycles ({maxReweighCycles}) reached for this transaction.");
        }

        // Generate reweigh ticket number from document sequence when not provided
        var effectiveTicketNumber = !string.IsNullOrWhiteSpace(ticketNumber)
            ? ticketNumber
            : await _documentNumberService.GenerateNumberAsync(
                original.OrganizationId,
                original.StationId,
                Models.System.DocumentTypes.ReweighTicket,
                original.VehicleRegNumber,
                original.Bound);

        var transaction = new WeighingTransaction
        {
            TicketNumber = effectiveTicketNumber,
            StationId = original.StationId,
            VehicleId = original.VehicleId,
            VehicleRegNumber = original.VehicleRegNumber,
            DriverId = original.DriverId,
            TransporterId = original.TransporterId,
            WeighedByUserId = userId,
            OriginalWeighingId = original.OriginalWeighingId ?? original.Id,
            ReweighCycleNo = original.ReweighCycleNo + 1,
            ControlStatus = "Pending",
            WeighedAt = DateTime.UtcNow
        };

        var created = await _weighingRepository.CreateTransactionAsync(transaction);

        // Update LoadCorrectionMemo with relief truck info and link reweigh transaction
        var rootWeighingId = original.OriginalWeighingId ?? original.Id;
        var memo = await _dbContext.LoadCorrectionMemos
            .FirstOrDefaultAsync(m => m.WeighingId == rootWeighingId);
        if (memo != null)
        {
            memo.ReweighWeighingId = created.Id;
            memo.ReweighScheduledAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(reliefTruckRegNumber))
            {
                memo.ReliefTruckRegNumber = reliefTruckRegNumber;
                memo.ReliefTruckEmptyWeightKg = reliefTruckEmptyWeightKg;
                memo.RedistributionType = "offload";
            }
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Updated LoadCorrectionMemo {MemoNo} with reweigh {ReweighId} and relief truck {Truck}",
                memo.MemoNo, created.Id, reliefTruckRegNumber ?? "N/A");
        }

        return created;
    }

    public async Task<WeighingTransaction> CaptureWeightsAsync(Guid transactionId, List<WeighingAxle> axles)
    {
        if (axles == null || axles.Count == 0)
        {
            throw new ArgumentException("At least one axle weight is required", nameof(axles));
        }

        var transaction = await _weighingRepository.GetTransactionByIdAsync(transactionId);
        if (transaction == null) throw new KeyNotFoundException($"Weighing transaction {transactionId} not found");

        // Resolve axle configuration and grouping data if missing
        var needsDataResolution = axles.Any(a => a.AxleConfigurationId == Guid.Empty || string.IsNullOrEmpty(a.AxleGrouping));
        if (needsDataResolution)
        {
            // Group axles by configuration to minimize DB calls
            var configIds = axles.Where(a => a.AxleConfigurationId != Guid.Empty)
                .Select(a => a.AxleConfigurationId)
                .Distinct()
                .ToList();

            var configs = new Dictionary<Guid, AxleConfiguration>();
            foreach (var cid in configIds)
            {
                var cfg = await _axleConfigurationRepository.GetByIdAsync(cid, includeWeightReferences: true);
                if (cfg != null) configs[cid] = cfg;
            }

            // Fallback for axles without any configId
            AxleConfiguration? standardConfig = null;
            if (axles.Any(a => a.AxleConfigurationId == Guid.Empty))
            {
                standardConfig = await _axleConfigurationRepository.GetStandardByAxleCountAsync(axles.Count);
            }

            foreach (var axle in axles)
            {
                var cid = axle.AxleConfigurationId == Guid.Empty ? standardConfig?.Id : axle.AxleConfigurationId;
                if (!cid.HasValue || cid == Guid.Empty) continue;

                if (!configs.TryGetValue(cid.Value, out var config) && cid == standardConfig?.Id)
                {
                    config = standardConfig;
                }
                else if (!configs.TryGetValue(cid.Value, out config))
                {
                    config = await _axleConfigurationRepository.GetByIdAsync(cid.Value, includeWeightReferences: true);
                    if (config != null) configs[cid.Value] = config;
                }

                if (config != null)
                {
                    axle.AxleConfigurationId = config.Id;
                    var weightRef = config.AxleWeightReferences?
                        .FirstOrDefault(r => r.AxlePosition == axle.AxleNumber);
                    if (weightRef != null)
                    {
                        axle.AxleWeightReferenceId = weightRef.Id;
                        axle.PermissibleWeightKg = weightRef.AxleLegalWeightKg;
                        axle.AxleGrouping = weightRef.AxleGrouping;
                        axle.AxleGroupId = weightRef.AxleGroupId;
                        axle.TyreTypeId = weightRef.TyreTypeId;
                    }
                }
            }
        }

        // Delete existing axles before saving new ones (handles re-capture scenario)
        await _weighingRepository.DeleteAxlesByTransactionIdAsync(transactionId);

        transaction.WeighingAxles = axles;
        transaction.GvwMeasuredKg = axles.Sum(a => a.MeasuredWeightKg);

        // Update CaptureStatus to "captured" when weights are submitted
        // Handles both "pending" (frontend-initiated) and "auto" (middleware autoweigh) flows
        if (transaction.CaptureStatus == "auto" || transaction.CaptureStatus == "pending")
        {
            var previousStatus = transaction.CaptureStatus;
            transaction.CaptureStatus = "captured";
            if (previousStatus == "auto")
            {
                transaction.CaptureSource = "frontend";
            }
            _logger.LogInformation(
                "Updated CaptureStatus from '{PreviousStatus}' to 'captured' for transaction {TransactionId}",
                previousStatus, transactionId);
        }

        // Save axles first so compliance calculation can find them
        await _weighingRepository.SaveTransactionWithNewAxlesAsync(transaction);

        // Calculate compliance (re-fetches from DB with saved axles)
        transaction = await CalculateComplianceAsync(transactionId);
        return transaction;
    }

    public async Task<WeighingTransaction> CalculateComplianceAsync(Guid transactionId)
    {
        // 1. Fetch transaction with axles
        var transaction = await _weighingRepository.GetTransactionByIdAsync(transactionId);
        if (transaction == null) throw new KeyNotFoundException($"Weighing transaction {transactionId} not found");

        if (transaction.WeighingAxles == null || !transaction.WeighingAxles.Any())
        {
            return transaction; // Nothing to calculate
        }

        // Resolve org to determine tenant mode (commercial vs enforcement)
        Organization? org = null;
        if (transaction.StationId.HasValue)
        {
            var stationOrgId = await _dbContext.Stations
                .Where(s => s.Id == transaction.StationId.Value)
                .Select(s => (Guid?)s.OrganizationId)
                .FirstOrDefaultAsync();
            if (stationOrgId.HasValue)
            {
                org = await _dbContext.Organizations.AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == stationOrgId.Value);
            }
        }
        var isCommercialMode = org?.TenantType == "CommercialWeighing";

        // 2. Identify the Axle Configuration
        var firstAxle = transaction.WeighingAxles.OrderBy(a => a.AxleNumber).First();
        var configId = firstAxle.AxleConfigurationId;

        // 3. Fetch Configuration Details (with fallback by axle count)
        AxleConfiguration? axleConfig = null;
        if (configId != Guid.Empty)
        {
            axleConfig = await _axleConfigurationRepository.GetByIdAsync(configId, includeWeightReferences: true);
        }

        // Fallback: look up standard config by axle count if no explicit config
        if (axleConfig == null)
        {
            var axleCount = transaction.WeighingAxles.Count;
            axleConfig = await _axleConfigurationRepository.GetStandardByAxleCountAsync(axleCount);

            // Update axles with resolved config ID for future lookups
            if (axleConfig != null)
            {
                foreach (var axle in transaction.WeighingAxles)
                {
                    axle.AxleConfigurationId = axleConfig.Id;
                }
            }
        }

        if (axleConfig == null)
        {
             transaction.ControlStatus = "Configuration Error";
             _logger.LogWarning("No axle configuration found for transaction {TransactionId} with {AxleCount} axles",
                 transactionId, transaction.WeighingAxles.Count);
             await _weighingRepository.UpdateTransactionAsync(transaction);
             return transaction;
        }

        // 4. Fetch Active Permit extensions
        var permit = await _permitRepository.GetActivePermitForVehicleAsync(transaction.VehicleId);
        transaction.HasPermit = permit != null;
        int axleExtension = permit?.AxleExtensionKg ?? 0;
        int gvwExtension = permit?.GvwExtensionKg ?? 0;

        // 5. Calculate Axle Compliance
        foreach (var axle in transaction.WeighingAxles)
        {
            var weightRef = axleConfig.AxleWeightReferences
                .FirstOrDefault(r => r.AxlePosition == axle.AxleNumber);

            if (weightRef != null)
            {
                // Apply permit extension if applicable
                axle.PermissibleWeightKg = weightRef.AxleLegalWeightKg + axleExtension;
                axle.AxleWeightReferenceId = weightRef.Id;
            }
        }

        // 6. Calculate GVW Compliance
        transaction.GvwMeasuredKg = transaction.WeighingAxles.Sum(a => a.MeasuredWeightKg);
        transaction.GvwPermissibleKg = axleConfig.GvwPermissibleKg + gvwExtension;
        
        if (transaction.GvwPermissibleKg == 0)
        {
             transaction.GvwPermissibleKg = transaction.WeighingAxles.Sum(a => a.PermissibleWeightKg);
        }

        var gvwOverload = transaction.GvwMeasuredKg - transaction.GvwPermissibleKg;
        transaction.OverloadKg = Math.Max(0, gvwOverload);

        // 7. Resolve applicable Act from settings (default: TRAFFIC_ACT)
        string legalFramework;
        if (transaction.ActId.HasValue)
        {
            var existingAct = await _dbContext.ActDefinitions
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == transaction.ActId.Value);
            legalFramework = existingAct?.Code ?? "TRAFFIC_ACT";
        }
        else
        {
            var defaultActSetting = await _dbContext.ApplicationSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SettingKey == "compliance.default_act_code");
            legalFramework = defaultActSetting?.SettingValue ?? "TRAFFIC_ACT";
            var resolvedAct = await _dbContext.ActDefinitions
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Code == legalFramework);
            if (resolvedAct != null)
                transaction.ActId = resolvedAct.Id;
        }

        // CRITICAL: Persist GvwPermissibleKg, per-axle PermissibleWeightKg, and ActId
        // to DB BEFORE delegating to AxleGroupAggregationService which re-fetches
        // the transaction via AsNoTracking(). Without this save, the aggregation
        // service reads stale data (GvwPermissibleKg=0) causing wrong overload/fees.
        await _weighingRepository.UpdateTransactionAsync(transaction);

        // 8. Delegate all compliance and tolerance calculation to the unified service
        var complianceResult = await _axleGroupAggregationService.CalculateComplianceAsync(transaction.Id);

        // 9. Map results back to transaction for persistence
        transaction.IsCompliant = complianceResult.IsCompliant;
        transaction.ControlStatus = complianceResult.OverallStatus;
        transaction.OverloadKg = (int)complianceResult.GvwOverloadKg;
        transaction.TotalFeeUsd = complianceResult.TotalFeeUsd;
        transaction.TotalFeeKes = complianceResult.TotalFeeKes;
        transaction.ViolationReason = string.Join("; ", complianceResult.ViolationReasons);
        
        transaction.GvwToleranceKg = complianceResult.GvwToleranceKg;
        transaction.GvwToleranceDisplay = complianceResult.GvwToleranceDisplay;
        transaction.AxleToleranceDisplay = complianceResult.AxleToleranceDisplay;
        transaction.OperationalAllowanceUsed = complianceResult.OperationalAllowanceUsed;
        transaction.ToleranceApplied = complianceResult.GvwToleranceKg > 0 || complianceResult.GroupResults.Any(g => g.ToleranceKg > 0);

        // Populate AxleType on each axle from group results (for ticket display)
        foreach (var groupResult in complianceResult.GroupResults)
        {
            foreach (var axleDetail in groupResult.Axles)
            {
                var axle = transaction.WeighingAxles.FirstOrDefault(a => a.AxleNumber == axleDetail.AxleNumber);
                if (axle != null && string.IsNullOrEmpty(axle.AxleType))
                {
                    axle.AxleType = groupResult.AxleType;
                    axle.AxleGrouping = groupResult.GroupLabel;
                }
            }
        }

        // 10. Handle Prosecution/Cases and Yard Entry if not compliant (Enforcement Mode)
        // Per FRD B.1: Auto-trigger Case Register + Yard Entry + Prohibition on overload
        if (!isCommercialMode && !transaction.IsCompliant && transaction.ControlStatus == "Overloaded")
        {
            try
            {
                // Create Case Register if it doesn't exist
                var existingCase = await _caseRegisterService.GetByWeighingIdAsync(transaction.Id);
                if (existingCase == null)
                {
                    var caseDto = await _caseRegisterService.CreateCaseFromWeighingAsync(
                        transaction.Id, transaction.WeighedByUserId);
                    _logger.LogInformation(
                        "Auto-created case register {CaseNo} for overloaded weighing {TransactionId}",
                        caseDto.CaseNo, transaction.Id);

                    // Auto-create Yard Entry when vehicle sent to yard
                    var existingYard = await _yardService.GetByWeighingIdAsync(transaction.Id);
                    if (existingYard == null)
                    {
                        await _yardService.CreateAsync(
                            new CreateYardEntryRequest
                            {
                                WeighingId = transaction.Id,
                                StationId = transaction.StationId ?? Guid.Empty,
                                Reason = "overload"
                            },
                            transaction.WeighedByUserId);
                    }

                    // Create Prohibition Order if it doesn't exist
                    var existingProhibition = await _prohibitionRepository.GetByWeighingIdAsync(transaction.Id);
                    if (existingProhibition == null)
                    {
                        var prohibitionNo = await _prohibitionRepository.GenerateProhibitionNumberAsync();
                        var prohibitionOrder = new ProhibitionOrder
                        {
                            WeighingId = transaction.Id,
                            ProhibitionNo = prohibitionNo,
                            IssuedById = transaction.WeighedByUserId,
                            Status = "Open",
                            Reason = transaction.ViolationReason,
                            IssuedAt = DateTime.UtcNow
                        };
                        await _prohibitionRepository.CreateAsync(prohibitionOrder);
                    }

                    // NOTIFY: Overload detected
                    await _notificationService.SendInternalNotificationAsync(
                        transaction.WeighedByUserId,
                        "Overload Detected",
                        $"Vehicle {transaction.VehicleRegNumber} is overloaded. Case {caseDto.CaseNo} created.",
                        "error",
                        $"/weighing/transactions/{transaction.Id}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-create case/yard/prohibition for weighing {TransactionId}", transaction.Id);
            }
        }

        // 9. MANUAL TAG ENFORCEMENT: Check for open manual KeNHA tags on weight-compliant vehicles
        // Per FRD A.2: Manual tags from KeNHA can bar vehicle from exiting even if weight-compliant
        // Commercial tenants have no KeNHA tag enforcement
        if (!isCommercialMode && transaction.ControlStatus is "Compliant" or "Warning")
        {
            try
            {
                var openTags = await _vehicleTagService.CheckVehicleTagsAsync(transaction.VehicleRegNumber);
                var manualTags = openTags.Where(t => t.TagType.Equals("manual", StringComparison.OrdinalIgnoreCase)).ToList();

                if (manualTags.Count > 0)
                {
                    var tagReasons = string.Join("; ", manualTags.Select(t => $"[{t.TagCategoryName}] {t.Reason}"));
                    _logger.LogInformation(
                        "Vehicle {VehicleReg} has {TagCount} open manual tag(s): {TagReasons}. Overriding to TagHold.",
                        transaction.VehicleRegNumber, manualTags.Count, tagReasons);

                    transaction.ControlStatus = "TagHold";
                    transaction.IsSentToYard = true;
                    transaction.ViolationReason = string.IsNullOrEmpty(transaction.ViolationReason)
                        ? $"Manual KeNHA tag hold: {tagReasons}"
                        : $"{transaction.ViolationReason}; Manual KeNHA tag hold: {tagReasons}";

                    // NOTIFY: Tag Hold
                    await _notificationService.SendInternalNotificationAsync(
                        transaction.WeighedByUserId,
                        "Vehicle Tag Hold Detected",
                        $"Vehicle {transaction.VehicleRegNumber} has open manual tags: {tagReasons}. Sent to yard.",
                        "warning",
                        $"/weighing/transactions/{transaction.Id}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to check vehicle tags for {VehicleReg}. Proceeding without tag enforcement.",
                    transaction.VehicleRegNumber);
                // Don't block weighing if tag check fails
            }
        }

        // 10. Persist Updates
        await _weighingRepository.UpdateTransactionAsync(transaction);

        // 10a. COMMERCIAL MODE: Create flat-fee invoice (skip all enforcement auto-triggers below)
        if (isCommercialMode && org != null)
        {
            await CreateCommercialWeighingInvoiceAsync(transaction, org);
            return transaction;
        }

        // 11. AUTO-TRIGGER: Create Case Register + Special Release for within-tolerance overload
        if (transaction.ControlStatus == "Warning" && !transaction.IsCompliant)
        {
            try
            {
                var existingCase = await _caseRegisterService.GetByWeighingIdAsync(transactionId);
                if (existingCase == null)
                {
                    var caseDto = await _caseRegisterService.CreateCaseFromWeighingAsync(
                        transactionId, transaction.WeighedByUserId);
                    _logger.LogInformation(
                        "Auto-created case register {CaseNo} for within-tolerance weighing {TransactionId}",
                        caseDto.CaseNo, transactionId);

                    // Auto-create special release with TOLERANCE type
                    var toleranceReleaseType = await _dbContext.ReleaseTypes
                        .FirstOrDefaultAsync(rt => rt.Code == "TOLERANCE");

                    if (toleranceReleaseType != null)
                    {
                        var opTolerance = await _toleranceRepository.GetByCodeAsync("OPERATIONAL_TOLERANCE");
                        int opToleranceKg = opTolerance?.ToleranceKg ?? 200;

                        var year = DateTime.UtcNow.Year;
                        var certCount = await _dbContext.SpecialReleases
                            .CountAsync(sr => sr.CreatedAt.Year == year);
                        var specialRelease = new Models.CaseManagement.SpecialRelease
                        {
                            CaseRegisterId = caseDto.Id,
                            CertificateNo = $"SR-TOL-{year}-{(certCount + 1):D6}",
                            ReleaseTypeId = toleranceReleaseType.Id,
                            OverloadKg = transaction.OverloadKg,
                            RedistributionAllowed = false,
                            ReweighRequired = false,
                            Reason = $"GVW overload of {transaction.OverloadKg}kg is within operational tolerance ({opToleranceKg}kg). Auto-released with warning.",
                            AuthorizedById = transaction.WeighedByUserId,
                            CreatedById = transaction.WeighedByUserId,
                            IssuedAt = DateTime.UtcNow,
                            IsApproved = true,
                            ApprovedById = transaction.WeighedByUserId,
                            ApprovedAt = DateTime.UtcNow,
                            CreatedAt = DateTime.UtcNow
                        };
                        _dbContext.SpecialReleases.Add(specialRelease);

                        // Auto-close case with SPECIAL_RELEASE disposition
                        // Note: We update the case directly via DbContext to avoid tracking conflicts,
                        // since CreateCaseAsync already has the entity tracked in this scope.
                        var specialReleaseDisposition = await _dbContext.DispositionTypes
                            .FirstOrDefaultAsync(dt => dt.Code == "SPECIAL_RELEASE");
                        var closedStatus = await _dbContext.CaseStatuses
                            .FirstOrDefaultAsync(cs => cs.Code == "CLOSED");
                        if (specialReleaseDisposition != null && closedStatus != null)
                        {
                            var trackedCase = await _dbContext.CaseRegisters.FindAsync(caseDto.Id);
                            if (trackedCase != null)
                            {
                                trackedCase.CaseStatusId = closedStatus.Id;
                                trackedCase.DispositionTypeId = specialReleaseDisposition.Id;
                                trackedCase.ClosingReason = $"Auto-closed: GVW overload within tolerance ({transaction.OverloadKg}kg <= {opToleranceKg}kg). Special release certificate {specialRelease.CertificateNo} issued.";
                                trackedCase.ClosedAt = DateTime.UtcNow;
                                trackedCase.ClosedById = transaction.WeighedByUserId;
                                trackedCase.UpdatedAt = DateTime.UtcNow;
                            }
                        }

                        await _dbContext.SaveChangesAsync();
                        _logger.LogInformation(
                            "Auto-created special release {CertNo} for within-tolerance case {CaseNo}",
                            specialRelease.CertificateNo, caseDto.CaseNo);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed auto-special-release for within-tolerance weighing {TransactionId}. Manual intervention required.",
                    transactionId);
            }
        }

        // 11. AUTO-TRIGGER: Create Case Register + Yard Entry + Load Correction Memo on overload (per FRD B.1)
        if (transaction.ControlStatus == "Overloaded" && !transaction.IsCompliant)
        {
            try
            {
                var existingCase = await _caseRegisterService.GetByWeighingIdAsync(transactionId);
                if (existingCase == null)
                {
                    var caseDto = await _caseRegisterService.CreateCaseFromWeighingAsync(
                        transactionId, transaction.WeighedByUserId);
                    _logger.LogInformation(
                        "Auto-created case register {CaseNo} for overloaded weighing {TransactionId}",
                        caseDto.CaseNo, transactionId);

                    // Auto-create Yard Entry when vehicle sent to yard
                    if (transaction.IsSentToYard)
                    {
                        var existingYard = await _yardService.GetByWeighingIdAsync(transactionId);
                        if (existingYard == null)
                        {
                            var yardDto = await _yardService.CreateAsync(
                                new CreateYardEntryRequest
                                {
                                    WeighingId = transactionId,
                                    StationId = transaction.StationId ?? Guid.Empty,
                                    Reason = "gvw_overload"
                                },
                                transaction.WeighedByUserId);
                            _logger.LogInformation(
                                "Auto-created yard entry for vehicle {VehicleReg} at station {StationId}",
                                transaction.VehicleRegNumber, transaction.StationId);
                        }
                    }

                    // NOTIFY: Overload detected
                    await _notificationService.SendInternalNotificationAsync(
                        transaction.WeighedByUserId,
                        "Overload Detected",
                        $"Vehicle {transaction.VehicleRegNumber} is overloaded by {transaction.OverloadKg:N0}kg. Case {caseDto.CaseNo} created.",
                        "error",
                        $"/weighing/transactions/{transaction.Id}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to auto-create case/yard for weighing {TransactionId}. Manual intervention required.",
                    transactionId);
                // Don't throw — weighing result is still valid
            }
        }

        // 13. AUTO-TRIGGER: Create Case Register + Yard Entry for manual KeNHA tag hold (per FRD A.2)
        if (transaction.ControlStatus == "TagHold")
        {
            try
            {
                var existingCase = await _caseRegisterService.GetByWeighingIdAsync(transactionId);
                if (existingCase == null)
                {
                    // Get TAG violation type for case creation
                    var tagViolationType = await _dbContext.ViolationTypes
                        .FirstOrDefaultAsync(vt => vt.Code == "TAG");

                    if (tagViolationType != null)
                    {
                        var caseRequest = new DTOs.CaseManagement.CreateCaseRegisterRequest
                        {
                            WeighingId = transactionId,
                            VehicleId = transaction.VehicleId,
                            DriverId = transaction.DriverId,
                            ViolationTypeId = tagViolationType.Id,
                            ViolationDetails = transaction.ViolationReason ?? "Vehicle held due to manual KeNHA tag"
                        };
                        var caseDto = await _caseRegisterService.CreateCaseAsync(caseRequest, transaction.WeighedByUserId);
                        _logger.LogInformation(
                            "Auto-created case register {CaseNo} for tag hold on vehicle {VehicleReg}",
                            caseDto.CaseNo, transaction.VehicleRegNumber);
                    }
                }

                // Auto-create Yard Entry for tag hold
                var existingYard = await _yardService.GetByWeighingIdAsync(transactionId);
                if (existingYard == null)
                {
                    await _yardService.CreateAsync(
                        new CreateYardEntryRequest
                        {
                            WeighingId = transactionId,
                            StationId = transaction.StationId ?? Guid.Empty,
                            Reason = "tag_hold"
                        },
                        transaction.WeighedByUserId);
                    _logger.LogInformation(
                        "Auto-created yard entry for tag hold on vehicle {VehicleReg}",
                        transaction.VehicleRegNumber);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed auto-create case/yard for tag hold on weighing {TransactionId}. Manual intervention required.",
                    transactionId);
            }
        }

        // 14. AUTO-TRIGGER: Close case + release yard + compliance cert on compliant reweigh (per FRD B.1)
        if (transaction.IsCompliant && transaction.OriginalWeighingId.HasValue)
        {
            try
            {
                var originalWeighingId = transaction.OriginalWeighingId.Value;
                var caseDto = await _caseRegisterService.GetByWeighingIdAsync(originalWeighingId);
                if (caseDto != null)
                {
                    // Build rich closing narration with payment details
                    var closingNarration = $"Vehicle reweighed and found compliant. Reweigh ticket: {transaction.TicketNumber}.";
                    var prosecution = await _dbContext.ProsecutionCases
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.CaseRegisterId == caseDto.Id);
                    if (prosecution != null)
                    {
                        var invoice = await _dbContext.Invoices
                            .AsNoTracking()
                            .FirstOrDefaultAsync(i => i.ProsecutionCaseId == prosecution.Id && i.Status == "paid");
                        if (invoice != null)
                        {
                            var receipt = await _dbContext.Receipts
                                .AsNoTracking()
                                .FirstOrDefaultAsync(r => r.InvoiceId == invoice.Id);
                            closingNarration = $"Vehicle reweighed and found compliant. Reweigh ticket: {transaction.TicketNumber}. " +
                                $"Prosecution charged under {prosecution.BestChargeBasis} basis. " +
                                $"Invoice: {invoice.InvoiceNo}, Fine: {invoice.AmountDue:N2} {invoice.Currency}, Status: {invoice.Status}." +
                                (receipt != null ? $" Receipt: {receipt.ReceiptNo}, Paid: {receipt.AmountPaid:N2} {receipt.Currency}." : "");
                        }
                        else
                        {
                            // Invoice exists but not paid
                            var pendingInvoice = await _dbContext.Invoices
                                .AsNoTracking()
                                .FirstOrDefaultAsync(i => i.ProsecutionCaseId == prosecution.Id);
                            if (pendingInvoice != null)
                            {
                                closingNarration += $" Invoice: {pendingInvoice.InvoiceNo}, Amount: {pendingInvoice.AmountDue:N2} {pendingInvoice.Currency}, Status: {pendingInvoice.Status}.";
                            }
                        }
                    }

                    // Close the case with COMPLIANCE_ACHIEVED disposition
                    var complianceDisposition = await _dbContext.DispositionTypes
                        .FirstOrDefaultAsync(dt => dt.Code == "COMPLIANCE_ACHIEVED");
                    if (complianceDisposition != null)
                    {
                        await _caseRegisterService.CloseCaseAsync(caseDto.Id,
                            new CloseCaseRequest
                            {
                                DispositionTypeId = complianceDisposition.Id,
                                ClosingReason = closingNarration
                            },
                            transaction.WeighedByUserId);
                        _logger.LogInformation(
                            "Auto-closed case {CaseNo} after compliant reweigh {TransactionId}",
                            caseDto.CaseNo, transactionId);
                    }

                    // Release vehicle from yard
                    var yardEntry = await _yardService.GetByWeighingIdAsync(originalWeighingId);
                    if (yardEntry != null && yardEntry.Status != "released")
                    {
                        await _yardService.ReleaseAsync(yardEntry.Id,
                            new ReleaseYardEntryRequest
                            {
                                Notes = $"Auto-released after compliant reweigh. Ticket: {transaction.TicketNumber}"
                            },
                            transaction.WeighedByUserId);
                        _logger.LogInformation(
                            "Auto-released vehicle {VehicleReg} from yard after compliant reweigh",
                            transaction.VehicleRegNumber);
                    }

                    // Find LoadCorrectionMemo and mark compliance achieved
                    var memo = await _dbContext.LoadCorrectionMemos
                        .FirstOrDefaultAsync(m => m.WeighingId == originalWeighingId);
                    if (memo != null)
                    {
                        memo.ComplianceAchieved = true;
                        memo.ReweighWeighingId = transactionId;
                    }

                    // Generate compliance certificate (linked to memo if exists)
                    var year = DateTime.UtcNow.Year;
                    var certCount = await _dbContext.ComplianceCertificates
                        .CountAsync(c => c.CreatedAt.Year == year);
                    var certificate = new ComplianceCertificate
                    {
                        CertificateNo = $"COMP-{year}-{(certCount + 1):D6}",
                        CaseRegisterId = caseDto.Id,
                        WeighingId = transactionId,
                        LoadCorrectionMemoId = memo?.Id,
                        IssuedById = transaction.WeighedByUserId,
                        IssuedAt = DateTime.UtcNow
                    };
                    _dbContext.ComplianceCertificates.Add(certificate);
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation(
                        "Auto-generated compliance certificate {CertNo} for case {CaseNo}",
                        certificate.CertificateNo, caseDto.CaseNo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed auto-close/release/certificate for compliant reweigh {TransactionId}. Manual intervention required.",
                    transactionId);
                // Don't throw — weighing result is still valid
            }
        }

        return transaction;
    }

    /// <summary>
    /// Creates a flat-fee commercial weighing invoice for a completed commercial weighing session.
    /// Uses the org's PaymentGateway to route to treasury-api (online) or mark as offline-pending.
    /// </summary>
    private async Task CreateCommercialWeighingInvoiceAsync(WeighingTransaction transaction, Organization org)
    {
        try
        {
            var existingInvoice = await _dbContext.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.WeighingId == transaction.Id && i.InvoiceType == "commercial_weighing_fee");
            if (existingInvoice != null)
                return; // Already created (idempotent)

            var invoiceNo = await _documentNumberService.GenerateNumberAsync(
                org.Id, transaction.StationId, Models.System.DocumentTypes.Invoice);
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

            // Create treasury payment intent if org uses the treasury gateway
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
                        "Failed to create treasury payment intent for invoice {InvoiceNo}. " +
                        "Invoice saved as pending; payment will need to be initiated manually.",
                        invoiceNo);
                }
            }

            _logger.LogInformation(
                "Created commercial weighing invoice {InvoiceNo} ({AmountDue} KES) for weighing {TransactionId}",
                invoiceNo, org.CommercialWeighingFeeKes, transaction.Id);

            // Fire-and-forget usage report to subscriptions-api
            if (!string.IsNullOrWhiteSpace(org.SsoTenantSlug))
            {
                _ = _subscriptionService.ReportUsageAsync(
                    org.SsoTenantSlug,
                    "weighing_transaction",
                    1,
                    new
                    {
                        weighing_id = transaction.Id,
                        station_id = transaction.StationId,
                        fee_kes = org.CommercialWeighingFeeKes
                    });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create commercial weighing invoice for transaction {TransactionId}. Manual intervention required.",
                transaction.Id);
            // Don't throw — weighing result is still valid
        }
    }

    public async Task<WeighingTransaction?> GetTransactionAsync(Guid id)
    {
        return await _weighingRepository.GetTransactionByIdAsync(id);
    }

    /// <summary>
    /// Gets detailed compliance result including axle groups, fees, and demerit points
    /// Implements Kenya Traffic Act Cap 403 Section 117A for NTSA license management
    /// </summary>
    public async Task<WeighingComplianceResultDto> GetComplianceResultAsync(Guid transactionId)
    {
        // Delegate to the AxleGroupAggregationService which has full compliance calculation
        return await _axleGroupAggregationService.CalculateComplianceAsync(transactionId);
    }

    public async Task<WeighingTransaction> UpdateTransactionAsync(WeighingTransaction transaction)
    {
        return await _weighingRepository.UpdateTransactionAsync(transaction);
    }

    public async Task DeleteTransactionAsync(Guid id)
    {
        var transaction = await _weighingRepository.GetTransactionByIdAsync(id);
        if (transaction == null)
            throw new KeyNotFoundException($"Weighing transaction {id} not found");

        // Only allow deletion if in Pending status
        if (transaction.ControlStatus != "Pending")
            throw new InvalidOperationException($"Cannot delete weighing in status '{transaction.ControlStatus}'. Only Pending transactions can be deleted.");

        await _weighingRepository.DeleteTransactionAsync(id);
    }

    public async Task<(List<WeighingTransaction> Items, int TotalCount)> SearchTransactionsAsync(
        Guid? stationId = null,
        string? vehicleRegNo = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? controlStatus = null,
        bool? isCompliant = null,
        Guid? operatorId = null,
        int skip = 0,
        int take = 50,
        string sortBy = "WeighedAt",
        string sortOrder = "desc",
        string? weighingType = null)
    {
        return await _weighingRepository.SearchTransactionsAsync(
            stationId,
            vehicleRegNo,
            fromDate,
            toDate,
            controlStatus,
            isCompliant,
            operatorId,
            skip,
            take,
            sortBy,
            sortOrder,
            weighingType);
    }

    public async Task<(List<WeighingTransaction> Items, int TotalCount)> SearchTransactionsLightAsync(
        Guid? stationId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int take = 10000)
    {
        return await _weighingRepository.SearchTransactionsLightAsync(stationId, fromDate, toDate, take);
    }

    /// <summary>
    /// Processes an autoweigh capture from TruConnect middleware.
    /// Creates weighing transaction, captures weights, and calculates compliance in a single operation.
    ///
    /// Supports two capture modes:
    /// - IsFinalCapture = false (auto): Creates preliminary record with CaptureStatus = "auto", stores GVW in AutoweighGvwKg
    /// - IsFinalCapture = true (captured): Updates existing auto record or creates new with CaptureStatus = "captured"
    /// </summary>
    public async Task<AutoweighResultDto> ProcessAutoweighAsync(AutoweighCaptureRequest request, Guid userId)
    {
        _logger.LogInformation(
            "Processing autoweigh: Station={StationId}, Vehicle={VehicleReg}, Source={Source}, IsFinal={IsFinal}",
            request.StationId, request.VehicleRegNumber, request.Source, request.IsFinalCapture);

        string vehicleRegNumber = request.VehicleRegNumber.ToUpperInvariant().Trim();

        // 1. Check for idempotency using ClientLocalId
        if (!string.IsNullOrEmpty(request.ClientLocalId))
        {
            var existingTransaction = await _weighingRepository.GetByClientLocalIdAsync(request.ClientLocalId);
            if (existingTransaction != null)
            {
                _logger.LogInformation(
                    "Idempotent request detected - returning existing transaction {TransactionId}",
                    existingTransaction.Id);

                return MapToAutoweighResult(existingTransaction, vehicleFound: true);
            }
        }

        // 2. Look for existing transaction to update
        WeighingTransaction? transaction = null;
        bool isUpdate = false;

        // 2a. If WeighingTransactionId provided, look up that specific transaction
        if (request.WeighingTransactionId.HasValue)
        {
            transaction = await _weighingRepository.GetTransactionByIdAsync(request.WeighingTransactionId.Value);
            if (transaction != null)
            {
                isUpdate = true;
                _logger.LogInformation(
                    "Found frontend-created transaction {TransactionId} for autoweigh sync",
                    transaction.Id);
            }
            else
            {
                _logger.LogWarning(
                    "WeighingTransactionId {TransactionId} not found, proceeding with normal flow",
                    request.WeighingTransactionId.Value);
            }
        }

        // 2b. Fallback: For final capture, look for existing auto-weigh record by vehicle
        if (transaction == null && request.IsFinalCapture)
        {
            transaction = await _weighingRepository.GetLatestAutoweighByVehicleAsync(
                vehicleRegNumber, request.StationId, request.Bound);

            if (transaction != null)
            {
                isUpdate = true;
                _logger.LogInformation(
                    "Found existing auto-weigh transaction {TransactionId} for final capture",
                    transaction.Id);
            }
        }
        else if (transaction == null)
        {
            // For auto-weigh (non-final), mark any previous incomplete sessions as not_weighed
            var markedCount = await _weighingRepository.MarkPendingAsNotWeighedAsync(
                vehicleRegNumber, request.StationId);

            if (markedCount > 0)
            {
                _logger.LogInformation(
                    "Marked {Count} previous auto-weigh transactions as not_weighed for vehicle {Vehicle}",
                    markedCount, vehicleRegNumber);
            }
        }

        // 3. Validate scale test requirement
        var isScaleTestRequired = await _settingsService.GetSettingValueAsync(SettingKeys.WeighingScaleTestRequired, false);
        var hasValidScaleTest = await _scaleTestRepository.HasPassedDailyCalibrationalAsync(
            request.StationId, request.Bound);

        if (isScaleTestRequired && !hasValidScaleTest)
        {
            _logger.LogWarning(
                "Autoweigh blocked - no valid scale test for Station {StationId}, Bound {Bound}",
                request.StationId, request.Bound);
            throw new InvalidOperationException(
                $"Scale test required before weighing. No passing scale test found for this station{(string.IsNullOrEmpty(request.Bound) ? "" : $" (Bound {request.Bound})")} today.");
        }
    
        // If scale test is NOT required and NOT found, scaleTestId will simply be null

        // 4. Get today's passing scale test
        var todaysTest = await _scaleTestRepository.GetTodaysPassingTestAsync(request.StationId, request.Bound);
        var scaleTestId = todaysTest?.Id;

        // 5. Look up or auto-create vehicle (required FK - cannot be Guid.Empty)
        Guid vehicleId;
        bool vehicleFound = false;

        if (request.VehicleId.HasValue)
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId.Value);
            if (vehicle != null)
            {
                vehicleId = vehicle.Id;
                vehicleFound = true;
                vehicleRegNumber = vehicle.RegNo;
            }
            else
            {
                // VehicleId provided but not found - auto-create with reg number
                var newVehicle = new Vehicle { RegNo = vehicleRegNumber };
                var created = await _vehicleRepository.CreateAsync(newVehicle);
                vehicleId = created.Id;
                _logger.LogInformation("Autoweigh: Auto-created vehicle {RegNo} with ID {VehicleId}", vehicleRegNumber, vehicleId);
            }
        }
        else
        {
            var vehicle = await _vehicleRepository.GetByRegNoAsync(vehicleRegNumber);
            if (vehicle != null)
            {
                vehicleId = vehicle.Id;
                vehicleFound = true;
            }
            else
            {
                // Vehicle not found by reg number - auto-create
                var newVehicle = new Vehicle { RegNo = vehicleRegNumber };
                var created = await _vehicleRepository.CreateAsync(newVehicle);
                vehicleId = created.Id;
                _logger.LogInformation("Autoweigh: Auto-created vehicle {RegNo} with ID {VehicleId}", vehicleRegNumber, vehicleId);
            }
        }

        // 6. Create or update transaction
        if (transaction == null)
        {
            // Resolve organization ID from station
            var station = await _dbContext.Stations.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.StationId);
            var orgId = station?.OrganizationId ?? Guid.Empty;

            // Generate ticket number using centralized document numbering service
            var ticketNumber = await _documentNumberService.GenerateNumberAsync(
                orgId, request.StationId, Models.System.DocumentTypes.WeightTicket,
                vehicleRegNumber, request.Bound);

            transaction = new WeighingTransaction
            {
                TicketNumber = ticketNumber,
                StationId = request.StationId,
                WeighedByUserId = userId,
                Bound = request.Bound,
                ScaleTestId = scaleTestId,
                VehicleId = vehicleId,
                VehicleRegNumber = vehicleRegNumber,
                WeighingType = request.WeighingMode ?? "static",
                ControlStatus = "Pending",
                WeighedAt = request.CapturedAt ?? DateTime.UtcNow,
                ClientLocalId = !string.IsNullOrEmpty(request.ClientLocalId) && Guid.TryParse(request.ClientLocalId, out var clientId) ? clientId : null,
                IsSync = true,
                SyncStatus = "synced",
                SyncAt = DateTime.UtcNow,
                // Set capture source and status based on IsFinalCapture
                CaptureSource = request.CaptureSource ?? (request.IsFinalCapture ? "frontend" : "auto"),
                CaptureStatus = request.IsFinalCapture ? "captured" : "auto"
            };

            // For auto-weigh (preliminary), also set AutoweighAt timestamp
            if (!request.IsFinalCapture)
            {
                transaction.AutoweighAt = DateTime.UtcNow;
            }

            transaction = await _weighingRepository.CreateTransactionAsync(transaction);

            _logger.LogInformation(
                "Created autoweigh transaction {TransactionId} with ticket {TicketNumber}, CaptureStatus={CaptureStatus}",
                transaction.Id, transaction.TicketNumber, transaction.CaptureStatus);
        }
        else
        {
            // Update existing auto-weigh record for final capture
            transaction.CaptureStatus = "captured";
            transaction.CaptureSource = request.CaptureSource ?? "frontend";
            transaction.SyncAt = DateTime.UtcNow;

            // Update vehicle if provided
            if (vehicleId != Guid.Empty)
            {
                transaction.VehicleId = vehicleId;
            }
        }

        // 7. Resolve axle configuration for proper FK references
        AxleConfiguration? resolvedConfig = null;
        var axleCount = request.Axles.Count;
        var firstAxleConfigId = request.Axles.FirstOrDefault()?.AxleConfigurationId;

        if (firstAxleConfigId.HasValue && firstAxleConfigId.Value != Guid.Empty)
        {
            resolvedConfig = await _axleConfigurationRepository.GetByIdAsync(firstAxleConfigId.Value, includeWeightReferences: true);
        }

        if (resolvedConfig == null)
        {
            resolvedConfig = await _axleConfigurationRepository.GetStandardByAxleCountAsync(axleCount);
        }

        // 8. Capture axle weights with resolved config
        var axles = request.Axles.Select(dto =>
        {
            var axle = new WeighingAxle
            {
                AxleNumber = dto.AxleNumber,
                MeasuredWeightKg = dto.MeasuredWeightKg,
                WeighingId = transaction.Id,
                CapturedAt = request.CapturedAt ?? DateTime.UtcNow
            };

            // Set config references from resolved config
            if (resolvedConfig != null)
            {
                axle.AxleConfigurationId = resolvedConfig.Id;
                var weightRef = resolvedConfig.AxleWeightReferences?
                    .FirstOrDefault(r => r.AxlePosition == dto.AxleNumber);
                if (weightRef != null)
                {
                    axle.AxleWeightReferenceId = weightRef.Id;
                    axle.PermissibleWeightKg = weightRef.AxleLegalWeightKg;
                    axle.AxleGrouping = weightRef.AxleGrouping;
                    axle.AxleGroupId = weightRef.AxleGroupId;
                }
            }
            else
            {
                axle.AxleConfigurationId = dto.AxleConfigurationId ?? Guid.Empty;
            }

            return axle;
        }).ToList();

        // Calculate GVW from axles
        var calculatedGvw = axles.Sum(a => a.MeasuredWeightKg);

        // Save axles directly (avoid CaptureWeightsAsync which changes status to "captured")
        if (isUpdate)
        {
            await _weighingRepository.DeleteAxlesByTransactionIdAsync(transaction.Id);
        }

        transaction.WeighingAxles = axles;
        transaction.GvwMeasuredKg = calculatedGvw;

        if (!request.IsFinalCapture)
        {
            transaction.AutoweighGvwKg = calculatedGvw;
        }

        await _weighingRepository.SaveTransactionWithNewAxlesAsync(transaction);

        // Calculate compliance (GVW calculation, fees, prohibition generation)
        transaction = await CalculateComplianceAsync(transaction.Id);

        _logger.LogInformation(
            "Autoweigh completed: Transaction={TransactionId}, GVW={GvwKg}kg, Status={Status}, Compliant={IsCompliant}, CaptureStatus={CaptureStatus}",
            transaction.Id, transaction.GvwMeasuredKg, transaction.ControlStatus, transaction.IsCompliant, transaction.CaptureStatus);

        // 10. Return result
        return MapToAutoweighResult(transaction, vehicleFound);
    }

    // Legacy GenerateAutoweighTicketNumber removed in Sprint 22.
    // Ticket number generation now uses IDocumentNumberService for configurable conventions.

    /// <summary>
    /// Maps a weighing transaction to AutoweighResultDto.
    /// </summary>
    private AutoweighResultDto MapToAutoweighResult(WeighingTransaction transaction, bool vehicleFound)
    {
        return new AutoweighResultDto
        {
            WeighingId = transaction.Id,
            TicketNumber = transaction.TicketNumber,
            VehicleRegNumber = transaction.VehicleRegNumber ?? string.Empty,
            VehicleId = transaction.VehicleId,
            VehicleFound = vehicleFound,
            GvwMeasuredKg = transaction.GvwMeasuredKg,
            GvwPermissibleKg = transaction.GvwPermissibleKg,
            GvwOverloadKg = transaction.OverloadKg,
            IsCompliant = transaction.IsCompliant,
            ControlStatus = transaction.ControlStatus ?? string.Empty,
            ViolationReason = transaction.ViolationReason ?? string.Empty,
            CaptureStatus = transaction.CaptureStatus ?? string.Empty,
            CaptureSource = transaction.CaptureSource ?? string.Empty,
            TotalFeeUsd = transaction.TotalFeeUsd,
            HasPermit = transaction.HasPermit,
            AxleCompliance = transaction.WeighingAxles?.Select(a => new AxleComplianceDto
            {
                AxleNumber = a.AxleNumber,
                MeasuredWeightKg = a.MeasuredWeightKg,
                PermissibleWeightKg = a.PermissibleWeightKg,
                OverloadKg = a.OverloadKg,
                IsCompliant = a.OverloadKg <= 0
            }).ToList() ?? new(),
            CapturedAt = transaction.WeighedAt,
            ProcessedAt = DateTime.UtcNow,
            Source = transaction.CaptureSource ?? "TruConnect"
        };
    }

    /// <summary>
    /// Generates a weight ticket PDF for the specified transaction.
    /// </summary>
    public async Task<byte[]> GenerateWeightTicketPdfAsync(Guid transactionId)
    {
        var transaction = await _weighingRepository.GetTransactionByIdAsync(transactionId);
        if (transaction == null)
            throw new KeyNotFoundException($"Weighing transaction {transactionId} not found");

        return await _pdfService.GenerateWeightTicketAsync(transaction);
    }
}
