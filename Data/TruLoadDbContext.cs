using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using TruLoad.Backend.Middleware;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.Common;
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
using TruLoad.Backend.Models.Notifications;
using TruLoad.Backend.Models.Views;


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
///
/// PERFORMANCE OPTIMIZATIONS:
/// - Vector index configuration is conditional based on context type (design-time vs runtime)
/// - pgvector extension creation is now handled by Docker init script (01-init-extensions.sql)
/// - Design-time factory (TruLoadDbContextFactory) skips expensive index config during 'dotnet ef' commands
///
/// DEPRECATED/REMOVED:
/// - EnsurePgVectorExtension() and EnsurePgVectorExtensionAsync() - no longer needed, extension created at init
/// - Redundant pgvector check in Program.cs startup
/// </summary>
public class TruLoadDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    /// <summary>
    /// Indicates whether this context is using the InMemory provider (for testing).
    /// Used by TruLoadModelCacheKeyFactory to create separate model caches.
    /// </summary>
    public bool IsInMemoryProvider { get; }

    private readonly ITenantContext _tenantContext;

    public TruLoadDbContext(DbContextOptions<TruLoadDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
        IsInMemoryProvider = options.Extensions.Any(e =>
            e.GetType().FullName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <summary>Design-time constructor used by TruLoadDbContextFactory (dotnet ef commands).</summary>
    public TruLoadDbContext(DbContextOptions<TruLoadDbContext> options)
        : this(options, new TenantContext()) { }

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
    public DbSet<ExchangeRate> ExchangeRates { get; set; } = null!;

    // ===== Exchange Rate API Settings =====
    public DbSet<ExchangeRateApiSettings> ExchangeRateApiSettings { get; set; } = null!;

    // ===== Offline Support Module =====
    public DbSet<DeviceSyncEvent> DeviceSyncEvents { get; set; } = null!;

    // ===== System Settings =====
    public DbSet<ApplicationSettings> ApplicationSettings { get; set; } = null!;

    // ===== Auth: Refresh Tokens & Push Subscriptions =====
    public DbSet<TruLoad.Backend.Models.Identity.RefreshToken> RefreshTokens { get; set; } = null!;
    public DbSet<PushSubscription> PushSubscriptions { get; set; } = null!;
    public DbSet<UserNotification> UserNotifications { get; set; } = null!;

    // ===== Document Conventions & Sequences (Sprint 22) =====
    public DbSet<DocumentConvention> DocumentConventions { get; set; } = null!;
    public DbSet<DocumentSequence> DocumentSequences { get; set; } = null!;

    // ===== Integration Configuration (Sprint 15: eCitizen) =====
    public DbSet<IntegrationConfig> IntegrationConfigs { get; set; } = null!;

    // ===== Read-Only Database Views (Keyless Entities) =====
    public DbSet<ActiveVehicleTag> ActiveVehicleTags { get; set; } = null!;
    public DbSet<YardStatusSummary> YardStatusSummaries { get; set; } = null!;
    public DbSet<ActiveCase> ActiveCases { get; set; } = null!;
    public DbSet<PendingCourtHearing> PendingCourtHearings { get; set; } = null!;
    public DbSet<ActiveArrestWarrant> ActiveArrestWarrants { get; set; } = null!;
    public DbSet<RecentCompliantWeighing> RecentCompliantWeighings { get; set; } = null!;
    public DbSet<PendingSpecialRelease> PendingSpecialReleases { get; set; } = null!;
    public DbSet<ActivePermit> ActivePermits { get; set; } = null!;

    // ===== Materialized Views for Dashboards =====
    public DbSet<MvDailyWeighingStats> MvDailyWeighingStats { get; set; } = null!;
    public DbSet<MvChargeSummary> MvChargeSummaries { get; set; } = null!;
    public DbSet<MvAxleGroupViolation> MvAxleGroupViolations { get; set; } = null!;
    public DbSet<MvDriverDemeritRanking> MvDriverDemeritRankings { get; set; } = null!;
    public DbSet<MvVehicleViolationHistory> MvVehicleViolationHistories { get; set; } = null!;
    public DbSet<MvStationPerformanceScorecard> MvStationPerformanceScorecards { get; set; } = null!;


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Register pgvector extension when available
        if (!IsInMemoryProvider)
            modelBuilder.HasPostgresExtension("vector");

        // ===== Apply Module-Specific Configurations (always applied) =====
        modelBuilder.ApplyUserManagementConfigurations();
        modelBuilder.ApplySystemConfigurationConfigurations();
        modelBuilder.ApplyAxleConfigurationConfigurations();
        modelBuilder.ApplyTrafficConfigurations();
        modelBuilder.ApplyCaseManagementConfigurations();
        modelBuilder.ApplyWeighingConfigurations();
        modelBuilder.ApplyInfrastructureConfigurations();
        modelBuilder.ApplyGeographicConfigurations();
        modelBuilder.ApplyYardConfigurations();
        modelBuilder.ApplyProsecutionConfigurations();
        modelBuilder.ApplyFinancialConfigurations();
        modelBuilder.ApplyOfflineConfigurations();

        // ===== Database Views & Keyless Entities Configuration =====
        modelBuilder.Entity<ActiveVehicleTag>(e => { e.HasNoKey(); e.ToView("active_vehicle_tags"); });
        modelBuilder.Entity<YardStatusSummary>(e => { e.HasNoKey(); e.ToView("yard_status_summary"); });
        modelBuilder.Entity<ActiveCase>(e => { e.HasNoKey(); e.ToView("active_cases"); });
        modelBuilder.Entity<PendingCourtHearing>(e => { e.HasNoKey(); e.ToView("pending_court_hearings"); });
        modelBuilder.Entity<ActiveArrestWarrant>(e => { e.HasNoKey(); e.ToView("active_arrest_warrants"); });
        modelBuilder.Entity<RecentCompliantWeighing>(e => { e.HasNoKey(); e.ToView("recent_compliant_weighings"); });
        modelBuilder.Entity<PendingSpecialRelease>(e => { e.HasNoKey(); e.ToView("pending_special_releases"); });
        modelBuilder.Entity<ActivePermit>(e => { e.HasNoKey(); e.ToView("active_permits"); });

        modelBuilder.Entity<MvDailyWeighingStats>(e => { e.HasNoKey(); e.ToView("mv_daily_weighing_stats"); });
        modelBuilder.Entity<MvChargeSummary>(e => { e.HasNoKey(); e.ToView("mv_charge_summaries"); });
        modelBuilder.Entity<MvAxleGroupViolation>(e => { e.HasNoKey(); e.ToView("mv_axle_group_violations"); });
        modelBuilder.Entity<MvDriverDemeritRanking>(e => { e.HasNoKey(); e.ToView("mv_driver_demerit_rankings"); });
        modelBuilder.Entity<MvVehicleViolationHistory>(e => { e.HasNoKey(); e.ToView("mv_vehicle_violation_history"); });
        modelBuilder.Entity<MvStationPerformanceScorecard>(e => { e.HasNoKey(); e.ToView("mv_station_performance_scorecard"); });

        // Post-configuration adjustments for InMemory provider
        if (IsInMemoryProvider)
        {
            modelBuilder.Entity<Models.Offline.DeviceSyncEvent>().Ignore(e => e.Payload);

            // pgvector Vector properties are not supported by the InMemory provider.
            // Ignore them explicitly so integration tests can run without Npgsql/pgvector.
            modelBuilder.Entity<Models.CaseManagement.CaseRegister>().Ignore(e => e.ViolationDetailsEmbedding);
            modelBuilder.Entity<Models.CaseManagement.CaseSubfile>().Ignore(e => e.ContentEmbedding);
            modelBuilder.Entity<Models.CaseManagement.CourtHearing>().Ignore(e => e.MinuteNotesEmbedding);
            modelBuilder.Entity<Models.Prosecution.ProsecutionCase>().Ignore(e => e.CaseNotesEmbedding);
            modelBuilder.Entity<Models.Yard.VehicleTag>().Ignore(e => e.ReasonEmbedding);
            modelBuilder.Entity<Models.Weighing.WeighingTransaction>().Ignore(e => e.ViolationReasonEmbedding);
            modelBuilder.Entity<Models.Weighing.Vehicle>().Ignore(e => e.DescriptionEmbedding);
        }

        // ===== Global Multi-tenancy Isolation & Column Mapping =====
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(TenantAwareEntity).IsAssignableFrom(entityType.ClrType))
            {
                // Set column names to snake_case for multi-tenancy columns to satisfy SQL views
                var orgIdProperty = entityType.FindProperty(nameof(TenantAwareEntity.OrganizationId));
                if (orgIdProperty != null)
                {
                    orgIdProperty.SetColumnName("organization_id");
                }

                var stationIdProperty = entityType.FindProperty(nameof(TenantAwareEntity.StationId));
                if (stationIdProperty != null)
                {
                    stationIdProperty.SetColumnName("station_id");
                }

                var method = typeof(TruLoadDbContext)
                    .GetMethod(nameof(ApplyFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.MakeGenericMethod(entityType.ClrType);

                method?.Invoke(this, new object[] { modelBuilder });
            }
        }
    }

    private void ApplyFilter<T>(ModelBuilder modelBuilder) where T : TenantAwareEntity
    {
        if (typeof(T) == typeof(Station))
        {
            modelBuilder.Entity<T>().HasQueryFilter(e => e.OrganizationId == _tenantContext.OrganizationId);
        }
        else
        {
            modelBuilder.Entity<T>().HasQueryFilter(e =>
                e.OrganizationId == _tenantContext.OrganizationId &&
                (!_tenantContext.StationId.HasValue || e.StationId == null || e.StationId == _tenantContext.StationId));
        }
    }

    public override int SaveChanges()
    {
        ApplyTenantMetadata();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTenantMetadata();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyTenantMetadata()
    {
        var entries = ChangeTracker.Entries<TenantAwareEntity>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                // Auto-populate OrganizationId if not set
                if (entry.Entity.OrganizationId == Guid.Empty)
                {
                    entry.Entity.OrganizationId = _tenantContext.OrganizationId;
                }

                // Auto-populate StationId if available in context and not explicitly set
                // Only populate if the entity specifically has a nullable StationId or it's empty
                if ((entry.Entity.StationId == null || entry.Entity.StationId == Guid.Empty) && _tenantContext.StationId.HasValue)
                {
                    entry.Entity.StationId = _tenantContext.StationId;
                }
            }
        }
    }

}