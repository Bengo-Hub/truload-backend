using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models.Offline;

namespace TruLoad.Backend.Data.Configurations.Offline;

/// <summary>
/// Offline Support Module DbContext Configuration
/// Contains configurations for device sync events with JSONB payload
/// </summary>
public static class OfflineModuleDbContextConfiguration
{
    /// <summary>
    /// Applies offline support module configurations to the model builder
    /// </summary>
    public static ModelBuilder ApplyOfflineConfigurations(this ModelBuilder modelBuilder)
    {
        // ===== DeviceSyncEvent Entity Configuration =====
        modelBuilder.Entity<DeviceSyncEvent>(entity =>
        {
            entity.ToTable("device_sync_events");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.DeviceId)
                .HasColumnName("device_id")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.EntityType)
                .HasColumnName("entity_type")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.EntityId)
                .HasColumnName("entity_id");

            entity.Property(e => e.CorrelationId)
                .HasColumnName("correlation_id")
                .IsRequired();

            entity.Property(e => e.Operation)
                .HasColumnName("operation")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.Payload)
                .HasColumnName("payload")
                .HasColumnType("jsonb")
                .IsRequired();

            entity.Property(e => e.SyncStatus)
                .HasColumnName("sync_status")
                .HasMaxLength(20)
                .HasDefaultValue("queued");

            entity.Property(e => e.SyncAttempts)
                .HasColumnName("sync_attempts")
                .HasDefaultValue(0);

            entity.Property(e => e.LastSyncAttemptAt)
                .HasColumnName("last_sync_attempt_at");

            entity.Property(e => e.ErrorMessage)
                .HasColumnName("error_message")
                .HasColumnType("text");

            entity.Property(e => e.SyncedAt)
                .HasColumnName("synced_at");

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
            entity.HasIndex(e => new { e.DeviceId, e.SyncStatus })
                .HasDatabaseName("idx_device_sync_device_status");

            entity.HasIndex(e => e.CorrelationId)
                .IsUnique()
                .HasDatabaseName("idx_device_sync_correlation");

            entity.HasIndex(e => new { e.SyncStatus, e.CreatedAt })
                .HasDatabaseName("idx_device_sync_status_created")
                .HasFilter("sync_status IN ('queued', 'failed')"); // Partial index for active queue

            entity.HasIndex(e => new { e.EntityType, e.EntityId })
                .HasDatabaseName("idx_device_sync_entity")
                .HasFilter("entity_id IS NOT NULL"); // Partial index for synced entities

            // CHECK constraints
            entity.HasCheckConstraint("chk_device_sync_entity_type",
                "entity_type IN ('weighing', 'case_register', 'yard_entry', 'vehicle_tag', 'special_release')");

            entity.HasCheckConstraint("chk_device_sync_operation",
                "operation IN ('create', 'update', 'delete')");

            entity.HasCheckConstraint("chk_device_sync_status",
                "sync_status IN ('queued', 'processing', 'synced', 'failed')");

            entity.HasCheckConstraint("chk_device_sync_attempts",
                "sync_attempts >= 0 AND sync_attempts <= 10");
        });

        return modelBuilder;
    }
}
