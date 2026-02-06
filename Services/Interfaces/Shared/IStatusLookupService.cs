namespace TruLoad.Backend.Services.Interfaces.Shared;

/// <summary>
/// Centralized service for looking up status/type entities by code.
/// Provides cached access to commonly used lookup values.
/// </summary>
public interface IStatusLookupService
{
    // Case Statuses
    Task<Guid> GetCaseStatusIdAsync(string code, CancellationToken ct = default);
    Task<Guid?> TryGetCaseStatusIdAsync(string code, CancellationToken ct = default);

    // Disposition Types
    Task<Guid> GetDispositionTypeIdAsync(string code, CancellationToken ct = default);
    Task<Guid?> TryGetDispositionTypeIdAsync(string code, CancellationToken ct = default);

    // Violation Types
    Task<Guid> GetViolationTypeIdAsync(string code, CancellationToken ct = default);
    Task<Guid?> TryGetViolationTypeIdAsync(string code, CancellationToken ct = default);

    // Hearing Statuses
    Task<Guid> GetHearingStatusIdAsync(string code, CancellationToken ct = default);
    Task<Guid?> TryGetHearingStatusIdAsync(string code, CancellationToken ct = default);

    // Hearing Outcomes
    Task<Guid> GetHearingOutcomeIdAsync(string code, CancellationToken ct = default);
    Task<Guid?> TryGetHearingOutcomeIdAsync(string code, CancellationToken ct = default);

    // Clear cache (useful for testing)
    void ClearCache();
}

/// <summary>
/// Common status codes used throughout the system.
/// Named CommonStatusCodes to avoid conflict with Microsoft.AspNetCore.Http.StatusCodes.
/// </summary>
public static class CommonStatusCodes
{
    // Case Statuses
    public const string CaseOpen = "OPEN";
    public const string CaseClosed = "CLOSED";
    public const string CaseEscalated = "ESCALATED";
    public const string CaseInvestigation = "INVESTIGATION";

    // Disposition Types
    public const string DispositionPending = "PENDING";
    public const string DispositionSpecialRelease = "SPECIAL_RELEASE";
    public const string DispositionProsecuted = "PROSECUTED";
    public const string DispositionPaid = "PAID";

    // Violation Types
    public const string ViolationOverload = "OVERLOAD";
    public const string ViolationTag = "TAG";
    public const string ViolationTaggedVehicle = "TAGGED_VEHICLE";

    // Hearing Statuses
    public const string HearingScheduled = "SCHEDULED";
    public const string HearingCompleted = "COMPLETED";
    public const string HearingAdjourned = "ADJOURNED";
    public const string HearingCancelled = "CANCELLED";

    // Hearing Outcomes
    public const string OutcomeConvicted = "CONVICTED";
    public const string OutcomeAcquitted = "ACQUITTED";
    public const string OutcomeDismissed = "DISMISSED";
}
