using System.Reflection;

namespace TruLoad.Backend.Data.Migrations;

/// <summary>
/// Helper to read SQL scripts from embedded resources for EF Core migrations.
/// </summary>
public static class MigrationScriptHelper
{
    private static readonly Assembly Assembly = typeof(MigrationScriptHelper).Assembly;

    /// <summary>
    /// Reads a SQL script from embedded resources.
    /// Script must be located in "Data/Migrations/Scripts/" as per project structure.
    /// </summary>
    /// <param name="scriptName">The name of the script file (e.g., "CreateRegularViews.sql")</param>
    /// <returns>The SQL script content</returns>
    /// <exception cref="FileNotFoundException">Thrown if the script is not found in embedded resources</exception>
    public static string GetScript(string scriptName)
    {
        // Manifest resource names use dots instead of slashes
        var resourceName = $"TruLoad.Backend.Data.Migrations.Scripts.{scriptName}";
        
        using var stream = Assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            // Try to find it if we got the namespace wrong
            var availableResources = Assembly.GetManifestResourceNames();
            var possibleMatch = availableResources.FirstOrDefault(r => r.EndsWith(scriptName, StringComparison.OrdinalIgnoreCase));
            
            if (possibleMatch != null)
            {
                using var fallbackStream = Assembly.GetManifestResourceStream(possibleMatch);
                using var fallbackReader = new StreamReader(fallbackStream!);
                return fallbackReader.ReadToEnd();
            }

            throw new FileNotFoundException($"SQL script '{scriptName}' not found as embedded resource. Expected: {resourceName}. Available: {string.Join(", ", availableResources)}");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
