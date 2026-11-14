using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TruLoad.Backend.Services.Implementations.Integration;
using Xunit;

namespace TruLoad.Backend.Tests.Unit.Services;

/// <summary>
/// CrmService files a transporter straight in marketflow-api: it resolves the auth tenant UUID
/// for the slug, then posts to the internal contact upsert with the service key.
/// </summary>
public class CrmServiceTests
{
    private const string TenantSlug = "acme-weigh";
    private const string ServiceKey = "test-internal-key";
    private static readonly Guid TenantId = Guid.Parse("7c1f6a52-4b8e-4f0e-9a51-2f1d3c4b5a69");

    private sealed class StubHandler : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string? Body)> Calls { get; } = new();
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            Calls.Add((request, body));
            return Respond(request);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    private static (CrmService service, StubHandler handler) Build(Dictionary<string, string?>? extra = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["INTERNAL_SERVICE_KEY"] = ServiceKey,
            ["Auth:BaseUrl"] = "http://auth.test",
            ["MARKETFLOW_API_URL"] = "http://marketflow.test/"
        };
        if (extra != null)
            foreach (var kv in extra) settings[kv.Key] = kv.Value;

        var handler = new StubHandler();
        var service = new CrmService(
            new HttpClient(handler),
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<CrmService>.Instance);
        return (service, handler);
    }

    private static HttpResponseMessage HappyPath(HttpRequestMessage req, Guid contactId) =>
        req.RequestUri!.Host == "auth.test"
            ? Json(HttpStatusCode.OK, new { id = TenantId.ToString(), slug = TenantSlug })
            : Json(HttpStatusCode.OK, new { id = contactId.ToString(), first_name = "Kimani", last_name = "Haulage Ltd" });

    [Fact]
    public async Task ResolveCustomer_PostsUpsertWithTenantUuidKeyAndSplitName()
    {
        var (service, handler) = Build();
        var contactId = Guid.NewGuid();
        handler.Respond = req => HappyPath(req, contactId);

        var result = await service.ResolveCustomerAsync(TenantSlug, "Kimani  Haulage Ltd", " ops@kimani.example ", "+254712345678");

        result.ContactId.Should().Be(contactId.ToString());
        handler.Calls.Should().HaveCount(2);

        handler.Calls[0].Request.RequestUri!.ToString().Should().Be($"http://auth.test/api/v1/tenants/by-slug/{TenantSlug}");

        var (upsert, body) = handler.Calls[1];
        upsert.Method.Should().Be(HttpMethod.Post);
        upsert.RequestUri!.ToString().Should().Be("http://marketflow.test/api/v1/internal/contacts/upsert");
        upsert.Headers.GetValues("X-API-Key").Should().ContainSingle().Which.Should().Be(ServiceKey);

        using var doc = JsonDocument.Parse(body!);
        var root = doc.RootElement;
        root.GetProperty("tenant_id").GetString().Should().Be(TenantId.ToString());
        root.GetProperty("email").GetString().Should().Be("ops@kimani.example");
        root.GetProperty("phone").GetString().Should().Be("+254712345678");
        root.GetProperty("first_name").GetString().Should().Be("Kimani");
        root.GetProperty("last_name").GetString().Should().Be("Haulage Ltd");
    }

    [Fact]
    public async Task ResolveCustomer_OmitsMissingFields()
    {
        var (service, handler) = Build();
        handler.Respond = req => HappyPath(req, Guid.NewGuid());

        await service.ResolveCustomerAsync(TenantSlug, "Kimani", null, "+254712345678");

        using var doc = JsonDocument.Parse(handler.Calls[1].Body!);
        doc.RootElement.TryGetProperty("email", out _).Should().BeFalse();
        doc.RootElement.TryGetProperty("last_name", out _).Should().BeFalse();
        doc.RootElement.GetProperty("first_name").GetString().Should().Be("Kimani");
    }

    [Fact]
    public async Task ResolveCustomer_CachesTenantLookup()
    {
        var (service, handler) = Build();
        handler.Respond = req => HappyPath(req, Guid.NewGuid());

        await service.ResolveCustomerAsync(TenantSlug, "Kimani Haulage", "ops@kimani.example", null);
        await service.ResolveCustomerAsync(TenantSlug, "Otieno Freight", "ops@otieno.example", null);

        handler.Calls.Count(c => c.Request.RequestUri!.Host == "auth.test").Should().Be(1);
        handler.Calls.Count(c => c.Request.RequestUri!.Host == "marketflow.test").Should().Be(2);
    }

    [Fact]
    public async Task ResolveCustomer_UsesClusterDefaultWhenUrlNotConfigured()
    {
        var (service, handler) = Build(new Dictionary<string, string?> { ["MARKETFLOW_API_URL"] = "" });
        handler.Respond = req => HappyPath(req, Guid.NewGuid());

        await service.ResolveCustomerAsync(TenantSlug, "Kimani Haulage", "ops@kimani.example", null);

        handler.Calls[1].Request.RequestUri!.ToString()
            .Should().Be(CrmService.DefaultMarketflowApiUrl + "/api/v1/internal/contacts/upsert");
    }

    [Fact]
    public async Task ResolveCustomer_OnCrmError_ThrowsWithCodeButNoContactDetails()
    {
        var (service, handler) = Build();
        handler.Respond = req => req.RequestUri!.Host == "auth.test"
            ? Json(HttpStatusCode.OK, new { id = TenantId.ToString() })
            : Json(HttpStatusCode.BadRequest, new { error = "bad contact ops@kimani.example +254712345678", code = "missing_identifier" });

        var act = () => service.ResolveCustomerAsync(TenantSlug, "Kimani Haulage", "ops@kimani.example", "+254712345678");

        var ex = (await act.Should().ThrowAsync<HttpRequestException>()).Which;
        ex.Message.Should().Contain("missing_identifier");
        ex.Message.Should().NotContain("ops@kimani.example").And.NotContain("+254712345678");
    }

    [Fact]
    public async Task ResolveCustomer_WhenTenantUnknown_ThrowsWithoutCallingCrm()
    {
        var (service, handler) = Build();
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        var act = () => service.ResolveCustomerAsync(TenantSlug, "Kimani Haulage", "ops@kimani.example", null);

        await act.Should().ThrowAsync<HttpRequestException>();
        handler.Calls.Should().ContainSingle().Which.Request.RequestUri!.Host.Should().Be("auth.test");
    }

    [Fact]
    public async Task ResolveCustomer_WithoutServiceKey_Throws()
    {
        var (service, handler) = Build(new Dictionary<string, string?> { ["INTERNAL_SERVICE_KEY"] = "" });

        var act = () => service.ResolveCustomerAsync(TenantSlug, "Kimani Haulage", "ops@kimani.example", null);

        await act.Should().ThrowAsync<InvalidOperationException>();
        handler.Calls.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Kimani", "Kimani", "")]
    [InlineData("  Kimani   Haulage  Ltd ", "Kimani", "Haulage Ltd")]
    [InlineData("", "", "")]
    [InlineData(null, "", "")]
    public void SplitName_MatchesTreasurySplit(string? name, string first, string last)
    {
        CrmService.SplitName(name).Should().Be((first, last));
    }
}
