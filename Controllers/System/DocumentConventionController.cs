using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Services.Interfaces.Infrastructure;

namespace TruLoad.Backend.Controllers.System;

[ApiController]
[Route("api/v1/document-conventions")]
[Authorize]
public class DocumentConventionController : ControllerBase
{
    private readonly TruLoadDbContext _context;
    private readonly IDocumentNumberService _documentNumberService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<DocumentConventionController> _logger;

    public DocumentConventionController(
        TruLoadDbContext context,
        IDocumentNumberService documentNumberService,
        ITenantContext tenantContext,
        ILogger<DocumentConventionController> logger)
    {
        _context = context;
        _documentNumberService = documentNumberService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Gets all document conventions for the current organization.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "Permission:config.read")]
    [ProducesResponseType(typeof(List<DocumentConventionDto>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var orgId = _tenantContext.OrganizationId;
        var conventions = await _context.DocumentConventions
            .Where(c => c.OrganizationId == orgId)
            .OrderBy(c => c.DocumentType)
            .Select(c => MapToDto(c))
            .ToListAsync();

        return Ok(conventions);
    }

    /// <summary>
    /// Gets a document convention by ID.
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:config.read")]
    [ProducesResponseType(typeof(DocumentConventionDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var convention = await _context.DocumentConventions.FindAsync(id);
        if (convention == null || convention.OrganizationId != _tenantContext.OrganizationId)
            return NotFound();

        return Ok(MapToDto(convention));
    }

    /// <summary>
    /// Updates a document convention.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:config.update")]
    [ProducesResponseType(typeof(DocumentConventionDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDocumentConventionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var convention = await _context.DocumentConventions.FindAsync(id);
        if (convention == null || convention.OrganizationId != _tenantContext.OrganizationId)
            return NotFound();

        convention.Prefix = request.Prefix ?? convention.Prefix;
        convention.IncludeStationCode = request.IncludeStationCode ?? convention.IncludeStationCode;
        convention.IncludeBound = request.IncludeBound ?? convention.IncludeBound;
        convention.IncludeDate = request.IncludeDate ?? convention.IncludeDate;
        convention.DateFormat = request.DateFormat ?? convention.DateFormat;
        convention.IncludeVehicleReg = request.IncludeVehicleReg ?? convention.IncludeVehicleReg;
        convention.SequencePadding = request.SequencePadding ?? convention.SequencePadding;
        convention.Separator = request.Separator ?? convention.Separator;
        convention.ResetFrequency = request.ResetFrequency ?? convention.ResetFrequency;
        convention.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated document convention {Id} for type {DocumentType}",
            id, convention.DocumentType);

        return Ok(MapToDto(convention));
    }

    /// <summary>
    /// Previews the next document number for a given document type.
    /// </summary>
    [HttpGet("preview")]
    [Authorize(Policy = "Permission:config.read")]
    [ProducesResponseType(typeof(DocumentNumberPreviewDto), 200)]
    public async Task<IActionResult> PreviewNumber(
        [FromQuery] string documentType,
        [FromQuery] string? stationCode = null,
        [FromQuery] string? bound = null,
        [FromQuery] string? vehicleReg = null)
    {
        var orgId = _tenantContext.OrganizationId;
        var stationId = _tenantContext.StationId;

        var preview = await _documentNumberService.PreviewNextNumberAsync(
            orgId, stationId, documentType, stationCode, vehicleReg, bound);

        return Ok(new DocumentNumberPreviewDto { NextNumber = preview, DocumentType = documentType });
    }

    private static DocumentConventionDto MapToDto(DocumentConvention c)
    {
        return new DocumentConventionDto
        {
            Id = c.Id,
            DocumentType = c.DocumentType,
            DisplayName = c.DisplayName,
            Prefix = c.Prefix,
            IncludeStationCode = c.IncludeStationCode,
            IncludeBound = c.IncludeBound,
            IncludeDate = c.IncludeDate,
            DateFormat = c.DateFormat,
            IncludeVehicleReg = c.IncludeVehicleReg,
            SequencePadding = c.SequencePadding,
            Separator = c.Separator,
            ResetFrequency = c.ResetFrequency,
            IsActive = c.IsActive,
        };
    }
}

// ============================================================================
// DTOs
// ============================================================================

public class DocumentConventionDto
{
    public Guid Id { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public bool IncludeStationCode { get; set; }
    public bool IncludeBound { get; set; }
    public bool IncludeDate { get; set; }
    public string DateFormat { get; set; } = string.Empty;
    public bool IncludeVehicleReg { get; set; }
    public int SequencePadding { get; set; }
    public string Separator { get; set; } = string.Empty;
    public string ResetFrequency { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class UpdateDocumentConventionRequest
{
    public string? Prefix { get; set; }
    public bool? IncludeStationCode { get; set; }
    public bool? IncludeBound { get; set; }
    public bool? IncludeDate { get; set; }
    public string? DateFormat { get; set; }
    public bool? IncludeVehicleReg { get; set; }
    public int? SequencePadding { get; set; }
    public string? Separator { get; set; }
    public string? ResetFrequency { get; set; }
}

public class DocumentNumberPreviewDto
{
    public string NextNumber { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
}
