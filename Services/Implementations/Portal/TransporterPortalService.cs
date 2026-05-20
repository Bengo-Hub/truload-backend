using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Portal;
using TruLoad.Backend.DTOs.Weighing;
using TruLoad.Backend.Services.Interfaces.Infrastructure;
using TruLoad.Backend.Services.Interfaces.Portal;
using TruLoad.Backend.Services.Interfaces.Subscription;

namespace TruLoad.Backend.Services.Implementations.Portal;

/// <summary>
/// Transporter Self-Service Portal service implementation.
/// Provides cross-tenant read access to weighing data by bypassing
/// the standard tenant query filters via IgnoreQueryFilters().
/// </summary>
public class TransporterPortalService : ITransporterPortalService
{
    private readonly TruLoadDbContext _dbContext;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IPdfService _pdfService;
    private readonly ILogger<TransporterPortalService> _logger;

    public TransporterPortalService(
        TruLoadDbContext dbContext,
        ISubscriptionService subscriptionService,
        IPdfService pdfService,
        ILogger<TransporterPortalService> logger)
    {
        _dbContext = dbContext;
        _subscriptionService = subscriptionService;
        _pdfService = pdfService;
        _logger = logger;
    }

    public async Task<PortalRegistrationResult> RegisterAsync(Guid userId, string userEmail, PortalRegistrationRequest request)
    {
        var transporter = await _dbContext.Transporters
            .IgnoreQueryFilters()
            .Where(t => t.IsActive &&
                (t.Email == request.Email ||
                 (!string.IsNullOrEmpty(request.Phone) && t.Phone == request.Phone) ||
                 (!string.IsNullOrEmpty(request.TransporterCode) && t.Code == request.TransporterCode)))
            .FirstOrDefaultAsync();

        if (transporter == null)
        {
            return new PortalRegistrationResult
            {
                Success = false,
                Message = "No matching transporter found. Please verify your email, phone number, or transporter code."
            };
        }

        if (transporter.PortalAccountId.HasValue && transporter.PortalAccountId != userId)
        {
            return new PortalRegistrationResult
            {
                Success = false,
                Message = "This transporter is already linked to a different portal account."
            };
        }

        transporter.PortalAccountId = userId;
        transporter.PortalAccountEmail = userEmail;
        transporter.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Portal account registered: UserId={UserId}, TransporterId={TransporterId}, TransporterName={TransporterName}",
            userId, transporter.Id, transporter.Name);

