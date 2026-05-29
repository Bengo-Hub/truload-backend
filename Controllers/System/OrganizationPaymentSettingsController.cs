using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Middleware;

namespace TruLoad.Backend.Controllers.System;

[ApiController]
[Route("api/v1/organization/payment-settings")]
[Authorize]
public class OrganizationPaymentSettingsController : ControllerBase
{
    private readonly TruLoadDbContext _context;
    private readonly ITenantContext _tenantContext;

    public OrganizationPaymentSettingsController(
        TruLoadDbContext context,
        ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaymentSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var org = await _context.Organizations
            .Where(o => o.Id == _tenantContext.OrganizationId)
            .Select(o => new PaymentSettingsDto
            {
                BankName = o.PaymentBankName,
                BankBranch = o.PaymentBankBranch,
                BankAccountNumber = o.PaymentBankAccountNumber,
                MpesaPaybillNumber = o.PaymentMpesaPaybillNumber,
                MpesaTillNumber = o.PaymentMpesaTillNumber,
            })
            .FirstOrDefaultAsync(ct);

        if (org == null) return NotFound();
        return Ok(org);
    }

    [HttpPut]
    [Authorize(Policy = "Permission:config.update")]
    [ProducesResponseType(typeof(PaymentSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] PaymentSettingsDto request, CancellationToken ct)
    {
        var org = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == _tenantContext.OrganizationId, ct);

        if (org == null) return NotFound();

        org.PaymentBankName = request.BankName?.Trim().NullIfEmpty();
        org.PaymentBankBranch = request.BankBranch?.Trim().NullIfEmpty();
        org.PaymentBankAccountNumber = request.BankAccountNumber?.Trim().NullIfEmpty();
        org.PaymentMpesaPaybillNumber = request.MpesaPaybillNumber?.Trim().NullIfEmpty();
        org.PaymentMpesaTillNumber = request.MpesaTillNumber?.Trim().NullIfEmpty();
        org.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return Ok(new PaymentSettingsDto
        {
            BankName = org.PaymentBankName,
            BankBranch = org.PaymentBankBranch,
            BankAccountNumber = org.PaymentBankAccountNumber,
            MpesaPaybillNumber = org.PaymentMpesaPaybillNumber,
            MpesaTillNumber = org.PaymentMpesaTillNumber,
        });
    }
}

public class PaymentSettingsDto
{
    public string? BankName { get; set; }
    public string? BankBranch { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? MpesaPaybillNumber { get; set; }
    public string? MpesaTillNumber { get; set; }
}

file static class StringExtensions
{
    internal static string? NullIfEmpty(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
