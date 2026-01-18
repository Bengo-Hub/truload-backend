using TruLoad.Backend.Models.Infrastructure;

namespace TruLoad.Backend.Data.Repositories.Infrastructure;

public interface IDocumentRepository
{
    Task<Document> CreateAsync(Document document);
    Task<Document?> GetByIdAsync(Guid id);
    Task<IEnumerable<Document>> GetByRelatedEntityAsync(string entityType, Guid entityId);
}
