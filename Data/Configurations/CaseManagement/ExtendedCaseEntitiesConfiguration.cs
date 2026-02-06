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
        ConfigureCaseParty(modelBuilder);
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

            // KenloadV2 CaseIOs pattern: IsCurrent flag for active IO tracking
            entity.Property(e => e.IsCurrent)
                .HasColumnName("is_current")
                .HasDefaultValue(true);

            // IO Rank tracking
            entity.Property(e => e.OfficerRank)
                .HasColumnName("officer_rank")
                .HasMaxLength(50);

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

            // Index for IsCurrent flag - efficient querying of active IO per case
            entity.HasIndex(e => new { e.CaseRegisterId, e.IsCurrent })
                .HasDatabaseName("idx_case_assignment_logs_current_io");

            // CHECK constraint - includes 'handover' for KenloadV2 compatibility
            entity.HasCheckConstraint("chk_case_assignment_type",
                "assignment_type IN ('initial', 're_assignment', 'transfer', 'handover')");
        });
    }

    private static void ConfigureCaseParty(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CaseParty>(entity =>
        {
            entity.ToTable("case_parties");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.CaseRegisterId)
                .HasColumnName("case_register_id")
                .IsRequired();

            entity.Property(e => e.PartyRole)
                .HasColumnName("party_role")
                .HasMaxLength(50)
                .HasDefaultValue("defendant_driver");

            entity.Property(e => e.UserId)
                .HasColumnName("user_id");

            entity.Property(e => e.DriverId)
                .HasColumnName("driver_id");

            entity.Property(e => e.VehicleOwnerId)
                .HasColumnName("vehicle_owner_id");

            entity.Property(e => e.TransporterId)
                .HasColumnName("transporter_id");

            entity.Property(e => e.ExternalName)
                .HasColumnName("external_name")
                .HasMaxLength(255);

            entity.Property(e => e.ExternalIdNumber)
                .HasColumnName("external_id_number")
                .HasMaxLength(50);

            entity.Property(e => e.ExternalPhone)
                .HasColumnName("external_phone")
                .HasMaxLength(50);

            entity.Property(e => e.Notes)
                .HasColumnName("notes")
                .HasColumnType("text");

            entity.Property(e => e.IsCurrentlyActive)
                .HasColumnName("is_currently_active")
                .HasDefaultValue(true);

            entity.Property(e => e.AddedAt)
                .HasColumnName("added_at")
                .HasDefaultValueSql("NOW()");

            entity.Property(e => e.RemovedAt)
                .HasColumnName("removed_at");

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
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Driver)
                .WithMany()
                .HasForeignKey(e => e.DriverId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.VehicleOwner)
                .WithMany()
                .HasForeignKey(e => e.VehicleOwnerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Transporter)
                .WithMany()
                .HasForeignKey(e => e.TransporterId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes
            entity.HasIndex(e => e.CaseRegisterId)
                .HasDatabaseName("idx_case_parties_case_id");

            entity.HasIndex(e => e.PartyRole)
                .HasDatabaseName("idx_case_parties_role");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("idx_case_parties_user_id");

            entity.HasIndex(e => e.DriverId)
                .HasDatabaseName("idx_case_parties_driver_id");

            entity.HasIndex(e => e.IsCurrentlyActive)
                .HasDatabaseName("idx_case_parties_active");

            // Composite index for finding active parties by role
            entity.HasIndex(e => new { e.CaseRegisterId, e.PartyRole, e.IsCurrentlyActive })
                .HasDatabaseName("idx_case_parties_case_role_active");

            // CHECK constraint for party role
            entity.HasCheckConstraint("chk_case_party_role",
                "party_role IN ('investigating_officer', 'ocs', 'arresting_officer', 'prosecutor', 'defendant_driver', 'defendant_owner', 'defendant_transporter', 'witness', 'complainant')");
        });
    }
}
