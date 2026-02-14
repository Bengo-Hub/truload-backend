using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.Services.Interfaces.CaseManagement;

namespace TruLoad.Backend.Controllers.CaseManagement;

[ApiController]
[Route("api/v1/cases/{caseId}/documents")]
[Authorize]
public class CaseDocumentController : ControllerBase
{
    private readonly ICaseDocumentService _documentService;

    public CaseDocumentController(ICaseDocumentService documentService)
    {
        _documentService = documentService;
    }

    /// <summary>
    /// Get all documents aggregated across all related entities for a case
    /// </summary>
    [HttpGet]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetDocuments(Guid caseId, CancellationToken ct)
    {
        var documents = await _documentService.GetDocumentsByCaseIdAsync(caseId, ct);
        return Ok(documents);
    }

    /// <summary>
    /// Get document count summary by type for a case
    /// </summary>
    [HttpGet("summary")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetDocumentSummary(Guid caseId, CancellationToken ct)
    {
        var summary = await _documentService.GetDocumentSummaryAsync(caseId, ct);
        return Ok(summary);
    }
}
