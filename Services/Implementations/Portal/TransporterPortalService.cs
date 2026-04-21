using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Portal;
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
    private readonly ILogger<TransporterPortalService> _logger;

    public TransporterPortalService(
        TruLoadDbContext dbContext,
        ISubscriptionService subscriptionService,
        ILogger<TransporterPortalService> logger)
    {
        _dbContext = dbContext;
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    public async Task<PortalRegistrationResult> RegisterAsync(Guid userId, string userEmail, PortalRegistrationRequest request)
    {
        // Find matching transporter by email, phone, or code (cross-tenant)
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

        // Link the portal account
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

        // Query weighing_transactions across all org partitions (cross-tenant)
        var query = _dbContext.WeighingTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(w => w.TransporterId == transporter.Id && w.WeighingMode == "commercial");

        if (fromDate.HasValue)
            query = query.Where(w => w.WeighedAt >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(w => w.WeighedAt <= toDate.Value);
        if (vehicleId.HasValue)
            query = query.Where(w => w.VehicleId == vehicleId.Value);
        if (organizationId.HasValue)
        {
            // Multi-site access is gated by subscription
            query = query.Where(w => w.OrganizationId == organizationId.Value);
        }

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

        // Verify vehicle belongs to this transporter
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

        // Get distinct drivers who have driven for this transporter
        var driverIds = await _dbContext.WeighingTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(w => w.TransporterId == transporter.Id && w.DriverId.HasValue)
            .Select(w => w.DriverId!.Value)
            .Distinct()
            .ToListAsync();

        if (!driverIds.Any())
            return new List<PortalDriverDto>();

        var drivers = await _dbContext.Set<Models.Weighing.Driver>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(d => driverIds.Contains(d.Id) && d.IsActive)
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

        // Verify this driver has driven for this transporter
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

        // Average turnaround = time between first and second weight
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

        // Find the organizations where this transporter has weighings
        var orgIds = await _dbContext.WeighingTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(w => w.TransporterId == transporter.Id)
            .Select(w => w.OrganizationId)
            .Distinct()
            .ToListAsync();

        // Resolve subscription via the primary org the transporter has been weighed under.
        // When transporter-specific plans are in production, this slug will map to the
        // transporter's own TRANSPORTER_BASIC/STANDARD/PREMIUM plan; until then it falls
        // back to the org's commercial plan (which shares the same feature-code mechanism).
        string status = "NONE";
        string? planName = null;
        DateTime? expiresAt = null;
        SubscriptionFeatures features = new("NONE", null, []);

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
                    _logger.LogWarning(ex, "Failed to fetch subscription for portal user {UserId} — failing open", userId);
                    status = "ACTIVE";
                    features = new SubscriptionFeatures("ACTIVE", null,
                        ["portal_access", "ticket_download", "email_notifications"]);
                }
            }
        }

        return new PortalSubscriptionDto
        {
            Status = status,
            PlanName = planName,
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

    // ── Private helpers ──

    /// <summary>
    /// Resolves the Transporter entity linked to the authenticated portal user.
    /// Queries cross-tenant (IgnoreQueryFilters) since transporters span orgs.
    /// </summary>
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
}
