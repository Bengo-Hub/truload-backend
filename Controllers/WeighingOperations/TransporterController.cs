using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Authorization.Attributes;
using TruLoad.Backend.Common;
using TruLoad.Backend.Data;
using TruLoad.Backend.DTOs.Portal;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Repositories.Weighing.Interfaces;
using TruLoad.Backend.Services.Interfaces.Financial;
using TruLoad.Backend.Services.Interfaces.Shared;

namespace TruLoad.Backend.Controllers.WeighingOperations;

/// <summary>
/// Manages transporter master data for weighing operations.
/// Transporters are companies that own vehicles being weighed.
/// </summary>
[ApiController]
[Route("api/v1/transporters")]
[Authorize]
[EnableRateLimiting("weighing")]
public class TransporterController : ControllerBase
{
    private readonly ITransporterRepository _repository;
    private readonly INotificationService _notificationService;
    private readonly ITreasuryService _treasuryService;
    private readonly TruLoadDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TransporterController> _logger;
    private static readonly Random _rnd = new();

    public TransporterController(
        ITransporterRepository repository,
        INotificationService notificationService,
        ITreasuryService treasuryService,
        TruLoadDbContext dbContext,
        ITenantContext tenantContext,
        IConfiguration configuration,
        ILogger<TransporterController> logger)
    {
        _repository = repository;
        _notificationService = notificationService;
        _treasuryService = treasuryService;
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Generates a unique transporter code from name (e.g. "Acme Ltd" -> "ACME-LTD-1234").
    /// </summary>
    private async Task<string> GenerateUniqueTransporterCodeAsync(string name)
    {
        var slug = new string(name
            .ToUpperInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == ' ')
            .ToArray());
        slug = slug.Replace(" ", "-").Trim('-');
        if (slug.Length > 25) slug = slug.Substring(0, 25);
        if (string.IsNullOrEmpty(slug)) slug = "TRP";

        for (int attempt = 0; attempt < 20; attempt++)
        {
            var suffix = _rnd.Next(1000, 99999).ToString();
            var code = $"{slug}-{suffix}";
            var existing = await _repository.GetByCodeAsync(code);
            if (existing == null) return code;
        }

        return $"{slug}-{Guid.NewGuid().ToString("N")[..8]}";
    }

