using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Services.Interfaces.Subscription;

namespace TruLoad.Backend.Controllers.Financial;

/// <summary>
/// Billing and subscription management for commercial weighing tenants.
/// Proxies requests to subscriptions-api using the user's SSO JWT for tenant resolution.
/// </summary>
[ApiController]
[Authorize]
public class BillingController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public BillingController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
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
        var jwt = ExtractBearerToken();
        if (jwt == null) return Unauthorized();
        var json = await _subscriptionService.GetSubscriptionJsonAsync(jwt, ct);
        return Content(json, "application/json");
    }

    /// <summary>Get billing details (current period, payment method, invoices) for the tenant.</summary>
    [HttpGet("api/v1/billing")]
    public async Task<IActionResult> GetBilling(CancellationToken ct)
    {
        var jwt = ExtractBearerToken();
        if (jwt == null) return Unauthorized();
        var json = await _subscriptionService.GetBillingJsonAsync(jwt, ct);
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
