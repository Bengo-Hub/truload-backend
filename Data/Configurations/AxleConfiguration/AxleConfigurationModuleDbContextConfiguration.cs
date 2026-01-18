using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.System;

namespace TruLoad.Backend.Data.Configurations.AxleConfiguration;

/// <summary>
/// Axle Configuration Module DbContext Configuration
/// Contains configurations for axle configuration entities including:
/// - TyreType, AxleGroup, AxleConfiguration, AxleWeightReference
/// - AxleFeeSchedule, WeighingAxle
/// </summary>
public static class AxleConfigurationModuleDbContextConfiguration
{
    /// <summary>
    /// Applies axle configuration module configurations to the model builder
    /// </summary>
    public static ModelBuilder ApplyAxleConfigurationConfigurations(this ModelBuilder modelBuilder)
    {
        // ===== AxleConfiguration Entity Configuration (Unified: Standard & Derived) =====
        modelBuilder.Entity<TruLoad.Backend.Models.AxleConfiguration>(entity =>
        {
            entity.ToTable("axle_configurations");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.AxleCode)
                .HasColumnName("axle_code")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.AxleName)
                .HasColumnName("axle_name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            entity.Property(e => e.AxleNumber)
                .HasColumnName("axle_number")
                .IsRequired();

            entity.Property(e => e.GvwPermissibleKg)
                .HasColumnName("gvw_permissible_kg")
                .IsRequired();

            entity.Property(e => e.IsStandard)
                .HasColumnName("is_standard")
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.LegalFramework)
                .HasColumnName("legal_framework")
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue("BOTH");

            entity.Property(e => e.VisualDiagramUrl)
                .HasColumnName("visual_diagram_url")
                .HasColumnType("text");

            entity.Property(e => e.Notes)
                .HasColumnName("notes")
                .HasColumnType("text");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired();

            entity.Property(e => e.DeletedAt)
                .HasColumnName("deleted_at");

            entity.Property(e => e.CreatedByUserId)
                .HasColumnName("created_by_user_id");

            // Unique constraint on axle_code
            entity.HasIndex(e => e.AxleCode)
                .IsUnique()
                .HasDatabaseName("idx_axle_configurations_code_unique");

            // Index on is_standard (for filtering standard configs)
            entity.HasIndex(e => e.IsStandard)
                .HasFilter("is_standard = true")
                .HasDatabaseName("idx_axle_configurations_standard");

            // Index on axle_number
            entity.HasIndex(e => e.AxleNumber)
                .HasDatabaseName("idx_axle_configurations_axle_number");

            // Index on legal_framework
            entity.HasIndex(e => e.LegalFramework)
                .HasDatabaseName("idx_axle_configurations_framework");

            // Index on is_active and soft delete
            entity.HasIndex(e => new { e.IsActive, e.DeletedAt })
                .HasFilter("is_active = true AND deleted_at IS NULL")
                .HasDatabaseName("idx_axle_configurations_active");

            // Relationship to User (creator of derived configs)
            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Relationships to navigation properties
            entity.HasMany(e => e.AxleWeightReferences)
                .WithOne("AxleConfiguration")
                .HasForeignKey(awr => awr.AxleConfigurationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.WeighingAxles)
                .WithOne("AxleConfiguration")
                .HasForeignKey(wa => wa.AxleConfigurationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ===== AxleWeightReference Entity Configuration =====
        modelBuilder.Entity<AxleWeightReference>(entity =>
        {
            entity.ToTable("axle_weight_references");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.AxleConfigurationId)
                .HasColumnName("axle_configuration_id")
                .IsRequired();

            entity.Property(e => e.AxlePosition)
                .HasColumnName("axle_position")
                .IsRequired();

            entity.Property(e => e.AxleLegalWeightKg)
                .HasColumnName("axle_legal_weight_kg")
                .IsRequired();

            entity.Property(e => e.AxleGroupId)
                .HasColumnName("axle_group_id")
                .IsRequired();

            entity.Property(e => e.AxleGrouping)
                .HasColumnName("axle_grouping")
                .HasMaxLength(10)
                .IsRequired();

            entity.Property(e => e.TyreTypeId)
                .HasColumnName("tyre_type_id");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            // Relationship to AxleConfiguration
            entity.HasOne(e => e.AxleConfiguration)
                .WithMany(ac => ac.AxleWeightReferences)
                .HasForeignKey(e => e.AxleConfigurationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship to AxleGroup
            entity.HasOne(e => e.AxleGroup)
                .WithMany(ag => ag.AxleWeightReferences)
                .HasForeignKey(e => e.AxleGroupId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship to TyreType
            entity.HasOne(e => e.TyreType)
                .WithMany(tt => tt.AxleWeightReferences)
                .HasForeignKey(e => e.TyreTypeId)
                .OnDelete(DeleteBehavior.SetNull);

            // Unique constraint: one position per configuration
            entity.HasIndex(e => new { e.AxleConfigurationId, e.AxlePosition })
                .IsUnique()
                .HasDatabaseName("idx_axle_weight_ref_config_position_unique");

            // Foreign key indexes for performance
            entity.HasIndex(e => e.AxleConfigurationId)
                .HasDatabaseName("idx_axle_weight_ref_config_id");

            entity.HasIndex(e => e.AxleGroupId)
                .HasDatabaseName("idx_axle_weight_ref_group_id");

            entity.HasIndex(e => e.TyreTypeId)
                .HasDatabaseName("idx_axle_weight_ref_tyre_type_id");
        });

        // ===== TyreType Entity Configuration =====
        modelBuilder.Entity<TyreType>(entity =>
        {
            entity.ToTable("tyre_types");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(1)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            entity.Property(e => e.TypicalMaxWeightKg)
                .HasColumnName("typical_max_weight_kg");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            // Unique index on code
            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("idx_tyre_types_code_unique");

            // Index on active status
            entity.HasIndex(e => e.IsActive)
                .HasFilter("is_active = true")
                .HasDatabaseName("idx_tyre_types_active");
        });

        // ===== AxleGroup Entity Configuration =====
        modelBuilder.Entity<AxleGroup>(entity =>
        {
            entity.ToTable("axle_groups");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            entity.Property(e => e.TypicalWeightKg)
                .HasColumnName("typical_weight_kg")
                .IsRequired();

            entity.Property(e => e.MinSpacingFeet)
                .HasColumnName("min_spacing_feet")
                .HasColumnType("numeric(4,1)");

            entity.Property(e => e.MaxSpacingFeet)
                .HasColumnName("max_spacing_feet")
                .HasColumnType("numeric(4,1)");

            entity.Property(e => e.AxleCountInGroup)
                .HasColumnName("axle_count_in_group")
                .IsRequired()
                .HasDefaultValue(1);

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            // Unique index on code
            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("idx_axle_groups_code_unique");

            // Index on active status
            entity.HasIndex(e => e.IsActive)
                .HasFilter("is_active = true")
                .HasDatabaseName("idx_axle_groups_active");
        });

        // ===== AxleFeeSchedule Entity Configuration =====
        modelBuilder.Entity<AxleFeeSchedule>(entity =>
        {
            entity.ToTable("axle_fee_schedules");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.LegalFramework)
                .HasColumnName("legal_framework")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.FeeType)
                .HasColumnName("fee_type")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.OverloadMinKg)
                .HasColumnName("overload_min_kg")
                .IsRequired();

            entity.Property(e => e.OverloadMaxKg)
                .HasColumnName("overload_max_kg");

            entity.Property(e => e.FeePerKgUsd)
                .HasColumnName("fee_per_kg_usd")
                .HasColumnType("numeric(10,4)")
                .IsRequired();

            entity.Property(e => e.FlatFeeUsd)
                .HasColumnName("flat_fee_usd")
                .HasColumnType("numeric(10,2)")
                .IsRequired()
                .HasDefaultValue(0m);

            entity.Property(e => e.DemeritPoints)
                .HasColumnName("demerit_points")
                .IsRequired()
                .HasDefaultValue(0);

            entity.Property(e => e.PenaltyDescription)
                .HasColumnName("penalty_description")
                .HasColumnType("text");

            entity.Property(e => e.EffectiveFrom)
                .HasColumnName("effective_from")
                .IsRequired();

            entity.Property(e => e.EffectiveTo)
                .HasColumnName("effective_to");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired();

            // Check constraint on legal framework
            entity.HasCheckConstraint("chk_legal_framework",
                "legal_framework IN ('EAC', 'TRAFFIC_ACT')");

            // Check constraint on fee type
            entity.HasCheckConstraint("chk_fee_type",
                "fee_type IN ('GVW', 'AXLE')");

            // Composite index on framework and fee type
            entity.HasIndex(e => new { e.LegalFramework, e.FeeType })
                .HasDatabaseName("idx_axle_fee_schedule_framework_type");

            // Index on effective date range
            entity.HasIndex(e => new { e.EffectiveFrom, e.EffectiveTo })
                .HasDatabaseName("idx_axle_fee_schedule_effective");
        });

