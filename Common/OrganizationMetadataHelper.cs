using System.Text.Json;

namespace TruLoad.Backend.Common;

/// <summary>
/// Typed access to <see cref="TruLoad.Backend.Models.Organization.MetadataJson"/> - a single reusable
/// jsonb "bag" column for small tenant-level attributes that don't earn a dedicated schema column
/// (read in one place, not queried at scale across the codebase). Mirrors
/// <c>CommercialWeighingService.MergeIndustryMetadata</c>'s merge shape so both jsonb "bag" columns
/// in the schema (this one and <c>WeighingTransaction.IndustryMetadata</c>) are read/written the same
/// way. Add a new constant key here - not a new column - the next time a similarly small,
/// rarely-queried tenant attribute is needed.
/// </summary>
public static class OrganizationMetadataHelper
{
    /// <summary>
    /// Commercial vertical/sub-use-case classification (see <see cref="TruLoad.Backend.Constants.CommercialVerticals"/>).
    /// String value, e.g. "waste_management". Absent for every organisation created before this
    /// feature existed and for any org that hasn't been classified since.
    /// </summary>
    public const string VerticalKey = "vertical";

    /// <summary>
    /// Reads a single string value out of an org's MetadataJson bag. Returns null when the JSON is
    /// absent, malformed, or doesn't contain the key - fails open to "not set" rather than throwing,
    /// same convention as the IndustryMetadata parsing helpers on WeighingTransaction.
    /// </summary>
    public static string? GetString(string? metadataJson, string key)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return null;

        try
        {
            var doc = JsonDocument.Parse(metadataJson);
            if (doc.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }
        catch { /* malformed metadata - treat as absent */ }

        return null;
    }

    /// <summary>Convenience accessor for the "vertical" key - see <see cref="VerticalKey"/>.</summary>
    public static string? GetVertical(string? metadataJson) => GetString(metadataJson, VerticalKey);

    /// <summary>
    /// Merges <paramref name="mergeData"/>'s properties into the existing MetadataJson bag,
    /// overwriting any keys in common and leaving every other existing key untouched - identical
    /// merge shape to <c>CommercialWeighingService.MergeIndustryMetadata</c>.
    /// </summary>
    public static string MergeMetadata(string? existingJson, object mergeData)
    {
        var existing = string.IsNullOrEmpty(existingJson)
            ? new Dictionary<string, object?>()
            : JsonSerializer.Deserialize<Dictionary<string, object?>>(existingJson)
              ?? new Dictionary<string, object?>();

        var mergeJson = JsonSerializer.Serialize(mergeData);
        var mergeDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(mergeJson)
                        ?? new Dictionary<string, object?>();

        foreach (var kvp in mergeDict)
            existing[kvp.Key] = kvp.Value;

        return JsonSerializer.Serialize(existing);
    }

    /// <summary>Sets the "vertical" key, leaving every other metadata key untouched.</summary>
    public static string MergeVertical(string? existingJson, string vertical) =>
        MergeMetadata(existingJson, new { vertical });
}
