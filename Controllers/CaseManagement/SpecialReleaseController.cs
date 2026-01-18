using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TruLoad.Backend.DTOs.CaseManagement;
using TruLoad.Backend.Services.Interfaces.CaseManagement;

namespace TruLoad.Backend.Controllers.CaseManagement;

[ApiController]
[Route("api/v1/case/special-releases")]
[Authorize]
public class SpecialReleaseController : ControllerBase
{
    private readonly ISpecialReleaseService _specialReleaseService;

    public SpecialReleaseController(ISpecialReleaseService specialReleaseService)
    {
        _specialReleaseService = specialReleaseService;
    }

    /// <summary>
    /// Get special release by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var release = await _specialReleaseService.GetByIdAsync(id);
        if (release == null) return NotFound();
        return Ok(release);
    }

    /// <summary>
    /// Get special release by certificate number
    /// </summary>
    [HttpGet("by-certificate/{certificateNo}")]
    public async Task<IActionResult> GetByCertificateNo(string certificateNo)
    {
        var release = await _specialReleaseService.GetByCertificateNoAsync(certificateNo);
        if (release == null) return NotFound();
        return Ok(release);
    }

    /// <summary>
    /// Get all special releases for a case
    /// </summary>
    [HttpGet("by-case/{caseRegisterId}")]
    public async Task<IActionResult> GetByCaseRegisterId(Guid caseRegisterId)
    {
        var releases = await _specialReleaseService.GetByCaseRegisterIdAsync(caseRegisterId);
        return Ok(releases);
    }

    /// <summary>
    /// Get pending approvals
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingApprovals([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        var releases = await _specialReleaseService.GetPendingApprovalsAsync(pageNumber, pageSize);
        return Ok(releases);
    }

    /// <summary>
    /// Request special release
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RequestSpecialRelease([FromBody] CreateSpecialReleaseRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found"));

        try
        {
            var created = await _specialReleaseService.RequestSpecialReleaseAsync(request, userId);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Approve special release
    /// </summary>
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveSpecialReleaseRequest request)
    {
        var approvedById = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found"));

        try
        {
            var approved = await _specialReleaseService.ApproveSpecialReleaseAsync(id, request, approvedById);
            return Ok(approved);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Reject special release
    /// </summary>
    [HttpPost("{id}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectSpecialReleaseRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var rejectedById = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found"));

        try
        {
            var rejected = await _specialReleaseService.RejectSpecialReleaseAsync(id, request, rejectedById);
            return Ok(rejected);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Generate special release certificate PDF
    /// </summary>
    [HttpGet("{id}/certificate/pdf")]
    public async Task<IActionResult> GetCertificatePdf(Guid id)
    {
        try
        {
            var pdfBytes = await _specialReleaseService.GenerateSpecialReleaseCertificatePdfAsync(id);
            return File(pdfBytes, "application/pdf", $"SpecialRelease_{id}.pdf");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (NotImplementedException)
        {
            return StatusCode(501, "Certificate PDF generation not yet implemented");
        }
    }
}
