using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Models.CaseManagement;

namespace TruLoad.Backend.Data.Configurations.CaseManagement;

/// <summary>
/// Configuration for extended case management entities.
/// Includes: Court, LoadCorrectionMemo, ComplianceCertificate, CaseAssignmentLog
/// </summary>
public static class ExtendedCaseEntitiesConfiguration
{
    public static void ApplyExtendedEntitiesConfigurations(this ModelBuilder modelBuilder)
    {
        ConfigureCourt(modelBuilder);
        ConfigureLoadCorrectionMemo(modelBuilder);
        ConfigureComplianceCertificate(modelBuilder);
        ConfigureCaseAssignmentLog(modelBuilder);
    }

    private static void ConfigureCourt(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Court>(entity =>
        {
            entity.ToTable("courts");
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

            entity.Property(e => e.Location)
                .HasColumnName("location")
                .HasMaxLength(500);

            entity.Property(e => e.CourtType)
                .HasColumnName("court_type")
                .HasMaxLength(50)
                .HasDefaultValue("magistrate");

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
                .HasDatabaseName("idx_courts_code");

            entity.HasIndex(e => e.Name)
                .HasDatabaseName("idx_courts_name");

            entity.HasIndex(e => e.CourtType)
                .HasDatabaseName("idx_courts_type");

            // CHECK constraint
            entity.HasCheckConstraint("chk_court_type",
                "court_type IN ('magistrate', 'high_court', 'appeal_court', 'supreme_court')");
        });
    }

