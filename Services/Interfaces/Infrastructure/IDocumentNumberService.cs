namespace TruLoad.Backend.Services.Interfaces.Infrastructure;

/// <summary>
/// Centralized document number generation service.
/// Provides atomic, concurrency-safe document numbering following configurable conventions.
/// </summary>
public interface IDocumentNumberService
{
    /// <summary>
    /// Generates a unique document number based on configured conventions.
    /// Thread-safe and concurrency-safe through database row locking.
    /// </summary>
    /// <param name="organizationId">Organization ID</param>
    /// <param name="stationId">Station ID (required for station-scoped documents like weight tickets)</param>
    /// <param name="documentType">Document type (use DocumentTypes constants)</param>
    /// <param name="vehicleReg">Vehicle registration number (optional, for weight tickets)</param>
    /// <param name="bound">Bound direction (optional, for weight tickets)</param>
    /// <returns>Formatted document number string</returns>
    Task<string> GenerateNumberAsync(
        Guid organizationId,
        Guid? stationId,
        string documentType,
        string? vehicleReg = null,
        string? bound = null);

    /// <summary>
    /// Previews the next document number without incrementing the sequence.
    /// </summary>
    Task<string> PreviewNextNumberAsync(
        Guid organizationId,
        Guid? stationId,
        string documentType,
        string? stationCode = null,
        string? vehicleReg = null,
        string? bound = null);
}