        // ===== WeighingAxle Entity Configuration =====
        modelBuilder.Entity<WeighingAxle>(entity =>
        {
            entity.ToTable("weighing_axles");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.WeighingId)
                .HasColumnName("weighing_id")
                .IsRequired();

            entity.Property(e => e.AxleNumber)
                .HasColumnName("axle_number")
                .IsRequired();

            entity.Property(e => e.MeasuredWeightKg)
                .HasColumnName("measured_weight_kg")
                .IsRequired();

            entity.Property(e => e.PermissibleWeightKg)
                .HasColumnName("permissible_weight_kg")
                .IsRequired();

            entity.Property(e => e.AxleConfigurationId)
                .HasColumnName("axle_configuration_id")
                .IsRequired();

            entity.Property(e => e.AxleWeightReferenceId)
                .HasColumnName("axle_weight_reference_id");

            entity.Property(e => e.AxleGroupId)
                .HasColumnName("axle_group_id")
                .IsRequired();

            entity.Property(e => e.AxleGrouping)
                .HasColumnName("axle_grouping")
                .HasMaxLength(10)
                .IsRequired();

            entity.Property(e => e.TyreTypeId)
                .HasColumnName("tyre_type_id");

            entity.Property(e => e.FeeUsd)
                .HasColumnName("fee_usd")
                .HasColumnType("numeric(18,2)")
                .IsRequired()
                .HasDefaultValue(0m);

            entity.Property(e => e.CapturedAt)
                .HasColumnName("captured_at")
                .IsRequired();

            // Relationship to AxleConfiguration
            entity.HasOne(e => e.AxleConfiguration)
                .WithMany(ac => ac.WeighingAxles)
                .HasForeignKey(e => e.AxleConfigurationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship to AxleWeightReference
            entity.HasOne(e => e.AxleWeightReference)
                .WithMany()
                .HasForeignKey(e => e.AxleWeightReferenceId)
                .OnDelete(DeleteBehavior.SetNull);

            // Relationship to AxleGroup
            entity.HasOne(e => e.AxleGroup)
                .WithMany(ag => ag.WeighingAxles)
                .HasForeignKey(e => e.AxleGroupId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship to TyreType
            entity.HasOne(e => e.TyreType)
                .WithMany(tt => tt.WeighingAxles)
                .HasForeignKey(e => e.TyreTypeId)
                .OnDelete(DeleteBehavior.SetNull);

            // Unique constraint: one entry per weighing per axle
            entity.HasIndex(e => new { e.WeighingId, e.AxleNumber })
                .IsUnique()
                .HasDatabaseName("idx_weighing_axles_weighing_axle_unique");

            // Indexes for performance
            entity.HasIndex(e => e.WeighingId)
                .HasDatabaseName("idx_weighing_axles_weighing");

            entity.HasIndex(e => e.AxleConfigurationId)
                .HasDatabaseName("idx_weighing_axles_configuration");

            entity.HasIndex(e => e.AxleGroupId)
                .HasDatabaseName("idx_weighing_axles_group");
        });

        return modelBuilder;
    }
}
