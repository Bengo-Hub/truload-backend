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
public class LoadCorrectionMemoController : ControllerBase
{
    private readonly ILoadCorrectionMemoService _memoService;
    private readonly TruLoadDbContext _context;
    private readonly IPdfService _pdfService;

    public LoadCorrectionMemoController(
        ILoadCorrectionMemoService memoService,
        TruLoadDbContext context,
        IPdfService pdfService)
    {
        _memoService = memoService;
        _context = context;
        _pdfService = pdfService;
    }

    [HttpGet("api/v1/case/memos/{id}")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var memo = await _memoService.GetByIdAsync(id, ct);
        if (memo == null) return NotFound();
        return Ok(memo);
    }

    [HttpGet("api/v1/case/memos/by-case/{caseId}")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetByCaseId(Guid caseId, CancellationToken ct)
    {
        var memos = await _memoService.GetByCaseIdAsync(caseId, ct);
        return Ok(memos);
    }

    [HttpGet("api/v1/case/memos/by-weighing/{weighingId}")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetByWeighingId(Guid weighingId, CancellationToken ct)
    {
        var memo = await _memoService.GetByWeighingIdAsync(weighingId, ct);
        if (memo == null) return NotFound();
        return Ok(memo);
    }

    /// <summary>
    /// Renders the Load Correction Memo as a PDF. The memo PDF compares the original
    /// (overloaded) weighing against the reweigh; it is therefore available only once the
    /// reweigh has been recorded. Before reweigh the memo is "conditional" (issued, awaiting
    /// correction) and has no PDF yet.
    /// </summary>
    [HttpGet("api/v1/case/memos/{id}/pdf")]
    [HasPermission("case.read")]
    public async Task<IActionResult> GetPdf(Guid id, CancellationToken ct)
    {
        var memo = await _context.LoadCorrectionMemos
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id && m.DeletedAt == null, ct);
        if (memo == null) return NotFound(new { message = "Load correction memo not found" });

        if (!memo.ReweighWeighingId.HasValue)
            return Conflict(new { message = "This load correction memo is still conditional — its PDF is available after the reweigh is recorded." });

        var original = await _context.WeighingTransactions
            .AsNoTracking().FirstOrDefaultAsync(w => w.Id == memo.WeighingId, ct);
        var reweigh = await _context.WeighingTransactions
            .AsNoTracking().FirstOrDefaultAsync(w => w.Id == memo.ReweighWeighingId.Value, ct);
        if (original == null || reweigh == null)
            return NotFound(new { message = "Weighing records for this memo could not be found" });

        var pdf = await _pdfService.GenerateLoadCorrectionMemoAsync(memo.CaseRegisterId, original, reweigh);
        return File(pdf, "application/pdf", $"LoadCorrectionMemo_{memo.MemoNo}.pdf");
    }
}
