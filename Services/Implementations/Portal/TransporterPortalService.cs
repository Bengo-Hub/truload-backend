using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Portal;
using TruLoad.Backend.DTOs.Weighing;
using TruLoad.Backend.Models.Portal;
using TruLoad.Backend.Services.Interfaces.Infrastructure;
using TruLoad.Backend.Services.Interfaces.Portal;
using TruLoad.Backend.Services.Interfaces.Shared;
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
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TransporterPortalService> _logger;

    public TransporterPortalService(
        TruLoadDbContext dbContext,
        ISubscriptionService subscriptionService,
        IPdfService pdfService,
        INotificationService notificationService,
        IConfiguration configuration,
        ILogger<TransporterPortalService> logger)
    {
        _dbContext = dbContext;
        _subscriptionService = subscriptionService;
        _pdfService = pdfService;
        _notificationService = notificationService;
        _configuration = configuration;
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

    public async Task<List<PortalTeamMemberDto>> GetTeamMembersAsync(Guid userId)
    {
        var transporter = await GetTransporterForUserAsync(userId);

        // Owner entry
        var ownerEntry = new PortalTeamMemberDto
        {
            UserId = transporter.PortalAccountId ?? Guid.Empty,
            UserEmail = transporter.PortalAccountEmail ?? string.Empty,
            UserName = transporter.Name,
            Role = "admin",
            JoinedAt = transporter.CreatedAt,
            IsOwner = true
        };

        var members = await _dbContext.PortalTeamMemberships
            .AsNoTracking()
            .Where(m => m.TransporterId == transporter.Id && m.IsActive)
            .Select(m => new PortalTeamMemberDto
            {
                UserId = m.UserId,
                UserEmail = m.UserEmail,
                UserName = m.UserName,
                Role = m.Role,
                JoinedAt = m.CreatedAt,
                IsOwner = false
            })
            .ToListAsync();

        var result = new List<PortalTeamMemberDto> { ownerEntry };
        result.AddRange(members);
        return result;
    }

    public async Task<(bool Success, string Message)> InviteTeamMemberAsync(
        Guid userId, string userEmail, string userName, InviteTeamMemberRequest request)
    {
        // Only the transporter owner can invite
        var transporter = await _dbContext.Transporters
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.PortalAccountId == userId && t.IsActive);

        if (transporter == null)
            return (false, "Only the portal owner (admin) can invite team members.");

        var role = request.Role.ToLowerInvariant();
        if (role != "manager" && role != "viewer")
            return (false, "Role must be 'manager' or 'viewer'.");

        // Check for existing active membership
        var existingMembership = await _dbContext.PortalTeamMemberships
            .AnyAsync(m => m.TransporterId == transporter.Id &&
                           m.UserEmail == request.Email &&
                           m.IsActive);

        if (existingMembership)
            return (false, $"{request.Email} is already an active team member.");

        // Check for pending (non-expired, non-revoked, non-accepted) invitation
        var existingInvite = await _dbContext.PortalTeamInvitations
            .AnyAsync(i => i.TransporterId == transporter.Id &&
                           i.InvitedEmail == request.Email &&
                           !i.IsRevoked &&
                           i.AcceptedAt == null &&
                           i.ExpiresAt > DateTime.UtcNow);

        if (existingInvite)
            return (false, $"A pending invitation already exists for {request.Email}.");

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        var invitation = new PortalTeamInvitation
        {
            Id = Guid.NewGuid(),
            TransporterId = transporter.Id,
            InvitedEmail = request.Email,
            Role = role,
            Token = token,
            CreatedByUserId = userId,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.PortalTeamInvitations.Add(invitation);
        await _dbContext.SaveChangesAsync();

        // Send invitation email (fire-and-forget style; don't fail the invite if email fails)
        try
        {
            await _notificationService.SendEmailAsync(
                "truload/portal_team_invite",
                request.Email,
                request.Email,
                new Dictionary<string, object>
                {
                    ["transporter_name"] = transporter.Name,
                    ["role"] = role,
                    ["invite_url"] = $"{(_configuration["FrontendUrl"]?.TrimEnd('/') ?? "https://truload.codevertexitsolutions.com")}/portal/invite/accept?token={token}",
                    ["expires_at"] = expiresAt.ToString("yyyy-MM-dd")
                },
                subject: $"You've been invited to {transporter.Name}'s TruLoad Portal",
                cancellationToken: CancellationToken.None,
                tenantSlug: null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send invitation email to {Email} for transporter {TransporterId}",
                request.Email, transporter.Id);
        }

        _logger.LogInformation(
            "Portal team invitation sent: TransporterId={TransporterId}, Email={Email}, Role={Role}, InvitedBy={UserId}",
            transporter.Id, request.Email, role, userId);

        return (true, $"Invitation sent to {request.Email}.");
    }

    public async Task<(bool Success, string Message)> RemoveTeamMemberAsync(Guid userId, Guid targetUserId)
    {
        // Only the transporter owner can remove members
        var transporter = await _dbContext.Transporters
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.PortalAccountId == userId && t.IsActive);

        if (transporter == null)
            return (false, "Only the portal owner (admin) can remove team members.");

        if (targetUserId == userId)
            return (false, "You cannot remove yourself as the portal owner.");

        var membership = await _dbContext.PortalTeamMemberships
            .FirstOrDefaultAsync(m => m.TransporterId == transporter.Id &&
                                      m.UserId == targetUserId &&
                                      m.IsActive);

        if (membership == null)
            return (false, "Team member not found.");

        membership.IsActive = false;
        membership.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Portal team member removed: TransporterId={TransporterId}, TargetUserId={TargetUserId}, RemovedBy={UserId}",
            transporter.Id, targetUserId, userId);

        return (true, "Team member removed successfully.");
    }

    public async Task<(bool Success, string Message)> AcceptInviteAsync(
        Guid userId, string userEmail, AcceptPortalInviteRequest request)
    {
        var invitation = await _dbContext.PortalTeamInvitations
            .Include(i => i.Transporter)
            .FirstOrDefaultAsync(i => i.Token == request.Token &&
                                      !i.IsRevoked &&
                                      i.AcceptedAt == null &&
                                      i.ExpiresAt > DateTime.UtcNow);

        if (invitation == null)
            return (false, "Invitation not found, expired, or already used.");

        if (invitation.Transporter == null || !invitation.Transporter.IsActive)
            return (false, "The transporter for this invitation is no longer active.");

        // Upsert membership
        var existing = await _dbContext.PortalTeamMemberships
            .FirstOrDefaultAsync(m => m.TransporterId == invitation.TransporterId && m.UserId == userId);

        if (existing != null)
        {
            existing.IsActive = true;
            existing.Role = invitation.Role;
            existing.UserEmail = userEmail;
            existing.UserName = request.UserName;
            existing.InvitedByUserId = invitation.CreatedByUserId;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var membership = new PortalTeamMembership
            {
                Id = Guid.NewGuid(),
                TransporterId = invitation.TransporterId,
                UserId = userId,
                UserEmail = userEmail,
                UserName = request.UserName,
                Role = invitation.Role,
                InvitedByUserId = invitation.CreatedByUserId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.PortalTeamMemberships.Add(membership);
        }

        invitation.AcceptedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Portal invitation accepted: TransporterId={TransporterId}, UserId={UserId}, Role={Role}",
            invitation.TransporterId, userId, invitation.Role);

        return (true, "Invitation accepted. You now have access to the portal.");
    }

    public async Task<int> CountBulkDownloadTicketsAsync(
        Guid userId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken)
    {
        var transporter = await GetTransporterForUserAsync(userId);
        return await _dbContext.WeighingTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(w => w.TransporterId == transporter.Id &&
                        w.WeighingMode == "commercial" &&
                        w.ControlStatus == "Complete" &&
                        w.WeighedAt >= fromDate &&
                        w.WeighedAt <= toDate)
            .CountAsync(cancellationToken);
    }

    public async Task<(byte[] Bytes, string FileName)> BulkDownloadTicketsAsync(
        Guid userId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken)
    {
        var transporter = await GetTransporterForUserAsync(userId);
        var limits = await ResolvePortalSubscriptionAsync(transporter);

        if (!limits.Features.DataExport)
            throw new UnauthorizedAccessException("Bulk ticket download requires a Standard or Premium subscription with the data export feature.");

        const int maxTransactions = 500;

        var transactions = await _dbContext.WeighingTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(w => w.Vehicle)
            .Include(w => w.Driver)
            .Include(w => w.Organization)
            .Include(w => w.Station)
            .Include(w => w.Cargo)
            .Include(w => w.Origin)
            .Include(w => w.Destination)
            .Where(w => w.TransporterId == transporter.Id &&
                        w.WeighingMode == "commercial" &&
                        w.ControlStatus == "Complete" &&
                        w.WeighedAt >= fromDate &&
                        w.WeighedAt <= toDate)
            .OrderByDescending(w => w.WeighedAt)
            .Take(maxTransactions)
            .ToListAsync(cancellationToken);

        if (!transactions.Any())
            throw new InvalidOperationException("No completed commercial weighing transactions found in the specified date range.");

        var pdfs = new List<(string TicketNo, byte[] PdfBytes)>();

        foreach (var tx in transactions)
        {
            try
            {
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
                pdfs.Add((tx.TicketNumber, pdfBytes));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate PDF for ticket {TicketNumber} during bulk download", tx.TicketNumber);
            }
        }

        if (!pdfs.Any())
            throw new InvalidOperationException("Failed to generate any ticket PDFs for the specified date range.");

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (ticketNo, pdfBytes) in pdfs)
            {
                var entry = archive.CreateEntry($"{ticketNo}.pdf");
                using var entryStream = entry.Open();
                await entryStream.WriteAsync(pdfBytes, cancellationToken);
            }
        }
        ms.Position = 0;
        var zipBytes = ms.ToArray();
        var fileName = $"tickets_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.zip";
        return (zipBytes, fileName);
    }

    public async Task<(int Imported, int Skipped, List<string> Errors)> ImportVehiclesAsync(
        Guid userId, IFormFile file, CancellationToken cancellationToken)
    {
        var transporter = await GetTransporterForUserAsync(userId);
        var limits = await ResolvePortalSubscriptionAsync(transporter);

        int imported = 0;
        int skipped = 0;
        var errors = new List<string>();

        using var reader = new StreamReader(file.OpenReadStream());

        // Skip header line
        var header = await reader.ReadLineAsync(cancellationToken);
        if (header == null)
            return (0, 0, new List<string> { "File is empty." });

        // Get current vehicle count for limit check
        var currentVehicleCount = await _dbContext.Vehicles
            .IgnoreQueryFilters()
            .CountAsync(v => v.TransporterId == transporter.Id && v.IsActive, cancellationToken);

        int rowIndex = 1;
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            rowIndex++;

            if (string.IsNullOrWhiteSpace(line)) continue;

            var columns = line.Split(',');

            // Parse registration (required)
            var registration = columns.Length > 0 ? columns[0].Trim().Trim('"') : string.Empty;
            if (string.IsNullOrWhiteSpace(registration))
            {
                errors.Add($"Row {rowIndex}: registration is required");
                skipped++;
                continue;
            }
            if (registration.Length > 20)
            {
                errors.Add($"Row {rowIndex}: registration '{registration}' exceeds 20 characters");
                skipped++;
                continue;
            }

            // Check vehicle limit
            if (limits.MaxVehicles != -1 && currentVehicleCount + imported >= limits.MaxVehicles)
            {
                errors.Add($"Row {rowIndex}: vehicle limit ({limits.MaxVehicles}) reached — upgrade your subscription to import more");
                skipped++;
                continue;
            }

            var make = columns.Length > 1 ? columns[1].Trim().Trim('"') : null;
            var model = columns.Length > 2 ? columns[2].Trim().Trim('"') : null;
            // axle_count (column 4) is not a Vehicle field we persist directly
            int? tareWeightKg = null;
            if (columns.Length > 4 && !string.IsNullOrWhiteSpace(columns[4]))
            {
                if (int.TryParse(columns[4].Trim().Trim('"'), out var tw))
                    tareWeightKg = tw;
                else
                {
                    errors.Add($"Row {rowIndex}: tare_weight_kg '{columns[4].Trim()}' is not a valid integer");
                    skipped++;
                    continue;
                }
            }

            // Check if vehicle already linked to this transporter
            var existingForTransporter = await _dbContext.Vehicles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(v => v.TransporterId == transporter.Id &&
                               v.RegNo == registration, cancellationToken);

            if (existingForTransporter)
            {
                skipped++;
                continue;
            }

            // Look for an existing vehicle record across all orgs
            var existingVehicle = await _dbContext.Vehicles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(v => v.RegNo == registration, cancellationToken);

            if (existingVehicle != null)
            {
                // Re-link to this transporter
                existingVehicle.TransporterId = transporter.Id;
                if (!string.IsNullOrWhiteSpace(make)) existingVehicle.Make = make;
                if (!string.IsNullOrWhiteSpace(model)) existingVehicle.Model = model;
                if (tareWeightKg.HasValue) existingVehicle.DefaultTareWeightKg = tareWeightKg;
                existingVehicle.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Create a new vehicle record
                var newVehicle = new Models.Weighing.Vehicle
                {
                    RegNo = registration,
                    Make = string.IsNullOrWhiteSpace(make) ? null : make,
                    Model = string.IsNullOrWhiteSpace(model) ? null : model,
                    DefaultTareWeightKg = tareWeightKg,
                    TransporterId = transporter.Id,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                _dbContext.Vehicles.Add(newVehicle);
            }

            imported++;
        }

        if (imported > 0)
            await _dbContext.SaveChangesAsync(cancellationToken);

        return (imported, skipped, errors);
    }

    // ── Private helpers ──

    private async Task<Models.Weighing.Transporter> GetTransporterForUserAsync(Guid userId)
    {
        // Owner lookup
        var transporter = await _dbContext.Transporters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.PortalAccountId == userId && t.IsActive);

        if (transporter != null) return transporter;

        // Team member lookup
        var membership = await _dbContext.PortalTeamMemberships
            .Include(m => m.Transporter)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.IsActive && m.Transporter!.IsActive);

        if (membership?.Transporter != null) return membership.Transporter;

        throw new InvalidOperationException("No transporter linked to this portal account. Please register first.");
    }

    /// <summary>
    /// Returns "admin"|"manager"|"viewer"|null for the user on the given transporter.
    /// Owner = "admin".
    /// </summary>
    private async Task<string?> GetTeamRoleAsync(Guid userId, Guid transporterId)
    {
        var transporter = await _dbContext.Transporters
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == transporterId);

        if (transporter?.PortalAccountId == userId)
            return "admin";

        var membership = await _dbContext.PortalTeamMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.TransporterId == transporterId && m.IsActive);

        return membership?.Role;
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
