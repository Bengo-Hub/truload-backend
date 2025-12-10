using Microsoft.EntityFrameworkCore;
using truload_backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Data.Seeders;
using TruLoad.Backend.Data.Seeders.UserManagement;
using TruLoad.Backend.Data.Seeders.WeighingOperations;
using TruLoad.Backend.Data.Seeders.SystemConfiguration;

namespace TruLoad.Data.Seeders;

/// <summary>
/// Main database seeder orchestrator for TruLoad
/// Coordinates modular seeders for user management, weighing operations, and system configuration
/// All seeders are idempotent - safe to run multiple times
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(TruLoadDbContext context, ILogger logger)
    {
        try
        {
            logger.LogInformation("=== Starting TruLoad Database Seeding ===");

            // Seed user management data (roles, organizations, departments, stations, work shifts)
            logger.LogInformation("Seeding user management data...");
            var userManagementSeeder = new UserManagementSeeder(context);
            await userManagementSeeder.SeedAsync();

            // Seed permissions and role-permission assignments
            logger.LogInformation("Seeding permissions...");
            await PermissionSeeder.SeedAsync(context);
            await RolePermissionSeeder.SeedAsync(context);

            // Seed users (requires organizations and roles to exist first)
            logger.LogInformation("Seeding users...");
            var userSeeder = new UserSeeder(context);
            await userSeeder.SeedAsync();

            // Seed weighing operations data (axle configurations, axle weight references)
            logger.LogInformation("Seeding weighing operations data...");
            var seedDataPath = Path.Combine(AppContext.BaseDirectory, "Data", "Seeders", "WeighingOperations");
            var weighingSeeder = new WeighingOperationsSeeder(context, seedDataPath);
            await weighingSeeder.SeedAsync();

            // Seed system configuration data (permit types, tolerance settings)
            logger.LogInformation("Seeding system configuration data...");
            var systemConfigSeeder = new SystemConfigurationSeeder(context);
            await systemConfigSeeder.SeedAsync();

            logger.LogInformation("=== Database seeding completed successfully ===");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during database seeding");
            throw;
        }
    }
}




