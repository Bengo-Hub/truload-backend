using System.Diagnostics;
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
    private VersionInfo? _cachedVersionInfo;

    public VersionService(IConfiguration configuration, IHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
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
        if (_cachedVersionInfo != null)
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

        return _cachedVersionInfo;
    }

    private string GetVersionFromSources()
    {
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
}
