using TruLoad.Backend.DTOs.System;

namespace TruLoad.Backend.Services.Interfaces.System;

/// <summary>
/// Persists and resolves the pluggable remote backup destination. The configuration
/// is stored as a single JSON document under <c>SettingKeys.BackupDestination</c> in the
/// application settings store; secret parameters are encrypted at rest using
/// <see cref="TruLoad.Backend.Infrastructure.Security.IEncryptionService"/>.
/// </summary>
public interface IBackupDestinationStore
{
    /// <summary>
    /// Returns the destination for API display, with all secret parameters MASKED.
    /// Never returns secrets in clear.
    /// </summary>
    Task<BackupDestinationDto> GetForDisplayAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the fully decrypted destination for internal use (mirroring). NEVER
    /// expose the result to an API response — it carries secrets in clear.
    /// </summary>
    Task<ResolvedBackupDestination> ResolveAsync(CancellationToken ct = default);

    /// <summary>
    /// Persists the destination. Secret params are encrypted at rest. Omitted secret
    /// fields are preserved from the existing stored config (so a masked GET value can
    /// be round-tripped without wiping the secret); an explicit empty string clears one.
    /// </summary>
    Task<BackupDestinationDto> SaveAsync(UpdateBackupDestinationRequest request, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Builds a resolved (decrypted) destination from an incoming request WITHOUT
    /// persisting it, merging omitted secrets from the stored config. Used by the
    /// "test connection" endpoint so an operator can validate before saving.
    /// </summary>
    Task<ResolvedBackupDestination> ResolveFromRequestAsync(UpdateBackupDestinationRequest request, CancellationToken ct = default);
}
