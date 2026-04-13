using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Reflection;
using TruLoad.Backend.Services.Interfaces;

namespace TruLoad.Backend.Services.Implementations.System;

/// <summary>
/// Service for retrieving application version information from multiple sources
/// </summary>
public class VersionService : IVersionService
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VersionService> _logger;
    private VersionInfo? _cachedVersionInfo;
    private DateTime _cacheExpiresAtUtc = DateTime.MinValue;

    public VersionService(
        IConfiguration configuration,
        IHostEnvironment environment,
        IHttpClientFactory httpClientFactory,
        ILogger<VersionService> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current application version
    /// </summary>
    public string GetVersion()
    {
        return GetVersionInfo().Version;
    }

    /// <summary>
    /// Gets detailed version information including build metadata
    /// </summary>
    public VersionInfo GetVersionInfo()
    {
        if (_cachedVersionInfo != null && DateTime.UtcNow < _cacheExpiresAtUtc)
        {
            return _cachedVersionInfo;
        }

        _cachedVersionInfo = new VersionInfo
        {
            Version = GetVersionFromSources(),
            BuildDate = GetBuildDate(),
            GitCommit = GetGitCommit(),
            GitBranch = GetGitBranch(),
            Environment = _environment.EnvironmentName
        };
        _cacheExpiresAtUtc = DateTime.UtcNow.AddMinutes(GetCacheRefreshMinutes());

        return _cachedVersionInfo;
    }

    private string GetVersionFromSources()
    {
        // Prefer latest GitHub release/tag so deployed UI always tracks latest published version.
        var githubVersion = GetGitHubLatestVersion();
        if (!string.IsNullOrEmpty(githubVersion))
        {
            return StripVersionPrefix(githubVersion);
        }

        // Try environment variable first (set by Docker build arg / CI/CD)
        var envVersion = Environment.GetEnvironmentVariable("VERSION")
            ?? _configuration["VERSION"];
        if (!string.IsNullOrEmpty(envVersion))
        {
            return StripVersionPrefix(envVersion);
        }

        // Try git tag (works in dev, not in Docker containers)
        var gitVersion = GetGitTagVersion();
        if (!string.IsNullOrEmpty(gitVersion))
        {
            return StripVersionPrefix(gitVersion);
        }

        // Try assembly version
        var assemblyVersion = GetAssemblyVersion();
        if (!string.IsNullOrEmpty(assemblyVersion))
        {
            return assemblyVersion;
        }

        // Final fallback
        return "1.0.0";
    }

    private int GetCacheRefreshMinutes()
    {
        var raw = _configuration["Versioning:RefreshMinutes"];
        if (int.TryParse(raw, out var minutes) && minutes > 0)
            return minutes;

        return 5;
    }

    /// <summary>
    /// Strip leading "v" or "V" prefix from version strings (e.g. "v1.0.4" → "1.0.4").
    /// </summary>
    private static string StripVersionPrefix(string version)
    {
        var trimmed = version.Trim();
        return trimmed.StartsWith('v') || trimmed.StartsWith('V')
            ? trimmed[1..]
            : trimmed;
    }

    private string GetAssemblyVersion()
    {
        try
        {
            var assembly = Assembly.GetEntryAssembly();
            if (assembly != null)
            {
                var version = assembly.GetName().Version;
                if (version != null)
                {
                    return $"{version.Major}.{version.Minor}.{version.Build}";
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    private string GetGitTagVersion()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "describe --tags --abbrev=0",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                return output.Trim();
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    private string? GetGitHubLatestVersion()
    {
        var repository = _configuration["Versioning:GitHubRepository"]
            ?? Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")
            ?? "Bengo-Hub/truload-backend";
        var apiBaseUrl = _configuration["Versioning:GitHubApiBaseUrl"] ?? "https://api.github.com";

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TruLoadBackend", "1.0"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            var token = _configuration["Versioning:GitHubToken"] ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrWhiteSpace(token))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
            }

            var latestReleaseUrl = $"{apiBaseUrl.TrimEnd('/')}/repos/{repository}/releases/latest";
            using var releaseResponse = client.GetAsync(latestReleaseUrl).GetAwaiter().GetResult();
            if (releaseResponse.IsSuccessStatusCode)
            {
                var latestRelease = releaseResponse.Content.ReadFromJsonAsync<GitHubReleaseResponse>()
                    .GetAwaiter().GetResult();
                if (!string.IsNullOrWhiteSpace(latestRelease?.TagName))
                {
                    return latestRelease.TagName;
                }
            }

            var tagsUrl = $"{apiBaseUrl.TrimEnd('/')}/repos/{repository}/tags?per_page=1";
            var tags = client.GetFromJsonAsync<List<GitHubTagResponse>>(tagsUrl).GetAwaiter().GetResult();
            var latestTag = tags?.FirstOrDefault()?.Name;
            if (!string.IsNullOrWhiteSpace(latestTag))
            {
                return latestTag;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to fetch latest version from GitHub");
        }

        return null;
    }

    private string GetGitCommit()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse --short HEAD",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                return output.Trim();
            }
        }
        catch
        {
            // Ignore errors
        }
        return "unknown";
    }

    private string GetGitBranch()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse --abbrev-ref HEAD",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                return output.Trim();
            }
        }
        catch
        {
            // Ignore errors
        }
        return "unknown";
    }

    private string GetBuildDate()
    {
        try
        {
            var assembly = Assembly.GetEntryAssembly();
            if (assembly != null)
            {
                var fileInfo = new FileInfo(assembly.Location);
                return fileInfo.LastWriteTimeUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
            }
        }
        catch
        {
            // Ignore errors
        }
        return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }

    private sealed class GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }
    }

    private sealed class GitHubTagResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
