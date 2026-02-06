using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.Infrastructure;

namespace TruLoad.Backend.Data.Configurations.Infrastructure
{
    /// <summary>
    /// Module-specific DbContext configuration for Infrastructure entities.
    /// Includes Station, ScaleTest, and reference data entities.
    /// </summary>
    public static class InfrastructureModuleDbContextConfiguration
    {
        /// <summary>
        /// Applies all infrastructure module entity configurations to the model builder.
        /// </summary>
        public static void ApplyInfrastructureConfigurations(this ModelBuilder modelBuilder)
        {
            // ScaleTest entity configuration
            modelBuilder.Entity<ScaleTest>(entity =>
            {
                entity.ToTable("scale_tests");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasColumnType("uuid")
                    .HasDefaultValueSql("gen_random_uuid()");

                entity.Property(e => e.StationId)
                    .HasColumnName("station_id")
                    .IsRequired();

                entity.Property(e => e.TestWeightKg)
                    .HasColumnName("test_weight_kg");

                entity.Property(e => e.Result)
                    .HasColumnName("result")
                    .HasMaxLength(20)
                    .IsRequired()
                    .HasDefaultValue("pass");

                entity.Property(e => e.DeviationKg)
                    .HasColumnName("deviation_kg");

                entity.Property(e => e.Details)
                    .HasColumnName("details")
                    .HasMaxLength(1000);

                entity.Property(e => e.CarriedAt)
                    .HasColumnName("carried_at")
                    .IsRequired();

                entity.Property(e => e.CarriedById)
                    .HasColumnName("carried_by_id")
                    .IsRequired();

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnName("updated_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.DeletedAt)
                    .HasColumnName("deleted_at");

                // Foreign keys
                entity.HasOne(e => e.Station)
                    .WithMany()
                    .HasForeignKey(e => e.StationId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CarriedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CarriedById)
                    .OnDelete(DeleteBehavior.Restrict);

                // Indexes
                entity.HasIndex(e => e.StationId);
                entity.HasIndex(e => e.CarriedById);
                entity.HasIndex(e => e.CarriedAt);
                entity.HasIndex(e => e.Result);
                entity.HasIndex(e => e.DeletedAt);
            });

            // CargoTypes entity configuration
            modelBuilder.Entity<CargoTypes>(entity =>
            {
                entity.ToTable("cargo_types");

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
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(e => e.Category)
                    .HasColumnName("category")
                    .HasMaxLength(50)
                    .HasDefaultValue("General");

                entity.Property(e => e.IsActive)
                    .HasColumnName("is_active")
                    .HasDefaultValue(true);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnName("updated_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.DeletedAt)
                    .HasColumnName("deleted_at");

                // Indexes
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.IsActive);
            });

            // OriginsDestinations entity configuration
            modelBuilder.Entity<OriginsDestinations>(entity =>
            {
                entity.ToTable("origins_destinations");

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
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(e => e.LocationType)
                    .HasColumnName("location_type")
                    .HasMaxLength(50)
                    .HasDefaultValue("city");

                entity.Property(e => e.Country)
                    .HasColumnName("country")
                    .HasMaxLength(100)
                    .HasDefaultValue("Kenya");

                entity.Property(e => e.IsActive)
                    .HasColumnName("is_active")
                    .HasDefaultValue(true);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnName("updated_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.DeletedAt)
                    .HasColumnName("deleted_at");

                // Indexes
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.Country);
                entity.HasIndex(e => e.IsActive);
            });

            // Roads entity configuration
            modelBuilder.Entity<Roads>(entity =>
            {
                entity.ToTable("roads");

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
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(e => e.RoadClass)
                    .HasColumnName("road_class")
                    .HasMaxLength(10);

                entity.Property(e => e.DistrictId)
                    .HasColumnName("district_id");

                entity.Property(e => e.TotalLengthKm)
                    .HasColumnName("total_length_km")
                    .HasColumnType("decimal(10,2)");

                entity.Property(e => e.IsActive)
                    .HasColumnName("is_active")
                    .HasDefaultValue(true);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnName("updated_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.DeletedAt)
                    .HasColumnName("deleted_at");

                // Foreign keys (optional - only if Districts entity exists)
                // entity.HasOne(e => e.District)
                //     .WithMany()
                //     .HasForeignKey(e => e.DistrictId)
                //     .OnDelete(DeleteBehavior.SetNull);

                // Indexes
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.RoadClass);
                entity.HasIndex(e => e.DistrictId);
                entity.HasIndex(e => e.IsActive);
            });

            // VehicleMake entity configuration
            modelBuilder.Entity<VehicleMake>(entity =>
            {
                entity.ToTable("vehicle_makes");

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
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(e => e.Country)
                    .HasColumnName("country")
                    .HasMaxLength(100);

                entity.Property(e => e.Description)
                    .HasColumnName("description")
                    .HasMaxLength(500);

                entity.Property(e => e.IsActive)
                    .HasColumnName("is_active")
                    .HasDefaultValue(true);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnName("updated_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.DeletedAt)
                    .HasColumnName("deleted_at");

                // Indexes
                entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName("idx_vehicle_makes_code");
                entity.HasIndex(e => e.Name).HasDatabaseName("idx_vehicle_makes_name");
                entity.HasIndex(e => e.Country).HasDatabaseName("idx_vehicle_makes_country");
                entity.HasIndex(e => e.IsActive).HasDatabaseName("idx_vehicle_makes_is_active");
            });

            // VehicleModel entity configuration
            modelBuilder.Entity<VehicleModel>(entity =>
            {
                entity.ToTable("vehicle_models");

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
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(e => e.MakeId)
                    .HasColumnName("make_id")
                    .IsRequired();

                entity.Property(e => e.VehicleCategory)
                    .HasColumnName("vehicle_category")
                    .HasMaxLength(50)
                    .HasDefaultValue("Truck");

                entity.Property(e => e.AxleConfigurationId)
                    .HasColumnName("axle_configuration_id");

                entity.Property(e => e.Description)
                    .HasColumnName("description")
                    .HasMaxLength(500);

                entity.Property(e => e.IsActive)
                    .HasColumnName("is_active")
                    .HasDefaultValue(true);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnName("updated_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.DeletedAt)
                    .HasColumnName("deleted_at");

                // Foreign keys
                entity.HasOne(e => e.Make)
                    .WithMany(m => m.Models)
                    .HasForeignKey(e => e.MakeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.AxleConfiguration)
                    .WithMany()
                    .HasForeignKey(e => e.AxleConfigurationId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Indexes
                entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName("idx_vehicle_models_code");
                entity.HasIndex(e => e.Name).HasDatabaseName("idx_vehicle_models_name");
                entity.HasIndex(e => e.MakeId).HasDatabaseName("idx_vehicle_models_make_id");
                entity.HasIndex(e => e.VehicleCategory).HasDatabaseName("idx_vehicle_models_category");
                entity.HasIndex(e => e.IsActive).HasDatabaseName("idx_vehicle_models_is_active");

                // CHECK constraint
                entity.HasCheckConstraint("chk_vehicle_category",
                    "vehicle_category IN ('Truck', 'Trailer', 'Bus', 'Van', 'Other')");
            });
        }
    }
}
