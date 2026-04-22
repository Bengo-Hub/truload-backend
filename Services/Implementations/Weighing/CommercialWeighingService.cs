using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Data.Repositories.Weighing;
using TruLoad.Backend.DTOs.Weighing;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Models.Financial;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Services.Interfaces.Financial;
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
    private readonly ILogger<CommercialWeighingService> _logger;

    public CommercialWeighingService(
        TruLoadDbContext dbContext,
        ITenantContext tenantContext,
        IVehicleRepository vehicleRepository,
        IDocumentNumberService documentNumberService,
        ITreasuryService treasuryService,
        ILogger<CommercialWeighingService> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _vehicleRepository = vehicleRepository;
        _documentNumberService = documentNumberService;
        _treasuryService = treasuryService;
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
            OrganizationId = orgId
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

        // Calculate discrepancy if expected weight provided
        if (transaction.ExpectedNetWeightKg.HasValue && transaction.NetWeightKg.HasValue)
        {
            transaction.WeightDiscrepancyKg = transaction.NetWeightKg.Value - transaction.ExpectedNetWeightKg.Value;
        }

        // Check commercial tolerance
        await CheckCommercialToleranceAsync(transaction);

        transaction.ControlStatus = "Complete";
        transaction.CaptureStatus = "captured";
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

                // Check tare expiry
                if (vehicle.LastTareWeighedAt.HasValue)
                {
                    var expiryDays = vehicle.TareExpiryDays ?? 90;
                    if (vehicle.LastTareWeighedAt.Value.AddDays(expiryDays) < DateTime.UtcNow)
                    {
                        _logger.LogWarning(
                            "Stored tare for vehicle {VehicleId} expired ({ExpiryDays} days). Last measured: {LastTareAt}",
                            transaction.VehicleId, expiryDays, vehicle.LastTareWeighedAt.Value);
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

        transaction.ControlStatus = "Complete";
        transaction.CaptureStatus = "captured";
        transaction.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        // Create commercial weighing invoice (idempotent)
        await CreateCommercialInvoiceAsync(transaction);

        _logger.LogInformation(
            "Stored tare used for {TransactionId}: tare={TareKg}kg ({Source}). Net={NetKg}kg",
            transactionId, tareWeightKg, tareSource, transaction.NetWeightKg);

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
            dto.TreasuryPaymentUrl = !string.IsNullOrWhiteSpace(invoice.TreasuryIntentId)
                ? $"https://books.codevertexitsolutions.com/pay?intent_id={invoice.TreasuryIntentId}"
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

        return transaction;
    }

    public async Task<List<VehicleTareHistoryDto>> GetVehicleTareHistoryAsync(Guid vehicleId)
    {
        var history = await _dbContext.VehicleTareHistory
            .AsNoTracking()
            .Include(h => h.Vehicle)
            .Include(h => h.Station)
            .Where(h => h.VehicleId == vehicleId)
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

        if (discrepancy > toleranceKg)
        {
            _logger.LogWarning(
                "Commercial tolerance exceeded for {TransactionId}: discrepancy={DiscrepancyKg}kg, tolerance={ToleranceKg}kg",
                transaction.Id, discrepancy, toleranceKg);
        }
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
        var toleranceExceeded = false;
        if (transaction.WeightDiscrepancyKg.HasValue && transaction.GvwToleranceKg > 0)
        {
            toleranceExceeded = Math.Abs(transaction.WeightDiscrepancyKg.Value) > transaction.GvwToleranceKg;
        }

        return new CommercialWeighingResultDto
        {
            Id = transaction.Id,
            TicketNumber = transaction.TicketNumber,
            ControlStatus = transaction.ControlStatus,
            WeighingMode = transaction.WeighingMode,

            VehicleId = transaction.VehicleId,
            VehicleRegNumber = transaction.VehicleRegNumber,
            VehicleMake = transaction.Vehicle?.Make,
            VehicleModel = transaction.Vehicle?.Model,
            TrailerRegNo = transaction.TrailerRegNo,

            DriverId = transaction.DriverId,
            DriverName = transaction.Driver?.FullNames,
            TransporterId = transaction.TransporterId,
            TransporterName = transaction.Transporter?.Name,
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
            SourceLocation = transaction.Origin?.Name,
            DestinationId = transaction.DestinationId,
            DestinationLocation = transaction.Destination?.Name,
            CargoId = transaction.CargoId,
            CargoType = transaction.Cargo?.Name,

            ToleranceExceeded = toleranceExceeded,
            ToleranceDisplay = transaction.GvwToleranceDisplay,
            ToleranceExceptionApproved = transaction.ToleranceExceptionApproved,
            ToleranceExceptionApprovedBy = transaction.ToleranceExceptionApprovedBy,
            ToleranceExceptionApprovedAt = transaction.ToleranceExceptionApprovedAt,

            FirstPassAxles = ParsePassWeights(transaction.IndustryMetadata, "firstPassWeights"),
            SecondPassAxles = ParsePassWeights(transaction.IndustryMetadata, "secondPassWeights"),

            IndustryMetadata = transaction.IndustryMetadata,
            WeighedAt = transaction.WeighedAt,
            CreatedAt = transaction.CreatedAt
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

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Tolerance exception approved for transaction {TransactionId} by user {UserId}", transactionId, approvedByUserId);

        return MapToCommercialResultDto(transaction);
    }
}
