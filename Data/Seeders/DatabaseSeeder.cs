using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Data.Seeders;
using TruLoad.Backend.Data.Seeders.UserManagement;
using TruLoad.Backend.Data.Seeders.WeighingOperations;
using TruLoad.Backend.Data.Seeders.SystemConfiguration;
using TruLoad.Backend.Data.Seeders.CaseManagement;
using TruLoad.Backend.Data.Seeders.Yard;
using TruLoad.Backend.Data.Seeders.Infrastructure;

namespace TruLoad.Data.Seeders;

/// <summary>
/// Main database seeder orchestrator for TruLoad
/// Coordinates modular seeders for user management, weighing operations, and system configuration
/// All seeders are idempotent - safe to run multiple times
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        TruLoadDbContext context, 
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ILogger logger)
    {
        try
        {
            logger.LogInformation("=== Starting TruLoad Database Seeding ===");

            // Seed roles first (includes SUPERUSER and all 7 roles)
            logger.LogInformation("Seeding roles...");
            var roleSeeder = new RoleSeeder(roleManager);
            await roleSeeder.SeedAsync();

            // Seed user management data (organizations, departments, stations, work shifts)
            logger.LogInformation("Seeding user management data...");
            var userManagementSeeder = new UserManagementSeeder(context);
            await userManagementSeeder.SeedAsync();

            // Seed permissions and role-permission assignments
            logger.LogInformation("Seeding permissions...");
            await PermissionSeeder.SeedAsync(context);
            await RolePermissionSeeder.SeedAsync(context);

            // Seed users (requires organizations and roles to exist first)
            logger.LogInformation("Seeding users...");
            var userSeeder = new UserSeeder(userManager, roleManager, context);
            await userSeeder.SeedAsync();

            // Seed weighing operations data (axle configurations, axle weight references)
            logger.LogInformation("Seeding weighing operations data...");
            var seedDataPath = Path.Combine(AppContext.BaseDirectory, "Data", "Seeders", "WeighingOperations");
            var weighingSeeder = new WeighingOperationsSeeder(context, seedDataPath);
            await weighingSeeder.SeedAsync();

            // Seed Technical / Annual Calibration Logic (requires UserManagementSeeder to have run so default station exists)
            logger.LogInformation("Seeding annual calibration baseline...");
            var annualCalibrationSeeder = new TruLoad.Backend.Data.Seeders.Technical.AnnualCalibrationSeeder(context);
            await annualCalibrationSeeder.SeedAsync();

            // Seed geographic data (Kenya counties and districts/subcounties) for prosecution and location hierarchy
            logger.LogInformation("Seeding Kenya counties and districts...");
            var kenyaGeographicSeeder = new KenyaCountiesDistrictsSeeder(context);
            await kenyaGeographicSeeder.SeedAsync();

            // Seed reference data (cargo types, origins/destinations, roads)
            logger.LogInformation("Seeding reference data...");
            var cargoTypesSeeder = new CargoTypesSeeder(context);
            await cargoTypesSeeder.SeedAsync();
            
            var originsDestinationsSeeder = new OriginsDestinationsSeeder(context);
            await originsDestinationsSeeder.SeedAsync();
            
            var roadsSeeder = new RoadsSeeder(context);
            await roadsSeeder.SeedAsync();

            // Link roads to counties/districts (many-to-many) and seed courts per county
            logger.LogInformation("Seeding road–county/district links and courts...");
            var kenyaRoadsCourtsSeeder = new KenyaRoadsCourtsSeeder(context);
            await kenyaRoadsCourtsSeeder.SeedAsync();
            
            // Seed fee bands for EAC and Traffic Act
            logger.LogInformation("Seeding fee bands...");
            var axleFeeScheduleSeeder = new AxleFeeScheduleSeeder(context);
            await axleFeeScheduleSeeder.SeedAsync();

            // Seed system configuration data (permit types, tolerance settings)
            logger.LogInformation("Seeding system configuration data...");
            var systemConfigSeeder = new SystemConfigurationSeeder(context);
            await systemConfigSeeder.SeedAsync();

            // Seed tag categories for vehicle tagging (KeNHA, KURA, etc.)
            logger.LogInformation("Seeding tag categories...");
            await TagCategorySeeder.SeedAsync(context);

            // Seed case management taxonomies (case statuses, disposition types, violation types, etc.)
            logger.LogInformation("Seeding case management taxonomies...");
            await CaseManagementTaxonomySeeder.SeedAsync(context);

            // Seed act definitions (Traffic Act Cap 403, EAC Act 2016) - required for prosecution
            logger.LogInformation("Seeding act definitions...");
            await ActDefinitionSeeder.SeedAsync(context);

            // Seed exchange rate defaults (USD/KES manual rate + API settings)
            logger.LogInformation("Seeding exchange rate defaults...");
            await ExchangeRateSeeder.SeedAsync(context);

            // Seed document naming conventions (Sprint 22)
            logger.LogInformation("Seeding document conventions...");
            var documentConventionSeeder = new DocumentConventionSeeder(context);
            await documentConventionSeeder.SeedAsync();

            // Seed document sequences (all document types, all orgs; aligned with document conventions)
            logger.LogInformation("Seeding document sequences...");
            var documentSequenceSeeder = new DocumentSequenceSeeder(context);
            await documentSequenceSeeder.SeedAsync();

            // Seed case officers: complainant and case manager accounts per enforcement org
            logger.LogInformation("Seeding case officers (complainants and case managers)...");
            var caseOfficerSeeder = new CaseOfficerSeeder(userManager, roleManager, context);
            await caseOfficerSeeder.SeedAsync();

            logger.LogInformation("=== Database seeding completed successfully ===");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during database seeding");
            throw;
        }
    }
}




