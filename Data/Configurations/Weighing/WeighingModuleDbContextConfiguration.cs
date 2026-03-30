using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Weighing;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.System;

namespace TruLoad.Backend.Data.Configurations.Weighing
{
    /// <summary>
    /// Module-specific DbContext configuration for Weighing entities.
    /// This keeps the main TruLoadDbContext clean and modular.
    /// </summary>
    public static class WeighingModuleDbContextConfiguration
    {
        /// <summary>
        /// Applies all weighing module entity configurations to the model builder.
        /// </summary>
        public static void ApplyWeighingConfigurations(this ModelBuilder modelBuilder)
        {
            // Apply dedicated AxleFeeSchedule configuration
            modelBuilder.ApplyConfiguration(new AxleFeeScheduleTypeConfiguration());
            // VehicleOwner entity configuration
            modelBuilder.Entity<VehicleOwner>(entity =>
            {
                entity.ToTable("vehicle_owners", "weighing");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasColumnType("uuid")
                    .HasDefaultValueSql("gen_random_uuid()");

                entity.Property(e => e.IdNoOrPassport)
                    .HasColumnName("id_no_or_passport")
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(e => e.FullName)
                    .HasColumnName("full_name")
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(e => e.Phone)
                    .HasColumnName("phone")
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(e => e.Email)
                    .HasColumnName("email")
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(e => e.Address)
                    .HasColumnName("address")
                    .HasMaxLength(500)
                    .IsRequired();

                entity.Property(e => e.NtacNo)
                    .HasColumnName("ntac_no")
                    .HasMaxLength(50);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Indexes
                entity.HasIndex(e => e.IdNoOrPassport).IsUnique();
                entity.HasIndex(e => e.NtacNo).IsUnique().HasFilter("ntac_no IS NOT NULL");
                entity.HasIndex(e => e.Email);
                entity.HasIndex(e => e.Phone);
            });

            // Transporter entity configuration
            modelBuilder.Entity<Transporter>(entity =>
            {
                entity.ToTable("transporters", "weighing");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasColumnType("uuid")
                    .HasDefaultValueSql("gen_random_uuid()");

                entity.Property(e => e.Code)
                    .HasColumnName("code")
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(e => e.Name)
                    .HasColumnName("name")
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(e => e.RegistrationNo)
                    .HasColumnName("registration_no")
                    .HasMaxLength(50);

                entity.Property(e => e.Phone)
                    .HasColumnName("phone")
                    .HasMaxLength(20);

                entity.Property(e => e.Email)
                    .HasColumnName("email")
                    .HasMaxLength(200);

                entity.Property(e => e.Address)
                    .HasColumnName("address")
                    .HasMaxLength(500);

                entity.Property(e => e.NtacNo)
                    .HasColumnName("ntac_no")
                    .HasMaxLength(50);

                entity.Property(e => e.IsActive)
                    .HasColumnName("is_active")
                    .HasDefaultValue(true);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnName("updated_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Indexes
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.RegistrationNo).IsUnique().HasFilter("registration_no IS NOT NULL");
                entity.HasIndex(e => e.NtacNo).IsUnique().HasFilter("ntac_no IS NOT NULL");
                entity.HasIndex(e => e.Email);
                entity.HasIndex(e => e.Phone);
                entity.HasIndex(e => e.IsActive);
            });

            // Vehicle entity configuration
            modelBuilder.Entity<Vehicle>(entity =>
            {
                entity.ToTable("vehicles", "weighing");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasColumnType("uuid")
                    .HasDefaultValueSql("gen_random_uuid()");

                entity.Property(e => e.RegNo)
                    .HasColumnName("reg_no")
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(e => e.Make)
                    .HasColumnName("make")
                    .HasMaxLength(100);

                entity.Property(e => e.Model)
                    .HasColumnName("model")
                    .HasMaxLength(100);

                entity.Property(e => e.VehicleType)
                    .HasColumnName("vehicle_type")
                    .HasMaxLength(50);

                entity.Property(e => e.Color)
                    .HasColumnName("color")
                    .HasMaxLength(50);

                entity.Property(e => e.ChassisNo)
                    .HasColumnName("chassis_no")
                    .HasMaxLength(50);

                entity.Property(e => e.EngineNo)
                    .HasColumnName("engine_no")
                    .HasMaxLength(50);

                entity.Property(e => e.YearOfManufacture)
                    .HasColumnName("year_of_manufacture");

                entity.Property(e => e.Description)
                    .HasColumnName("description")
                    .HasMaxLength(1000);

                entity.Property(e => e.IsFlagged)
                    .HasColumnName("is_flagged")
                    .HasDefaultValue(false);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                    .HasColumnName("updated_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Foreign keys
                entity.Property(e => e.OwnerId)
                    .HasColumnName("owner_id");

                entity.Property(e => e.TransporterId)
                    .HasColumnName("transporter_id");

                entity.Property(e => e.AxleConfigurationId)
                    .HasColumnName("axle_configuration_id");

                // Relationships
                entity.HasOne(e => e.Owner)
                    .WithMany(o => o.Vehicles)
                    .HasForeignKey(e => e.OwnerId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.Transporter)
                    .WithMany(t => t.Vehicles)
                    .HasForeignKey(e => e.TransporterId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.AxleConfiguration)
                    .WithMany()
                    .HasForeignKey(e => e.AxleConfigurationId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Indexes
                entity.HasIndex(e => e.RegNo).IsUnique();
                entity.HasIndex(e => e.ChassisNo)
                    .IsUnique()
                    .HasFilter("chassis_no IS NOT NULL");
                entity.HasIndex(e => e.EngineNo);
                entity.HasIndex(e => e.OwnerId);
                entity.HasIndex(e => e.TransporterId);
                entity.HasIndex(e => e.AxleConfigurationId);
                entity.HasIndex(e => e.IsFlagged);

                // DescriptionEmbedding: always mapped with HNSW index
                entity.Property(e => e.DescriptionEmbedding).HasColumnType("vector(384)");
                entity.HasIndex(e => e.DescriptionEmbedding)
                    .HasMethod("hnsw")
                    .HasOperators("vector_cosine_ops");
            });

            // Permit entity configuration
            modelBuilder.Entity<Permit>(entity =>
            {
                entity.ToTable("permits", "weighing");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasColumnType("uuid")
                    .HasDefaultValueSql("gen_random_uuid()");

                entity.Property(e => e.PermitNo)
                    .HasColumnName("permit_no")
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(e => e.ValidFrom)
                    .HasColumnName("valid_from")
                    .IsRequired();

                entity.Property(e => e.ValidTo)
                    .HasColumnName("valid_to")
                    .IsRequired();

                entity.Property(e => e.AxleExtensionKg)
                    .HasColumnName("axle_extension_kg");

                entity.Property(e => e.GvwExtensionKg)
                    .HasColumnName("gvw_extension_kg");

                entity.Property(e => e.Status)
                    .HasColumnName("status")
                    .HasMaxLength(20)
                    .HasDefaultValue("active")
                    .IsRequired();

                entity.Property(e => e.IssuingAuthority)
                    .HasColumnName("issuing_authority")
                    .HasMaxLength(255);

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.OrganizationId)
                    .HasColumnName("organization_id");

                entity.Property(e => e.StationId)
                    .HasColumnName("station_id");

                // Foreign keys
                entity.Property(e => e.PermitTypeId)
                    .HasColumnName("permit_type_id")
                    .IsRequired();

                entity.Property(e => e.VehicleId)
                    .HasColumnName("vehicle_id")
                    .IsRequired();

                // Relationships
                entity.HasOne(e => e.PermitType)
                    .WithMany(pt => pt.Permits)
                    .HasForeignKey(e => e.PermitTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Vehicle)
                    .WithMany(v => v.Permits)
                    .HasForeignKey(e => e.VehicleId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Check constraints
                entity.HasCheckConstraint("chk_permit_status", "\"status\" IN ('active', 'expired', 'revoked')");
                entity.HasCheckConstraint("chk_permit_dates", "\"valid_to\" > \"valid_from\"");

                // Indexes
                entity.HasIndex(e => e.PermitNo).IsUnique();
                entity.HasIndex(e => e.PermitTypeId);
                entity.HasIndex(e => e.VehicleId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.ValidFrom);
                entity.HasIndex(e => e.ValidTo);
            });

            // WeighingTransaction entity configuration
            modelBuilder.Entity<WeighingTransaction>(entity =>
            {
                entity.ToTable("weighing_transactions", "weighing");

                entity.HasKey(e => new { e.Id, e.OrganizationId });

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasColumnType("uuid")
                    .HasDefaultValueSql("gen_random_uuid()");

                entity.Property(e => e.TicketNumber)
                    .HasColumnName("ticket_number")
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(e => e.VehicleRegNumber)
                    .HasColumnName("vehicle_reg_number")
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(e => e.GvwMeasuredKg)
                    .HasColumnName("gvw_measured_kg");

                entity.Property(e => e.GvwPermissibleKg)
                    .HasColumnName("gvw_permissible_kg");

                entity.Property(e => e.OverloadKg)
                    .HasColumnName("overload_kg");

                entity.Property(e => e.ControlStatus)
                    .HasColumnName("control_status")
                    .HasMaxLength(50)
                    .HasDefaultValue("Pending");

                entity.Property(e => e.ViolationReason)
                    .HasColumnName("violation_reason")
                    .HasMaxLength(1000);

                entity.Property(e => e.HasPermit)
                    .HasColumnName("has_permit")
                    .HasDefaultValue(false);

                entity.Property(e => e.ReweighCycleNo)
                    .HasColumnName("reweigh_cycle_no")
                    .HasDefaultValue(0);

                entity.Property(e => e.OriginalWeighingId)
                    .HasColumnName("original_weighing_id");

                entity.Property(e => e.TotalFeeUsd)
                    .HasColumnName("total_fee_usd")
                    .HasColumnType("decimal(18,2)")
                    .HasDefaultValue(0m);

                entity.Property(e => e.WeighedAt)
                    .HasColumnName("weighed_at")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.IsSync)
                    .HasColumnName("is_sync")
                    .HasDefaultValue(false);

                entity.Property(e => e.OrganizationId)
                    .HasColumnName("organization_id");

                // Foreign Keys
                entity.Property(e => e.VehicleId).HasColumnName("vehicle_id").IsRequired();
                entity.Property(e => e.DriverId).HasColumnName("driver_id");
                entity.Property(e => e.TransporterId).HasColumnName("transporter_id");
                entity.Property(e => e.StationId).HasColumnName("station_id").IsRequired();
                entity.Property(e => e.WeighedByUserId).HasColumnName("weighed_by_user_id").IsRequired();
                
                // Relationships (Using Restrict to prevent accidental cascade deletes of history)
                entity.HasOne(e => e.Vehicle)
                    .WithMany()
                    .HasForeignKey(e => e.VehicleId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Driver)
                    .WithMany()
                    .HasForeignKey(e => e.DriverId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Transporter)
                    .WithMany()
                    .HasForeignKey(e => e.TransporterId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Station)
                    .WithMany()
                    .HasForeignKey(e => e.StationId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.WeighedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.WeighedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.OriginalWeighing)
                    .WithMany()
                    .HasForeignKey(e => new { e.OriginalWeighingId, e.OrganizationId })
                    .OnDelete(DeleteBehavior.Restrict);

                // Indexes
                entity.HasIndex(e => e.TicketNumber).IsUnique();
                entity.HasIndex(e => e.VehicleRegNumber);
                entity.HasIndex(e => e.WeighedAt);

                // Composite indexes for performance optimization (Jan 10, 2026)
                entity.HasIndex(e => new { e.StationId, e.WeighedAt })
                    .HasDatabaseName("IX_weighing_transactions_station_date");

                entity.HasIndex(e => new { e.StationId, e.ControlStatus, e.WeighedAt })
                    .HasDatabaseName("IX_weighing_transactions_station_status_date");

                entity.HasIndex(e => new { e.VehicleId, e.WeighedAt })
                    .HasDatabaseName("IX_weighing_transactions_vehicle_date");
                entity.HasIndex(e => e.StationId);
                entity.HasIndex(e => e.ControlStatus);
                entity.HasIndex(e => e.OriginalWeighingId);

                // ViolationReasonEmbedding: always mapped with HNSW index
                entity.Property(e => e.ViolationReasonEmbedding).HasColumnType("vector(384)");
                entity.HasIndex(e => e.ViolationReasonEmbedding)
                    .HasMethod("hnsw")
                    .HasOperators("vector_cosine_ops");
            });

            // WeighingAxle entity configuration
            modelBuilder.Entity<WeighingAxle>(entity =>
            {
                entity.ToTable("weighing_axles", "weighing");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasDefaultValueSql("gen_random_uuid()");

                entity.Property(e => e.WeighingId).HasColumnName("weighing_id").IsRequired();
                entity.Property(e => e.AxleNumber).HasColumnName("axle_number").IsRequired();
                entity.Property(e => e.MeasuredWeightKg).HasColumnName("measured_weight_kg");
                entity.Property(e => e.PermissibleWeightKg).HasColumnName("permissible_weight_kg");
                entity.Property(e => e.AxleConfigurationId).HasColumnName("axle_configuration_id");
                entity.Property(e => e.AxleWeightReferenceId).HasColumnName("axle_weight_reference_id");
                entity.Property(e => e.AxleGroupId).HasColumnName("axle_group_id");
                entity.Property(e => e.AxleGrouping).HasColumnName("axle_grouping").HasMaxLength(10);

                // Regulatory compliance properties
                entity.Property(e => e.AxleType)
                    .HasColumnName("axle_type")
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(e => e.AxleSpacingMeters)
                    .HasColumnName("axle_spacing_meters")
                    .HasColumnType("decimal(5,2)");

                entity.Property(e => e.PavementDamageFactor)
                    .HasColumnName("pavement_damage_factor")
                    .HasColumnType("decimal(10,4)")
                    .HasDefaultValue(0.0000m);

                entity.Property(e => e.GroupAggregateWeightKg)
                    .HasColumnName("group_aggregate_weight_kg");

                entity.Property(e => e.GroupPermissibleWeightKg)
                    .HasColumnName("group_permissible_weight_kg");

                entity.Property(e => e.TyreTypeId).HasColumnName("tyre_type_id");
                entity.Property(e => e.FeeUsd).HasColumnName("fee_usd").HasColumnType("decimal(18,2)").HasDefaultValue(0m);
                entity.Property(e => e.CapturedAt).HasColumnName("captured_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Relationships
                entity.HasOne(e => e.WeighingTransaction)
                    .WithMany(w => w.WeighingAxles)
                    .HasForeignKey(e => new { e.WeighingId, e.OrganizationId })
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.AxleConfiguration)
                    .WithMany(ac => ac.WeighingAxles)
                    .HasForeignKey(e => e.AxleConfigurationId)
                    .OnDelete(DeleteBehavior.Restrict);

                 entity.HasOne(e => e.AxleWeightReference)
                    .WithMany()
                    .HasForeignKey(e => e.AxleWeightReferenceId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.AxleGroup)
                    .WithMany(ag => ag.WeighingAxles)
                    .HasForeignKey(e => e.AxleGroupId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.TyreType)
                    .WithMany(tt => tt.WeighingAxles)
                    .HasForeignKey(e => e.TyreTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Indexes
                entity.HasIndex(e => new { e.WeighingId, e.AxleNumber }).IsUnique();
                entity.HasIndex(e => e.AxleType).HasDatabaseName("IX_weighing_axles_axle_type");
                entity.HasIndex(e => new { e.WeighingId, e.AxleGrouping }).HasDatabaseName("IX_weighing_axles_weighing_grouping");
                entity.HasIndex(e => new { e.WeighingId, e.AxleGrouping, e.AxleType }).HasDatabaseName("IX_weighing_axles_weighing_grouping_type");
            });

            // Driver entity configuration
            modelBuilder.Entity<Driver>(entity =>
            {
                entity.ToTable("drivers", "weighing");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasDefaultValueSql("gen_random_uuid()");

                entity.Property(e => e.NtsaId).HasColumnName("ntsa_id").HasMaxLength(50);
                entity.Property(e => e.IdNumber).HasColumnName("id_number").HasMaxLength(20);
                entity.Property(e => e.DrivingLicenseNo).HasColumnName("driving_license_no").HasMaxLength(50);
                entity.Property(e => e.FullNames).HasColumnName("full_names").HasMaxLength(100).IsRequired();
                entity.Property(e => e.Surname).HasColumnName("surname").HasMaxLength(100).IsRequired();
                entity.Property(e => e.Gender).HasColumnName("gender").HasMaxLength(20);
                entity.Property(e => e.Nationality).HasColumnName("nationality").HasMaxLength(50);
                entity.Property(e => e.DateOfBirth).HasColumnName("date_of_birth");
                entity.Property(e => e.Address).HasColumnName("address").HasMaxLength(255);
                entity.Property(e => e.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20);
                entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(100);
                entity.Property(e => e.LicenseClass).HasColumnName("license_class").HasMaxLength(20);
                entity.Property(e => e.LicenseIssueDate).HasColumnName("license_issue_date");
                entity.Property(e => e.LicenseExpiryDate).HasColumnName("license_expiry_date");
                entity.Property(e => e.LicenseStatus).HasColumnName("license_status").HasMaxLength(20).HasDefaultValue("active");
                entity.Property(e => e.IsProfessionalDriver).HasColumnName("is_professional_driver").HasDefaultValue(false);
                entity.Property(e => e.CurrentDemeritPoints).HasColumnName("current_demerit_points").HasDefaultValue(0);
                entity.Property(e => e.SuspensionStartDate).HasColumnName("suspension_start_date");
                entity.Property(e => e.SuspensionEndDate).HasColumnName("suspension_end_date");
                entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Indexes — filtered unique so multiple NULLs are allowed
                entity.HasIndex(e => e.DrivingLicenseNo).IsUnique().HasFilter("driving_license_no IS NOT NULL");
                entity.HasIndex(e => e.IdNumber).IsUnique().HasFilter("id_number IS NOT NULL");
                entity.HasIndex(e => e.NtsaId);
            });

            // DriverDemeritRecord entity configuration
            modelBuilder.Entity<DriverDemeritRecord>(entity =>
            {
                entity.ToTable("driver_demerit_records", "weighing");

                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
                
                entity.Property(e => e.DriverId).HasColumnName("driver_id").IsRequired();
                entity.Property(e => e.CaseRegisterId).HasColumnName("case_register_id");
                entity.Property(e => e.WeighingId).HasColumnName("weighing_id");
                entity.Property(e => e.ViolationDate).HasColumnName("violation_date").IsRequired();
                entity.Property(e => e.PointsAssigned).HasColumnName("points_assigned").IsRequired();
                entity.Property(e => e.FeeScheduleId).HasColumnName("fee_schedule_id");
                entity.Property(e => e.LegalFramework).HasColumnName("legal_framework").HasMaxLength(20).IsRequired();
                entity.Property(e => e.ViolationType).HasColumnName("violation_type").HasMaxLength(50).IsRequired();
                entity.Property(e => e.OverloadKg).HasColumnName("overload_kg");
                entity.Property(e => e.PenaltyAmountUsd).HasColumnName("penalty_amount_usd").HasColumnType("decimal(12,2)").HasDefaultValue(0);
                entity.Property(e => e.PaymentStatus).HasColumnName("payment_status").HasMaxLength(20).HasDefaultValue("pending");
                entity.Property(e => e.PointsExpiryDate).HasColumnName("points_expiry_date").IsRequired();
                entity.Property(e => e.IsExpired).HasColumnName("is_expired").HasDefaultValue(false);
                entity.Property(e => e.Notes).HasColumnName("notes").HasColumnType("text");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Relationships
                entity.HasOne(e => e.Driver)
                    .WithMany(d => d.DemeritRecords)
                    .HasForeignKey(e => e.DriverId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.FeeSchedule)
                    .WithMany()
                    .HasForeignKey(e => e.FeeScheduleId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ProhibitionOrder entity configuration
            modelBuilder.Entity<ProhibitionOrder>(entity =>
            {
                entity.ToTable("prohibition_orders", "weighing");

                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

                entity.Property(e => e.WeighingId).HasColumnName("weighing_id").IsRequired();
                entity.Property(e => e.ProhibitionNo).HasColumnName("prohibition_no").HasMaxLength(50).IsRequired();
                entity.Property(e => e.IssuedAt).HasColumnName("issued_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(e => e.IssuedById).HasColumnName("issued_by_id").IsRequired();
                entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("Open");
                entity.Property(e => e.Reason).HasColumnName("reason").HasColumnType("text");
                entity.Property(e => e.ClosedAt).HasColumnName("closed_at");

                entity.HasOne(e => e.Weighing)
                    .WithMany()
                    .HasForeignKey(e => new { e.WeighingId, e.OrganizationId })
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.IssuedBy)
                    .WithMany()
                    .HasForeignKey(e => e.IssuedById)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.ProhibitionNo).IsUnique();
                entity.HasIndex(e => e.WeighingId);
                entity.HasIndex(e => e.IssuedById);
                entity.HasIndex(e => e.Status);
            });
    }
}
}
