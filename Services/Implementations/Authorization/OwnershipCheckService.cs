using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Services.Interfaces.Authorization;

namespace TruLoad.Backend.Services.Implementations.Authorization;

/// <summary>
/// Implementation of resource ownership verification service.
/// Checks if users have ownership rights to specific resources based on their assignments.
/// </summary>
public class OwnershipCheckService : IOwnershipCheckService
{
    private readonly TruLoadDbContext _context;
    private readonly ILogger<OwnershipCheckService> _logger;

    public OwnershipCheckService(
        TruLoadDbContext context,
        ILogger<OwnershipCheckService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> UserOwnsWeighingTransactionAsync(
        Guid userId,
        Guid weighingTransactionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var transaction = await _context.WeighingTransactions
                .AsNoTracking()
                .Where(wt => wt.Id == weighingTransactionId)
                .Select(wt => wt.WeighedByUserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (transaction == Guid.Empty)
            {
                _logger.LogWarning(
                    "Weighing transaction not found for ownership check: TransactionId={TransactionId}",
                    weighingTransactionId);
                return false;
            }

            return transaction == userId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error checking weighing transaction ownership: UserId={UserId}, TransactionId={TransactionId}",
                userId, weighingTransactionId);
            return false;
        }
    }

    public async Task<bool> UserBelongsToWeighingStationAsync(
        Guid userId,
        Guid weighingTransactionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stationId = await _context.WeighingTransactions
                .AsNoTracking()
                .Where(wt => wt.Id == weighingTransactionId)
                .Select(wt => wt.StationId)
                .FirstOrDefaultAsync(cancellationToken);

            if (stationId == null || stationId == Guid.Empty)
            {
                _logger.LogWarning(
                    "Weighing transaction not found for station check: TransactionId={TransactionId}",
                    weighingTransactionId);
                return false;
            }

            return await UserBelongsToStationAsync(userId, stationId.Value, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error checking weighing station ownership: UserId={UserId}, TransactionId={TransactionId}",
                userId, weighingTransactionId);
            return false;
        }
    }

    public async Task<bool> UserOwnsCaseRegisterAsync(
        Guid userId,
        Guid caseRegisterId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var caseOwnerId = await _context.CaseRegisters
                .AsNoTracking()
                .Where(cr => cr.Id == caseRegisterId)
                .Select(cr => cr.CreatedById)
                .FirstOrDefaultAsync(cancellationToken);

            if (caseOwnerId == null || caseOwnerId == Guid.Empty)
            {
                _logger.LogWarning(
                    "Case register not found or has no owner for ownership check: CaseRegisterId={CaseRegisterId}",
                    caseRegisterId);
                return false;
            }

            return caseOwnerId == userId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error checking case register ownership: UserId={UserId}, CaseRegisterId={CaseRegisterId}",
                userId, caseRegisterId);
            return false;
        }
    }

    public async Task<bool> UserOwnsSpecialReleaseAsync(
        Guid userId,
        Guid specialReleaseId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Special releases are owned via the associated case register
            var caseRegisterId = await _context.SpecialReleases
                .AsNoTracking()
                .Where(sr => sr.Id == specialReleaseId)
                .Select(sr => sr.CaseRegisterId)
                .FirstOrDefaultAsync(cancellationToken);

            if (caseRegisterId == Guid.Empty)
            {
                _logger.LogWarning(
                    "Special release not found for ownership check: SpecialReleaseId={SpecialReleaseId}",
                    specialReleaseId);
                return false;
            }

            // Check ownership via the case register
            return await UserOwnsCaseRegisterAsync(userId, caseRegisterId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error checking special release ownership: UserId={UserId}, SpecialReleaseId={SpecialReleaseId}",
                userId, specialReleaseId);
            return false;
        }
    }

    public async Task<bool> UserBelongsToStationAsync(
        Guid userId,
        Guid stationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if user is directly assigned to the station
            var userStation = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.StationId)
                .FirstOrDefaultAsync(cancellationToken);

            return userStation == stationId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error checking station membership: UserId={UserId}, StationId={StationId}",
                userId, stationId);
            return false;
        }
    }

    public async Task<bool> UserBelongsToDepartmentAsync(
        Guid userId,
        Guid departmentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if user is directly assigned to the department
            var userDepartment = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.DepartmentId)
                .FirstOrDefaultAsync(cancellationToken);

            return userDepartment == departmentId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error checking department membership: UserId={UserId}, DepartmentId={DepartmentId}",
                userId, departmentId);
            return false;
        }
    }

    public async Task<bool> UserBelongsToOrganizationAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if user belongs to the organization (via station -> organization)
            var userOrganization = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Include(u => u.Station)
                .Select(u => u.Station!.OrganizationId)
                .FirstOrDefaultAsync(cancellationToken);

            return userOrganization == organizationId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error checking organization membership: UserId={UserId}, OrganizationId={OrganizationId}",
                userId, organizationId);
            return false;
        }
    }
}
