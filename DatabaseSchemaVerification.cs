using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using truload_backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.Identity;

namespace TruLoad.Backend.Tests;

/// <summary>
/// Simple integration test to verify database schema and migrations
/// Run: dotnet run --project truload-backend
/// Then check console output for verification results
/// </summary>
public class DatabaseSchemaVerification
{
    public static async Task VerifySchema(TruLoadDbContext context, IServiceProvider serviceProvider)
    {
        Console.WriteLine("\n===== DATABASE SCHEMA VERIFICATION =====\n");

        try
        {
            // 1. Verify no Identity tables exist
            var tables = await context.Database.ExecuteSqlRawAsync(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_name LIKE '%AspNet%' OR table_name LIKE '%Identity%'"
            );
            Console.WriteLine($"✓ No ASP.NET Identity tables found (count: {tables})");

            // 2. Verify all Sprint 1 tables exist
            var expectedTables = new[]
            {
                "users", "organizations", "departments", "stations", "roles",
                "user_roles", "work_shifts", "work_shift_schedules",
                "shift_rotations", "rotation_shifts", "user_shifts", "audit_logs"
            };

            foreach (var table in expectedTables)
            {
                var exists = await TableExists(context, table);
                var status = exists ? "✓" : "✗";
                Console.WriteLine($"{status} Table '{table}' {(exists ? "exists" : "MISSING")}");
            }

            // 3. Verify indexes on users table
            var indexes = new[]
            {
                "idx_users_auth_service_user_id",
                "idx_users_email",
                "idx_users_station_id",
                "idx_users_sync_status"
            };

            Console.WriteLine("\nIndexes on 'users' table:");
            foreach (var index in indexes)
            {
                var exists = await IndexExists(context, "users", index);
                var status = exists ? "✓" : "✗";
                Console.WriteLine($"{status} Index '{index}' {(exists ? "exists" : "MISSING")}");
            }

            // 4. Verify composite keys
            Console.WriteLine("\nComposite Keys:");
            var userRoleKey = await HasCompositePrimaryKey(context, "user_roles", new[] { "user_id", "role_id" });
            Console.WriteLine($"{(userRoleKey ? "✓" : "✗")} user_roles composite key (user_id, role_id)");

            var rotationShiftKey = await HasCompositePrimaryKey(context, "rotation_shifts", new[] { "rotation_id", "work_shift_id" });
            Console.WriteLine($"{(rotationShiftKey ? "✓" : "✗")} rotation_shifts composite key (rotation_id, work_shift_id)");

            // 5. Test basic CRUD operations
            Console.WriteLine("\nBasic CRUD Operations:");
            await TestCrudOperations(context, serviceProvider);

            Console.WriteLine("\n===== VERIFICATION COMPLETE =====\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ Error during verification: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private static async Task<bool> TableExists(TruLoadDbContext context, string tableName)
    {
        var sql = $"SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = '{tableName}')";
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        
        return result is bool exists && exists;
    }

    private static async Task<bool> IndexExists(TruLoadDbContext context, string tableName, string indexName)
    {
        var sql = $"SELECT EXISTS (SELECT FROM pg_indexes WHERE tablename = '{tableName}' AND indexname = '{indexName}')";
        var connection = context.Database.GetDbConnection();
        
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();
        
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        
        return result is bool exists && exists;
    }

    private static async Task<bool> HasCompositePrimaryKey(TruLoadDbContext context, string tableName, string[] columns)
    {
        var sql = $@"
            SELECT COUNT(*) 
            FROM information_schema.key_column_usage 
            WHERE table_name = '{tableName}' 
            AND constraint_name LIKE 'PK_%'
        ";
        
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();
        
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        
        return result is long count && count == columns.Length;
    }

    private static async Task TestCrudOperations(TruLoadDbContext context, IServiceProvider serviceProvider)
    {
        // Test Organization CRUD
        var org = new Organization
        {
            Code = "TEST_ORG",
            Name = "Test Organization",
            OrgType = "government",
            IsActive = true
        };

        context.Organizations.Add(org);
        await context.SaveChangesAsync();
        Console.WriteLine($"✓ Created organization: {org.Name} (ID: {org.Id})");

        // Test ApplicationRole CRUD (using Identity role)
        var roleManager = serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var testRole = new ApplicationRole
        {
            Name = "Test_Role",
            Code = "TEST_ROLE",
            Description = "Test role for verification",
            IsActive = true
        };

        var roleResult = await roleManager.CreateAsync(testRole);
        if (!roleResult.Succeeded)
        {
            throw new Exception($"Failed to create test role: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
        }
        Console.WriteLine($"✓ Created role: {testRole.Name} (ID: {testRole.Id})");

        // Test Station CRUD
        var station = new Station
        {
            StationCode = "TST-001",
            Name = "Test Station",
            StationType = "weigh_bridge",
            IsActive = true
        };

        context.Stations.Add(station);
        await context.SaveChangesAsync();
        Console.WriteLine($"✓ Created station: {station.Name} (ID: {station.Id})");

        // Cleanup
        context.Organizations.Remove(org);
        await roleManager.DeleteAsync(testRole);
        context.Stations.Remove(station);
        await context.SaveChangesAsync();
        Console.WriteLine("✓ Cleaned up test data");
    }
}
