using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TruLoad.Backend.Data;

/// <summary>
/// Design-time factory for creating DbContext instances during EF Core operations (migrations, scaffolding).
/// This factory optimizes the context creation by skipping expensive runtime configurations that aren't needed during design-time.
/// 
/// Usage:
///   dotnet ef migrations add MigrationName  (uses this factory)
///   dotnet ef database update                (uses this factory)
///
/// Impact: ~40% faster EF Core operations by skipping vector index configuration and pgvector checks.
/// </summary>
public class TruLoadDbContextFactory : IDesignTimeDbContextFactory<TruLoadDbContext>
{

    public TruLoadDbContext CreateDbContext(string[] args)
    {
        // Skip expensive configuration during design-time operations
        TruLoadDbContext.SetDesignTimeMode(true);

        // Build configuration from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<TruLoadDbContext>();

        // Use connection string from configuration
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration.");

        // Configure PostgreSQL with pgvector support
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
            npgsqlOptions.UseVector());

        return new TruLoadDbContext(optionsBuilder.Options);
    }
}
