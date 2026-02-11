using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.Identity;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Models.System;
using TruLoad.Backend.Models.Infrastructure;
using TruLoad.Backend.Data.Configurations.Weighing;
using TruLoad.Backend.Data.Configurations.UserManagement;
using TruLoad.Backend.Data.Configurations.AxleConfiguration;
using TruLoad.Backend.Data.Configurations.Traffic;
using TruLoad.Backend.Data.Configurations.SystemConfiguration;
using TruLoad.Backend.Data.Configurations.Infrastructure;
using TruLoad.Backend.Data.Configurations.CaseManagement;
using TruLoad.Backend.Models.CaseManagement;
using TruLoad.Backend.Models.Yard;
using TruLoad.Backend.Models.Prosecution;
using TruLoad.Backend.Models.Financial;
using TruLoad.Backend.Models.Offline;
using TruLoad.Backend.Data.Configurations.Yard;
using TruLoad.Backend.Data.Configurations.Prosecution;
using TruLoad.Backend.Data.Configurations.Financial;
using TruLoad.Backend.Data.Configurations.Offline;

namespace TruLoad.Backend.Data;

/// <summary>
/// Custom model cache key factory that creates separate caches for InMemory and PostgreSQL providers.
/// This is necessary because Vector properties must be ignored for InMemory but mapped for PostgreSQL.
/// </summary>
public class TruLoadModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        if (context is TruLoadDbContext truLoadContext)
        {
            return (context.GetType(), truLoadContext.IsInMemoryProvider, designTime);
        }
        return (context.GetType(), designTime);
    }
}

