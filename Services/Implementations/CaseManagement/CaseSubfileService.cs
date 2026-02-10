using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Services.Interfaces.CaseManagement;

namespace TruLoad.Backend.Services.Implementations.CaseManagement;

/// <summary>
/// Service implementation for case subfile (document) management.
/// Handles CRUD for case documents across subfile types B-J.
/// </summary>
public class CaseSubfileService : ICaseSubfileService
{
    private readonly TruLoadDbContext _context;

    public CaseSubfileService(TruLoadDbContext context)
    {
        _context = context;
    }

    public async Task<CaseSubfileDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var subfile = await _context.CaseSubfiles
            .Include(s => s.CaseRegister)
            .Include(s => s.SubfileType)
            .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null, ct);

        return subfile == null ? null : MapToDto(subfile);
    }

    public async Task<IEnumerable<CaseSubfileDto>> GetByCaseIdAsync(Guid caseRegisterId, CancellationToken ct = default)
    {
        var subfiles = await _context.CaseSubfiles
            .Include(s => s.CaseRegister)
            .Include(s => s.SubfileType)
            .Where(s => s.CaseRegisterId == caseRegisterId && s.DeletedAt == null)
            .OrderByDescending(s => s.UploadedAt)
            .ToListAsync(ct);

        return subfiles.Select(MapToDto);
    }

    public async Task<IEnumerable<CaseSubfileDto>> GetByCaseAndTypeAsync(Guid caseRegisterId, Guid subfileTypeId, CancellationToken ct = default)
    {
        var subfiles = await _context.CaseSubfiles
            .Include(s => s.CaseRegister)
            .Include(s => s.SubfileType)
            .Where(s => s.CaseRegisterId == caseRegisterId
                && s.SubfileTypeId == subfileTypeId
                && s.DeletedAt == null)
            .OrderByDescending(s => s.UploadedAt)
            .ToListAsync(ct);

        return subfiles.Select(MapToDto);
    }

    public async Task<IEnumerable<CaseSubfileDto>> SearchAsync(CaseSubfileSearchCriteria criteria, CancellationToken ct = default)
    {
        var query = _context.CaseSubfiles
            .Include(s => s.CaseRegister)
            .Include(s => s.SubfileType)
            .Where(s => s.DeletedAt == null)
            .AsQueryable();

        if (criteria.CaseRegisterId.HasValue)
            query = query.Where(s => s.CaseRegisterId == criteria.CaseRegisterId.Value);

        if (criteria.SubfileTypeId.HasValue)
            query = query.Where(s => s.SubfileTypeId == criteria.SubfileTypeId.Value);

        if (!string.IsNullOrWhiteSpace(criteria.DocumentType))
            query = query.Where(s => s.DocumentType == criteria.DocumentType);

        var subfiles = await query
            .OrderByDescending(s => s.UploadedAt)
            .Skip(criteria.Skip)
            .Take(criteria.PageSize)
            .ToListAsync(ct);

        return subfiles.Select(MapToDto);
    }

    public async Task<SubfileCompletionDto> GetSubfileCompletionAsync(Guid caseRegisterId, CancellationToken ct = default)
    {
        var allTypes = await _context.SubfileTypes
            .Where(t => t.DeletedAt == null)
            .OrderBy(t => t.Code)
            .ToListAsync(ct);

        var caseSubfiles = await _context.CaseSubfiles
            .Where(s => s.CaseRegisterId == caseRegisterId && s.DeletedAt == null)
            .ToListAsync(ct);

        var items = allTypes.Select(type =>
        {
            var docsForType = caseSubfiles.Count(s => s.SubfileTypeId == type.Id);
            return new SubfileTypeCompletionItem
            {
                SubfileTypeId = type.Id,
                SubfileTypeCode = type.Code,
                SubfileTypeName = type.Name,
                HasDocuments = docsForType > 0,
                DocumentCount = docsForType
            };
        }).ToList();

        return new SubfileCompletionDto
        {
            CaseRegisterId = caseRegisterId,
            Items = items,
            TotalTypes = items.Count,
            CompletedTypes = items.Count(i => i.HasDocuments)
        };
    }

    public async Task<CaseSubfileDto> CreateAsync(CreateCaseSubfileRequest request, Guid userId, CancellationToken ct = default)
    {
        // Verify case exists
        var caseRegister = await _context.CaseRegisters.FindAsync(new object[] { request.CaseRegisterId }, ct)
            ?? throw new InvalidOperationException($"Case {request.CaseRegisterId} not found");

        var subfile = new CaseSubfile
        {
            Id = Guid.NewGuid(),
            CaseRegisterId = request.CaseRegisterId,
            SubfileTypeId = request.SubfileTypeId,
            SubfileName = request.SubfileName,
            DocumentType = request.DocumentType,
            Content = request.Content,
            FilePath = request.FilePath,
            FileUrl = request.FileUrl,
            MimeType = request.MimeType,
            FileSizeBytes = request.FileSizeBytes,
            Metadata = request.Metadata,
            UploadedById = userId,
            UploadedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.CaseSubfiles.Add(subfile);
        await _context.SaveChangesAsync(ct);

        // Reload with navigation properties
        return (await GetByIdAsync(subfile.Id, ct))!;
    }

    public async Task<CaseSubfileDto> UpdateAsync(Guid id, UpdateCaseSubfileRequest request, CancellationToken ct = default)
    {
        var subfile = await _context.CaseSubfiles.FindAsync(new object[] { id }, ct)
            ?? throw new InvalidOperationException($"Subfile {id} not found");

        if (subfile.DeletedAt != null)
            throw new InvalidOperationException("Cannot update a deleted subfile");

        if (!string.IsNullOrWhiteSpace(request.SubfileName))
            subfile.SubfileName = request.SubfileName;

        if (!string.IsNullOrWhiteSpace(request.Content))
            subfile.Content = request.Content;

        if (!string.IsNullOrWhiteSpace(request.FilePath))
            subfile.FilePath = request.FilePath;

        if (!string.IsNullOrWhiteSpace(request.FileUrl))
            subfile.FileUrl = request.FileUrl;

        if (!string.IsNullOrWhiteSpace(request.Metadata))
            subfile.Metadata = request.Metadata;

        subfile.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return (await GetByIdAsync(id, ct))!;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var subfile = await _context.CaseSubfiles.FindAsync(new object[] { id }, ct);
        if (subfile == null)
            return false;

        subfile.DeletedAt = DateTime.UtcNow;
        subfile.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return true;
    }

    private CaseSubfileDto MapToDto(CaseSubfile subfile)
    {
        // Resolve uploader name if available
        string? uploadedByName = null;
        if (subfile.UploadedById.HasValue)
        {
            var user = _context.Users.Find(subfile.UploadedById.Value);
            if (user != null)
                uploadedByName = user.FullName;
        }

        return new CaseSubfileDto
        {
            Id = subfile.Id,
            CaseRegisterId = subfile.CaseRegisterId,
            CaseNo = subfile.CaseRegister?.CaseNo,
            SubfileTypeId = subfile.SubfileTypeId,
            SubfileTypeName = subfile.SubfileType?.Name,
            SubfileName = subfile.SubfileName,
            DocumentType = subfile.DocumentType,
            Content = subfile.Content,
            FilePath = subfile.FilePath,
            FileUrl = subfile.FileUrl,
            MimeType = subfile.MimeType,
            FileSizeBytes = subfile.FileSizeBytes,
            Checksum = subfile.Checksum,
            UploadedById = subfile.UploadedById,
            UploadedByName = uploadedByName,
            UploadedAt = subfile.UploadedAt,
            Metadata = subfile.Metadata,
            CreatedAt = subfile.CreatedAt
        };
    }
}
