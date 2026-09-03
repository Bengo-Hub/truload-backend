namespace TruLoad.Backend.Constants;

/// <summary>
/// Commercial-weighing vertical/sub-use-case presets. A vertical is stored under the "vertical" key
/// of <see cref="TruLoad.Backend.Models.Organization.MetadataJson"/> (see
/// <see cref="TruLoad.Backend.Common.OrganizationMetadataHelper"/>) - deliberately a metadata value,
/// not a dedicated typed column, since it's read in exactly one place (module/report-catalog
/// resolution) rather than queried at scale like <c>TenantType</c>/<c>WeighingBusinessModel</c>.
///
/// Presets are data, not branches: onboarding a new vertical (or changing what one grants) is a
/// dictionary entry here, never an if/switch scattered across controllers/services. Every preset
/// currently resolves to the same <see cref="TenantModules.DefaultCommercialWeighingModules"/> set
/// because no commercial module is vertical-specific yet - the distinct keys exist so a future
/// vertical-specific module (or report-catalog restriction - see
/// <c>DTOs.Reporting.ReportDtos.ReportDefinitionDto.AllowedVerticals</c>) can be added as a data
/// change to one preset's <see cref="VerticalPreset.DefaultEnabledModules"/>, not a new column or a
/// new branch anywhere else.
/// </summary>
public static class CommercialVerticals
{
    public const string WasteManagement = "waste_management";
    public const string Quarry = "quarry";
    public const string Factory = "factory";
    public const string Logistics = "logistics";
    public const string Agriculture = "agriculture";
    public const string General = "general";

    public class VerticalPreset
    {
        public string Key { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public IReadOnlyList<string> DefaultEnabledModules { get; init; } = Array.Empty<string>();
    }

    public static readonly IReadOnlyDictionary<string, VerticalPreset> Presets =
        new Dictionary<string, VerticalPreset>(StringComparer.OrdinalIgnoreCase)
        {
            [WasteManagement] = new VerticalPreset
            {
                Key = WasteManagement,
                DisplayName = "Waste Management",
                DefaultEnabledModules = TenantModules.DefaultCommercialWeighingModules,
            },
            [Quarry] = new VerticalPreset
            {
                Key = Quarry,
                DisplayName = "Quarry / Mining",
                DefaultEnabledModules = TenantModules.DefaultCommercialWeighingModules,
            },
            [Factory] = new VerticalPreset
            {
                Key = Factory,
                DisplayName = "Factory / Manufacturing",
                DefaultEnabledModules = TenantModules.DefaultCommercialWeighingModules,
            },
            [Logistics] = new VerticalPreset
            {
                Key = Logistics,
                DisplayName = "Logistics & Transport",
                DefaultEnabledModules = TenantModules.DefaultCommercialWeighingModules,
            },
            [Agriculture] = new VerticalPreset
            {
                Key = Agriculture,
                DisplayName = "Agriculture",
                DefaultEnabledModules = TenantModules.DefaultCommercialWeighingModules,
            },
            [General] = new VerticalPreset
            {
                Key = General,
                DisplayName = "General / Other",
                DefaultEnabledModules = TenantModules.DefaultCommercialWeighingModules,
            },
        };

    /// <summary>
    /// Resolves a stored/submitted vertical key to its preset, or null when the key is null/blank/
    /// unrecognised. Null input (every org before this feature, and any unclassified org since) is
    /// the common case and always resolves to null - callers fall through to their existing
    /// TenantType-based default, unchanged.
    /// </summary>
    public static VerticalPreset? Resolve(string? verticalKey)
    {
        if (string.IsNullOrWhiteSpace(verticalKey)) return null;
        return Presets.TryGetValue(verticalKey, out var preset) ? preset : null;
    }
}
