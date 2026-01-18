using TruLoad.Backend.Data.Repositories.Weighing;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Models;
using TruLoad.Backend.Services.Interfaces.Weighing;
using TruLoad.Backend.Repositories.Weighing.Interfaces;
using TruLoad.Backend.Services.Interfaces.Infrastructure;
using TruLoad.Backend.Data.Repositories.Infrastructure;
using TruLoad.Backend.Models.Infrastructure;

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

    public WeighingService(
        IWeighingRepository weighingRepository,
        IAxleConfigurationRepository axleConfigurationRepository,
        IPermitRepository permitRepository,
        IProhibitionRepository prohibitionRepository,
        IToleranceRepository toleranceRepository,
        IAxleFeeScheduleRepository feeScheduleRepository,
        IPdfService pdfService,
        IBlobStorageService storageService,
        IDocumentRepository documentRepository)
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
    }

    public async Task<WeighingTransaction> InitiateWeighingAsync(string ticketNumber, Guid stationId, Guid userId)
    {
        var transaction = new WeighingTransaction
        {
            TicketNumber = ticketNumber,
            StationId = stationId,
            WeighedByUserId = userId,
            ControlStatus = "Pending",
            WeighedAt = DateTime.UtcNow
        };

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
        var transaction = await _weighingRepository.GetTransactionByIdAsync(transactionId);
        if (transaction == null) throw new KeyNotFoundException($"Weighing transaction {transactionId} not found");

        transaction.WeighingAxles = axles;
        transaction.GvwMeasuredKg = axles.Sum(a => a.MeasuredWeightKg);
        
        // Initial simplified compliace check
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
        bool hasAxleOverload = false;
        
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
            
            if (axle.OverloadKg > 0) hasAxleOverload = true;
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
    /// </summary>
    private async Task ProcessAxleGroupsAsync(WeighingTransaction transaction, string legalFramework)
    {
        if (transaction.WeighingAxles == null || !transaction.WeighingAxles.Any())
            return;

        // Group axles by AxleGrouping (A, B, C, D)
        var axleGroups = transaction.WeighingAxles
            .GroupBy(a => a.AxleGrouping)
            .ToList();

        // Query group tolerance (5% for Traffic Act and EAC Act)
        int groupTolerancePercentage = 5; // Default per regulatory requirements
        var toleranceSetting = await _toleranceRepository.GetToleranceAsync(legalFramework, "AXLE");
        if (toleranceSetting != null)
        {
            groupTolerancePercentage = (int)toleranceSetting.TolerancePercentage;
        }

        foreach (var group in axleGroups)
        {
            // Calculate group aggregates
            int groupTotalMeasured = group.Sum(a => a.MeasuredWeightKg);
            int groupTotalPermissible = group.Sum(a => a.PermissibleWeightKg);
            int groupToleranceKg = (int)Math.Round(groupTotalPermissible * (groupTolerancePercentage / 100.0));

            // Calculate Pavement Damage Factor (Fourth Power Law)
            decimal pdf = CalculatePavementDamageFactor(groupTotalMeasured, groupTotalPermissible);

            // Cache group values in each axle for performance
            foreach (var axle in group)
            {
                axle.GroupAggregateWeightKg = groupTotalMeasured;
                axle.GroupPermissibleWeightKg = groupTotalPermissible;
                axle.PavementDamageFactor = pdf;

                // Infer axle type from group characteristics (simplified logic)
                // In production, this should come from vehicle configuration
                if (axle.AxleGrouping == "A")
                {
                    axle.AxleType = "Steering";
                }
                else if (group.Count() >= 3)
                {
                    axle.AxleType = "Tridem";
                }
                else if (group.Count() == 2)
                {
                    axle.AxleType = "Tandem";
                }
                else
                {
                    axle.AxleType = "SingleDrive";
                }
            }
        }
    }

    /// <summary>
    /// Calculate Pavement Damage Factor using Fourth Power Law (AASHO Road Test 1958-1960)
    /// PDF = (ActualWeight / PermissibleWeight) ^ 4
    /// </summary>
    private decimal CalculatePavementDamageFactor(int actualWeightKg, int permissibleWeightKg)
    {
        if (permissibleWeightKg == 0) return 0m;

        var ratio = (double)actualWeightKg / permissibleWeightKg;
        var pdf = Math.Pow(ratio, 4);

        return (decimal)pdf;
    }

    /// <summary>
    /// Calculate fees for overloaded axles and GVW using AxleFeeSchedule repository
    /// Implements Kenya Traffic Act Cap 403 and EAC Act 2016 fee structures
    /// </summary>
    private async Task CalculateFeesAsync(WeighingTransaction transaction, string legalFramework)
    {
        if (transaction.WeighingAxles == null || !transaction.WeighingAxles.Any())
            return;

        decimal totalFeeUsd = 0m;

        // Calculate GVW fee if overloaded
        if (transaction.OverloadKg > 0)
        {
            var gvwFeeResult = await _feeScheduleRepository.CalculateFeeAsync(
                legalFramework, "GVW", transaction.OverloadKg);

            if (gvwFeeResult.HasValue)
            {
                totalFeeUsd += gvwFeeResult.Value.FeeAmountUsd;
            }
        }

        // Calculate axle fees per group (not per individual axle)
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
                    var axleFeeResult = await _feeScheduleRepository.CalculateFeeAsync(
                        legalFramework, "AXLE", groupOverload);

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
            // Log and continue - don't break the weighing flow if PDF fails
            // In production, we might queue this for retry
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
            // Log and continue
        }
    }

    public async Task<WeighingTransaction?> GetTransactionAsync(Guid id)
    {
        return await _weighingRepository.GetTransactionByIdAsync(id);
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
        string sortOrder = "desc")
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
            sortOrder);
    }
}
