using TruLoad.Backend.Data.Repositories.Weighing;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Models;
using TruLoad.Backend.Services.Interfaces.Weighing;
using TruLoad.Backend.Repositories.Weighing.Interfaces;
using TruLoad.Backend.Services.Interfaces.Infrastructure;
using TruLoad.Backend.Data.Repositories.Infrastructure;
using TruLoad.Backend.Models.Infrastructure;
using TruLoad.Backend.DTOs.Weighing;
using TruLoad.Backend.Repositories.Infrastructure;

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
        _logger = logger;
    }

    /// <summary>
    /// Initiates a weighing transaction with scale test validation.
    /// Per FRD: Scale test must be completed once daily per station/bound before weighing operations.
    /// All required FK fields (vehicleId) must be provided to avoid FK constraint violations on save.
    /// </summary>
    public async Task<WeighingTransaction> InitiateWeighingAsync(
        string ticketNumber,
        Guid stationId,
        Guid userId,
        Guid vehicleId,
        string vehicleRegNo,
        string? bound = null,
        Guid? scaleTestId = null,
        Guid? driverId = null,
        Guid? transporterId = null,
        string weighingType = "static")
    {
        // Validate scale test requirement
        var hasValidScaleTest = await _scaleTestRepository.HasPassedDailyCalibrationalAsync(stationId, bound);

        if (!hasValidScaleTest)
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

        var transaction = new WeighingTransaction
        {
            TicketNumber = ticketNumber,
            StationId = stationId,
            WeighedByUserId = userId,
            VehicleId = vehicleId,
            VehicleRegNumber = vehicleRegNo,
            DriverId = driverId,
            TransporterId = transporterId,
            WeighingType = weighingType,
            Bound = bound,
            ScaleTestId = validScaleTestId,
            ControlStatus = "Pending",
            WeighedAt = DateTime.UtcNow
        };

        _logger.LogInformation(
            "Weighing transaction initiated: {TicketNumber}, Station: {StationId}, Vehicle: {VehicleId}, Bound: {Bound}, ScaleTest: {ScaleTestId}",
            ticketNumber, stationId, vehicleId, bound, validScaleTestId);

        return await _weighingRepository.CreateTransactionAsync(transaction);
    }

    public async Task<WeighingTransaction> InitiateReweighAsync(Guid originalTransactionId, string ticketNumber, Guid userId)
    {
        var original = await _weighingRepository.GetTransactionByIdAsync(originalTransactionId);
        if (original == null) throw new KeyNotFoundException($"Original weighing transaction {originalTransactionId} not found");

        if (original.ReweighCycleNo >= 8)
        {
            throw new InvalidOperationException("Maximum reweigh cycles (8) reached for this transaction.");
        }

        var transaction = new WeighingTransaction
        {
            TicketNumber = ticketNumber,
            StationId = original.StationId,
            VehicleId = original.VehicleId,
            VehicleRegNumber = original.VehicleRegNumber,
            DriverId = original.DriverId,
            TransporterId = original.TransporterId,
            WeighedByUserId = userId,
            OriginalWeighingId = original.OriginalWeighingId ?? original.Id, // Link to the very first weighing if possible
            ReweighCycleNo = original.ReweighCycleNo + 1,
            ControlStatus = "Pending",
            WeighedAt = DateTime.UtcNow
        };

        return await _weighingRepository.CreateTransactionAsync(transaction);
    }

    public async Task<WeighingTransaction> CaptureWeightsAsync(Guid transactionId, List<WeighingAxle> axles)
    {
        if (axles == null || axles.Count == 0)
        {
            throw new ArgumentException("At least one axle weight is required", nameof(axles));
        }

        var transaction = await _weighingRepository.GetTransactionByIdAsync(transactionId);
        if (transaction == null) throw new KeyNotFoundException($"Weighing transaction {transactionId} not found");

        transaction.WeighingAxles = axles;
        transaction.GvwMeasuredKg = axles.Sum(a => a.MeasuredWeightKg);

        // If this transaction was auto-weighed by middleware, update to "captured" now that
        // the frontend is submitting final weights
        if (transaction.CaptureStatus == "auto")
        {
            transaction.CaptureStatus = "captured";
            transaction.CaptureSource = "frontend";
            _logger.LogInformation(
                "Updated CaptureStatus from 'auto' to 'captured' for transaction {TransactionId}",
                transactionId);
        }

        // Initial simplified compliance check
        await CalculateComplianceAsync(transactionId); // Should be called explicitly usually, but good to have here

        await _weighingRepository.UpdateTransactionAsync(transaction);
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

        // 2. Identify the Axle Configuration
        var firstAxle = transaction.WeighingAxles.OrderBy(a => a.AxleNumber).First();
        var configId = firstAxle.AxleConfigurationId;

        // 3. Fetch Configuration Details
        var axleConfig = await _axleConfigurationRepository.GetByIdAsync(configId, includeWeightReferences: true);
        if (axleConfig == null)
        {
             transaction.ControlStatus = "Configuration Error";
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

        // 7. Query Tolerance Settings (Replace hardcoded 200kg)
        // Default to Traffic Act (stricter: 0% GVW tolerance, 5% axle group tolerance)
        string legalFramework = "TRAFFIC_ACT";
        int gvwToleranceKg = await _toleranceRepository.CalculateToleranceKgAsync(
            legalFramework, "GVW", transaction.GvwPermissibleKg);

        // 8. Calculate Axle Group Aggregation & Compliance
        await ProcessAxleGroupsAsync(transaction, legalFramework);

        // 9. Calculate Fees for Overloaded Axles and GVW
        await CalculateFeesAsync(transaction, legalFramework);

        // 10. Determine Status & Operational Tolerance checking
        var violationReasons = new List<string>();

        if (transaction.OverloadKg > gvwToleranceKg)
        {
            violationReasons.Add($"GVW Overload: {transaction.OverloadKg}kg (Permissible: {transaction.GvwPermissibleKg}kg, Tolerance: {gvwToleranceKg}kg)");
        }

        // Check for axle group violations (not individual axles)
        var groupViolations = transaction.WeighingAxles
            .Where(a => a.GroupAggregateWeightKg.HasValue && a.GroupPermissibleWeightKg.HasValue)
            .GroupBy(a => a.AxleGrouping)
            .Select(g => g.First())
            .Where(a => (a.GroupAggregateWeightKg!.Value - a.GroupPermissibleWeightKg!.Value) > 0);

        foreach (var groupAxle in groupViolations)
        {
            var groupOverload = groupAxle.GroupAggregateWeightKg!.Value - groupAxle.GroupPermissibleWeightKg!.Value;
            violationReasons.Add($"Axle Group {groupAxle.AxleGrouping} Overload: {groupOverload}kg");
        }

        transaction.ViolationReason = string.Join("; ", violationReasons);

        bool hasGroupViolation = groupViolations.Any();

        if (transaction.OverloadKg <= gvwToleranceKg && !hasGroupViolation)
        {
            transaction.ControlStatus = "Compliant";
            transaction.IsCompliant = true;
            transaction.IsSentToYard = false;
        }
        else
        {
            transaction.IsCompliant = false;

            // Use operational tolerance for auto-release warning (200kg default from database)
            var operationalTolerance = await _toleranceRepository.GetByCodeAsync("OPERATIONAL_TOLERANCE");
            int operationalToleranceKg = operationalTolerance?.ToleranceKg ?? 200;

            if (transaction.OverloadKg <= operationalToleranceKg && transaction.OverloadKg > 0 && !hasGroupViolation)
            {
                transaction.ControlStatus = "Warning";
                transaction.IsSentToYard = false; // Auto-release
            }
            else
            {
                transaction.ControlStatus = "Overloaded";
                transaction.IsSentToYard = true;

                // 8. Generate Prohibition Order if not exists
                var existingProhibition = await _prohibitionRepository.GetByWeighingIdAsync(transactionId);
                if (existingProhibition == null)
                {
                    var prohibitionNo = await _prohibitionRepository.GenerateProhibitionNumberAsync();
                    var prohibitionOrder = new ProhibitionOrder
                    {
                        WeighingId = transactionId,
                        ProhibitionNo = prohibitionNo,
                        IssuedById = transaction.WeighedByUserId,
                        Status = "Open",
                        Reason = transaction.ViolationReason,
                        IssuedAt = DateTime.UtcNow
                    };
                    await _prohibitionRepository.CreateAsync(prohibitionOrder);
                }
            }
        }

        // 9. Persist Updates
        await _weighingRepository.UpdateTransactionAsync(transaction);

        // 10. Generate Weight Ticket PDF
        await SaveTransactionDocumentAsync(transaction);

        // 11. Generate Prohibition Order PDF if created
        if (transaction.ControlStatus == "Overloaded" && !transaction.IsCompliant)
        {
            var prohibition = await _prohibitionRepository.GetByWeighingIdAsync(transactionId);
            if (prohibition != null)
            {
                await SaveProhibitionDocumentAsync(prohibition, transaction);
            }
        }

        return transaction;
    }

    /// <summary>
    /// Process axle groups for regulatory compliance
    /// Implements Kenya Traffic Act Cap 403 and EAC Act 2016 group-based tolerance
    /// Uses the AxleGroupAggregationService for proper tolerance calculation:
    /// - 5% tolerance for SINGLE axle groups (Steering, SingleDrive)
    /// - 0% tolerance for MULTI-axle groups (Tandem, Tridem)
    /// </summary>
    private async Task ProcessAxleGroupsAsync(WeighingTransaction transaction, string legalFramework)
    {
        if (transaction.WeighingAxles == null || !transaction.WeighingAxles.Any())
            return;

        // Get operational tolerance from settings (default 200kg)
        var operationalTolerance = await _toleranceRepository.GetByCodeAsync("OPERATIONAL_TOLERANCE");
        int operationalToleranceKg = operationalTolerance?.ToleranceKg ?? 200;

        // Use the AxleGroupAggregationService for proper tolerance calculation
        var groupResults = await _axleGroupAggregationService.AggregateAxleGroupsAsync(
            transaction.WeighingAxles,
            legalFramework,
            operationalToleranceKg);

        // Map group results back to weighing axles
        foreach (var groupResult in groupResults)
        {
            var groupAxles = transaction.WeighingAxles
                .Where(a => a.AxleGrouping == groupResult.GroupLabel)
                .ToList();

            foreach (var axle in groupAxles)
            {
                axle.GroupAggregateWeightKg = groupResult.GroupWeightKg;
                axle.GroupPermissibleWeightKg = groupResult.GroupPermissibleKg;
                axle.PavementDamageFactor = groupResult.PavementDamageFactor;
                axle.AxleType = groupResult.AxleType;
                // Tolerance info (ToleranceKg: 5% for single, 0% for grouped) available via GetComplianceResultAsync
            }
        }
    }

    /// <summary>
    /// Calculate fees for overloaded axles and GVW using per-axle-type fee schedules
    /// Implements Kenya Traffic Act Cap 403 with differentiated fees:
    /// - Steering axle: Different rate
    /// - Single Drive axle: Different rate
    /// - Tandem (2-axle group): Different rate
    /// - Tridem (3-axle group): Different rate
    /// </summary>
    private async Task CalculateFeesAsync(WeighingTransaction transaction, string legalFramework)
    {
        if (transaction.WeighingAxles == null || !transaction.WeighingAxles.Any())
            return;

        decimal totalFeeUsd = 0m;

        // Calculate GVW fee if overloaded (still uses the standard fee schedule)
        if (transaction.OverloadKg > 0)
        {
            var gvwFeeResult = await _feeScheduleRepository.CalculateFeeAsync(
                legalFramework, "GVW", transaction.OverloadKg);

            if (gvwFeeResult.HasValue)
            {
                totalFeeUsd += gvwFeeResult.Value.FeeAmountUsd;
            }
        }

        // Calculate axle fees per group using per-axle-type fee schedule
        // Each axle type has different fee rates per Kenya Traffic Act Cap 403
        var processedGroups = new HashSet<string>();

        foreach (var axle in transaction.WeighingAxles)
        {
            // Process each group only once
            if (!processedGroups.Contains(axle.AxleGrouping) &&
                axle.GroupAggregateWeightKg.HasValue &&
                axle.GroupPermissibleWeightKg.HasValue)
            {
                processedGroups.Add(axle.AxleGrouping);

                int groupOverload = axle.GroupAggregateWeightKg.Value - axle.GroupPermissibleWeightKg.Value;

                if (groupOverload > 0)
                {
                    // Determine axle type for fee calculation
                    string axleType = axle.AxleType;
                    if (string.IsNullOrEmpty(axleType))
                    {
                        axleType = _axleGroupAggregationService.DetermineAxleType(
                            transaction.WeighingAxles.Count(a => a.AxleGrouping == axle.AxleGrouping),
                            axle.AxleGrouping);
                    }

                    // Use per-axle-type fee calculation
                    var axleFeeResult = await _feeScheduleRepository.CalculateFeeAsync(
                        legalFramework, axleType.ToUpperInvariant(), groupOverload);

                    if (axleFeeResult.HasValue)
                    {
                        // Distribute fee equally across axles in the group
                        var groupAxles = transaction.WeighingAxles
                            .Where(a => a.AxleGrouping == axle.AxleGrouping)
                            .ToList();

                        decimal feePerAxle = axleFeeResult.Value.FeeAmountUsd / groupAxles.Count;

                        foreach (var groupAxle in groupAxles)
                        {
                            groupAxle.FeeUsd = feePerAxle;
                        }

                        totalFeeUsd += axleFeeResult.Value.FeeAmountUsd;
                    }
                    else
                    {
                        // Fallback to generic AXLE fee if per-type fee not found
                        var fallbackFeeResult = await _feeScheduleRepository.CalculateFeeAsync(
                            legalFramework, "AXLE", groupOverload);

                        if (fallbackFeeResult.HasValue)
                        {
                            var groupAxles = transaction.WeighingAxles
                                .Where(a => a.AxleGrouping == axle.AxleGrouping)
                                .ToList();

                            decimal feePerAxle = fallbackFeeResult.Value.FeeAmountUsd / groupAxles.Count;

                            foreach (var groupAxle in groupAxles)
                            {
                                groupAxle.FeeUsd = feePerAxle;
                            }

                            totalFeeUsd += fallbackFeeResult.Value.FeeAmountUsd;
                        }
                    }
                }
            }
        }

        // Update transaction total fee
        transaction.TotalFeeUsd = totalFeeUsd;
    }

    private async Task SaveTransactionDocumentAsync(WeighingTransaction transaction)
    {
        try
        {
            var pdfBytes = await _pdfService.GenerateWeightTicketAsync(transaction);
            var fileName = $"WeightTicket_{transaction.TicketNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";

            using var ms = new MemoryStream(pdfBytes);
            var (filePath, checksum, fileSize) = await _storageService.SaveAsync(ms, fileName, "weighing/tickets");

            var doc = new Document
            {
                FileName = fileName,
                MimeType = "application/pdf",
                FileSize = fileSize,
                FilePath = filePath,
                FileUrl = _storageService.GetFileUrl(filePath),
                DocumentType = "WeightTicket",
                RelatedEntityType = "WeighingTransaction",
                RelatedEntityId = transaction.Id,
                UploadedById = transaction.WeighedByUserId,
                CreatedAt = DateTime.UtcNow
            };

            await _documentRepository.CreateAsync(doc);
        }
        catch (Exception ex)
        {
            // Log the error but don't break the weighing flow if PDF fails
            // In production, this could be queued for retry
            _logger.LogError(ex,
                "Failed to generate weight ticket PDF for transaction {TransactionId}, ticket {TicketNumber}",
                transaction.Id, transaction.TicketNumber);
        }
    }

    private async Task SaveProhibitionDocumentAsync(ProhibitionOrder order, WeighingTransaction transaction)
    {
        try
        {
            var pdfBytes = await _pdfService.GenerateProhibitionOrderAsync(order);
            var fileName = $"ProhibitionOrder_{order.ProhibitionNo}.pdf";

            using var ms = new MemoryStream(pdfBytes);
            var (filePath, checksum, fileSize) = await _storageService.SaveAsync(ms, fileName, "weighing/prohibitions");

            var doc = new Document
            {
                FileName = fileName,
                MimeType = "application/pdf",
                FileSize = fileSize,
                FilePath = filePath,
                FileUrl = _storageService.GetFileUrl(filePath),
                DocumentType = "ProhibitionOrder",
                RelatedEntityType = "ProhibitionOrder",
                RelatedEntityId = order.Id,
                UploadedById = transaction.WeighedByUserId,
                CreatedAt = DateTime.UtcNow
            };

            await _documentRepository.CreateAsync(doc);
        }
        catch (Exception ex)
        {
            // Log the error but don't break the weighing flow if PDF fails
            _logger.LogError(ex,
                "Failed to generate prohibition order PDF for order {ProhibitionNo}, transaction {TransactionId}",
                order.ProhibitionNo, transaction.Id);
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
        var hasValidScaleTest = await _scaleTestRepository.HasPassedDailyCalibrationalAsync(
            request.StationId, request.Bound);

        if (!hasValidScaleTest)
        {
            _logger.LogWarning(
                "Autoweigh blocked - no valid scale test for Station {StationId}, Bound {Bound}",
                request.StationId, request.Bound);
            throw new InvalidOperationException(
                $"Scale test required before weighing. No passing scale test found for this station{(string.IsNullOrEmpty(request.Bound) ? "" : $" (Bound {request.Bound})")} today.");
        }

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
            // Generate ticket number (format: AUTO-YYYYMMDD-HHMMSS-XXX)
            var ticketNumber = GenerateAutoweighTicketNumber(request.StationId);

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

        // 7. Capture axle weights
        var axles = request.Axles.Select(dto => new WeighingAxle
        {
            AxleNumber = dto.AxleNumber,
            MeasuredWeightKg = dto.MeasuredWeightKg,
            AxleConfigurationId = dto.AxleConfigurationId ?? Guid.Empty,
            WeighingId = transaction.Id,
            CapturedAt = request.CapturedAt ?? DateTime.UtcNow
        }).ToList();

        // Calculate GVW from axles
        var calculatedGvw = axles.Sum(a => a.MeasuredWeightKg);

        if (isUpdate)
        {
            // For updates, clear old axles and add new ones
            transaction.WeighingAxles = axles;
            transaction.GvwMeasuredKg = calculatedGvw;
            transaction = await _weighingRepository.UpdateTransactionAsync(transaction);
        }
        else
        {
            transaction = await CaptureWeightsAsync(transaction.Id, axles);
        }

        // 8. For auto-weigh (preliminary), store GVW in AutoweighGvwKg field
        if (!request.IsFinalCapture)
        {
            transaction.AutoweighGvwKg = calculatedGvw;
            await _weighingRepository.UpdateTransactionAsync(transaction);
        }

        // 9. Calculate compliance (already includes GVW calculation, fees, prohibition generation)
        transaction = await CalculateComplianceAsync(transaction.Id);

        _logger.LogInformation(
            "Autoweigh completed: Transaction={TransactionId}, GVW={GvwKg}kg, Status={Status}, Compliant={IsCompliant}, CaptureStatus={CaptureStatus}",
            transaction.Id, transaction.GvwMeasuredKg, transaction.ControlStatus, transaction.IsCompliant, transaction.CaptureStatus);

        // 10. Return result
        return MapToAutoweighResult(transaction, vehicleFound);
    }

    /// <summary>
    /// Generates a unique ticket number for autoweigh transactions.
    /// Format: AUTO-{StationCode}-{YYYYMMDD}-{HHMMSS}-{Random3Digits}
    /// </summary>
    private string GenerateAutoweighTicketNumber(Guid stationId)
    {
        var timestamp = DateTime.UtcNow;
        var random = new Random().Next(100, 999);
        var shortStationId = stationId.ToString("N")[..4].ToUpperInvariant();
        return $"AUTO-{shortStationId}-{timestamp:yyyyMMdd}-{timestamp:HHmmss}-{random}";
    }

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
