using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Data.Repositories.Weighing;

public class WeighingRepository : IWeighingRepository
{
    private readonly TruLoadDbContext _context;

    public WeighingRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<WeighingTransaction> CreateTransactionAsync(WeighingTransaction transaction)
    {
        _context.WeighingTransactions.Add(transaction);
        await _context.SaveChangesAsync();
        return transaction;
    }

    public async Task<WeighingTransaction?> GetTransactionByIdAsync(Guid id)
    {
        return await _context.WeighingTransactions
            .AsNoTracking()
            .Include(t => t.WeighingAxles)
                .ThenInclude(wa => wa.AxleConfiguration)
            .Include(t => t.WeighingAxles)
                .ThenInclude(wa => wa.AxleGroup)
            .Include(t => t.WeighingAxles)
                .ThenInclude(wa => wa.TyreType)
            .Include(t => t.Vehicle)
            .Include(t => t.Driver)
            .Include(t => t.Transporter)
            .Include(t => t.Station)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<WeighingTransaction?> GetTransactionByTicketNumberAsync(string ticketNumber)
    {
        return await _context.WeighingTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TicketNumber == ticketNumber);
    }

    public async Task<WeighingTransaction> UpdateTransactionAsync(WeighingTransaction transaction)
    {
        _context.WeighingTransactions.Update(transaction);
        await _context.SaveChangesAsync();
        return transaction;
    }

    public async Task DeleteTransactionAsync(Guid id)
    {
        var transaction = await _context.WeighingTransactions.FindAsync(id);
        if (transaction != null)
        {
            _context.WeighingTransactions.Remove(transaction);
            await _context.SaveChangesAsync();
        }
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
        var query = _context.WeighingTransactions
            .AsNoTracking()
            .Include(t => t.WeighingAxles)
            .Include(t => t.Vehicle)
                .ThenInclude(v => v!.AxleConfiguration)
            .Include(t => t.Driver)
            .Include(t => t.Transporter)
            .Include(t => t.Station)
            .Include(t => t.Origin)
            .Include(t => t.Destination)
            .Include(t => t.Cargo)
            .AsQueryable();

        // Apply filters
        if (stationId.HasValue)
            query = query.Where(t => t.StationId == stationId.Value);

        if (!string.IsNullOrWhiteSpace(vehicleRegNo))
            query = query.Where(t => t.VehicleRegNumber.Contains(vehicleRegNo));

        if (fromDate.HasValue)
            query = query.Where(t => t.WeighedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(t => t.WeighedAt <= toDate.Value);

        if (!string.IsNullOrWhiteSpace(controlStatus))
            query = query.Where(t => t.ControlStatus == controlStatus);

        if (isCompliant.HasValue)
            query = query.Where(t => t.IsCompliant == isCompliant.Value);

        if (operatorId.HasValue)
            query = query.Where(t => t.WeighedByUserId == operatorId.Value);

        if (!string.IsNullOrWhiteSpace(weighingType))
            query = query.Where(t => t.WeighingType == weighingType);

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting
        query = sortBy.ToLower() switch
        {
            "ticketnumber" => sortOrder.ToLower() == "asc"
                ? query.OrderBy(t => t.TicketNumber)
                : query.OrderByDescending(t => t.TicketNumber),
            "vehicleregnumber" => sortOrder.ToLower() == "asc"
                ? query.OrderBy(t => t.VehicleRegNumber)
                : query.OrderByDescending(t => t.VehicleRegNumber),
            "gvwmeasuredkg" => sortOrder.ToLower() == "asc"
                ? query.OrderBy(t => t.GvwMeasuredKg)
                : query.OrderByDescending(t => t.GvwMeasuredKg),
            "overloadkg" => sortOrder.ToLower() == "asc"
                ? query.OrderBy(t => t.OverloadKg)
                : query.OrderByDescending(t => t.OverloadKg),
            "controlstatus" => sortOrder.ToLower() == "asc"
                ? query.OrderBy(t => t.ControlStatus)
                : query.OrderByDescending(t => t.ControlStatus),
            _ => sortOrder.ToLower() == "asc"
                ? query.OrderBy(t => t.WeighedAt)
                : query.OrderByDescending(t => t.WeighedAt)
        };

        // Apply pagination
        var items = await query
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(List<WeighingTransaction> Items, int TotalCount)> SearchTransactionsLightAsync(
        Guid? stationId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int take = 10000)
    {
        var query = _context.WeighingTransactions
            .AsNoTracking()
            .AsQueryable();

        if (stationId.HasValue)
            query = query.Where(t => t.StationId == stationId.Value);

        if (fromDate.HasValue)
            query = query.Where(t => t.WeighedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(t => t.WeighedAt <= toDate.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(t => t.WeighedAt)
            .Take(take)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<WeighingTransaction?> GetByClientLocalIdAsync(string clientLocalId)
    {
        if (string.IsNullOrWhiteSpace(clientLocalId))
            return null;

        // ClientLocalId is stored as Guid, so try to parse
        if (!Guid.TryParse(clientLocalId, out var parsedId))
            return null;

        return await _context.WeighingTransactions
            .AsNoTracking()
            .Include(t => t.WeighingAxles)
            .Include(t => t.Vehicle)
            .Include(t => t.Driver)
            .FirstOrDefaultAsync(t => t.ClientLocalId == parsedId);
    }

    public async Task<WeighingTransaction?> GetLatestAutoweighByVehicleAsync(
        string vehicleRegNumber,
        Guid stationId,
        string? bound = null)
    {
        var normalizedRegNumber = vehicleRegNumber.ToUpperInvariant().Trim();
        var today = DateTime.UtcNow.Date;

        var query = _context.WeighingTransactions
            .Include(t => t.WeighingAxles)
            .Where(t => t.VehicleRegNumber.ToUpper() == normalizedRegNumber)
            .Where(t => t.StationId == stationId)
            .Where(t => t.CaptureStatus == "auto")
            .Where(t => t.WeighedAt >= today); // Only look at today's transactions

        if (!string.IsNullOrEmpty(bound))
        {
            query = query.Where(t => t.Bound == bound);
        }

        return await query
            .OrderByDescending(t => t.WeighedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<int> MarkPendingAsNotWeighedAsync(
        string? vehicleRegNumber,
        Guid stationId,
        Guid? excludeTransactionId = null)
    {
        var today = DateTime.UtcNow.Date;

        var query = _context.WeighingTransactions
            .Where(t => t.StationId == stationId)
            .Where(t => t.CaptureStatus == "auto")
            .Where(t => t.WeighedAt >= today);

        if (!string.IsNullOrEmpty(vehicleRegNumber))
        {
            var normalizedRegNumber = vehicleRegNumber.ToUpperInvariant().Trim();
            query = query.Where(t => t.VehicleRegNumber.ToUpper() == normalizedRegNumber);
        }

        if (excludeTransactionId.HasValue)
        {
            query = query.Where(t => t.Id != excludeTransactionId.Value);
        }

        var pendingTransactions = await query.ToListAsync();

        foreach (var transaction in pendingTransactions)
        {
            transaction.CaptureStatus = "not_weighed";
            transaction.SyncAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return pendingTransactions.Count;
    }
}
