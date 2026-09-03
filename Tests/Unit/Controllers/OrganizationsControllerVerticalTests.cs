using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TruLoad.Backend.Constants;
using TruLoad.Backend.DTOs.User;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Models;
using TruLoad.Backend.Repositories.UserManagement.Interfaces;
using TruLoad.Controllers;
using Xunit;

namespace Truload.Backend.Tests.Unit.Controllers;

/// <summary>
/// Integration-style tests for OrganizationsController's Phase 2 vertical classification wiring -
/// exercises the real Create/GetById actions (not a reimplementation of their logic) against a
/// mocked repository, so these fail if the controller's actual module-resolution behaviour
/// regresses. The most important case is <see cref="ExistingOrgWithNoVerticalKey_ResolvesModules_ExactlyAsBeforeFeature"/>:
/// an org with no "vertical" MetadataJson key (every commercial org that existed before this
/// feature shipped) must resolve to the exact same default module list it did before.
/// </summary>
public class OrganizationsControllerVerticalTests
{
    private static OrganizationsController BuildController(Mock<IOrganizationRepository> repo)
        => new(repo.Object, Mock.Of<ITenantContext>(), Mock.Of<ILogger<OrganizationsController>>());

    [Fact]
    public async Task Create_WithRecognisedVertical_PersistsPresetModulesOnReturnedDto()
    {
        var repo = new Mock<IOrganizationRepository>();
        repo.Setup(r => r.CodeExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repo.Setup(r => r.CreateAsync(It.IsAny<Organization>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Organization org, CancellationToken _) => org); // simulate persistence: echo back what was passed in

        var controller = BuildController(repo);

        var request = new CreateOrganizationRequest
        {
            Code = "TEST-QUARRY",
            Name = "Test Quarry Co",
            Vertical = CommercialVerticals.Quarry,
        };

        var result = await controller.Create(request, CancellationToken.None);

        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var dto = created.Value.Should().BeOfType<OrganizationDto>().Subject;

        dto.Vertical.Should().Be(CommercialVerticals.Quarry);
        // Quarry preset today resolves to the same DefaultCommercialWeighingModules set as the
        // generic commercial default (see CommercialVerticals) - asserting the actual preset value
        // here (not just "non-null") so this test still catches a future preset divergence.
        dto.EnabledModules.Should().BeEquivalentTo(TenantModules.DefaultCommercialWeighingModules);
    }

    [Fact]
    public async Task Create_WithUnrecognisedVertical_IgnoresItSilently()
    {
        var repo = new Mock<IOrganizationRepository>();
        repo.Setup(r => r.CodeExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repo.Setup(r => r.CreateAsync(It.IsAny<Organization>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Organization org, CancellationToken _) => org);

        var controller = BuildController(repo);

        var request = new CreateOrganizationRequest
        {
            Code = "TEST-BOGUS",
            Name = "Test Bogus Co",
            Vertical = "not_a_real_vertical",
        };

        var result = await controller.Create(request, CancellationToken.None);

        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var dto = created.Value.Should().BeOfType<OrganizationDto>().Subject;

        dto.Vertical.Should().BeNull();
    }

    [Fact]
    public async Task ExistingOrgWithNoVerticalKey_ResolvesModules_ExactlyAsBeforeFeature()
    {
        var existingCommercialOrg = new Organization
        {
            Id = Guid.NewGuid(),
            Code = "PRE-EXISTING",
            Name = "Pre-existing Commercial Org",
            TenantType = TenantModules.TenantTypeCommercialWeighing,
            MetadataJson = null, // every org before this feature shipped
        };

        var repo = new Mock<IOrganizationRepository>();
        repo.Setup(r => r.GetByIdAsync(existingCommercialOrg.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCommercialOrg);

        var controller = BuildController(repo);

        var result = await controller.GetById(existingCommercialOrg.Id, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<OrganizationDto>().Subject;

        dto.Vertical.Should().BeNull();
        dto.EnabledModules.Should().BeEquivalentTo(TenantModules.DefaultCommercialWeighingModules);
    }

    [Fact]
    public async Task ExistingEnforcementOrgWithNoVerticalKey_ResolvesModules_ExactlyAsBeforeFeature()
    {
        var existingEnforcementOrg = new Organization
        {
            Id = Guid.NewGuid(),
            Code = "PRE-EXISTING-ENF",
            Name = "Pre-existing Enforcement Org",
            TenantType = TenantModules.TenantTypeAxleLoadEnforcement,
            MetadataJson = null,
        };

        var repo = new Mock<IOrganizationRepository>();
        repo.Setup(r => r.GetByIdAsync(existingEnforcementOrg.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEnforcementOrg);

        var controller = BuildController(repo);

        var result = await controller.GetById(existingEnforcementOrg.Id, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<OrganizationDto>().Subject;

        dto.Vertical.Should().BeNull();
        dto.EnabledModules.Should().BeEquivalentTo(TenantModules.AllModules);
    }
}
