using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.Services.Interfaces.Weighing;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Models;
using TruLoad.Backend.DTOs.Weighing;
using System.Security.Claims;

namespace TruLoad.Backend.Controllers.WeighingOperations;

[ApiController]
[Route("api/v1/weighing-transactions")]
[Authorize]
public class WeighingController : ControllerBase
{
    private readonly IWeighingService _weighingService;
    private readonly ILogger<WeighingController> _logger;

    public WeighingController(IWeighingService weighingService, ILogger<WeighingController> logger)
    {
        _weighingService = weighingService;
        _logger = logger;
    }

    /// <summary>
    /// Searches weighing transactions with filters, pagination, and sorting.
    /// </summary>
    /// <param name="request">Search filters and pagination parameters</param>
    /// <returns>Paginated list of weighing transactions</returns>
    [HttpGet]
    [Authorize(Policy = "Permission:weighing.read")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(WeighingSearchResultDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Search([FromQuery] SearchWeighingRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var (items, totalCount) = await _weighingService.SearchTransactionsAsync(
                request.StationId,
                request.VehicleRegNo,
                request.FromDate,
                request.ToDate,
                request.ControlStatus,
                request.IsCompliant,
                request.OperatorId,
                request.Skip,
                request.Take,
                request.SortBy,
                request.SortOrder);

            var dtos = items.Select(t => MapToDto(t)).ToList();

            var result = new WeighingSearchResultDto
            {
                Items = dtos,
                TotalCount = totalCount,
                Skip = request.Skip,
                Take = request.Take
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching weighing transactions");
            return StatusCode(500, "An error occurred while searching weighing transactions.");
        }
    }

    /// <summary>
    /// Gets a weighing transaction by ID.
    /// </summary>
    /// <param name="id">Transaction ID</param>
    /// <returns>Weighing transaction details with axle weights and compliance info</returns>
    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:weighing.read")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(WeighingTransactionDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var transaction = await _weighingService.GetTransactionAsync(id);
            if (transaction == null)
            {
                return NotFound($"Weighing transaction {id} not found");
            }

            var dto = MapToDto(transaction);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving weighing transaction {TransactionId}", id);
            return StatusCode(500, "An error occurred while retrieving the weighing transaction.");
        }
    }

    /// <summary>
    /// Initiates a new weighing transaction.
    /// Call this endpoint when vehicle enters the weighing deck.
    /// </summary>
    /// <param name="request">Weighing transaction details</param>
    /// <returns>Created transaction with ID and initial status</returns>
    [HttpPost]
    [Authorize(Policy = "Permission:weighing.create")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(WeighingTransactionDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateWeighingRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            return Unauthorized("User ID not found in claims");
        }

        try
        {
            var transaction = await _weighingService.InitiateWeighingAsync(
                request.TicketNumber, 
                request.StationId, 
                userGuid);
            
            transaction.VehicleId = request.VehicleId;
            transaction.DriverId = request.DriverId;
            transaction.TransporterId = request.TransporterId;

            var dto = MapToDto(transaction);
            return CreatedAtAction(nameof(GetById), new { id = transaction.Id }, dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating weighing transaction");
            return StatusCode(500, "An error occurred while creating the weighing transaction.");
        }
    }

    /// <summary>
    /// Updates weighing transaction details (vehicle info, driver, etc.).
    /// </summary>
    /// <param name="id">Transaction ID</param>
    /// <param name="request">Updated transaction details</param>
    /// <returns>Updated transaction</returns>
    [HttpPut("{id}")]
    [Authorize(Policy = "Permission:weighing.update")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(WeighingTransactionDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWeighingRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var transaction = await _weighingService.GetTransactionAsync(id);
            if (transaction == null)
            {
                return NotFound($"Weighing transaction {id} not found");
            }

            // Update only provided fields
            if (!string.IsNullOrEmpty(request.VehicleRegNumber))
                transaction.VehicleRegNumber = request.VehicleRegNumber;
            
            if (request.DriverId.HasValue)
                transaction.DriverId = request.DriverId;
            
            if (request.TransporterId.HasValue)
                transaction.TransporterId = request.TransporterId;

            await _weighingService.UpdateTransactionAsync(transaction);

            var dto = MapToDto(transaction);
            return Ok(dto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Weighing transaction {id} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating weighing transaction {TransactionId}", id);
            return StatusCode(500, "An error occurred while updating the weighing transaction.");
        }
    }

    /// <summary>
    /// Deletes a weighing transaction (only allowed in Pending status).
    /// </summary>
    /// <param name="id">Transaction ID</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [Authorize(Policy = "Permission:weighing.delete")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var transaction = await _weighingService.GetTransactionAsync(id);
            if (transaction == null)
            {
                return NotFound($"Weighing transaction {id} not found");
            }

            if (transaction.ControlStatus != "Pending")
            {
                return BadRequest($"Cannot delete weighing in status '{transaction.ControlStatus}'. Only Pending transactions can be deleted.");
            }

            await _weighingService.DeleteTransactionAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Weighing transaction {id} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting weighing transaction {TransactionId}", id);
            return StatusCode(500, "An error occurred while deleting the weighing transaction.");
        }
    }