/// <summary>
/// Main database context for TruLoad application
/// Uses ASP.NET Core Identity for local authentication
/// </summary>
public class TruLoadDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private static bool _pgvectorChecked = false;
    private static readonly object _lock = new();

    /// <summary>
    /// Indicates whether this context is using the InMemory provider (for testing).
    /// Used by TruLoadModelCacheKeyFactory to create separate model caches.
    /// </summary>
    public bool IsInMemoryProvider { get; }

    public TruLoadDbContext(DbContextOptions<TruLoadDbContext> options)
        : base(options)
    {
        // Detect InMemory provider at construction time by checking options extensions
        IsInMemoryProvider = options.Extensions.Any(e =>
            e.GetType().FullName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // Register custom model cache key factory to support different models for InMemory vs PostgreSQL
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, TruLoadModelCacheKeyFactory>();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        if (IsInMemoryProvider)
        {
            // For InMemory provider, ignore types not supported by the InMemory provider
            // This prevents EF from trying to map PostgreSQL-specific types
            configurationBuilder.IgnoreAny<Pgvector.Vector>();
            configurationBuilder.IgnoreAny<System.Text.Json.JsonDocument>();
        }
    }

    /// <summary>
    /// Ensures the pgvector extension is enabled in the database.
    /// This method is idempotent and thread-safe.
    /// Should be called during application startup or before first use.
    /// </summary>
    public async Task EnsurePgVectorExtensionAsync(CancellationToken cancellationToken = default)
    {
        if (_pgvectorChecked) return;

        lock (_lock)
        {
            if (_pgvectorChecked) return;

            try
            {
                // Just try to create the extension - it's idempotent
                Database.ExecuteSqlRaw("CREATE EXTENSION IF NOT EXISTS vector");
                _pgvectorChecked = true;
            }
            catch (Exception)
            {
                // Extension might already exist or user lacks permissions
                // Log and continue - the migration will handle it if needed
                _pgvectorChecked = true;
            }
        }
    }

    /// <summary>
    /// Synchronous version of EnsurePgVectorExtensionAsync
    /// </summary>
    public void EnsurePgVectorExtension()
    {
        if (_pgvectorChecked) return;

        lock (_lock)
        {
            if (_pgvectorChecked) return;

            try
            {
                // Just try to create the extension - it's idempotent
                Database.ExecuteSqlRaw("CREATE EXTENSION IF NOT EXISTS vector");
                _pgvectorChecked = true;
            }
            catch (Exception)
            {
                // Extension might already exist or user lacks permissions
                // Log and continue - the migration will handle it if needed
                _pgvectorChecked = true;
            }
        }
    }

    // ===== Sprint 1: User Management & Identity =====
    // Note: Users and Roles are managed by Identity (AspNetUsers, AspNetRoles tables)
    public DbSet<Organization> Organizations { get; set; } = null!;
    public DbSet<Department> Departments { get; set; } = null!;
    public DbSet<Permission> Permissions { get; set; } = null!;
    public DbSet<RolePermission> RolePermissions { get; set; } = null!;
    public DbSet<WorkShift> WorkShifts { get; set; } = null!;
    public DbSet<WorkShiftSchedule> WorkShiftSchedules { get; set; } = null!;
    public DbSet<ShiftRotation> ShiftRotations { get; set; } = null!;
    public DbSet<RotationShift> RotationShifts { get; set; } = null!;
    public DbSet<UserShift> UserShifts { get; set; } = null!;
    public DbSet<Station> Stations { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<Document> Documents { get; set; } = null!;

    // ===== Database Seeding History =====
    public DbSet<DatabaseSeedingHistory> DatabaseSeedingHistory { get; set; } = null!;

    // ===== Reference Data & Infrastructure =====
    public DbSet<ScaleTest> ScaleTests { get; set; } = null!;
    public DbSet<CargoTypes> CargoTypes { get; set; } = null!;
    public DbSet<OriginsDestinations> OriginsDestinations { get; set; } = null!;
    public DbSet<VehicleMake> VehicleMakes { get; set; } = null!;
    public DbSet<VehicleModel> VehicleModels { get; set; } = null!;
    public DbSet<Roads> Roads { get; set; } = null!;
    public DbSet<Counties> Counties { get; set; } = null!;
    public DbSet<Districts> Districts { get; set; } = null!;
    public DbSet<Subcounty> Subcounties { get; set; } = null!;
    public DbSet<HardwareHealthLog> HardwareHealthLogs { get; set; } = null!;
    public DbSet<WeighbridgeHardware> WeighbridgeHardware { get; set; } = null!;
    
    // ===== Weighing Operations: Axle Configurations & References =====
    public DbSet<TyreType> TyreTypes { get; set; } = null!;
    public DbSet<AxleGroup> AxleGroups { get; set; } = null!;
    public DbSet<AxleConfiguration> AxleConfigurations { get; set; } = null!;
    public DbSet<AxleWeightReference> AxleWeightReferences { get; set; } = null!;
    public DbSet<AxleFeeSchedule> AxleFeeSchedules { get; set; } = null!;
    public DbSet<WeighingAxle> WeighingAxles { get; set; } = null!;
    public DbSet<WeighingTransaction> WeighingTransactions { get; set; } = null!;
    public DbSet<ProhibitionOrder> ProhibitionOrders { get; set; } = null!;
    
    // ===== Vehicle & Permit Management =====
    public DbSet<VehicleOwner> VehicleOwners { get; set; } = null!;
    public DbSet<Transporter> Transporters { get; set; } = null!;
    public DbSet<Vehicle> Vehicles { get; set; } = null!;
    public DbSet<Permit> Permits { get; set; } = null!;
    
    // ===== Traffic: Driver & Demerit Points Management =====
    public DbSet<Driver> Drivers { get; set; } = null!;
    public DbSet<DriverDemeritRecord> DriverDemeritRecords { get; set; } = null!;
    
    // ===== System Configuration =====
    public DbSet<PermitType> PermitTypes { get; set; } = null!;
    public DbSet<ToleranceSetting> ToleranceSettings { get; set; } = null!;

    // ===== Sprint 11: Fee & Demerit Points System =====
    public DbSet<AxleTypeOverloadFeeSchedule> AxleTypeOverloadFeeSchedules { get; set; } = null!;
    public DbSet<DemeritPointSchedule> DemeritPointSchedules { get; set; } = null!;
    public DbSet<PenaltySchedule> PenaltySchedules { get; set; } = null!;
    
    // ===== Sprint 10: Case Management & Special Release =====
    public DbSet<CaseRegister> CaseRegisters { get; set; } = null!;
    public DbSet<CaseSubfile> CaseSubfiles { get; set; } = null!;
    public DbSet<SpecialRelease> SpecialReleases { get; set; } = null!;
    public DbSet<ArrestWarrant> ArrestWarrants { get; set; } = null!;
    public DbSet<CourtHearing> CourtHearings { get; set; } = null!;
    public DbSet<CaseClosureChecklist> CaseClosureChecklists { get; set; } = null!;
    public DbSet<LoadCorrectionMemo> LoadCorrectionMemos { get; set; } = null!;
    public DbSet<ComplianceCertificate> ComplianceCertificates { get; set; } = null!;
    public DbSet<CaseAssignmentLog> CaseAssignmentLogs { get; set; } = null!;
    public DbSet<CaseParty> CaseParties { get; set; } = null!;
    public DbSet<Court> Courts { get; set; } = null!;

    // ===== Case Management Configuration/Taxonomy Tables =====
    public DbSet<ViolationType> ViolationTypes { get; set; } = null!;
    public DbSet<LegalSection> LegalSections { get; set; } = null!;
    public DbSet<ActDefinition> ActDefinitions { get; set; } = null!;
    public DbSet<CaseManager> CaseManagers { get; set; } = null!;
    public DbSet<HearingType> HearingTypes { get; set; } = null!;
    public DbSet<HearingStatus> HearingStatuses { get; set; } = null!;
    public DbSet<HearingOutcome> HearingOutcomes { get; set; } = null!;
    public DbSet<DispositionType> DispositionTypes { get; set; } = null!;
    public DbSet<CaseStatus> CaseStatuses { get; set; } = null!;
    public DbSet<ReleaseType> ReleaseTypes { get; set; } = null!;
    public DbSet<WarrantStatus> WarrantStatuses { get; set; } = null!;
    public DbSet<SubfileType> SubfileTypes { get; set; } = null!;
    public DbSet<ClosureType> ClosureTypes { get; set; } = null!;
    public DbSet<CaseReviewStatus> CaseReviewStatuses { get; set; } = null!;

    // ===== Yard & Tags Module =====
    public DbSet<YardEntry> YardEntries { get; set; } = null!;
    public DbSet<VehicleTag> VehicleTags { get; set; } = null!;
    public DbSet<TagCategory> TagCategories { get; set; } = null!;

    // ===== Prosecution Module =====
    public DbSet<ProsecutionCase> ProsecutionCases { get; set; } = null!;

    // ===== Financial Module =====
    public DbSet<Invoice> Invoices { get; set; } = null!;
    public DbSet<PaymentCallback> PaymentCallbacks { get; set; } = null!;
    public DbSet<Receipt> Receipts { get; set; } = null!;

    // ===== Offline Support Module =====
    public DbSet<DeviceSyncEvent> DeviceSyncEvents { get; set; } = null!;

    // ===== System Settings =====
    public DbSet<ApplicationSettings> ApplicationSettings { get; set; } = null!;

    // ===== Integration Configuration (Sprint 15: eCitizen) =====
    public DbSet<IntegrationConfig> IntegrationConfigs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (!IsInMemoryProvider)
        {
            // Register PostgreSQL extensions for production/development
            // pgvector for vector similarity search
            modelBuilder.HasPostgresExtension("vector");

            // Explicitly map vector properties (they have [NotMapped] by default for InMemory compatibility)
            // These properties must be explicitly configured for PostgreSQL with HNSW indexes
            modelBuilder.Entity<Models.CaseManagement.CaseRegister>(entity =>
            {
                entity.Property(e => e.ViolationDetailsEmbedding).HasColumnType("vector(384)");
                entity.HasIndex(e => e.ViolationDetailsEmbedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
            });

            modelBuilder.Entity<Models.CaseManagement.CaseSubfile>(entity =>
            {
                entity.Property(e => e.ContentEmbedding).HasColumnType("vector(384)");
                entity.HasIndex(e => e.ContentEmbedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
            });

            modelBuilder.Entity<Models.CaseManagement.CourtHearing>(entity =>
            {
                entity.Property(e => e.MinuteNotesEmbedding).HasColumnType("vector(384)");
                entity.HasIndex(e => e.MinuteNotesEmbedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
            });

            modelBuilder.Entity<Models.Weighing.Vehicle>(entity =>
            {
                entity.Property(e => e.DescriptionEmbedding).HasColumnType("vector(384)");
                entity.HasIndex(e => e.DescriptionEmbedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
            });

            modelBuilder.Entity<Models.Weighing.WeighingTransaction>(entity =>
            {
                entity.Property(e => e.ViolationReasonEmbedding).HasColumnType("vector(384)");
                entity.HasIndex(e => e.ViolationReasonEmbedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
            });

            modelBuilder.Entity<Models.Prosecution.ProsecutionCase>(entity =>
            {
                entity.Property(e => e.CaseNotesEmbedding).HasColumnType("vector(384)");
                entity.HasIndex(e => e.CaseNotesEmbedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
            });

            modelBuilder.Entity<Models.Yard.VehicleTag>(entity =>
            {
                entity.Property(e => e.ReasonEmbedding).HasColumnType("vector(384)");
                entity.HasIndex(e => e.ReasonEmbedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
            });
        }

        // ===== Apply Module-Specific Configurations =====
        // User Management Module Configurations
        modelBuilder.ApplyUserManagementConfigurations();

        // System Configuration Module Configurations
        modelBuilder.ApplySystemConfigurationConfigurations();

        // Axle Configuration Module Configurations
        modelBuilder.ApplyAxleConfigurationConfigurations();

        // Traffic Module Configurations
        modelBuilder.ApplyTrafficConfigurations();

        // Case Management Module Configurations (Sprint 10 + Sprint 11 Extended)
        modelBuilder.ApplyCaseManagementConfigurations();

        // Weighing Module Configurations
        modelBuilder.ApplyWeighingConfigurations();

        // Infrastructure Module Configurations (ScaleTests, Reference Data)
        modelBuilder.ApplyInfrastructureConfigurations();

        // Geographic Module Configurations (Sprint 11)
        modelBuilder.ApplyGeographicConfigurations();

        // Yard Module Configurations (Sprint 11)
        modelBuilder.ApplyYardConfigurations();

        // Prosecution Module Configurations (Sprint 11)
        modelBuilder.ApplyProsecutionConfigurations();

        // Financial Module Configurations (Sprint 11)
        modelBuilder.ApplyFinancialConfigurations();

        // Offline Support Module Configurations (Sprint 11)
        modelBuilder.ApplyOfflineConfigurations();

        // Post-configuration adjustments for InMemory provider
        // Must be done AFTER Apply*Configurations() since those add the properties
        if (IsInMemoryProvider)
        {
            // Ignore properties with types not supported by InMemory provider
            modelBuilder.Entity<Models.Offline.DeviceSyncEvent>().Ignore(e => e.Payload);
        }
    }
}