    /// <summary>
    /// Gets all transporters
    /// </summary>
    /// <param name="includeInactive">Include inactive transporters</param>
    /// <returns>List of transporters</returns>
    [HttpGet]
    [HasPermission("transporter.read")]
    [ProducesResponseType(typeof(List<Transporter>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
    {
        var transporters = await _repository.GetAllAsync(includeInactive);
        return Ok(transporters);
    }

    /// <summary>
    /// Gets all active transporters
    /// </summary>
    /// <returns>List of active transporters</returns>
    [HttpGet("active")]
    [HasPermission("transporter.read")]
    [ProducesResponseType(typeof(List<Transporter>), 200)]
    public async Task<IActionResult> GetAllActive()
    {
        var transporters = await _repository.GetAllActiveAsync();
        return Ok(transporters);
    }

    /// <summary>
    /// Search transporters by name, code, registration number, phone, email, or NTAC number
    /// </summary>
    /// <param name="query">Search query</param>
    /// <returns>Matching transporters (max 50)</returns>
    [HttpGet("search")]
    [HasPermission("transporter.read")]
    [ProducesResponseType(typeof(List<Transporter>), 200)]
    public async Task<IActionResult> Search([FromQuery] string query = "")
    {
        var transporters = await _repository.SearchAsync(query);
        return Ok(transporters);
    }

    /// <summary>
    /// Gets a transporter by ID
    /// </summary>
    /// <param name="id">Transporter ID</param>
    /// <returns>Transporter details with vehicles</returns>
    [HttpGet("{id}")]
    [HasPermission("transporter.read")]
    [ProducesResponseType(typeof(Transporter), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var transporter = await _repository.GetByIdAsync(id);
        if (transporter == null)
            return NotFound(new { Message = $"Transporter with ID {id} not found" });

        return Ok(transporter);
    }

    /// <summary>
    /// Gets a transporter by code
    /// </summary>
    /// <param name="code">Transporter code</param>
    /// <returns>Transporter details</returns>
    [HttpGet("code/{code}")]
    [HasPermission("transporter.read")]
    [ProducesResponseType(typeof(Transporter), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetByCode(string code)
    {
        var transporter = await _repository.GetByCodeAsync(code);
        if (transporter == null)
            return NotFound(new { Message = $"Transporter with code {code} not found" });

        return Ok(transporter);
    }

    /// <summary>
    /// Creates a new transporter
    /// </summary>
    /// <param name="transporter">Transporter data</param>
    /// <returns>Created transporter</returns>
    [HttpPost]
    [Authorize(Policy = "Permission:transporter.create")]
    [ProducesResponseType(typeof(Transporter), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Create([FromBody] Transporter transporter)
    {
        if (string.IsNullOrWhiteSpace(transporter.Name))
            return BadRequest(new { Message = "Transporter name is required" });

        // Auto-generate code from name if not provided (per Section 15: only name mandatory)
        if (string.IsNullOrWhiteSpace(transporter.Code))
        {
            transporter.Code = await GenerateUniqueTransporterCodeAsync(transporter.Name);
        }
        else
        {
            var existing = await _repository.GetByCodeAsync(transporter.Code);
            if (existing != null)
                return Conflict(new { Message = $"Transporter with code {transporter.Code} already exists" });
        }

        try
        {
            var created = await _repository.CreateAsync(transporter);
            _logger.LogInformation("Created transporter {Code} - {Name}", created.Code, created.Name);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating transporter {Code}", transporter.Code);
            return StatusCode(500, "An error occurred while creating the transporter");
        }
    }

    /// <summary>
    /// Updates an existing transporter
    /// </summary>
    /// <param name="id">Transporter ID</param>
    /// <param name="transporter">Updated transporter data</param>
    /// <returns>Updated transporter</returns>
    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:transporter.update")]
    [ProducesResponseType(typeof(Transporter), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Update(Guid id, [FromBody] Transporter transporter)
    {
        if (id != transporter.Id)
            return BadRequest(new { Message = "ID mismatch" });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound(new { Message = $"Transporter with ID {id} not found" });

        // Preserve existing code when frontend sends empty (Section 15: only name mandatory)
        if (string.IsNullOrWhiteSpace(transporter.Code))
            transporter.Code = existing.Code;

        // Check for duplicate code (only when code was changed)
        var duplicate = await _repository.GetByCodeAsync(transporter.Code);
        if (duplicate != null && duplicate.Id != id)
            return Conflict(new { Message = $"Transporter with code {transporter.Code} already exists" });

        try
        {
            var updated = await _repository.UpdateAsync(transporter);
            _logger.LogInformation("Updated transporter {Id} - {Code}", id, updated.Code);
            return Ok(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating transporter {Id}", id);
            return StatusCode(500, "An error occurred while updating the transporter");
        }
    }

    /// <summary>
    /// Soft deletes a transporter
    /// </summary>
    /// <param name="id">Transporter ID</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [Authorize(Policy = "Permission:transporter.delete")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success = await _repository.SoftDeleteAsync(id);
        if (!success)
            return NotFound(new { Message = $"Transporter with ID {id} not found" });

        _logger.LogInformation("Soft deleted transporter {Id}", id);
        return NoContent();
    }

    /// <summary>
    /// Sends a portal invite email to the transporter's registered PortalAccountEmail, inviting
    /// them to self-service link/register their TruLoad Portal account. Portal linking itself is
    /// otherwise entirely self-service (TransporterPortalService.RegisterAsync matches the signing-up
    /// user by email/phone/transporter code) with no existing trigger to prompt a transporter to do
    /// so - this just sends that prompt. Reuses the same email template and INotificationService
    /// plumbing as the existing team-member invite (TransporterPortalService.InviteTeamMemberAsync)
    /// rather than adding a new email-sending mechanism.
    /// </summary>
    [HttpPost("{id}/invite-portal")]
    [Authorize(Policy = "Permission:transporter.update")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> InvitePortal(Guid id)
    {
        var transporter = await _repository.GetByIdAsync(id);
        if (transporter == null)
            return NotFound(new { Message = $"Transporter with ID {id} not found" });

        if (string.IsNullOrWhiteSpace(transporter.PortalAccountEmail))
        {
            return BadRequest(new
            {
                Message = "This transporter has no portal account email configured. Set PortalAccountEmail before sending an invite."
            });
        }

        var portalUrl = $"{(_configuration["FrontendUrl"]?.TrimEnd('/') ?? "https://truload.codevertexafrica.com")}/portal";

        try
        {
            var sent = await _notificationService.SendEmailAsync(
                "truload/portal_team_invite",
                transporter.PortalAccountEmail!,
                transporter.Name,
                new Dictionary<string, object>
                {
                    ["transporter_name"] = transporter.Name,
                    ["role"] = "owner",
                    ["invite_url"] = portalUrl,
                    ["expires_at"] = "-"
                },
                subject: $"You're invited to {transporter.Name}'s TruLoad Portal",
                cancellationToken: HttpContext.RequestAborted,
                tenantSlug: null);

            if (!sent)
            {
                _logger.LogWarning("Portal invite email failed to send for transporter {TransporterId} ({Email})", id, transporter.PortalAccountEmail);
                return StatusCode(500, new { Message = "Failed to send the portal invite email." });
            }

            _logger.LogInformation("Portal invite email sent for transporter {TransporterId} ({Email})", id, transporter.PortalAccountEmail);
            return Ok(new { Message = $"Portal invite sent to {transporter.PortalAccountEmail}." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending portal invite for transporter {TransporterId}", id);
            return StatusCode(500, new { Message = "An error occurred while sending the portal invite." });
        }
    }

    /// <summary>
    /// Gets a transporter's AR statement (live from treasury-api) — the commercial-ops view,
    /// unlike the transporter-portal's own-account-only GetStatement. IsLinked=false when no
    /// treasury Invoice has been created for them yet (CrmContactId unset).
    /// </summary>
    [HttpGet("{id}/statement")]
    [HasPermission("billing.statements.view")]
    [ProducesResponseType(typeof(PortalStatementDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetStatement(
        Guid id,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var transporter = await _repository.GetByIdAsync(id);
        if (transporter == null)
            return NotFound(new { Message = $"Transporter with ID {id} not found" });

        if (transporter.CrmContactId == null)
        {
            return Ok(new PortalStatementDto
            {
                IsLinked = false,
                OnAccountBilling = transporter.OnAccountBilling,
                CreditLimitKes = transporter.CreditLimitKes
            });
        }

        // Transporter is a global entity (not org-scoped). This is the commercial-ops view, so
        // "statement" means "what this transporter owes OUR org" — resolve via the caller's own
        // current tenant, not any org the transporter may have also weighed at elsewhere.
        var org = await _dbContext.Organizations
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == _tenantContext.OrganizationId);

        if (org == null || string.IsNullOrWhiteSpace(org.SsoTenantSlug))
        {
            return Ok(new PortalStatementDto
            {
                IsLinked = false,
                OnAccountBilling = transporter.OnAccountBilling,
                CreditLimitKes = transporter.CreditLimitKes
            });
        }

        try
        {
            var stmt = await _treasuryService.GetCustomerStatementAsync(org.SsoTenantSlug, transporter.CrmContactId.Value, fromDate, toDate);

            // Tonnage view uses OUR OWN EAT-calendar-day resolution of the same fromDate/toDate
            // request params (WeighingQueryHelpers.ResolveEatDayRange - the same helper every other
            // weighing endpoint uses), rather than treasury's echoed-back stmt.From/To, since
            // treasury's date semantics aren't guaranteed to match this platform's EAT-day
            // convention for WeighedAt (a true UTC instant).
            var (tonnageFromUtc, tonnageToExclusive) = WeighingQueryHelpers.ResolveEatDayRange(fromDate, toDate);
            var tonnageSummary = await WeighingQueryHelpers.ComputeTransporterTonnageSummaryAsync(
                _dbContext, org.Id, transporter.Id, tonnageFromUtc, tonnageToExclusive, HttpContext.RequestAborted);

            return Ok(new PortalStatementDto
            {
                IsLinked = true,
                CustomerName = stmt.CustomerName ?? transporter.Name,
                From = stmt.From,
                To = stmt.To,
                TotalInvoiced = stmt.TotalInvoiced,
                TotalPaid = stmt.TotalPaid,
                ClosingBalance = stmt.ClosingBalance,
                OnAccountBilling = transporter.OnAccountBilling,
                CreditLimitKes = transporter.CreditLimitKes,
                Lines = stmt.Lines.Select(l => new PortalStatementLineDto
                {
                    Date = l.Date,
                    DocType = l.DocType,
                    Reference = l.Reference,
                    Debit = l.Debit,
                    Credit = l.Credit,
                    Balance = l.Balance,
                    Status = l.Status
                }).ToList(),
                TonnageSummary = tonnageSummary
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching statement for transporter {TransporterId}", id);
            return StatusCode(500, new { Message = "An error occurred while retrieving the statement." });
        }
    }
}
