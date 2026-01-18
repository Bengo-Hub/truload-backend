using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TruLoad.Backend.Models.System;

namespace TruLoad.Backend.Data.Configurations.Weighing;

/// <summary>
/// Entity configuration for AxleFeeSchedule
/// Defines table mapping, constraints, and indexes for fee calculation tiers
/// </summary>
public class AxleFeeScheduleTypeConfiguration : IEntityTypeConfiguration<AxleFeeSchedule>
{
    public void Configure(EntityTypeBuilder<AxleFeeSchedule> builder)
    {
        // Table naming
        builder.ToTable("axle_fee_schedules");
        
        // Primary key
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()")
            .ValueGeneratedOnAdd();
        
        // Column mapping and constraints
        builder.Property(x => x.LegalFramework)
            .HasColumnName("legal_framework")
            .HasMaxLength(50)
            .IsRequired();
            
        builder.Property(x => x.FeeType)
            .HasColumnName("fee_type")
            .HasMaxLength(20)
            .IsRequired();
            
        builder.Property(x => x.OverloadMinKg)
            .HasColumnName("overload_min_kg")
            .IsRequired();
            
        builder.Property(x => x.OverloadMaxKg)
            .HasColumnName("overload_max_kg");
            
        builder.Property(x => x.FeePerKgUsd)
            .HasColumnName("fee_per_kg_usd")
            .HasPrecision(18, 4)
            .IsRequired();
            
        builder.Property(x => x.FlatFeeUsd)
            .HasColumnName("flat_fee_usd")
            .HasPrecision(18, 2)
            .HasDefaultValue(0m)
            .IsRequired();
            
        builder.Property(x => x.DemeritPoints)
            .HasColumnName("demerit_points")
            .HasDefaultValue(0)
            .IsRequired();
            
        builder.Property(x => x.PenaltyDescription)
            .HasColumnName("penalty_description")
            .HasMaxLength(500)
            .IsRequired();
            
        builder.Property(x => x.EffectiveFrom)
            .HasColumnName("effective_from")
            .IsRequired();
            
        builder.Property(x => x.EffectiveTo)
            .HasColumnName("effective_to");
            
        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();
            
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .ValueGeneratedOnAdd()
            .IsRequired();
            
        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .ValueGeneratedOnAdd()
            .IsRequired();
        
        // Indexes for common queries
        builder.HasIndex(x => x.LegalFramework)
            .HasDatabaseName("IX_axle_fee_schedules_legal_framework");
            
        builder.HasIndex(x => x.FeeType)
            .HasDatabaseName("IX_axle_fee_schedules_fee_type");
            
        builder.HasIndex(x => new { x.LegalFramework, x.FeeType, x.OverloadMinKg, x.OverloadMaxKg })
            .HasDatabaseName("IX_axle_fee_schedules_lookup");
            
        builder.HasIndex(x => x.IsActive)
            .HasDatabaseName("IX_axle_fee_schedules_is_active");
            
        builder.HasIndex(x => x.EffectiveFrom)
            .HasDatabaseName("IX_axle_fee_schedules_effective_from");
        
        // Check constraints
        builder.HasCheckConstraint(
            "chk_axle_fee_schedules_overload_range",
            "\"overload_min_kg\" >= 0 AND (\"overload_max_kg\" IS NULL OR \"overload_max_kg\" >= \"overload_min_kg\")");
            
        builder.HasCheckConstraint(
            "chk_axle_fee_schedules_dates",
            "\"effective_to\" IS NULL OR \"effective_to\" > \"effective_from\"");
            
        builder.HasCheckConstraint(
            "chk_axle_fee_schedules_legal_framework",
            "\"legal_framework\" IN ('EAC', 'TRAFFIC_ACT')");
            
        builder.HasCheckConstraint(
            "chk_axle_fee_schedules_fee_type",
            "\"fee_type\" IN ('GVW', 'AXLE')");
    }
}
