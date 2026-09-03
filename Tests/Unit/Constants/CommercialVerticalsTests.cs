using FluentAssertions;
using TruLoad.Backend.Constants;
using Xunit;

namespace Truload.Backend.Tests.Unit.Constants;

/// <summary>
/// Unit tests for CommercialVerticals - the data-driven vertical preset table backing Phase 2's
/// commercial vertical/sub-use-case classification. Resolve(null) returning null is the load-bearing
/// case: every organisation without a "vertical" MetadataJson key (every org before this feature,
/// and any unclassified org since) must fall through unchanged to the existing TenantType-based
/// module defaults in OrganizationsController.ResolveEnabledModules.
/// </summary>
public class CommercialVerticalsTests
{
    [Fact]
    public void Resolve_Null_ReturnsNull()
    {
        CommercialVerticals.Resolve(null).Should().BeNull();
    }

    [Fact]
    public void Resolve_Blank_ReturnsNull()
    {
        CommercialVerticals.Resolve("   ").Should().BeNull();
    }

    [Fact]
    public void Resolve_UnrecognisedKey_ReturnsNull()
    {
        CommercialVerticals.Resolve("not_a_real_vertical").Should().BeNull();
    }

    [Theory]
    [InlineData(CommercialVerticals.WasteManagement)]
    [InlineData(CommercialVerticals.Quarry)]
    [InlineData(CommercialVerticals.Factory)]
    [InlineData(CommercialVerticals.Logistics)]
    [InlineData(CommercialVerticals.Agriculture)]
    [InlineData(CommercialVerticals.General)]
    public void Resolve_KnownKey_ReturnsPresetWithMatchingKeyAndCommercialModules(string key)
    {
        var preset = CommercialVerticals.Resolve(key);

        preset.Should().NotBeNull();
        preset!.Key.Should().Be(key);
        preset.DefaultEnabledModules.Should().BeEquivalentTo(TenantModules.DefaultCommercialWeighingModules);
    }

    [Fact]
    public void Resolve_IsCaseInsensitive()
    {
        CommercialVerticals.Resolve("QUARRY").Should().NotBeNull();
        CommercialVerticals.Resolve("Quarry").Should().NotBeNull();
    }
}
