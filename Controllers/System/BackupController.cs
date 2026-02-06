using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruLoad.Backend.DTOs.System;
using TruLoad.Backend.Services.Interfaces.System;
using TruLoad.Backend.Authorization.Attributes;

namespace TruLoad.Backend.Controllers.System;

/// <summary>
/// Controller for database backup and restore operations.
/// </summary>
[ApiController]
[Route("api/v1/system/backups")]
[Authorize]
public class BackupController : ControllerBase
{
    private readonly IBackupService _backupService;
    private readonly ILogger<BackupController> _logger;

    public BackupController(
        IBackupService backupService,
        ILogger<BackupController> logger)
    {
        _backupService = backupService;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(global::System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }

    /// <summary>
    /// Get backup system status.
    /// </summary>
    [HttpGet("status")]
    [HasPermission("system.backup_restore")]
    [ProducesResponseType(typeof(BackupSystemStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BackupSystemStatusDto>> GetStatus(CancellationToken ct)
    {
        var status = await _backupService.GetStatusAsync(ct);
        return Ok(status);
    }

    /// <summary>
    /// List all available backups.
    /// </summary>
    [HttpGet]
    [HasPermission("system.backup_restore")]
    [ProducesResponseType(typeof(BackupListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BackupListResponse>> ListBackups(CancellationToken ct)
    {
        var backups = await _backupService.ListBackupsAsync(ct);
        return Ok(backups);
    }

    /// <summary>
    /// Create a new backup.
    /// </summary>
    [HttpPost]
    [HasPermission("system.backup_restore")]
    [ProducesResponseType(typeof(CreateBackupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateBackupResponse>> CreateBackup(
        [FromBody] CreateBackupRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetUserId();
        var result = await _backupService.CreateBackupAsync(request, userId, ct);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        _logger.LogInformation("Backup created by user {UserId}: {FileName}", userId, result.FileName);
        return Ok(result);
    }

    /// <summary>
    /// Download a backup file.
    /// </summary>
    [HttpGet("{fileName}/download")]
    [HasPermission("system.backup_restore")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DownloadBackup(string fileName, CancellationToken ct)
    {
        var result = await _backupService.DownloadBackupAsync(fileName, ct);
        if (result == null)
        {
            return NotFound(new { message = "Backup file not found" });
        }

        return File(result.Value.Stream, result.Value.ContentType, result.Value.FileName);
    }

    /// <summary>
    /// Delete a backup file.
    /// </summary>
    [HttpDelete("{fileName}")]
    [HasPermission("system.backup_restore")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteBackup(string fileName, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await _backupService.DeleteBackupAsync(fileName, userId, ct);

        if (!result)
        {
            return NotFound(new { message = "Backup file not found or could not be deleted" });
        }

        _logger.LogInformation("Backup deleted by user {UserId}: {FileName}", userId, fileName);
        return Ok(new { message = "Backup deleted successfully" });
    }

    /// <summary>
    /// Validate a backup file.
    /// </summary>
    [HttpPost("{fileName}/validate")]
    [HasPermission("system.backup_restore")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ValidateBackup(string fileName, CancellationToken ct)
    {
        var isValid = await _backupService.ValidateBackupAsync(fileName, ct);

        if (!isValid)
        {
            return BadRequest(new { message = "Backup file is invalid or corrupted" });
        }

        return Ok(new { message = "Backup file is valid" });
    }

    /// <summary>
    /// Restore from a backup file.
    /// WARNING: This is a destructive operation that replaces the current database.
    /// </summary>
    [HttpPost("restore")]
    [HasPermission("system.backup_restore")]
    [ProducesResponseType(typeof(RestoreBackupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RestoreBackupResponse>> RestoreBackup(
        [FromBody] RestoreBackupRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = GetUserId();
        _logger.LogWarning("Restore operation initiated by user {UserId} from backup {FileName}",
            userId, request.FileName);

        var result = await _backupService.RestoreBackupAsync(request, userId, ct);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