    /// <summary>
    /// Captures axle weights for a weighing transaction.
    /// Unified endpoint for all weighing modes (Static, WIM, Mobile):
    /// Frontend sends array of WeighingAxle objects regardless of capture mode.
    /// Backend calculates GVW and validates compliance.
    /// </summary>
    /// <param name="id">Transaction ID</param>
    /// <param name="request">Captured axle weights (multiple axles in single call)</param>
    /// <returns>Updated transaction with compliance status</returns>
    [HttpPost("{id}/capture-weights")]
    [Authorize(Policy = "Permission:weighing.create")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(WeighingResultDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CaptureWeights(Guid id, [FromBody] CaptureWeightsRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (request.Axles == null || !request.Axles.Any())
        {
            return BadRequest("At least one axle weight must be provided");
        }

        try
        {
            // Map DTOs to entities
            var axles = request.Axles.Select(dto => new WeighingAxle
            {
                AxleNumber = dto.AxleNumber,
                MeasuredWeightKg = dto.MeasuredWeightKg,
                AxleConfigurationId = dto.AxleConfigurationId ?? Guid.Empty,
                WeighingId = id,
                CapturedAt = DateTime.UtcNow
            }).ToList();

            var transaction = await _weighingService.CaptureWeightsAsync(id, axles);
            
            // Calculate compliance after weights are captured
            transaction = await _weighingService.CalculateComplianceAsync(id);

            var resultDto = MapToResultDto(transaction);
            return Ok(resultDto);
        }
        catch (KeyNotFoundException)
        {
            return NotFound($"Weighing transaction {id} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing weights for transaction {TransactionId}", id);
            return StatusCode(500, "An error occurred while capturing weights.");
        }
    }

    /// <summary>
    /// Initiates a reweigh cycle for a non-compliant vehicle.
    /// </summary>
    /// <param name="request">Original weighing ID and new ticket number</param>
    /// <returns>New reweigh transaction</returns>
    [HttpPost("reweigh")]
    [Authorize(Policy = "Permission:weighing.create")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(WeighingTransactionDto), 201)]
    [ProducesResponseType(404)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> StartReweigh([FromBody] InitiateReweighRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            return Unauthorized("User ID not found in claims");
        }

        try
        {
            var transaction = await _weighingService.InitiateReweighAsync(
                request.OriginalWeighingId, 
                request.ReweighTicketNumber, 
                userGuid);
            
            var dto = MapToDto(transaction);
            return CreatedAtAction(nameof(GetById), new { id = transaction.Id }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Original weighing transaction not found.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating reweigh transaction");
            return StatusCode(500, "An error occurred while initiating the reweigh transaction.");
        }
    }

    /// <summary>
    /// Maps WeighingTransaction entity to WeighingTransactionDto.
    /// </summary>
    private WeighingTransactionDto MapToDto(WeighingTransaction transaction)
    {
        return new WeighingTransactionDto
        {
            Id = transaction.Id,
            TicketNumber = transaction.TicketNumber,
            VehicleId = transaction.VehicleId,
            VehicleRegNumber = transaction.VehicleRegNumber,
            DriverId = transaction.DriverId,
            TransporterId = transaction.TransporterId,
            StationId = transaction.StationId,
            WeighedByUserId = transaction.WeighedByUserId,
            GvwMeasuredKg = transaction.GvwMeasuredKg,
            GvwPermissibleKg = transaction.GvwPermissibleKg,
            OverloadKg = transaction.OverloadKg,
            ControlStatus = transaction.ControlStatus,
            TotalFeeUsd = transaction.TotalFeeUsd,
            WeighedAt = transaction.WeighedAt,
            IsSync = transaction.IsSync,
            IsCompliant = transaction.IsCompliant,
            IsSentToYard = transaction.IsSentToYard,
            ViolationReason = transaction.ViolationReason,
            ReweighCycleNo = transaction.ReweighCycleNo,
            OriginalWeighingId = transaction.OriginalWeighingId,
            HasPermit = transaction.HasPermit,
            WeighingAxles = transaction.WeighingAxles?.Select(a => new WeighingAxleDto
            {
                Id = a.Id,
                AxleNumber = a.AxleNumber,
                MeasuredWeightKg = a.MeasuredWeightKg,
                PermissibleWeightKg = a.PermissibleWeightKg,
                OverloadKg = a.OverloadKg,
                AxleConfigurationId = a.AxleConfigurationId,
                AxleWeightReferenceId = a.AxleWeightReferenceId,
                CapturedAt = a.CapturedAt
            }).ToList() ?? new()
        };
    }

    /// <summary>
    /// Maps WeighingTransaction entity to WeighingResultDto (compliance focused).
    /// </summary>
    private WeighingResultDto MapToResultDto(WeighingTransaction transaction)
    {
        return new WeighingResultDto
        {
            WeighingId = transaction.Id,
            TicketNumber = transaction.TicketNumber,
            VehicleRegNumber = transaction.VehicleRegNumber,
            GvwMeasuredKg = transaction.GvwMeasuredKg,
            GvwPermissibleKg = transaction.GvwPermissibleKg,
            GvwOverloadKg = transaction.OverloadKg,
            IsCompliant = transaction.IsCompliant,
            ControlStatus = transaction.ControlStatus,
            ViolationReason = transaction.ViolationReason,
            TotalFeeUsd = transaction.TotalFeeUsd,
            HasPermit = transaction.HasPermit,
            ReweighCycleNo = transaction.ReweighCycleNo,
            WeighedAt = transaction.WeighedAt,
            AxleCompliance = transaction.WeighingAxles?.Select(a => new AxleComplianceDto
            {
                AxleNumber = a.AxleNumber,
                MeasuredWeightKg = a.MeasuredWeightKg,
                PermissibleWeightKg = a.PermissibleWeightKg,
                OverloadKg = a.OverloadKg,
                IsCompliant = a.OverloadKg <= 0
            }).ToList() ?? new()
        };
    }
}
