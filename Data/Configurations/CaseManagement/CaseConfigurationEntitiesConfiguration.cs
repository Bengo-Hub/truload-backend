using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.CaseManagement;

namespace TruLoad.Backend.Data.Configurations.CaseManagement;

/// <summary>
/// Configuration for case management configuration/taxonomy entities.
/// Includes all lookup tables: ViolationType, LegalSection, ActDefinition, CaseManager,
/// HearingType, HearingStatus, HearingOutcome, DispositionType, CaseStatus, ReleaseType,
/// WarrantStatus, SubfileType, ClosureType, CaseReviewStatus
/// </summary>
public static class CaseConfigurationEntitiesConfiguration
{
    public static void ApplyConfigurationEntitiesConfigurations(this ModelBuilder modelBuilder)
    {
        ConfigureViolationType(modelBuilder);
        ConfigureLegalSection(modelBuilder);
        ConfigureActDefinition(modelBuilder);
        ConfigureCaseManager(modelBuilder);
        ConfigureHearingType(modelBuilder);
        ConfigureHearingStatus(modelBuilder);
        ConfigureHearingOutcome(modelBuilder);
        ConfigureDispositionType(modelBuilder);
        ConfigureCaseStatus(modelBuilder);
        ConfigureReleaseType(modelBuilder);
        ConfigureWarrantStatus(modelBuilder);
        ConfigureSubfileType(modelBuilder);
        ConfigureClosureType(modelBuilder);
        ConfigureCaseReviewStatus(modelBuilder);
    }

