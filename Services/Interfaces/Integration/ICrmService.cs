namespace TruLoad.Backend.Services.Interfaces.Integration;

/// <summary>
/// The CRM contact marketflow-api filed (or found) for a name plus email or phone.
/// </summary>
public record CrmContactResult(string ContactId);

/// <summary>
/// Client for marketflow-api, the CRM that owns customer identity on the platform.
/// </summary>
public interface ICrmService
{
    /// <summary>
    /// Files a customer in the CRM (deduped by email, then phone) under the tenant behind
    /// <paramref name="tenantSlug"/> and returns the CRM contact id. Needs at least one of email
    /// or phone. Throws on any failure, so callers that can carry on without the link must catch.
    /// </summary>
    Task<CrmContactResult> ResolveCustomerAsync(
        string tenantSlug,
        string name,
        string? email,
        string? phone,
        CancellationToken ct = default);
}
