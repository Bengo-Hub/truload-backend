using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.System;

namespace TruLoad.Backend.Data.Configurations.SystemConfiguration;

/// <summary>
/// System Configuration Module DbContext Configuration
/// Contains configurations for system configuration entities including:
/// - PermitType, ToleranceSetting
/// </summary>
public static class SystemConfigurationModuleDbContextConfiguration
{
    /// <summary>
    /// Applies system configuration module configurations to the model builder
    /// </summary>
    public static ModelBuilder ApplySystemConfigurationConfigurations(this ModelBuilder modelBuilder)
    {
        // ===== PermitType Entity Configuration =====
        modelBuilder.Entity<TruLoad.Backend.Models.System.PermitType>(entity =>
        {
            entity.ToTable("permit_types");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            entity.Property(e => e.AxleExtensionKg)
                .HasColumnName("axle_extension_kg")
                .IsRequired();

            entity.Property(e => e.GvwExtensionKg)
                .HasColumnName("gvw_extension_kg")
                .IsRequired();

            entity.Property(e => e.ValidityDays)
                .HasColumnName("validity_days");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");

            // Unique constraint on code
            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("idx_permit_types_code");
        });

        // ===== ToleranceSetting Entity Configuration =====
        modelBuilder.Entity<TruLoad.Backend.Models.ToleranceSetting>(entity =>
        {
            entity.ToTable("tolerance_settings");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.LegalFramework)
                .HasColumnName("legal_framework")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.TolerancePercentage)
                .HasColumnName("tolerance_percentage")
                .HasColumnType("decimal(5,2)")
                .IsRequired();

            entity.Property(e => e.ToleranceKg)
                .HasColumnName("tolerance_kg");

            entity.Property(e => e.AppliesTo)
                .HasColumnName("applies_to")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("text");

            entity.Property(e => e.EffectiveFrom)
                .HasColumnName("effective_from")
                .IsRequired();

            entity.Property(e => e.EffectiveTo)
                .HasColumnName("effective_to");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");

            // Unique constraint on code
            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("idx_tolerance_settings_code");

            // Index on effective dates for active settings
            entity.HasIndex(e => new { e.EffectiveFrom, e.EffectiveTo })
                .HasDatabaseName("idx_tolerance_settings_effective_dates");
        });

        // ===== Document Entity Configuration =====
        modelBuilder.Entity<TruLoad.Backend.Models.Infrastructure.Document>(entity =>
        {
            entity.ToTable("documents");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.FileName)
                .HasColumnName("file_name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.MimeType)
                .HasColumnName("mime_type")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.FileSize)
                .HasColumnName("file_size")
                .IsRequired();

            entity.Property(e => e.FilePath)
                .HasColumnName("file_path")
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.FileUrl)
                .HasColumnName("file_url")
                .HasMaxLength(500);

            entity.Property(e => e.Checksum)
                .HasColumnName("checksum")
                .HasMaxLength(64);

            entity.Property(e => e.DocumentType)
                .HasColumnName("document_type")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.RelatedEntityType)
                .HasColumnName("related_entity_type")
                .HasMaxLength(100);

            entity.Property(e => e.RelatedEntityId)
                .HasColumnName("related_entity_id");

            entity.Property(e => e.UploadedById)
                .HasColumnName("uploaded_by_id");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Relationships
            entity.HasOne(e => e.UploadedBy)
                .WithMany()
                .HasForeignKey(e => e.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            entity.HasIndex(e => new { e.RelatedEntityType, e.RelatedEntityId });
            entity.HasIndex(e => e.DocumentType);
        });

        // ===== AxleTypeOverloadFeeSchedule Entity Configuration (Sprint 11) =====
        modelBuilder.Entity<AxleTypeOverloadFeeSchedule>(entity =>
        {
            entity.ToTable("axle_type_overload_fee_schedules");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.OverloadMinKg)
                .HasColumnName("overload_min_kg")
                .IsRequired();

            entity.Property(e => e.OverloadMaxKg)
                .HasColumnName("overload_max_kg");

            entity.Property(e => e.SteeringAxleFeeUsd)
                .HasColumnName("steering_axle_fee_usd")
                .HasColumnType("decimal(10,2)")
                .IsRequired();

            entity.Property(e => e.SingleDriveAxleFeeUsd)
                .HasColumnName("single_drive_axle_fee_usd")
                .HasColumnType("decimal(10,2)")
                .IsRequired();

            entity.Property(e => e.TandemAxleFeeUsd)
                .HasColumnName("tandem_axle_fee_usd")
                .HasColumnType("decimal(10,2)")
                .IsRequired();

            entity.Property(e => e.TridemAxleFeeUsd)
                .HasColumnName("tridem_axle_fee_usd")
                .HasColumnType("decimal(10,2)")
                .IsRequired();

            entity.Property(e => e.QuadAxleFeeUsd)
                .HasColumnName("quad_axle_fee_usd")
                .HasColumnType("decimal(10,2)")
                .IsRequired();

            entity.Property(e => e.LegalFramework)
                .HasColumnName("legal_framework")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.EffectiveFrom)
                .HasColumnName("effective_from")
                .IsRequired();

            entity.Property(e => e.EffectiveTo)
                .HasColumnName("effective_to");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");

            entity.Property(e => e.DeletedAt)
                .HasColumnName("deleted_at");

            // Index for fee lookup by overload range
            entity.HasIndex(e => new { e.OverloadMinKg, e.OverloadMaxKg, e.IsActive })
                .HasDatabaseName("idx_axle_fee_overload_range");

            // Index on effective dates
            entity.HasIndex(e => new { e.EffectiveFrom, e.EffectiveTo })
                .HasDatabaseName("idx_axle_fee_effective_dates");
        });

        // ===== DemeritPointSchedule Entity Configuration (Sprint 11) =====
        modelBuilder.Entity<DemeritPointSchedule>(entity =>
        {
            entity.ToTable("demerit_point_schedules");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ViolationType)
                .HasColumnName("violation_type")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.OverloadMinKg)
                .HasColumnName("overload_min_kg")
                .IsRequired();

            entity.Property(e => e.OverloadMaxKg)
                .HasColumnName("overload_max_kg");

            entity.Property(e => e.Points)
                .HasColumnName("points")
                .IsRequired();

            entity.Property(e => e.LegalFramework)
                .HasColumnName("legal_framework")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.EffectiveFrom)
                .HasColumnName("effective_from")
                .IsRequired();

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");

            entity.Property(e => e.DeletedAt)
                .HasColumnName("deleted_at");

            // Index for point lookup by violation type and overload range
            entity.HasIndex(e => new { e.ViolationType, e.OverloadMinKg, e.OverloadMaxKg, e.IsActive })
                .HasDatabaseName("idx_demerit_violation_overload");

            // Index on legal framework
            entity.HasIndex(e => e.LegalFramework)
                .HasDatabaseName("idx_demerit_legal_framework");
        });

        // ===== PenaltySchedule Entity Configuration (Sprint 11) =====
        modelBuilder.Entity<PenaltySchedule>(entity =>
        {
            entity.ToTable("penalty_schedules");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.PointsMin)
                .HasColumnName("points_min")
                .IsRequired();

            entity.Property(e => e.PointsMax)
                .HasColumnName("points_max");

            entity.Property(e => e.PenaltyDescription)
                .HasColumnName("penalty_description")
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.SuspensionDays)
                .HasColumnName("suspension_days");

            entity.Property(e => e.RequiresCourt)
                .HasColumnName("requires_court")
                .IsRequired();

            entity.Property(e => e.AdditionalFineUsd)
                .HasColumnName("additional_fine_usd")
                .HasColumnType("decimal(10,2)")
                .IsRequired();

            entity.Property(e => e.AdditionalFineKes)
                .HasColumnName("additional_fine_kes")
                .HasColumnType("decimal(12,2)")
                .IsRequired();

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");

            entity.Property(e => e.DeletedAt)
                .HasColumnName("deleted_at");

            // Index for penalty lookup by points range
            entity.HasIndex(e => new { e.PointsMin, e.PointsMax, e.IsActive })
                .HasDatabaseName("idx_penalty_points_range");
        });

        return modelBuilder;
    }
}