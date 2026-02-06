using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models.Yard;

namespace TruLoad.Backend.Data.Configurations.Yard;

/// <summary>
/// Yard & Tags Module DbContext Configuration
/// Contains configurations for yard entries, vehicle tags, and tag categories
/// </summary>
public static class YardModuleDbContextConfiguration
{
    /// <summary>
    /// Applies yard module configurations to the model builder
    /// </summary>
    public static ModelBuilder ApplyYardConfigurations(this ModelBuilder modelBuilder)
    {
        // ===== YardEntry Entity Configuration =====
        modelBuilder.Entity<YardEntry>(entity =>
        {
            entity.ToTable("yard_entries", t =>
            {
                t.HasCheckConstraint("chk_yard_entry_status", "status IN ('pending', 'processing', 'released', 'escalated')");
                t.HasCheckConstraint("chk_yard_entry_reason", "reason IN ('redistribution', 'gvw_overload', 'permit_check', 'offload')");
            });
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.WeighingId)
                .HasColumnName("weighing_id")
                .IsRequired();

            entity.Property(e => e.StationId)
                .HasColumnName("station_id")
                .IsRequired();

            entity.Property(e => e.Reason)
                .HasColumnName("reason")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .HasDefaultValue("pending");

            entity.Property(e => e.EnteredAt)
                .HasColumnName("entered_at")
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.ReleasedAt)
                .HasColumnName("released_at");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.DeletedAt)
                .HasColumnName("deleted_at");

            // Relationships
            entity.HasOne(e => e.Weighing)
                .WithMany()
                .HasForeignKey(e => e.WeighingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Station)
                .WithMany()
                .HasForeignKey(e => e.StationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            entity.HasIndex(e => e.WeighingId)
                .HasDatabaseName("idx_yard_entries_weighing_id");

            entity.HasIndex(e => e.StationId)
                .HasDatabaseName("idx_yard_entries_station_id");

            entity.HasIndex(e => e.Status)
                .HasDatabaseName("idx_yard_entries_status");

            entity.HasIndex(e => e.EnteredAt)
                .HasDatabaseName("idx_yard_entries_entered_at");

            // Composite index for station + status queries
            entity.HasIndex(e => new { e.StationId, e.Status })
                .HasDatabaseName("idx_yard_entries_station_status");
        });

        // ===== VehicleTag Entity Configuration =====
        modelBuilder.Entity<VehicleTag>(entity =>
        {
            entity.ToTable("vehicle_tags", t =>
            {
                t.HasCheckConstraint("chk_vehicle_tag_type", "tag_type IN ('automatic', 'manual')");
                t.HasCheckConstraint("chk_vehicle_tag_status", "status IN ('open', 'closed')");
            });
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.RegNo)
                .HasColumnName("reg_no")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.TagType)
                .HasColumnName("tag_type")
                .HasMaxLength(20)
                .HasDefaultValue("automatic");

            entity.Property(e => e.TagCategoryId)
                .HasColumnName("tag_category_id")
                .IsRequired();

            entity.Property(e => e.Reason)
                .HasColumnName("reason")
                .HasColumnType("text")
                .IsRequired();

            entity.Property(e => e.StationCode)
                .HasColumnName("station_code")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .HasDefaultValue("open");

            entity.Property(e => e.TagPhotoPath)
                .HasColumnName("tag_photo_path")
                .HasMaxLength(500);

            entity.Property(e => e.EffectiveTimePeriod)
                .HasColumnName("effective_time_period");

            entity.Property(e => e.CreatedById)
                .HasColumnName("created_by_id")
                .IsRequired();

            entity.Property(e => e.ClosedById)
                .HasColumnName("closed_by_id");

            entity.Property(e => e.ClosedReason)
                .HasColumnName("closed_reason")
                .HasMaxLength(500);

            entity.Property(e => e.OpenedAt)
                .HasColumnName("opened_at")
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.ClosedAt)
                .HasColumnName("closed_at");

            entity.Property(e => e.Exported)
                .HasColumnName("exported")
                .HasDefaultValue(false);

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.DeletedAt)
                .HasColumnName("deleted_at");

            // Relationships
            entity.HasOne(e => e.TagCategory)
                .WithMany(tc => tc.VehicleTags)
                .HasForeignKey(e => e.TagCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ClosedBy)
                .WithMany()
                .HasForeignKey(e => e.ClosedById)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes
            entity.HasIndex(e => e.RegNo)
                .HasDatabaseName("idx_vehicle_tags_reg_no");

            entity.HasIndex(e => e.TagCategoryId)
                .HasDatabaseName("idx_vehicle_tags_category_id");

            entity.HasIndex(e => e.Status)
                .HasDatabaseName("idx_vehicle_tags_status");

            entity.HasIndex(e => e.StationCode)
                .HasDatabaseName("idx_vehicle_tags_station_code");

            entity.HasIndex(e => e.OpenedAt)
                .HasDatabaseName("idx_vehicle_tags_opened_at");

            // Composite index for reg_no + status (active tags lookup)
            entity.HasIndex(e => new { e.RegNo, e.Status })
                .HasDatabaseName("idx_vehicle_tags_reg_status");

            // NOTE: ReasonEmbedding vector property is configured in TruLoadDbContext.OnModelCreating
            // conditionally for PostgreSQL only (not supported by InMemory provider for tests)
        });

        // ===== TagCategory Entity Configuration =====
        modelBuilder.Entity<TagCategory>(entity =>
        {
            entity.ToTable("tag_categories");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
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
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.DeletedAt)
                .HasColumnName("deleted_at");

            // Indexes
            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("idx_tag_categories_code");

            entity.HasIndex(e => e.Name)
                .HasDatabaseName("idx_tag_categories_name");
        });

        return modelBuilder;
    }
}
