using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Services.Interfaces.Subscription;

namespace TruLoad.Backend.Controllers.Financial;

/// <summary>
/// Billing and subscription management for commercial weighing tenants.
/// Proxies requests to subscriptions-api - plan listing is a public passthrough; current-subscription
/// lookup resolves the caller's own org via ITenantContext and calls subscriptions-api S2S (not by
/// forwarding the user's own JWT, which subscriptions-api's JWKS validator can never verify for a
/// truload-issued token - see ISubscriptionService.GetSubscriptionJsonAsync).
/// </summary>
[ApiController]
[Authorize]
public class BillingController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ITenantContext _tenantContext;
    private readonly TruLoadDbContext _db;

    public BillingController(ISubscriptionService subscriptionService, ITenantContext tenantContext, TruLoadDbContext db)
    {
        _subscriptionService = subscriptionService;
        _tenantContext = tenantContext;
        _db = db;
    }

    /// <summary>List all available subscription plans (public — no tenant context needed).</summary>
    [HttpGet("api/v1/billing/plans")]
    public async Task<IActionResult> GetPlans(CancellationToken ct)
    {
        var json = await _subscriptionService.GetPlansJsonAsync(ct);
        return Content(json, "application/json");
    }

    /// <summary>Get current subscription for the authenticated commercial tenant.</summary>
    [HttpGet("api/v1/billing/subscription")]
    public async Task<IActionResult> GetSubscription(CancellationToken ct)
    {
        var slug = await GetSsoTenantSlugAsync(ct);
        if (string.IsNullOrWhiteSpace(slug))
            return NotFound(new { message = "This organisation has no linked subscriptions-api tenant." });

        var json = await _subscriptionService.GetSubscriptionJsonAsync(slug, ct);
        return Content(json, "application/json");
    }

    private async Task<string?> GetSsoTenantSlugAsync(CancellationToken ct)
    {
        return await _db.Organizations
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(o => o.Id == _tenantContext.OrganizationId)
            .Select(o => o.SsoTenantSlug)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Get billing details (current period, payment method, invoices) for the tenant.</summary>
    [HttpGet("api/v1/billing")]
    public async Task<IActionResult> GetBilling(CancellationToken ct)
    {
        var slug = await GetSsoTenantSlugAsync(ct);
        if (string.IsNullOrWhiteSpace(slug))
            return NotFound(new { message = "This organisation has no linked subscriptions-api tenant." });

        var json = await _subscriptionService.GetBillingJsonAsync(slug, ct);
        return Content(json, "application/json");
    }

    /// <summary>Upgrade or downgrade the subscription plan.</summary>
    [HttpPut("api/v1/billing/plan")]
    public async Task<IActionResult> ChangePlan([FromBody] ChangePlanRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.PlanCode))
            return BadRequest("plan_code is required");

        var jwt = ExtractBearerToken();
        if (jwt == null) return Unauthorized();

        try
        {
            var json = await _subscriptionService.ChangePlanJsonAsync(jwt, request.PlanCode, ct);
            return Content(json, "application/json");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private string? ExtractBearerToken()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return authHeader["Bearer ".Length..].Trim();
        return null;
    }
}

public class ChangePlanRequest
{
    public string PlanCode { get; set; } = string.Empty;
}
