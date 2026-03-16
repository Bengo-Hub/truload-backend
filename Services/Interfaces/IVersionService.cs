using System.Diagnostics;
using System.Reflection;

namespace TruLoad.Backend.Services.Interfaces;

/// <summary>
/// Service for retrieving application version information
/// </summary>
public interface IVersionService
{
    /// <summary>
    /// Gets the current application version
    /// </summary>
    string GetVersion();

    /// <summary>
    /// Gets detailed version information including build metadata
    /// </summary>
    VersionInfo GetVersionInfo();
}

/// <summary>
/// Version information model
/// </summary>
public class VersionInfo
{
    public string Version { get; set; } = "1.0.0";
    public string BuildDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    public string GitCommit { get; set; } = "unknown";
    public string GitBranch { get; set; } = "unknown";
    public string Environment { get; set; } = "development";
}
