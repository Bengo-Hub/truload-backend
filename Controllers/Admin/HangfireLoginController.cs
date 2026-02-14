using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Models.Identity;

namespace TruLoad.Backend.Controllers.Admin;

/// <summary>
/// Serves a minimal login page for the Hangfire dashboard.
/// Uses ASP.NET Identity cookie auth so the Hangfire authorization filter
/// can verify the user's identity via the standard Identity cookie.
/// </summary>
[ApiController]
[AllowAnonymous]
public class HangfireLoginController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;

    public HangfireLoginController(SignInManager<ApplicationUser> signInManager)
    {
        _signInManager = signInManager;
    }

    [HttpGet("/hangfire/login")]
    public IActionResult Login(
        [FromQuery] string? returnUrl = "/hangfire",
        [FromQuery] string? error = null)
    {
        var safeReturnUrl = global::System.Net.WebUtility.HtmlEncode(returnUrl ?? "/hangfire");
        var errorHtml = !string.IsNullOrEmpty(error)
            ? "<div class=\"error\">" + global::System.Net.WebUtility.HtmlEncode(error) + "</div>"
            : "";

        var html = "<!DOCTYPE html>" +
            "<html lang=\"en\">" +
            "<head>" +
            "<meta charset=\"UTF-8\">" +
            "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">" +
            "<title>TruLoad - Hangfire Login</title>" +
            "<style>" +
            "* { margin: 0; padding: 0; box-sizing: border-box; }" +
            "body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #1a1a2e; color: #e0e0e0; display: flex; align-items: center; justify-content: center; min-height: 100vh; }" +
            ".card { background: #16213e; border-radius: 12px; padding: 2.5rem; width: 100%; max-width: 400px; box-shadow: 0 8px 32px rgba(0,0,0,0.3); }" +
            "h1 { font-size: 1.5rem; margin-bottom: 0.5rem; color: #e94560; }" +
            "p { font-size: 0.9rem; color: #8892b0; margin-bottom: 1.5rem; }" +
            "label { display: block; margin-bottom: 0.3rem; font-size: 0.85rem; color: #8892b0; }" +
            "input { width: 100%; padding: 0.75rem; border: 1px solid #2a2a4a; border-radius: 6px; background: #0f3460; color: #e0e0e0; font-size: 1rem; margin-bottom: 1rem; }" +
            "input:focus { outline: none; border-color: #e94560; }" +
            "button { width: 100%; padding: 0.75rem; background: #e94560; color: white; border: none; border-radius: 6px; font-size: 1rem; cursor: pointer; }" +
            "button:hover { background: #c73a52; }" +
            ".error { background: #3a1c1c; border: 1px solid #e94560; padding: 0.75rem; border-radius: 6px; margin-bottom: 1rem; font-size: 0.85rem; color: #ff6b6b; }" +
            "</style>" +
            "</head>" +
            "<body>" +
            "<div class=\"card\">" +
            "<h1>TruLoad Hangfire</h1>" +
            "<p>Sign in to access the background jobs dashboard</p>" +
            errorHtml +
            "<form method=\"post\" action=\"/hangfire/login\">" +
            "<input type=\"hidden\" name=\"returnUrl\" value=\"" + safeReturnUrl + "\" />" +
            "<label for=\"email\">Email</label>" +
            "<input type=\"email\" id=\"email\" name=\"email\" required autofocus placeholder=\"admin@example.com\" />" +
            "<label for=\"password\">Password</label>" +
            "<input type=\"password\" id=\"password\" name=\"password\" required placeholder=\"Password\" />" +
            "<button type=\"submit\">Sign In</button>" +
            "</form>" +
            "</div>" +
            "</body>" +
            "</html>";

        return Content(html, "text/html");
    }

    [HttpPost("/hangfire/login")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> LoginPost(
        [FromForm] string email,
        [FromForm] string password,
        [FromForm] string? returnUrl = "/hangfire")
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return Redirect($"/hangfire/login?returnUrl={Uri.EscapeDataString(returnUrl ?? "/hangfire")}&error=Email+and+password+are+required");
        }

        var result = await _signInManager.PasswordSignInAsync(email, password, isPersistent: false, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            return Redirect(returnUrl ?? "/hangfire");
        }

        var errorMsg = result.IsLockedOut
            ? "Account+is+locked+out"
            : "Invalid+email+or+password";

        return Redirect($"/hangfire/login?returnUrl={Uri.EscapeDataString(returnUrl ?? "/hangfire")}&error={errorMsg}");
    }
}
