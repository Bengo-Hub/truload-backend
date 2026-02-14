using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using TruLoad.Backend.Services.Implementations.Financial;
using TruLoad.Backend.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TruLoad.Backend.Tests.Unit.Services;

public class ECitizenServiceTests
{
    [Fact]
    public void ComputeSecureHash_ShouldMatchPesaflowPythonReference()
    {
        // Arrange - values taken from Tests/e2e/compute_pesaflow_hash.py
        var apiKey = "hkW0lc/+xu9GA5Di";
        var apiSecret = "tgia2h6QEcwqPmJ1Uxv3V9I7cqf6Ub7X";
        var apiClientId = "588";
        var serviceId = "235330";
        var amount = "100";
        var clientIdNumber = "TEST-ID-001";
        var currency = "KES";
        var billRefNumber = "LOCAL-20260211135546";
        var billDesc = "Test Overload Fine";
        var clientName = "Test User";

        var dataString = string.Concat(apiClientId, amount, serviceId, clientIdNumber, currency, billRefNumber, billDesc, clientName, apiSecret);

        // Create service instance with minimal dependencies (not used by ComputeSecureHash)
        var httpClient = new HttpClient();
        var options = new DbContextOptionsBuilder<TruLoadDbContext>()
            .UseInMemoryDatabase(databaseName: "ECitizenServiceTests_Db")
            .Options;
        var db = new TruLoadDbContext(options);

        var svc = new ECitizenService(httpClient, db, Mock.Of<TruLoad.Backend.Services.Interfaces.System.IIntegrationConfigService>(),
            Mock.Of<TruLoad.Backend.Services.Interfaces.Financial.IReceiptService>(), Mock.Of<StackExchange.Redis.IConnectionMultiplexer>(),
            Mock.Of<ILogger<ECitizenService>>());

        // Expected value from Python test (base64 of lowercase hex of HMAC-SHA256)
        var expected = "NjZkYzY1ODc3NjQzZmM2NDQyNzlhMjg4YjEzMTM1OTNkNjY3YjU1NTE0NzNmNTRkODA0Y2U5NTgyYTAxODczZQ==";

        // Act
        var actual = svc.ComputeSecureHash(dataString, apiKey);

        // Assert
        actual.Should().Be(expected);
    }
}
