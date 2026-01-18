using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models.Infrastructure;

namespace TruLoad.Backend.Data.Configurations.Infrastructure;

/// <summary>
/// Geographic Module DbContext Configuration
/// Contains configurations for geographic entities: Counties, Districts, Subcounties
/// </summary>
public static class GeographicModuleDbContextConfiguration
{
    /// <summary>
    /// Applies geographic module configurations to the model builder
    /// </summary>
    public static ModelBuilder ApplyGeographicConfigurations(this ModelBuilder modelBuilder)
    {
        // ===== Subcounty Entity Configuration =====
        modelBuilder.Entity<Subcounty>(entity =>
        {
            entity.ToTable("subcounties");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.DistrictId)
                .HasColumnName("district_id")
                .IsRequired();

            entity.Property(e => e.Code)
                .HasColumnName("code")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

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
            entity.HasOne(e => e.District)
                .WithMany()
                .HasForeignKey(e => e.DistrictId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("idx_subcounties_code");

            entity.HasIndex(e => e.DistrictId)
                .HasDatabaseName("idx_subcounties_district_id");

            entity.HasIndex(e => e.Name)
                .HasDatabaseName("idx_subcounties_name");
        });

        return modelBuilder;
    }
}
