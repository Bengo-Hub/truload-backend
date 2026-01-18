using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Infrastructure;

namespace TruLoad.Backend.Data.Repositories.Infrastructure;

public class DocumentRepository : IDocumentRepository
{
    private readonly TruLoadDbContext _context;

    public DocumentRepository(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<Document> CreateAsync(Document document)
    {
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();
        return document;
    }

    public async Task<Document?> GetByIdAsync(Guid id)
    {
        return await _context.Documents
            .AsNoTracking()
            .Include(d => d.UploadedBy)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<IEnumerable<Document>> GetByRelatedEntityAsync(string entityType, Guid entityId)
    {
        return await _context.Documents
            .AsNoTracking()
            .Where(d => d.RelatedEntityType == entityType && d.RelatedEntityId == entityId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }
}
