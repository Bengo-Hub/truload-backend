using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TruLoad.Backend.Data;

/// <summary>
/// Design-time factory for creating DbContext instances during EF Core operations (migrations, scaffolding).
///
/// Usage:
///   dotnet ef migrations add MigrationName  (uses this factory)
///   dotnet ef database update                (uses this factory)
///
/// Vector column types are conditionally included based on DATABASE_NO_VECTOR env var.
/// Set DATABASE_NO_VECTOR=true when running ef commands against a DB without pgvector.
/// </summary>
public class TruLoadDbContextFactory : IDesignTimeDbContextFactory<TruLoadDbContext>
{
    public TruLoadDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<TruLoadDbContext>();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration.");

        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
            npgsqlOptions.UseVector());

        return new TruLoadDbContext(optionsBuilder.Options);
    }
}
