namespace TruLoad.Backend.Services.Interfaces.Authorization;

/// <summary>
/// Service for verifying resource ownership and access rights.
/// Used for enforcing read_own, update_own permissions where users can only access their own resources.
/// </summary>
public interface IOwnershipCheckService
{
    /// <summary>
    /// Check if the current user owns a weighing transaction.
    /// </summary>
    /// <param name="userId">User ID to check</param>
    /// <param name="weighingTransactionId">Weighing transaction ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user owns the resource</returns>
    Task<bool> UserOwnsWeighingTransactionAsync(
        Guid userId,
        Guid weighingTransactionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the current user is assigned to the station that owns a weighing transaction.
    /// </summary>
    /// <param name="userId">User ID to check</param>
    /// <param name="weighingTransactionId">Weighing transaction ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user is assigned to the station</returns>
    Task<bool> UserBelongsToWeighingStationAsync(
        Guid userId,
        Guid weighingTransactionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the current user owns a case register.
    /// </summary>
    /// <param name="userId">User ID to check</param>
    /// <param name="caseRegisterId">Case register ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user owns the resource</returns>
    Task<bool> UserOwnsCaseRegisterAsync(
        Guid userId,
        Guid caseRegisterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the current user owns a special release request.
    /// </summary>
    /// <param name="userId">User ID to check</param>
    /// <param name="specialReleaseId">Special release ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user owns the resource</returns>
    Task<bool> UserOwnsSpecialReleaseAsync(
        Guid userId,
        Guid specialReleaseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the current user belongs to a specific station.
    /// </summary>
    /// <param name="userId">User ID to check</param>
    /// <param name="stationId">Station ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user belongs to the station</returns>
    Task<bool> UserBelongsToStationAsync(
        Guid userId,
        Guid stationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the current user belongs to a specific department.
    /// </summary>
    /// <param name="userId">User ID to check</param>
    /// <param name="departmentId">Department ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user belongs to the department</returns>
    Task<bool> UserBelongsToDepartmentAsync(
        Guid userId,
        Guid departmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the current user belongs to a specific organization.
    /// </summary>
    /// <param name="userId">User ID to check</param>
    /// <param name="organizationId">Organization ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user belongs to the organization</returns>
    Task<bool> UserBelongsToOrganizationAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken = default);
}
