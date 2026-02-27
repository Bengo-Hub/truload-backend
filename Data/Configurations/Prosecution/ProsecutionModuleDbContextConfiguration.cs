using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models.Prosecution;

namespace TruLoad.Backend.Data.Configurations.Prosecution;

/// <summary>
/// Prosecution Module DbContext Configuration
/// Contains configurations for prosecution cases with automated charge computation
/// </summary>
public static class ProsecutionModuleDbContextConfiguration
{
    /// <summary>
    /// Applies prosecution module configurations to the model builder
    /// </summary>
    public static ModelBuilder ApplyProsecutionConfigurations(this ModelBuilder modelBuilder)
    {
        // ===== ProsecutionCase Entity Configuration =====
        modelBuilder.Entity<ProsecutionCase>(entity =>
        {
            entity.ToTable("prosecution_cases");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.CaseRegisterId)
                .HasColumnName("case_register_id")
                .IsRequired();

            entity.Property(e => e.WeighingId)
                .HasColumnName("weighing_id");

            entity.Property(e => e.ProsecutionOfficerId)
                .HasColumnName("prosecution_officer_id")
                .IsRequired();

            entity.Property(e => e.ActId)
                .HasColumnName("act_id")
                .IsRequired();

            entity.Property(e => e.GvwOverloadKg)
                .HasColumnName("gvw_overload_kg");

            entity.Property(e => e.GvwFeeUsd)
                .HasColumnName("gvw_fee_usd")
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.GvwFeeKes)
                .HasColumnName("gvw_fee_kes")
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.MaxAxleOverloadKg)
                .HasColumnName("max_axle_overload_kg");

            entity.Property(e => e.MaxAxleFeeUsd)
                .HasColumnName("max_axle_fee_usd")
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.MaxAxleFeeKes)
                .HasColumnName("max_axle_fee_kes")
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.BestChargeBasis)
                .HasColumnName("best_charge_basis")
                .HasMaxLength(10)
                .HasDefaultValue("gvw");

            entity.Property(e => e.PenaltyMultiplier)
                .HasColumnName("penalty_multiplier")
                .HasColumnType("decimal(5,2)")
                .HasDefaultValue(1.0m);

            entity.Property(e => e.OffenseCount)
                .HasColumnName("offense_count")
                .HasDefaultValue(0);

            entity.Property(e => e.DemeritPoints)
                .HasColumnName("demerit_points")
                .HasDefaultValue(0);

            entity.Property(e => e.TotalFeeUsd)
                .HasColumnName("total_fee_usd")
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.TotalFeeKes)
                .HasColumnName("total_fee_kes")
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.ForexRate)
                .HasColumnName("forex_rate")
                .HasColumnType("decimal(10,4)");

            entity.Property(e => e.CertificateNo)
                .HasColumnName("certificate_no")
                .HasMaxLength(50);

            entity.Property(e => e.CaseNotes)
                .HasColumnName("case_notes")
                .HasColumnType("text");

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .HasDefaultValue("pending");

            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.OrganizationId)
                .HasColumnName("organization_id");

            entity.Property(e => e.StationId)
                .HasColumnName("station_id");

            entity.Property(e => e.DeletedAt)
                .HasColumnName("deleted_at");

            // Relationships
            entity.HasOne(e => e.CaseRegister)
                .WithMany()
                .HasForeignKey(e => e.CaseRegisterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Weighing)
                .WithMany()
                .HasForeignKey(e => new { e.WeighingId, e.OrganizationId })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ProsecutionOfficer)
                .WithMany()
                .HasForeignKey(e => e.ProsecutionOfficerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Act)
                .WithMany()
                .HasForeignKey(e => e.ActId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            entity.HasIndex(e => e.CaseRegisterId)
                .IsUnique() // One-to-one with CaseRegister
                .HasDatabaseName("idx_prosecution_cases_case_register_id");

            entity.HasIndex(e => e.WeighingId)
                .HasDatabaseName("idx_prosecution_cases_weighing_id");

            entity.HasIndex(e => e.ProsecutionOfficerId)
                .HasDatabaseName("idx_prosecution_cases_officer_id");

            entity.HasIndex(e => e.CertificateNo)
                .IsUnique()
                .HasDatabaseName("idx_prosecution_cases_certificate_no");

            entity.HasIndex(e => e.Status)
                .HasDatabaseName("idx_prosecution_cases_status");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("idx_prosecution_cases_created_at");

            // CaseNotesEmbedding: always mapped with HNSW index
            entity.Property(e => e.CaseNotesEmbedding).HasColumnType("vector(384)");
            entity.HasIndex(e => e.CaseNotesEmbedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");

            // CHECK constraints
            entity.HasCheckConstraint("chk_prosecution_case_basis",
                "best_charge_basis IN ('gvw', 'axle')");

            entity.HasCheckConstraint("chk_prosecution_case_status",
                "status IN ('pending', 'invoiced', 'paid', 'court')");

            entity.HasCheckConstraint("chk_prosecution_penalty_multiplier",
                "penalty_multiplier >= 1.0 AND penalty_multiplier <= 10.0");
        });

        return modelBuilder;
    }
}
