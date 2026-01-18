using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Common;

namespace TruLoad.Backend.Repositories.Common;

/// <summary>
/// Base repository providing common CRUD operations and patterns for all repositories.
/// Implements soft-delete, active filtering, code lookups, and standard query patterns.
/// </summary>
/// <typeparam name="TEntity">Entity type that inherits from BaseEntity</typeparam>
public abstract class BaseRepository<TEntity> where TEntity : BaseEntity
{
    protected readonly TruLoadDbContext _context;
    protected readonly DbSet<TEntity> _dbSet;
    protected readonly ILogger _logger;

    protected BaseRepository(TruLoadDbContext context, ILogger logger)
    {
        _context = context;
        _dbSet = context.Set<TEntity>();
        _logger = logger;
    }

    /// <summary>
    /// Gets an entity by its ID, optionally including inactive entities.
    /// </summary>
    public virtual async Task<TEntity?> GetByIdAsync(
        Guid id,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(e => e.IsActive);
        }

        query = query.Where(e => e.DeletedAt == null);

        return await query.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    /// <summary>
    /// Gets all entities with optional filtering by active status.
    /// </summary>
    public virtual async Task<List<TEntity>> GetAllAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(e => e.IsActive);
        }

        query = query.Where(e => e.DeletedAt == null);

        return await query.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Creates a new entity in the database.
    /// </summary>
    public virtual async Task<TEntity> CreateAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbSet.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "{EntityType} created with ID: {Id}",
            typeof(TEntity).Name,
            entity.Id);

        return entity;
    }

    /// <summary>
    /// Updates an existing entity in the database.
    /// </summary>
    public virtual async Task<TEntity> UpdateAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;

        _dbSet.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "{EntityType} updated with ID: {Id}",
            typeof(TEntity).Name,
            entity.Id);

        return entity;
    }

    /// <summary>
    /// Soft-deletes an entity by setting DeletedAt and IsActive flags.
    /// </summary>
    public virtual async Task<bool> SoftDeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbSet.FindAsync(new object[] { id }, cancellationToken);

        if (entity == null)
        {
            _logger.LogWarning(
                "{EntityType} not found for soft-delete: {Id}",
                typeof(TEntity).Name,
                id);
            return false;
        }

        entity.IsActive = false;
        entity.DeletedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "{EntityType} soft-deleted: {Id}",
            typeof(TEntity).Name,
            id);

        return true;
    }

    /// <summary>
    /// Hard-deletes an entity permanently from the database.
    /// Use with caution - prefer SoftDeleteAsync for most cases.
    /// </summary>
    public virtual async Task<bool> HardDeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbSet.FindAsync(new object[] { id }, cancellationToken);

        if (entity == null)
        {
            _logger.LogWarning(
                "{EntityType} not found for hard-delete: {Id}",
                typeof(TEntity).Name,
                id);
            return false;
        }

        _dbSet.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "{EntityType} PERMANENTLY DELETED: {Id}",
            typeof(TEntity).Name,
            id);

        return true;
    }

    /// <summary>
    /// Checks if an entity exists by ID.
    /// </summary>
    public virtual async Task<bool> ExistsAsync(
        Guid id,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(e => e.IsActive);
        }

        query = query.Where(e => e.DeletedAt == null);

        return await query.AnyAsync(e => e.Id == id, cancellationToken);
    }

    /// <summary>
    /// Gets the total count of entities, optionally including inactive.
    /// </summary>
    public virtual async Task<int> CountAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(e => e.IsActive);
        }

        query = query.Where(e => e.DeletedAt == null);

        return await query.CountAsync(cancellationToken);
    }

    /// <summary>
    /// Applies common filtering to a query (IsActive and DeletedAt).
    /// </summary>
    protected IQueryable<TEntity> ApplyCommonFilters(
        IQueryable<TEntity> query,
        bool includeInactive = false)
    {
        if (!includeInactive)
        {
            query = query.Where(e => e.IsActive);
        }

        return query.Where(e => e.DeletedAt == null);
    }
}