    private static void ConfigureViolationType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ViolationType>(entity =>
        {
            entity.ToTable("violation_types");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            entity.Property(e => e.Severity)
                .HasColumnName("severity")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            // Indexes
            entity.HasIndex(e => e.Code)
                .HasDatabaseName("idx_violation_types_code")
                .IsUnique();

            entity.HasIndex(e => e.IsActive)
                .HasDatabaseName("idx_violation_types_active")
                .HasFilter("is_active = TRUE");

            // CHECK constraint
            entity.HasCheckConstraint("ck_violation_types_severity",
                "severity IN ('low', 'medium', 'high', 'critical')");
        });
    }

    private static void ConfigureLegalSection(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LegalSection>(entity =>
        {
            entity.ToTable("legal_sections");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.LegalFramework)
                .HasColumnName("legal_framework")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.SectionNo)
                .HasColumnName("section_no")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Title)
                .HasColumnName("title")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.Property(e => e.DeletedAt)
                .HasColumnName("deleted_at")
                .HasColumnType("timestamp with time zone");

            // Indexes
            entity.HasIndex(e => new { e.LegalFramework, e.SectionNo })
                .HasDatabaseName("IX_legal_sections_framework_section_no")
                .IsUnique();

            entity.HasIndex(e => e.IsActive)
                .HasDatabaseName("IX_legal_sections_is_active")
                .HasFilter("is_active = TRUE");

            // Check constraint for legal framework
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_legal_sections_framework",
                "legal_framework IN ('CPC', 'PC', 'TRAFFIC_ACT', 'OTHER')"));
        });
    }

    private static void ConfigureActDefinition(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActDefinition>(entity =>
        {
            entity.ToTable("act_definitions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.ActType)
                .HasColumnName("act_type")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.FullName)
                .HasColumnName("full_name")
                .HasColumnType("text");

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            entity.Property(e => e.EffectiveDate)
                .HasColumnName("effective_date")
                .HasColumnType("date");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            // Indexes
            entity.HasIndex(e => e.Code)
                .HasDatabaseName("idx_act_definitions_code")
                .IsUnique();

            entity.HasIndex(e => e.ActType)
                .HasDatabaseName("idx_act_definitions_type");

            entity.HasIndex(e => e.IsActive)
                .HasDatabaseName("idx_act_definitions_active")
                .HasFilter("is_active = TRUE");

            // CHECK constraint
            entity.HasCheckConstraint("ck_act_definitions_act_type",
                "act_type IN ('EAC', 'Traffic')");
        });
    }

    private static void ConfigureCaseManager(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CaseManager>(entity =>
        {
            entity.ToTable("case_managers");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.UserId)
                .HasColumnName("user_id")
                .IsRequired();

            entity.Property(e => e.RoleType)
                .HasColumnName("role_type")
                .HasMaxLength(30)
                .IsRequired();

            entity.Property(e => e.Specialization)
                .HasColumnName("specialization")
                .HasMaxLength(100);

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            // Indexes
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("idx_case_managers_user")
                .HasFilter("is_active = TRUE");

            entity.HasIndex(e => new { e.RoleType, e.IsActive })
                .HasDatabaseName("idx_case_managers_role");

            // CHECK constraint
            entity.HasCheckConstraint("ck_case_managers_role_type",
                "role_type IN ('case_manager', 'prosecutor', 'investigator')");
        });
    }

    private static void ConfigureHearingType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HearingType>(entity =>
        {
            entity.ToTable("hearing_types");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            // Indexes
            entity.HasIndex(e => e.Code)
                .HasDatabaseName("idx_hearing_types_code")
                .IsUnique();
        });
    }

    private static void ConfigureHearingStatus(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HearingStatus>(entity =>
        {
            entity.ToTable("hearing_statuses");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            // Indexes
            entity.HasIndex(e => e.Code)
                .HasDatabaseName("idx_hearing_statuses_code")
                .IsUnique();
        });
    }

    private static void ConfigureHearingOutcome(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HearingOutcome>(entity =>
        {
            entity.ToTable("hearing_outcomes");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            // Indexes
            entity.HasIndex(e => e.Code)
                .HasDatabaseName("idx_hearing_outcomes_code")
                .IsUnique();
        });
    }

    private static void ConfigureDispositionType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DispositionType>(entity =>
        {
            entity.ToTable("disposition_types");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            // Indexes
            entity.HasIndex(e => e.Code)
                .HasDatabaseName("idx_disposition_types_code")
                .IsUnique();
        });
    }

    private static void ConfigureCaseStatus(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CaseStatus>(entity =>
        {
            entity.ToTable("case_statuses");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            // Indexes
            entity.HasIndex(e => e.Code)
                .HasDatabaseName("idx_case_statuses_code")
                .IsUnique();
        });
    }

    private static void ConfigureReleaseType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReleaseType>(entity =>
        {
            entity.ToTable("release_types");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            // Indexes
            entity.HasIndex(e => e.Code)
                .HasDatabaseName("idx_release_types_code")
                .IsUnique();
        });
    }

    private static void ConfigureWarrantStatus(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WarrantStatus>(entity =>
        {
            entity.ToTable("warrant_statuses");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            // Indexes
            entity.HasIndex(e => e.Code)
                .HasDatabaseName("idx_warrant_statuses_code")
                .IsUnique();
        });
    }

    private static void ConfigureSubfileType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SubfileType>(entity =>
        {
            entity.ToTable("subfile_types");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            entity.Property(e => e.ExampleDocuments)
                .HasColumnName("example_documents")
                .HasColumnType("text");

            entity.Property(e => e.IsMandatory)
                .HasColumnName("is_mandatory")
                .HasDefaultValue(false);

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            // Indexes
            entity.HasIndex(e => e.Code)
                .HasDatabaseName("idx_subfile_types_code")
                .IsUnique();
        });
    }

    private static void ConfigureClosureType(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClosureType>(entity =>
        {
            entity.ToTable("closure_types");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            // Indexes
            entity.HasIndex(e => e.Code)
                .HasDatabaseName("idx_closure_types_code")
                .IsUnique();
        });
    }

    private static void ConfigureCaseReviewStatus(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CaseReviewStatus>(entity =>
        {
            entity.ToTable("case_review_statuses");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            // Indexes
            entity.HasIndex(e => e.Code)
                .HasDatabaseName("idx_case_review_statuses_code")
                .IsUnique();
        });
    }
}
