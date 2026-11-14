using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TruLoad.Backend.Controllers.WeighingOperations;
using TruLoad.Backend.Data;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Repositories.Weighing.Interfaces;
using TruLoad.Backend.Services.Interfaces.Financial;
using TruLoad.Backend.Services.Interfaces.Integration;
using TruLoad.Backend.Services.Interfaces.Shared;
using Xunit;

namespace Truload.Backend.Tests.Unit;

/// <summary>
/// A new transporter is filed in the MarketFlow CRM when it is created, and creation never
/// depends on the CRM being reachable.
/// </summary>
public class TransporterCrmLinkTests
{
    private const string TenantSlug = "acme-weigh";

    private static (TransporterController controller, Mock<ICrmService> crm, Mock<ITransporterRepository> repo)
        Build(string paymentGateway = "treasury", string? slug = TenantSlug)
    {
        var orgId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<TruLoadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new TruLoadDbContext(options);
        db.Organizations.Add(new Organization
        {
            Id = orgId,
            Code = "ACME",
            Name = "Acme Weighbridge",
            PaymentGateway = paymentGateway,
            SsoTenantSlug = slug
        });
        db.SaveChanges();

        var repo = new Mock<ITransporterRepository>();
        repo.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transporter?)null);
        repo.Setup(r => r.CreateAsync(It.IsAny<Transporter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transporter t, CancellationToken _) => t);

        var crm = new Mock<ICrmService>();
        var tenant = Mock.Of<ITenantContext>(t => t.OrganizationId == orgId);

        var controller = new TransporterController(
            repo.Object,
            Mock.Of<INotificationService>(),
            Mock.Of<ITreasuryService>(),
            crm.Object,
            db,
            tenant,
            new ConfigurationBuilder().Build(),
            NullLogger<TransporterController>.Instance);
        return (controller, crm, repo);
    }

    private static Transporter NewTransporter() => new()
    {
        Name = "Kimani Haulage",
        Code = "KIMANI-1",
        Email = "ops@kimani.example",
        Phone = "+254712345678"
    };

    [Fact]
    public async Task Create_SetsCrmContactIdFromCrmAnswer()
    {
        var (controller, crm, _) = Build();
        var crmId = Guid.NewGuid();
        crm.Setup(c => c.ResolveCustomerAsync(
                TenantSlug, "Kimani Haulage", "ops@kimani.example", "+254712345678", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CrmContactResult(crmId.ToString()));

        var result = await controller.Create(NewTransporter());

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.Value.Should().BeOfType<Transporter>().Which.CrmContactId.Should().Be(crmId);
    }

    [Fact]
    public async Task Create_WhenCrmIsDown_StillCreatesWithNullCrmContactId()
    {
        var (controller, crm, repo) = Build();
        crm.Setup(c => c.ResolveCustomerAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("marketflow unreachable"));

        var result = await controller.Create(NewTransporter());

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.Value.Should().BeOfType<Transporter>().Which.CrmContactId.Should().BeNull();
        repo.Verify(r => r.CreateAsync(It.IsAny<Transporter>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WithNoEmailOrPhone_DoesNotCallCrm()
    {
        var (controller, crm, _) = Build();
        var transporter = NewTransporter();
        transporter.Email = null;
        transporter.Phone = "  ";

        var result = await controller.Create(transporter);

        result.Should().BeOfType<CreatedAtActionResult>()
            .Which.Value.Should().BeOfType<Transporter>().Which.CrmContactId.Should().BeNull();
        crm.Verify(c => c.ResolveCustomerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Create_WhenCrmIsSlow_GivesUpAndCreates()
    {
        var (controller, crm, _) = Build();
        controller.CrmLinkTimeout = TimeSpan.FromMilliseconds(100);
        crm.Setup(c => c.ResolveCustomerAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, string _, string? _, string? _, CancellationToken ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                return new CrmContactResult(Guid.NewGuid().ToString());
            });

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = await controller.Create(NewTransporter());
        watch.Stop();

        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
        result.Should().BeOfType<CreatedAtActionResult>()
            .Which.Value.Should().BeOfType<Transporter>().Which.CrmContactId.Should().BeNull();
    }

    [Fact]
    public async Task Create_ForNonTreasuryTenant_DoesNotCallCrm()
    {
        var (controller, crm, _) = Build(paymentGateway: "ecitizen_pesaflow", slug: null);

        var result = await controller.Create(NewTransporter());

        result.Should().BeOfType<CreatedAtActionResult>();
        crm.Verify(c => c.ResolveCustomerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
