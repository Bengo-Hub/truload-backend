using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.Services.Interfaces.CaseManagement;

namespace TruLoad.Backend.Controllers.CaseManagement;

[ApiController]
[Authorize]
public class LoadCorrectionMemoController : ControllerBase
{
    private readonly ILoadCorrectionMemoService _memoService;

    public LoadCorrectionMemoController(ILoadCorrectionMemoService memoService)
    {
        _memoService = memoService;
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
}
