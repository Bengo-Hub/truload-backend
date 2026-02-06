using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Repositories.Weighing.Interfaces;

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
    private readonly ILogger<TransporterController> _logger;

    public TransporterController(ITransporterRepository repository, ILogger<TransporterController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Gets all transporters
    /// </summary>
    /// <param name="includeInactive">Include inactive transporters</param>
    /// <returns>List of transporters</returns>
    [HttpGet]
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
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Check for duplicate code
        var existing = await _repository.GetByCodeAsync(transporter.Code);
        if (existing != null)
            return Conflict(new { Message = $"Transporter with code {transporter.Code} already exists" });

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

        // Check for duplicate code
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
}
