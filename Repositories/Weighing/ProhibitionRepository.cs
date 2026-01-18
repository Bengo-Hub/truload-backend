using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Weighing;

namespace TruLoad.Backend.Data.Repositories.Weighing;

public class ProhibitionRepository : IProhibitionRepository
{
    private readonly TruLoadDbContext _context;

    public ProhibitionRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<ProhibitionOrder?> GetByWeighingIdAsync(Guid weighingId)
    {
        return await _context.ProhibitionOrders
            .AsNoTracking()
            .Include(p => p.Weighing)
            .Include(p => p.IssuedBy)
            .FirstOrDefaultAsync(p => p.WeighingId == weighingId);
    }

    public async Task<ProhibitionOrder> CreateAsync(ProhibitionOrder order)
    {
        _context.ProhibitionOrders.Add(order);
        await _context.SaveChangesAsync();
        return order;
    }

    public async Task UpdateAsync(ProhibitionOrder order)
    {
        _context.ProhibitionOrders.Entry(order).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task<string> GenerateProhibitionNumberAsync()
    {
        // Simple sequence-based generation for MVP: PROH-YYYYMM-XXXX
        var prefix = $"PROH-{DateTime.UtcNow:yyyyMM}";
        var count = await _context.ProhibitionOrders
            .CountAsync(p => p.ProhibitionNo.StartsWith(prefix));
        
        return $"{prefix}-{(count + 1):D4}";
    }
}
