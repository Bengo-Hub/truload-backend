using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;

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

        return modelBuilder;
    }
}