using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Models.System;

namespace TruLoad.Backend.Controllers.System;

/// <summary>
/// CRUD for document sequences (current counter per org/station/document type).
/// Used for weight tickets, reweigh tickets, etc. Sequences are created on first use by DocumentNumberService; this API allows listing and optional reset.
/// </summary>
[ApiController]
[Route("api/v1/document-sequences")]
[Authorize]
public class DocumentSequenceController : ControllerBase
{
    private readonly TruLoadDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<DocumentSequenceController> _logger;

    public DocumentSequenceController(
        TruLoadDbContext context,
        ITenantContext tenantContext,
        ILogger<DocumentSequenceController> logger)
    {
        _context = context;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Gets all document sequences for the current organization.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Permission:config.read")]
    [ProducesResponseType(typeof(List<DocumentSequenceDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? stationId = null)
    {
        var orgId = _tenantContext.OrganizationId;
        var query = _context.DocumentSequences
            .AsNoTracking()
            .Where(s => s.OrganizationId == orgId);

        if (stationId.HasValue)
            query = query.Where(s => s.StationId == stationId.Value);

        var list = await query
            .OrderBy(s => s.DocumentType)
            .ThenBy(s => s.StationId)
            .Select(s => new DocumentSequenceDto
            {
                Id = s.Id,
                OrganizationId = s.OrganizationId,
                StationId = s.StationId,
                StationName = s.Station != null ? s.Station.Name : null,
                DocumentType = s.DocumentType,
                CurrentSequence = s.CurrentSequence,
                ResetFrequency = s.ResetFrequency,
                LastResetDate = s.LastResetDate,
                UpdatedAt = s.UpdatedAt,
            })
            .ToListAsync();

        return Ok(list);
    }

    /// <summary>
    /// Gets a document sequence by ID.
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:config.read")]
    [ProducesResponseType(typeof(DocumentSequenceDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var seq = await _context.DocumentSequences
            .AsNoTracking()
            .Include(s => s.Station)
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == _tenantContext.OrganizationId);

        if (seq == null) return NotFound();

        return Ok(MapToDto(seq));
    }

    /// <summary>
    /// Updates a document sequence (e.g. set current value, reset frequency). Use with care: changing CurrentSequence can create duplicate numbers if not coordinated.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:config.update")]
    [ProducesResponseType(typeof(DocumentSequenceDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDocumentSequenceRequest request)
    {
        var seq = await _context.DocumentSequences
            .Include(s => s.Station)
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == _tenantContext.OrganizationId);

        if (seq == null) return NotFound();

        if (request.CurrentSequence.HasValue && request.CurrentSequence.Value < 0)
            return BadRequest("CurrentSequence cannot be negative.");

        if (request.CurrentSequence.HasValue)
            seq.CurrentSequence = request.CurrentSequence.Value;

        if (!string.IsNullOrWhiteSpace(request.ResetFrequency))
        {
            var valid = new[] { "daily", "monthly", "yearly", "never" };
            if (!valid.Contains(request.ResetFrequency.Trim().ToLowerInvariant()))
                return BadRequest("ResetFrequency must be one of: daily, monthly, yearly, never.");
            seq.ResetFrequency = request.ResetFrequency.Trim();
        }

        if (request.ResetNow == true)
        {
            seq.CurrentSequence = 0;
            seq.LastResetDate = DateTime.UtcNow;
        }

        seq.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated document sequence {Id} for type {DocumentType}", id, seq.DocumentType);
        return Ok(MapToDto(seq));
    }

    private static DocumentSequenceDto MapToDto(DocumentSequence s)
    {
        return new DocumentSequenceDto
        {
            Id = s.Id,
            OrganizationId = s.OrganizationId,
            StationId = s.StationId,
            StationName = s.Station?.Name,
            DocumentType = s.DocumentType,
            CurrentSequence = s.CurrentSequence,
            ResetFrequency = s.ResetFrequency,
            LastResetDate = s.LastResetDate,
            UpdatedAt = s.UpdatedAt,
        };
    }
}

public class DocumentSequenceDto
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? StationId { get; set; }
    public string? StationName { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public int CurrentSequence { get; set; }
    public string ResetFrequency { get; set; } = string.Empty;
    public DateTime LastResetDate { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateDocumentSequenceRequest
{
    /// <summary>Set the current sequence value (use with care).</summary>
    public int? CurrentSequence { get; set; }
    /// <summary>Reset frequency: daily, monthly, yearly, never.</summary>
    public string? ResetFrequency { get; set; }
    /// <summary>If true, reset counter to 0 and set LastResetDate to now.</summary>
    public bool? ResetNow { get; set; }
}
