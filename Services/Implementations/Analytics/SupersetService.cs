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

    public async Task<SupersetGuestTokenResponse> GetGuestTokenAsync(SupersetGuestTokenRequest request, string? username = null, string? firstName = null, string? lastName = null, CancellationToken ct = default)
    {
        var accessToken = await GetAccessTokenAsync(ct);

        var guestTokenRequest = new
        {
            user = new
            {
                username = username ?? "guest",
                first_name = firstName ?? "Guest",
                last_name = lastName ?? "User"
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

        // Filter by service tag if configured
        if (!string.IsNullOrWhiteSpace(_supersetOptions.ServiceTag))
        {
            var tag = _supersetOptions.ServiceTag;
            dashboards = dashboards
                .Where(d =>
                    d.Title.Contains(tag, StringComparison.OrdinalIgnoreCase) ||
                    (d.Slug != null && d.Slug.Contains(tag, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            _logger.LogDebug("Filtered dashboards by service tag '{Tag}': {Count} of {Total}",
                tag, dashboards.Count, resultArray.GetArrayLength());
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

            // Validate the SQL before execution
            if (!ValidateSql(sql))
            {
                return new NaturalLanguageQueryResponse(
                    request.Question,
                    sql,
                    null,
                    "The generated SQL failed security validation. It must be a read-only SELECT statement.",
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

        var prompt = $@"You are a PostgreSQL expert. Generate ONLY a single SQL SELECT query for the question below.

DATABASE SCHEMA:
{schemaInfo}

QUESTION: {question}

FEW-SHOT EXAMPLES:
Q: ""How many weighing transactions were recorded today?""
SQL: SELECT COUNT(*) as today_count FROM weighing_transactions WHERE created_at >= CURRENT_DATE AND deleted_at IS NULL;

Q: ""Show me the top 5 transporters with the highest total overload this year""
SQL: SELECT t.name, SUM(wt.overload_kg) as total_overload FROM weighing_transactions wt JOIN transporters t ON wt.transporter_id = t.id WHERE wt.created_at >= DATE_TRUNC('year', CURRENT_DATE) AND wt.is_compliant = false AND wt.deleted_at IS NULL GROUP BY t.name ORDER BY total_overload DESC LIMIT 5;

Q: ""What is the average compliance rate across all stations?""
SQL: SELECT s.name, (COUNT(CASE WHEN wt.is_compliant = true THEN 1 END) * 100.0 / COUNT(*)) as compliance_rate FROM weighing_transactions wt JOIN stations s ON wt.station_id = s.id WHERE wt.deleted_at IS NULL GROUP BY s.name;

RULES:
1. Output ONLY the SQL query. No explanations, no markdown, no comments.
2. Use PostgreSQL syntax (date_trunc, INTERVAL, CURRENT_DATE, COALESCE, etc.).
3. NEVER use DELETE, UPDATE, INSERT, DROP, ALTER, TRUNCATE, or CREATE statements.
4. NEVER join tables unless the question explicitly requires data from multiple tables.
5. Always add LIMIT 1000 at the end.
6. Use table aliases (e.g., wt for weighing_transactions).
7. For date filters, use: created_at >= CURRENT_DATE - INTERVAL '30 days'
8. Use is_compliant = true/false for compliance checks.
9. For counts, use: SELECT COUNT(*) as total FROM ...
10. For aggregations, use meaningful column aliases.

SQL:";

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

    private static bool ValidateSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return false;

        var upperSql = sql.ToUpperInvariant().Trim();

        // Must start with SELECT (ignoring whitespace and optional WITH)
        if (!upperSql.StartsWith("SELECT") && !upperSql.StartsWith("WITH"))
        {
            return false;
        }

        // Blacklist destructive commands
        string[] blacklist = { "DELETE", "UPDATE", "INSERT", "DROP", "ALTER", "TRUNCATE", "CREATE", "GRANT", "REVOKE", "EXEC", "EXECUTE" };
        
        // Simple word-boundary check to avoid false positives (e.g., "updated_at" column)
        foreach (var word in blacklist)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(upperSql, $@"\b{word}\b"))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<List<Dictionary<string, object>>?> ExecuteSqlQueryAsync(string sql, CancellationToken ct)
    {
        var accessToken = await GetAccessTokenAsync(ct);

        var queryRequest = new
        {
            database_id = _supersetOptions.DatabaseId, // Use configured database ID
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
TABLES AND COLUMNS:

weighing_transactions (
  id UUID PK, ticket_number VARCHAR, vehicle_reg_number VARCHAR,
  gvw_measured_kg INT, gvw_permissible_kg INT, overload_kg INT,
  is_compliant BOOLEAN, control_status VARCHAR, total_fee_usd DECIMAL,
  weighed_at TIMESTAMPTZ, capture_source VARCHAR,
  station_id UUID FK, vehicle_id UUID FK, driver_id UUID FK, transporter_id UUID FK,
  created_at TIMESTAMPTZ, deleted_at TIMESTAMPTZ NULL
)

transporters (
  id UUID PK, name VARCHAR, code VARCHAR, contact_person VARCHAR, email VARCHAR
)

case_registers (
  id UUID PK, case_no VARCHAR, vehicle_id UUID FK, weighing_id UUID FK NULL,
  violation_type_id UUID FK, case_status_id UUID FK, disposition_type_id UUID FK NULL,
  escalated_to_case_manager BOOLEAN, created_at TIMESTAMPTZ, closed_at TIMESTAMPTZ NULL,
  deleted_at TIMESTAMPTZ NULL
)

invoices (
  id UUID PK, invoice_no VARCHAR, amount_due DECIMAL, currency VARCHAR,
  status VARCHAR, generated_at TIMESTAMPTZ, due_date TIMESTAMPTZ,
  prosecution_case_id UUID FK NULL, deleted_at TIMESTAMPTZ NULL
)

receipts (
  id UUID PK, receipt_no VARCHAR, amount_paid DECIMAL, payment_method VARCHAR,
  invoice_id UUID FK, created_at TIMESTAMPTZ, deleted_at TIMESTAMPTZ NULL
)

yard_entries (
  id UUID PK, vehicle_reg_number VARCHAR, entry_time TIMESTAMPTZ,
  release_time TIMESTAMPTZ NULL, status VARCHAR, weighing_id UUID FK NULL,
  created_at TIMESTAMPTZ, deleted_at TIMESTAMPTZ NULL
)

stations (
  id UUID PK, name VARCHAR, code VARCHAR, county VARCHAR, road VARCHAR,
  is_active BOOLEAN
)

EXAMPLE QUERIES:
-- Count all weighing transactions
SELECT COUNT(*) as total FROM weighing_transactions WHERE deleted_at IS NULL;

-- Overloaded vehicles in last 30 days
SELECT vehicle_reg_number, overload_kg, created_at
FROM weighing_transactions
WHERE is_overloaded = true AND deleted_at IS NULL
  AND created_at >= CURRENT_DATE - INTERVAL '30 days'
ORDER BY created_at DESC LIMIT 1000;

-- Total revenue by currency
SELECT currency, SUM(amount_due) as total_revenue, COUNT(*) as invoice_count
FROM invoices WHERE deleted_at IS NULL AND status != 'cancelled'
GROUP BY currency;
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
