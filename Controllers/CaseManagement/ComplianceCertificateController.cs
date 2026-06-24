using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.Data;
using TruLoad.Backend.Services.Interfaces.CaseManagement;
using TruLoad.Backend.Services.Interfaces.Infrastructure;

namespace TruLoad.Backend.Controllers.CaseManagement;

[ApiController]
[Authorize]
public class ComplianceCertificateController : ControllerBase
{
    private readonly IComplianceCertificateService _certService;
    private readonly TruLoadDbContext _context;
    private readonly IPdfService _pdfService;

    public ComplianceCertificateController(
        IComplianceCertificateService certService,
        TruLoadDbContext context,
        IPdfService pdfService)
    {
        _certService = certService;
        _context = context;
        _pdfService = pdfService;
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

    /// <summary>
    /// Renders the Compliance Certificate as a PDF — issued once a reweigh confirms the
    /// vehicle is within legal limits.
    /// </summary>
    [HttpGet("api/v1/case/certificates/{id}/pdf")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetPdf(Guid id, CancellationToken ct)
    {
        var cert = await _context.ComplianceCertificates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.DeletedAt == null, ct);
        if (cert == null) return NotFound(new { message = "Compliance certificate not found" });

        // ComplianceCertificate.WeighingId is the compliant reweigh used to certify the vehicle.
        var reweigh = await _context.WeighingTransactions
            .AsNoTracking().FirstOrDefaultAsync(w => w.Id == cert.WeighingId, ct);
        if (reweigh == null)
            return NotFound(new { message = "Weighing record for this certificate could not be found" });

        var pdf = await _pdfService.GenerateComplianceCertificateAsync(cert.CaseRegisterId, reweigh);
        return File(pdf, "application/pdf", $"ComplianceCertificate_{cert.CertificateNo}.pdf");
    }
}
