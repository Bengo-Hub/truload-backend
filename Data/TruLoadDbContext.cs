using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace truload_backend.Data;

/// <summary>
/// Main database context for TruLoad application
/// Inherits from IdentityDbContext to support ASP.NET Identity for user management
/// </summary>
public class TruLoadDbContext : IdentityDbContext<IdentityUser>
{
    public TruLoadDbContext(DbContextOptions<TruLoadDbContext> options)
        : base(options)
    {
    }

    // ===== User Management & Authentication =====
    // IdentityUser, IdentityRole, etc. are inherited from IdentityDbContext

    // ===== Reference & Settings Module =====
    // DbSet<Station> Stations { get; set; }
    // DbSet<Camera> Cameras { get; set; }
    // DbSet<IoDevice> IoDevices { get; set; }
    // DbSet<Route> Routes { get; set; }
    // DbSet<Cargo> Cargos { get; set; }
    // DbSet<Location> Locations { get; set; }
    // DbSet<Transporter> Transporters { get; set; }
    // DbSet<VehicleMake> VehicleMakes { get; set; }

    // ===== Axle Configuration & Acts =====
    // DbSet<AxleConfiguration> AxleConfigurations { get; set; }
    // DbSet<EacActFee> EacActFees { get; set; }
    // DbSet<TrafficActFee> TrafficActFees { get; set; }
    // DbSet<PermitType> PermitTypes { get; set; }

    // ===== Weighing Module =====
    // DbSet<Vehicle> Vehicles { get; set; }
    // DbSet<Driver> Drivers { get; set; }
    // DbSet<WeighSession> WeighSessions { get; set; }
    // DbSet<WeighReading> WeighReadings { get; set; }
    // DbSet<ScaleTest> ScaleTests { get; set; }

    // ===== Yard & Tags =====
    // DbSet<YardEntry> YardEntries { get; set; }
    // DbSet<VehicleTag> VehicleTags { get; set; }

    // ===== Prosecution Module =====
    // DbSet<ProsecutionCase> ProsecutionCases { get; set; }
    // DbSet<ProhibitionOrder> ProhibitionOrders { get; set; }
    // DbSet<LoadCorrectionMemo> LoadCorrectionMemos { get; set; }
    // DbSet<ComplianceCertificate> ComplianceCertificates { get; set; }
    // DbSet<OverloadInvoice> OverloadInvoices { get; set; }

    // ===== Special Release =====
    // DbSet<SpecialRelease> SpecialReleases { get; set; }
    // DbSet<Permit> Permits { get; set; }

    // ===== Vehicle Inspection =====
    // DbSet<VehicleInspection> VehicleInspections { get; set; }
    // DbSet<InspectionCheckpoint> InspectionCheckpoints { get; set; }

    // ===== Reporting & Analytics =====
    // DbSet<DailyReport> DailyReports { get; set; }
    // DbSet<WeighbridgePerformance> WeighbridgePerformances { get; set; }

    // ===== Technical Module =====
    // DbSet<CalibrationCertificate> CalibrationCertificates { get; set; }
    // DbSet<SystemHealthCheck> SystemHealthChecks { get; set; }

    // ===== Audit & System =====
    // DbSet<AuditLog> AuditLogs { get; set; }
    // DbSet<SystemSetting> SystemSettings { get; set; }
    // DbSet<DollarRate> DollarRates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ===== Configure Identity Tables =====
        modelBuilder.Entity<IdentityUser>().ToTable("Users");
        modelBuilder.Entity<IdentityRole>().ToTable("Roles");
        modelBuilder.Entity<IdentityUserRole<string>>().ToTable("UserRoles");
        modelBuilder.Entity<IdentityUserClaim<string>>().ToTable("UserClaims");
        modelBuilder.Entity<IdentityUserLogin<string>>().ToTable("UserLogins");
        modelBuilder.Entity<IdentityRoleClaim<string>>().ToTable("RoleClaims");
        modelBuilder.Entity<IdentityUserToken<string>>().ToTable("UserTokens");

        // ===== Entity Configurations =====
        // Configure entity relationships, indexes, constraints, etc.
        // Example:
        // modelBuilder.Entity<WeighSession>()
        //     .HasOne(w => w.Vehicle)
        //     .WithMany(v => v.WeighSessions)
        //     .HasForeignKey(w => w.VehicleId);

        // modelBuilder.Entity<WeighSession>()
        //     .HasIndex(w => w.RegistrationNumber);

        // modelBuilder.Entity<WeighSession>()
        //     .HasIndex(w => w.WeighDate);

        // modelBuilder.Entity<ProsecutionCase>()
        //     .HasOne(p => p.WeighSession)
        //     .WithOne(w => w.ProsecutionCase)
        //     .HasForeignKey<ProsecutionCase>(p => p.WeighSessionId);

        // ===== Seed Data =====
        // Seed initial data for reference tables, roles, etc.
        // Example:
        // modelBuilder.Entity<IdentityRole>().HasData(
        //     new IdentityRole { Id = "1", Name = "Administrator", NormalizedName = "ADMINISTRATOR" },
        //     new IdentityRole { Id = "2", Name = "Operator", NormalizedName = "OPERATOR" },
        //     new IdentityRole { Id = "3", Name = "Supervisor", NormalizedName = "SUPERVISOR" }
        // );

        // Seed Axle Configurations as per EAC/Traffic Act
        // Seed EAC and Traffic Act fee tables
        // Seed default system settings
    }
}

