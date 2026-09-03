using FluentAssertions;
using TruLoad.Backend.Common;
using Xunit;

namespace Truload.Backend.Tests.Unit.Common;

/// <summary>
/// Unit tests for OrganizationMetadataHelper - the typed accessor for Organization.MetadataJson
/// (Phase 2 of the commercial vertical classification work). The most important case here is the
/// "no metadata set" path: every organisation that existed before this column was added, and any
/// org that hasn't set any metadata key since, must resolve every key as absent (null) - not throw,
/// not default to some other value - so every call site that reads this bag stays byte-identical to
/// its pre-metadata behaviour for those orgs.
/// </summary>
public class OrganizationMetadataHelperTests
{
    [Fact]
    public void GetVertical_NullMetadataJson_ReturnsNull()
    {
        OrganizationMetadataHelper.GetVertical(null).Should().BeNull();
    }

    [Fact]
    public void GetVertical_EmptyMetadataJson_ReturnsNull()
    {
        OrganizationMetadataHelper.GetVertical(string.Empty).Should().BeNull();
    }

    [Fact]
    public void GetVertical_MalformedJson_ReturnsNullInsteadOfThrowing()
    {
        OrganizationMetadataHelper.GetVertical("{not valid json").Should().BeNull();
    }

    [Fact]
    public void GetVertical_JsonWithoutVerticalKey_ReturnsNull()
    {
        OrganizationMetadataHelper.GetVertical("""{"someOtherKey":"x"}""").Should().BeNull();
    }

    [Fact]
    public void MergeVertical_ThenGetVertical_RoundTrips()
    {
        var json = OrganizationMetadataHelper.MergeVertical(null, "quarry");

        OrganizationMetadataHelper.GetVertical(json).Should().Be("quarry");
    }

    [Fact]
    public void MergeVertical_PreservesExistingUnrelatedKeys()
    {
        var existing = OrganizationMetadataHelper.MergeMetadata(null, new { someFutureKey = "keepme" });

        var updated = OrganizationMetadataHelper.MergeVertical(existing, "factory");

        OrganizationMetadataHelper.GetVertical(updated).Should().Be("factory");
        OrganizationMetadataHelper.GetString(updated, "someFutureKey").Should().Be("keepme");
    }

    [Fact]
    public void MergeVertical_OverwritesPreviousVerticalValue()
    {
        var first = OrganizationMetadataHelper.MergeVertical(null, "quarry");
        var second = OrganizationMetadataHelper.MergeVertical(first, "logistics");

        OrganizationMetadataHelper.GetVertical(second).Should().Be("logistics");
    }
}
