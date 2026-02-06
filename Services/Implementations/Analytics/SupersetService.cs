using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TruLoad.Backend.DTOs.Analytics;
using TruLoad.Backend.Services.Interfaces.Analytics;

namespace TruLoad.Backend.Services.Implementations.Analytics;

/// <summary>
/// Implementation of ISupersetService for Apache Superset integration.
/// Handles authentication, guest token generation, and dashboard retrieval.
/// </summary>
public class SupersetService : ISupersetService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly SupersetOptions _supersetOptions;
    private readonly OllamaOptions _ollamaOptions;
    private readonly ILogger<SupersetService> _logger;
    private const string AccessTokenCacheKey = "superset_access_token";

    public SupersetService(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptions<SupersetOptions> supersetOptions,
        IOptions<OllamaOptions> ollamaOptions,
        ILogger<SupersetService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _supersetOptions = supersetOptions.Value;
        _ollamaOptions = ollamaOptions.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_supersetOptions.BaseUrl);
    }

    public async Task<SupersetGuestTokenResponse> GetGuestTokenAsync(SupersetGuestTokenRequest request, CancellationToken ct = default)
    {
        var accessToken = await GetAccessTokenAsync(ct);

        var guestTokenRequest = new
        {
            user = new
            {
                username = "guest",
                first_name = "Guest",
                last_name = "User"
            },
            resources = request.DashboardIds.Select(id => new
            {
                type = "dashboard",
                id = id.ToString()
            }).ToList(),
            rls = request.Filters != null
                ? request.Filters.Select(f => (object)new { clause = $"{f.Key} = '{f.Value}'" }).ToList()
                : new List<object>()
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/security/guest_token/")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(guestTokenRequest),
                Encoding.UTF8,
                "application/json"
            )
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<JsonElement>(content);

        var token = result.GetProperty("token").GetString() ?? throw new InvalidOperationException("Failed to get guest token");
        var expiresAt = DateTime.UtcNow.AddMinutes(_supersetOptions.GuestTokenExpiryMinutes);

        _logger.LogInformation("Generated Superset guest token for dashboards: {Dashboards}", string.Join(", ", request.DashboardIds));

        return new SupersetGuestTokenResponse(token, expiresAt);
    }

    public async Task<List<SupersetDashboardDto>> GetDashboardsAsync(CancellationToken ct = default)
    {
        var accessToken = await GetAccessTokenAsync(ct);

        var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dashboard/");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<JsonElement>(content);

        var dashboards = new List<SupersetDashboardDto>();
        if (result.TryGetProperty("result", out var resultArray))
        {
            foreach (var item in resultArray.EnumerateArray())
            {
                dashboards.Add(ParseDashboard(item));
            }
        }

        return dashboards;
    }

    public async Task<SupersetDashboardDto?> GetDashboardAsync(int dashboardId, CancellationToken ct = default)
    {
        var accessToken = await GetAccessTokenAsync(ct);

        var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/dashboard/{dashboardId}");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(httpRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<JsonElement>(content);

        if (result.TryGetProperty("result", out var dashboardData))
        {
            return ParseDashboard(dashboardData);
        }

        return null;
    }

    public async Task<NaturalLanguageQueryResponse> ExecuteNaturalLanguageQueryAsync(NaturalLanguageQueryRequest request, CancellationToken ct = default)
    {
        try
        {
            // Generate SQL using Ollama
            var sql = await GenerateSqlWithOllamaAsync(request.Question, request.SchemaContext, ct);

            if (string.IsNullOrEmpty(sql))
            {
                return new NaturalLanguageQueryResponse(
                    request.Question,
                    "",
                    null,
                    "Failed to generate SQL from the question",
                    false
                );
            }

            // Execute the SQL via Superset's SQL Lab API
            var results = await ExecuteSqlQueryAsync(sql, ct);

            return new NaturalLanguageQueryResponse(
                request.Question,
                sql,
                results,
                null,
                true
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing natural language query: {Question}", request.Question);
            return new NaturalLanguageQueryResponse(
                request.Question,
                "",
                null,
                ex.Message,
                false
            );
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(AccessTokenCacheKey, out string? cachedToken) && !string.IsNullOrEmpty(cachedToken))
        {
            return cachedToken;
        }

        var loginRequest = new
        {
            username = _supersetOptions.Username,
            password = _supersetOptions.Password,
            provider = "db",
            refresh = true
        };

        var response = await _httpClient.PostAsync(
            "/api/v1/security/login",
            new StringContent(
                JsonSerializer.Serialize(loginRequest),
                Encoding.UTF8,
                "application/json"
            ),
            ct
        );

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<JsonElement>(content);

        var accessToken = result.GetProperty("access_token").GetString() ?? throw new InvalidOperationException("Failed to get access token");

        // Cache token for 4 hours (typical Superset token lifetime)
        _cache.Set(AccessTokenCacheKey, accessToken, TimeSpan.FromHours(4));

        _logger.LogInformation("Obtained new Superset access token");

        return accessToken;
    }

    private async Task<string> GenerateSqlWithOllamaAsync(string question, string? schemaContext, CancellationToken ct)
    {
        using var ollamaClient = new HttpClient
        {
            BaseAddress = new Uri(_ollamaOptions.BaseUrl),
            Timeout = TimeSpan.FromSeconds(_ollamaOptions.TimeoutSeconds)
        };

        var schemaInfo = schemaContext ?? GetDefaultSchemaContext();

        var prompt = $@"You are a SQL expert. Convert the following natural language question to a PostgreSQL query.

Database Schema:
{schemaInfo}

Question: {question}

Rules:
1. Only return the SQL query, no explanations
2. Use PostgreSQL syntax
3. Always include appropriate WHERE clauses for safety
4. Limit results to 1000 rows maximum
5. Use table aliases for readability

SQL Query:";

        var ollamaRequest = new
        {
            model = _ollamaOptions.Model,
            prompt = prompt,
            stream = false
        };

        var response = await ollamaClient.PostAsync(
            "/api/generate",
            new StringContent(
                JsonSerializer.Serialize(ollamaRequest),
                Encoding.UTF8,
                "application/json"
            ),
            ct
        );

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Ollama request failed with status {Status}", response.StatusCode);
            return string.Empty;
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<JsonElement>(content);

        var generatedText = result.GetProperty("response").GetString() ?? string.Empty;

        // Clean up the SQL (remove markdown code blocks if present)
        generatedText = generatedText.Trim();
        if (generatedText.StartsWith("```sql"))
        {
            generatedText = generatedText[6..];
        }
        if (generatedText.StartsWith("```"))
        {
            generatedText = generatedText[3..];
        }
        if (generatedText.EndsWith("```"))
        {
            generatedText = generatedText[..^3];
        }

        return generatedText.Trim();
    }

    private async Task<List<Dictionary<string, object>>?> ExecuteSqlQueryAsync(string sql, CancellationToken ct)
    {
        var accessToken = await GetAccessTokenAsync(ct);

        var queryRequest = new
        {
            database_id = 1, // Default database ID
            sql = sql,
            runAsync = false,
            select_as_cta = false,
            ctas_method = "TABLE"
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sqllab/execute/")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(queryRequest),
                Encoding.UTF8,
                "application/json"
            )
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(httpRequest, ct);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<JsonElement>(content);

        if (result.TryGetProperty("data", out var data))
        {
            var results = new List<Dictionary<string, object>>();
            foreach (var row in data.EnumerateArray())
            {
                var dict = new Dictionary<string, object>();
                foreach (var prop in row.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.Number => prop.Value.GetDouble(),
                        JsonValueKind.String => prop.Value.GetString() ?? "",
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => prop.Value.ToString()
                    };
                }
                results.Add(dict);
            }
            return results;
        }

        return null;
    }

    private static string GetDefaultSchemaContext()
    {
        return @"
Tables:
- weighing_transactions (id, ticket_number, registration_number, gross_weight_kg, tare_weight_kg, net_weight_kg, created_at, station_id)
- case_registers (id, case_no, status, violation_type, created_at, closed_at)
- invoices (id, invoice_number, amount_usd, status, created_at)
- receipts (id, receipt_number, amount_usd, payment_method, created_at)
- yard_entries (id, registration_number, entry_time, release_time, status)
- vehicle_tags (id, registration_number, tag_type, is_open, created_at)
";
    }

    private static SupersetDashboardDto ParseDashboard(JsonElement item)
    {
        return new SupersetDashboardDto(
            Id: item.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
            Title: item.TryGetProperty("dashboard_title", out var title) ? title.GetString() ?? "" : "",
            Slug: item.TryGetProperty("slug", out var slug) ? slug.GetString() : null,
            Url: item.TryGetProperty("url", out var url) ? url.GetString() : null,
            ThumbnailUrl: item.TryGetProperty("thumbnail_url", out var thumb) ? thumb.GetString() : null,
            Published: item.TryGetProperty("published", out var pub) && pub.GetBoolean(),
            CreatedAt: item.TryGetProperty("created_on_delta_humanized", out _) ? DateTime.UtcNow : null,
            ChangedAt: item.TryGetProperty("changed_on_delta_humanized", out _) ? DateTime.UtcNow : null
        );
    }
}