        return new PortalRegistrationResult
        {
            Success = true,
            Message = "Portal account successfully linked to transporter.",
            TransporterId = transporter.Id,
            TransporterName = transporter.Name
        };
    }

    public async Task<PortalPagedResult<PortalWeighingDto>> GetWeighingsAsync(
        Guid userId, int page, int pageSize,
        DateTime? fromDate, DateTime? toDate,
        Guid? vehicleId, Guid? organizationId)
    {
        var transporter = await GetTransporterForUserAsync(userId);
        var limits = await ResolvePortalSubscriptionAsync(transporter);

        // Enforce history date window based on subscription tier
        var cutoff = DateTime.UtcNow.AddMonths(-limits.HistoryMonths);
        var effectiveFrom = fromDate.HasValue && fromDate.Value > cutoff ? fromDate.Value : cutoff;

        var query = _dbContext.WeighingTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(w => w.TransporterId == transporter.Id &&
                        w.WeighingMode == "commercial" &&
                        w.WeighedAt >= effectiveFrom);

        if (toDate.HasValue)
            query = query.Where(w => w.WeighedAt <= toDate.Value);
        if (vehicleId.HasValue)
            query = query.Where(w => w.VehicleId == vehicleId.Value);
        if (organizationId.HasValue)
            query = query.Where(w => w.OrganizationId == organizationId.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(w => w.WeighedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(w => new PortalWeighingDto
            {
                Id = w.Id,
                TicketNumber = w.TicketNumber,
                VehicleRegNumber = w.VehicleRegNumber,
                ControlStatus = w.ControlStatus,
                TareWeightKg = w.TareWeightKg,
                GrossWeightKg = w.GrossWeightKg,
                NetWeightKg = w.NetWeightKg,
                AdjustedNetWeightKg = w.AdjustedNetWeightKg,
                CargoType = w.Cargo != null ? w.Cargo.Name : null,
                ConsignmentNo = w.ConsignmentNo,
                OrderReference = w.OrderReference,
                OrganizationId = w.OrganizationId,
                OrganizationName = w.Organization != null ? w.Organization.Name : string.Empty,
                StationName = w.Station != null ? w.Station.Name : null,
                WeighedAt = w.WeighedAt,
                CreatedAt = w.CreatedAt
            })
            .ToListAsync();

        return new PortalPagedResult<PortalWeighingDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PortalWeighingDto> GetWeighingDetailAsync(Guid userId, Guid weighingId)
    {
        var transporter = await GetTransporterForUserAsync(userId);

        var weighing = await _dbContext.WeighingTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(w => w.Id == weighingId && w.TransporterId == transporter.Id)
            .Select(w => new PortalWeighingDto
            {
                Id = w.Id,
                TicketNumber = w.TicketNumber,
                VehicleRegNumber = w.VehicleRegNumber,
                ControlStatus = w.ControlStatus,
                TareWeightKg = w.TareWeightKg,
                GrossWeightKg = w.GrossWeightKg,
                NetWeightKg = w.NetWeightKg,
                AdjustedNetWeightKg = w.AdjustedNetWeightKg,
                CargoType = w.Cargo != null ? w.Cargo.Name : null,
                ConsignmentNo = w.ConsignmentNo,
                OrderReference = w.OrderReference,
                OrganizationId = w.OrganizationId,
                OrganizationName = w.Organization != null ? w.Organization.Name : string.Empty,
                StationName = w.Station != null ? w.Station.Name : null,
                WeighedAt = w.WeighedAt,
                CreatedAt = w.CreatedAt
            })
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException($"Weighing transaction {weighingId} not found for this transporter.");

        return weighing;
    }

    public async Task<(byte[] Bytes, string FileName)> DownloadWeighingPdfAsync(Guid userId, Guid weighingId)
    {
        var transporter = await GetTransporterForUserAsync(userId);

        var tx = await _dbContext.WeighingTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(w => w.Vehicle)
            .Include(w => w.Driver)
            .Include(w => w.Organization)
            .Include(w => w.Station)
            .Include(w => w.Cargo)
            .Include(w => w.Origin)
            .Include(w => w.Destination)
            .FirstOrDefaultAsync(w => w.Id == weighingId && w.TransporterId == transporter.Id)
            ?? throw new KeyNotFoundException($"Weighing transaction {weighingId} not found for this transporter.");

        var result = new CommercialWeighingResultDto
        {
            Id = tx.Id,
            TicketNumber = tx.TicketNumber,
            ControlStatus = tx.ControlStatus,
            WeighingMode = tx.WeighingMode,
            WeighingScaleType = tx.WeighingScaleType,

            VehicleId = tx.VehicleId,
            VehicleRegNumber = tx.VehicleRegNumber,
            VehicleMake = tx.Vehicle?.Make,
            VehicleModel = tx.Vehicle?.Model,

            DriverId = tx.DriverId,
            DriverName = tx.Driver != null ? $"{tx.Driver.FullNames} {tx.Driver.Surname}" : null,
            TransporterId = tx.TransporterId,
            TransporterName = transporter.Name,

            StationId = tx.StationId ?? Guid.Empty,
            StationName = tx.Station?.Name,

            TareWeightKg = tx.TareWeightKg,
            GrossWeightKg = tx.GrossWeightKg,
            NetWeightKg = tx.NetWeightKg,
            FirstWeightKg = tx.FirstWeightKg,
            FirstWeightType = tx.FirstWeightType,
            FirstWeightAt = tx.FirstWeightAt,
            SecondWeightKg = tx.SecondWeightKg,
            SecondWeightType = tx.SecondWeightType,
            SecondWeightAt = tx.SecondWeightAt,
            TareSource = tx.TareSource,

            QualityDeductionKg = tx.QualityDeductionKg,
            AdjustedNetWeightKg = tx.AdjustedNetWeightKg,

            ConsignmentNo = tx.ConsignmentNo,
            OrderReference = tx.OrderReference,
            ExpectedNetWeightKg = tx.ExpectedNetWeightKg,
            WeightDiscrepancyKg = tx.WeightDiscrepancyKg,
            SealNumbers = tx.SealNumbers,
            Remarks = tx.Remarks,

            SourceLocation = tx.Origin?.Name,
            DestinationLocation = tx.Destination?.Name,
            CargoType = tx.Cargo?.Name,

            ToleranceExceeded = tx.ToleranceExceeded,
        };

        var stationId = tx.StationId ?? Guid.Empty;
        var pdfBytes = await _pdfService.GenerateCommercialWeightTicketAsync(result, stationId);
        var fileName = $"ticket-{tx.TicketNumber}.pdf";
        return (pdfBytes, fileName);
    }

    public async Task<List<PortalVehicleDto>> GetVehiclesAsync(Guid userId)
    {
        var transporter = await GetTransporterForUserAsync(userId);

        var vehicles = await _dbContext.Vehicles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(v => v.TransporterId == transporter.Id && v.IsActive)
            .Select(v => new PortalVehicleDto
            {
                Id = v.Id,
                RegNo = v.RegNo,
                Make = v.Make,
                Model = v.Model,
                VehicleType = v.VehicleType,
                DefaultTareWeightKg = v.DefaultTareWeightKg,
                LastTareWeightKg = v.LastTareWeightKg,
                LastTareWeighedAt = v.LastTareWeighedAt,
                TotalWeighings = _dbContext.WeighingTransactions
                    .IgnoreQueryFilters()
                    .Count(w => w.VehicleId == v.Id && w.TransporterId == transporter.Id),
                LastWeighedAt = _dbContext.WeighingTransactions
                    .IgnoreQueryFilters()
                    .Where(w => w.VehicleId == v.Id && w.TransporterId == transporter.Id)
                    .Max(w => (DateTime?)w.WeighedAt)
            })
            .ToListAsync();

        return vehicles;
    }

    public async Task<List<PortalVehicleWeightTrendDto>> GetVehicleWeightTrendsAsync(Guid userId, Guid vehicleId)
    {
        var transporter = await GetTransporterForUserAsync(userId);

        // Require vehicle_trends feature
        var limits = await ResolvePortalSubscriptionAsync(transporter);
        if (!limits.Features.VehicleTrends)
            throw new UnauthorizedAccessException("Vehicle weight trends require a Standard or Premium subscription.");

        var vehicleExists = await _dbContext.Vehicles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(v => v.Id == vehicleId && v.TransporterId == transporter.Id);

        if (!vehicleExists)
            throw new KeyNotFoundException($"Vehicle {vehicleId} not found for this transporter.");

        var trends = await _dbContext.WeighingTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(w => w.VehicleId == vehicleId &&
                        w.TransporterId == transporter.Id &&
                        w.WeighingMode == "commercial" &&
                        w.TareWeightKg.HasValue)
            .OrderByDescending(w => w.WeighedAt)
            .Take(100)
            .Select(w => new PortalVehicleWeightTrendDto
            {
                Date = w.WeighedAt,
                TareWeightKg = w.TareWeightKg,
                GrossWeightKg = w.GrossWeightKg,
                NetWeightKg = w.NetWeightKg,
                StationName = w.Station != null ? w.Station.Name : null
            })
            .ToListAsync();

        return trends;
    }

    public async Task<List<PortalDriverDto>> GetDriversAsync(Guid userId)
    {
        var transporter = await GetTransporterForUserAsync(userId);

        var directDriverIds = await _dbContext.Set<Models.Weighing.Driver>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(d => d.TransporterId == transporter.Id && d.IsActive)
            .Select(d => d.Id)
            .ToListAsync();

        var transactionDriverIds = await _dbContext.WeighingTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(w => w.TransporterId == transporter.Id && w.DriverId.HasValue)
            .Select(w => w.DriverId!.Value)
            .Distinct()
            .ToListAsync();

        var allDriverIds = directDriverIds.Union(transactionDriverIds).Distinct().ToList();

        if (!allDriverIds.Any())
            return new List<PortalDriverDto>();

        var drivers = await _dbContext.Set<Models.Weighing.Driver>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(d => allDriverIds.Contains(d.Id) && d.IsActive)
            .Select(d => new PortalDriverDto
            {
                Id = d.Id,
                FullName = d.FullNames + " " + d.Surname,
                DrivingLicenseNo = d.DrivingLicenseNo,
                PhoneNumber = d.PhoneNumber,
                LicenseStatus = d.LicenseStatus,
                LicenseExpiryDate = d.LicenseExpiryDate,
                TotalTrips = _dbContext.WeighingTransactions
                    .IgnoreQueryFilters()
                    .Count(w => w.DriverId == d.Id && w.TransporterId == transporter.Id)
            })
            .ToListAsync();

        return drivers;
    }

    public async Task<PortalDriverPerformanceDto> GetDriverPerformanceAsync(Guid userId, Guid driverId)
    {
        var transporter = await GetTransporterForUserAsync(userId);

        // Require driver_reports feature
        var limits = await ResolvePortalSubscriptionAsync(transporter);
        if (!limits.Features.DriverReports)
            throw new UnauthorizedAccessException("Driver performance reports require a Standard or Premium subscription.");

        var hasTrips = await _dbContext.WeighingTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(w => w.DriverId == driverId && w.TransporterId == transporter.Id);

        if (!hasTrips)
            throw new KeyNotFoundException($"Driver {driverId} not found for this transporter.");

        var driver = await _dbContext.Set<Models.Weighing.Driver>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == driverId)
            ?? throw new KeyNotFoundException($"Driver {driverId} not found.");

        var weighings = await _dbContext.WeighingTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(w => w.DriverId == driverId &&
                        w.TransporterId == transporter.Id &&
                        w.WeighingMode == "commercial")
            .Select(w => new
            {
                w.NetWeightKg,
                w.FirstWeightAt,
                w.SecondWeightAt
            })
            .ToListAsync();

        var tripCount = weighings.Count;
        var totalNetWeight = weighings.Where(w => w.NetWeightKg.HasValue).Sum(w => (long)w.NetWeightKg!.Value);
        var avgPayload = tripCount > 0
            ? weighings.Where(w => w.NetWeightKg.HasValue).Average(w => (double)w.NetWeightKg!.Value)
            : 0;

        var turnaroundMinutes = weighings
            .Where(w => w.FirstWeightAt.HasValue && w.SecondWeightAt.HasValue)
            .Select(w => (w.SecondWeightAt!.Value - w.FirstWeightAt!.Value).TotalMinutes)
            .ToList();

        var avgTurnaround = turnaroundMinutes.Any() ? turnaroundMinutes.Average() : 0;

        return new PortalDriverPerformanceDto
        {
            DriverId = driverId,
            DriverName = driver.FullNames + " " + driver.Surname,
            TripCount = tripCount,
            TotalNetWeightKg = totalNetWeight,
            AvgPayloadKg = avgPayload,
            AvgTurnaroundMinutes = avgTurnaround
        };
    }

    public async Task<PortalPagedResult<PortalConsignmentDto>> GetConsignmentsAsync(
        Guid userId, int page, int pageSize,
        DateTime? fromDate, DateTime? toDate)
    {
        var transporter = await GetTransporterForUserAsync(userId);

        // Require consignment_tracking feature
        var limits = await ResolvePortalSubscriptionAsync(transporter);
        if (!limits.Features.ConsignmentTracking)
            throw new UnauthorizedAccessException("Consignment tracking requires a Standard or Premium subscription.");

        var query = _dbContext.WeighingTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(w => w.TransporterId == transporter.Id &&
                        w.WeighingMode == "commercial" &&
                        w.ConsignmentNo != null);

        if (fromDate.HasValue)
            query = query.Where(w => w.WeighedAt >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(w => w.WeighedAt <= toDate.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(w => w.WeighedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(w => new PortalConsignmentDto
            {
                WeighingId = w.Id,
                TicketNumber = w.TicketNumber,
                ConsignmentNo = w.ConsignmentNo,
                OrderReference = w.OrderReference,
                VehicleRegNumber = w.VehicleRegNumber,
                CargoType = w.Cargo != null ? w.Cargo.Name : null,
                ExpectedNetWeightKg = w.ExpectedNetWeightKg,
                ActualNetWeightKg = w.NetWeightKg,
                WeightDiscrepancyKg = w.WeightDiscrepancyKg,
                SealNumbers = w.SealNumbers,
                SourceLocation = w.Origin != null ? w.Origin.Name : null,
                DestinationLocation = w.Destination != null ? w.Destination.Name : null,
                ControlStatus = w.ControlStatus,
                OrganizationName = w.Organization != null ? w.Organization.Name : string.Empty,
                WeighedAt = w.WeighedAt
            })
            .ToListAsync();

        return new PortalPagedResult<PortalConsignmentDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PortalSubscriptionDto> GetFeatureAccessAsync(Guid userId)
    {
        var transporter = await GetTransporterForUserAsync(userId);
        return await ResolvePortalSubscriptionAsync(transporter);
    }

    // ── Private helpers ──

    private async Task<Models.Weighing.Transporter> GetTransporterForUserAsync(Guid userId)
    {
        var transporter = await _dbContext.Transporters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.PortalAccountId == userId && t.IsActive);

        if (transporter == null)
            throw new InvalidOperationException("No transporter linked to this portal account. Please register first.");

        return transporter;
    }

    /// <summary>
    /// Resolves subscription limits for a transporter.
    /// Fails open: returns basic-tier defaults if the subscription service is unavailable.
    /// </summary>
    private async Task<PortalSubscriptionDto> ResolvePortalSubscriptionAsync(Models.Weighing.Transporter transporter)
    {
        string status = "NONE";
        string? planName = null;
        DateTime? expiresAt = null;
        SubscriptionFeatures features = new("NONE", null, []);

        var orgIds = await _dbContext.WeighingTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(w => w.TransporterId == transporter.Id)
            .Select(w => w.OrganizationId)
            .Distinct()
            .ToListAsync();

        if (orgIds.Any())
        {
            var primaryOrg = await _dbContext.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => orgIds.Contains(o.Id) && o.SsoTenantSlug != null);

            if (primaryOrg?.SsoTenantSlug != null)
            {
                try
                {
                    var sub = await _subscriptionService.GetTenantSubscriptionAsync(primaryOrg.SsoTenantSlug);
                    status = sub.Status;
                    planName = sub.PlanName;
                    expiresAt = sub.ExpiresAt;

                    features = await _subscriptionService.GetFeaturesAsync(primaryOrg.SsoTenantSlug);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch subscription for transporter {TransporterId} — failing open", transporter.Id);
                    status = "ACTIVE";
                    features = new SubscriptionFeatures("ACTIVE", null,
                        ["portal_access", "ticket_download", "email_notifications"]);
                }
            }
        }

        var (tier, historyMonths, maxVehicles, maxDrivers) = DeriveTierLimits(planName);

        return new PortalSubscriptionDto
        {
            Status = status,
            PlanName = planName,
            Tier = tier,
            HistoryMonths = historyMonths,
            MaxVehicles = maxVehicles,
            MaxDrivers = maxDrivers,
            ExpiresAt = expiresAt,
            Features = new PortalFeatureAccess
            {
                MultiSiteAccess     = features.Has("multi_site_access"),
                DataExport          = features.Has("data_export"),
                DriverReports       = features.Has("driver_reports"),
                VehicleTrends       = features.Has("vehicle_trends"),
                ApiAccess           = features.Has("api_access"),
                Analytics           = features.Has("analytics"),
                ConsignmentTracking = features.Has("consignment_tracking"),
            }
        };
    }

    private static (string tier, int historyMonths, int maxVehicles, int maxDrivers) DeriveTierLimits(string? planName)
    {
        var plan = (planName ?? string.Empty).ToLowerInvariant();
        if (plan.Contains("premium"))
            return ("premium", 24, -1, -1);
        if (plan.Contains("standard"))
            return ("standard", 12, 50, 25);
        return ("basic", 3, 10, 5);
    }
}