    private static void ConfigureLoadCorrectionMemo(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LoadCorrectionMemo>(entity =>
        {
            entity.ToTable("load_correction_memos");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.MemoNo)
                .HasColumnName("memo_no")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.CaseRegisterId)
                .HasColumnName("case_register_id")
                .IsRequired();

            entity.Property(e => e.WeighingId)
                .HasColumnName("weighing_id")
                .IsRequired();

            entity.Property(e => e.OverloadKg)
                .HasColumnName("overload_kg");

            entity.Property(e => e.RedistributionType)
                .HasColumnName("redistribution_type")
                .HasMaxLength(50);

            entity.Property(e => e.ReweighScheduledAt)
                .HasColumnName("reweigh_scheduled_at");

            entity.Property(e => e.ReweighWeighingId)
                .HasColumnName("reweigh_weighing_id");

            entity.Property(e => e.ComplianceAchieved)
                .HasColumnName("compliance_achieved")
                .HasDefaultValue(false);

            entity.Property(e => e.IssuedById)
                .HasColumnName("issued_by_id")
                .IsRequired();

            entity.Property(e => e.IssuedAt)
                .HasColumnName("issued_at")
                .HasDefaultValueSql("NOW()");

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
            entity.HasOne(e => e.CaseRegister)
                .WithMany()
                .HasForeignKey(e => e.CaseRegisterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Weighing)
                .WithMany()
                .HasForeignKey(e => e.WeighingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ReweighWeighing)
                .WithMany()
                .HasForeignKey(e => e.ReweighWeighingId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.IssuedBy)
                .WithMany()
                .HasForeignKey(e => e.IssuedById)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            entity.HasIndex(e => e.MemoNo)
                .IsUnique()
                .HasDatabaseName("idx_load_correction_memos_memo_no");

            entity.HasIndex(e => e.CaseRegisterId)
                .HasDatabaseName("idx_load_correction_memos_case_id");

            entity.HasIndex(e => e.WeighingId)
                .HasDatabaseName("idx_load_correction_memos_weighing_id");

            entity.HasIndex(e => e.IssuedAt)
                .HasDatabaseName("idx_load_correction_memos_issued_at");

            // CHECK constraint
            entity.HasCheckConstraint("chk_load_correction_redistribution_type",
                "redistribution_type IN ('offload', 'redistribute')");
        });
    }

    private static void ConfigureComplianceCertificate(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ComplianceCertificate>(entity =>
        {
            entity.ToTable("compliance_certificates");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.CertificateNo)
                .HasColumnName("certificate_no")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.CaseRegisterId)
                .HasColumnName("case_register_id")
                .IsRequired();

            entity.Property(e => e.WeighingId)
                .HasColumnName("weighing_id")
                .IsRequired();

            entity.Property(e => e.LoadCorrectionMemoId)
                .HasColumnName("load_correction_memo_id");

            entity.Property(e => e.IssuedById)
                .HasColumnName("issued_by_id")
                .IsRequired();

            entity.Property(e => e.IssuedAt)
                .HasColumnName("issued_at")
                .HasDefaultValueSql("NOW()");

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
            entity.HasOne(e => e.CaseRegister)
                .WithMany()
                .HasForeignKey(e => e.CaseRegisterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Weighing)
                .WithMany()
                .HasForeignKey(e => e.WeighingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.LoadCorrectionMemo)
                .WithMany(lcm => lcm.ComplianceCertificates)
                .HasForeignKey(e => e.LoadCorrectionMemoId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.IssuedBy)
                .WithMany()
                .HasForeignKey(e => e.IssuedById)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            entity.HasIndex(e => e.CertificateNo)
                .IsUnique()
                .HasDatabaseName("idx_compliance_certificates_cert_no");

            entity.HasIndex(e => e.CaseRegisterId)
                .HasDatabaseName("idx_compliance_certificates_case_id");

            entity.HasIndex(e => e.WeighingId)
                .HasDatabaseName("idx_compliance_certificates_weighing_id");

            entity.HasIndex(e => e.IssuedAt)
                .HasDatabaseName("idx_compliance_certificates_issued_at");
        });
    }

    private static void ConfigureCaseAssignmentLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CaseAssignmentLog>(entity =>
        {
            entity.ToTable("case_assignment_logs");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.CaseRegisterId)
                .HasColumnName("case_register_id")
                .IsRequired();

            entity.Property(e => e.PreviousOfficerId)
                .HasColumnName("previous_officer_id");

            entity.Property(e => e.NewOfficerId)
                .HasColumnName("new_officer_id")
                .IsRequired();

            entity.Property(e => e.AssignedById)
                .HasColumnName("assigned_by_id")
                .IsRequired();

            entity.Property(e => e.AssignmentType)
                .HasColumnName("assignment_type")
                .HasMaxLength(20)
                .HasDefaultValue("initial");

            entity.Property(e => e.Reason)
                .HasColumnName("reason")
                .HasColumnType("text");

            entity.Property(e => e.AssignedAt)
                .HasColumnName("assigned_at")
                .HasDefaultValueSql("NOW()");

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
            entity.HasOne(e => e.CaseRegister)
                .WithMany()
                .HasForeignKey(e => e.CaseRegisterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.PreviousOfficer)
                .WithMany()
                .HasForeignKey(e => e.PreviousOfficerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.NewOfficer)
                .WithMany()
                .HasForeignKey(e => e.NewOfficerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.AssignedBy)
                .WithMany()
                .HasForeignKey(e => e.AssignedById)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            entity.HasIndex(e => e.CaseRegisterId)
                .HasDatabaseName("idx_case_assignment_logs_case_id");

            entity.HasIndex(e => e.NewOfficerId)
                .HasDatabaseName("idx_case_assignment_logs_new_officer_id");

            entity.HasIndex(e => e.AssignedAt)
                .HasDatabaseName("idx_case_assignment_logs_assigned_at");

            // Composite index for officer assignment history
            entity.HasIndex(e => new { e.CaseRegisterId, e.AssignedAt })
                .HasDatabaseName("idx_case_assignment_logs_case_timeline");

            // CHECK constraint
            entity.HasCheckConstraint("chk_case_assignment_type",
                "assignment_type IN ('initial', 're_assignment', 'transfer')");
        });
    }
}
