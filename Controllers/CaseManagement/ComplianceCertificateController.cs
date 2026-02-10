using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.Services.Interfaces.CaseManagement;

namespace TruLoad.Backend.Controllers.CaseManagement;

[ApiController]
[Authorize]
public class ComplianceCertificateController : ControllerBase
{
    private readonly IComplianceCertificateService _certService;

    public ComplianceCertificateController(IComplianceCertificateService certService)
    {
        _certService = certService;
    }

    [HttpGet("api/v1/case/certificates/{id}")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var cert = await _certService.GetByIdAsync(id, ct);
        if (cert == null) return NotFound();
        return Ok(cert);
    }

    [HttpGet("api/v1/case/certificates/by-case/{caseId}")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetByCaseId(Guid caseId, CancellationToken ct)
    {
        var certs = await _certService.GetByCaseIdAsync(caseId, ct);
        return Ok(certs);
    }

    [HttpGet("api/v1/case/certificates/by-weighing/{weighingId}")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetByWeighingId(Guid weighingId, CancellationToken ct)
    {
        var cert = await _certService.GetByWeighingIdAsync(weighingId, ct);
        if (cert == null) return NotFound();
        return Ok(cert);
    }
}
