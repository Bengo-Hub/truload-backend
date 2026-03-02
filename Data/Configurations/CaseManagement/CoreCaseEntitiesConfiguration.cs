using Microsoft.EntityFrameworkCore;
using TruLoad.Backend.Data;
using TruLoad.Backend.Models;
using TruLoad.Backend.Models.CaseManagement;

namespace TruLoad.Backend.Data.Configurations.CaseManagement;

/// <summary>
/// Configuration for core case management entities.
/// Includes: CaseRegister, CaseSubfile, SpecialRelease, ArrestWarrant, CourtHearing, CaseClosureChecklist
/// </summary>
public static class CoreCaseEntitiesConfiguration
{
    public static void ApplyCoreEntitiesConfigurations(this ModelBuilder modelBuilder)
    {
        ConfigureCaseRegister(modelBuilder);
        ConfigureCaseSubfile(modelBuilder);
        ConfigureSpecialRelease(modelBuilder);
        ConfigureArrestWarrant(modelBuilder);
        ConfigureCourtHearing(modelBuilder);
        ConfigureCaseClosureChecklist(modelBuilder);
    }

    private static void ConfigureCaseRegister(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CaseRegister>(entity =>
        {
            entity.ToTable("case_registers");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.CaseNo)
                .HasColumnName("case_no")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.WeighingId)
                .HasColumnName("weighing_id");

            entity.Property(e => e.YardEntryId)
                .HasColumnName("yard_entry_id");

            entity.Property(e => e.ProhibitionOrderId)
                .HasColumnName("prohibition_order_id");

            entity.Property(e => e.VehicleId)
                .HasColumnName("vehicle_id")
                .IsRequired();

            entity.Property(e => e.DriverId)
                .HasColumnName("driver_id");

            entity.Property(e => e.ViolationTypeId)
                .HasColumnName("violation_type_id")
                .IsRequired();

            entity.Property(e => e.RoadId)
                .HasColumnName("road_id");

            entity.Property(e => e.CountyId)
                .HasColumnName("county_id");

            entity.Property(e => e.DistrictId)
                .HasColumnName("district_id");

            entity.Property(e => e.SubcountyId)
                .HasColumnName("subcounty_id");

            entity.Property(e => e.ViolationDetails)
                .HasColumnName("violation_details")
                .HasColumnType("text");

            entity.Property(e => e.ActId)
                .HasColumnName("act_id");

            entity.Property(e => e.DriverNtacNo)
                .HasColumnName("driver_ntac_no")
                .HasMaxLength(50);

            entity.Property(e => e.TransporterNtacNo)
                .HasColumnName("transporter_ntac_no")
                .HasMaxLength(50);

            entity.Property(e => e.ObNo)
                .HasColumnName("ob_no")
                .HasMaxLength(50);

            entity.Property(e => e.CourtId)
                .HasColumnName("court_id");

            entity.Property(e => e.DispositionTypeId)
                .HasColumnName("disposition_type_id");

            entity.Property(e => e.CaseStatusId)
                .HasColumnName("case_status_id")
                .IsRequired();

            entity.Property(e => e.EscalatedToCaseManager)
                .HasColumnName("escalated_to_case_manager")
                .HasDefaultValue(false);

            entity.Property(e => e.CaseManagerId)
                .HasColumnName("case_manager_id");

            entity.Property(e => e.ProsecutorId)
                .HasColumnName("prosecutor_id");

            entity.Property(e => e.ComplainantOfficerId)
                .HasColumnName("complainant_officer_id");

            entity.Property(e => e.DetentionStationId)
                .HasColumnName("detention_station_id");

            entity.Property(e => e.InvestigatingOfficerId)
                .HasColumnName("investigating_officer_id");

            entity.Property(e => e.InvestigatingOfficerAssignedById)
                .HasColumnName("investigating_officer_assigned_by_id");

            entity.Property(e => e.InvestigatingOfficerAssignedAt)
                .HasColumnName("investigating_officer_assigned_at")
                .HasColumnType("timestamp with time zone");

            entity.Property(e => e.CreatedById)
                .HasColumnName("created_by_id");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.Property(e => e.OrganizationId)
                .HasColumnName("organization_id");

            entity.Property(e => e.StationId)
                .HasColumnName("station_id");

            entity.Property(e => e.ClosedAt)
                .HasColumnName("closed_at")
                .HasColumnType("timestamp with time zone");

            entity.Property(e => e.ClosedById)
                .HasColumnName("closed_by_id");

            entity.Property(e => e.ClosingReason)
                .HasColumnName("closing_reason")
                .HasColumnType("text");

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            // Indexes
            entity.HasIndex(e => e.CaseNo)
                .HasDatabaseName("idx_case_registers_case_no")
                .IsUnique();

            entity.HasIndex(e => new { e.CaseStatusId, e.CreatedAt })
                .HasDatabaseName("idx_case_registers_status");

            entity.HasIndex(e => e.WeighingId)
                .HasDatabaseName("idx_case_registers_weighing")
                .HasFilter("weighing_id IS NOT NULL");

            entity.HasIndex(e => new { e.VehicleId, e.CreatedAt })
                .HasDatabaseName("idx_case_registers_vehicle");

            entity.HasIndex(e => e.ViolationTypeId)
                .HasDatabaseName("idx_case_registers_violation_type");

            entity.HasIndex(e => e.RoadId)
                .HasDatabaseName("idx_case_registers_road")
                .HasFilter("road_id IS NOT NULL");

            entity.HasIndex(e => e.CountyId)
                .HasDatabaseName("idx_case_registers_county")
                .HasFilter("county_id IS NOT NULL");

            entity.HasIndex(e => e.CourtId)
                .HasDatabaseName("idx_case_registers_court")
                .HasFilter("court_id IS NOT NULL");

            entity.HasIndex(e => e.DriverNtacNo)
                .HasDatabaseName("idx_case_registers_driver_ntac")
                .HasFilter("driver_ntac_no IS NOT NULL");

            entity.HasIndex(e => e.TransporterNtacNo)
                .HasDatabaseName("idx_case_registers_transporter_ntac")
                .HasFilter("transporter_ntac_no IS NOT NULL");

            entity.HasIndex(e => e.EscalatedToCaseManager)
                .HasDatabaseName("idx_case_registers_escalated")
                .HasFilter("escalated_to_case_manager = TRUE");

            // ViolationDetailsEmbedding: always mapped with HNSW index
            entity.Property(e => e.ViolationDetailsEmbedding).HasColumnType("vector(384)");
            entity.HasIndex(e => e.ViolationDetailsEmbedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");

            // Relationships
            entity.HasOne(e => e.ViolationType)
                .WithMany(v => v.CaseRegisters)
                .HasForeignKey(e => e.ViolationTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ActDefinition)
                .WithMany(a => a.CaseRegisters)
                .HasForeignKey(e => e.ActId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.DispositionType)
                .WithMany(d => d.CaseRegisters)
                .HasForeignKey(e => e.DispositionTypeId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.CaseStatus)
                .WithMany(s => s.CaseRegisters)
                .HasForeignKey(e => e.CaseStatusId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CaseManager)
                .WithMany(cm => cm.CaseRegisters)
                .HasForeignKey(e => e.CaseManagerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ComplainantOfficer)
                .WithMany()
                .HasForeignKey(e => e.ComplainantOfficerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.DetentionStation)
                .WithMany()
                .HasForeignKey(e => e.DetentionStationId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.Subfiles)
                .WithOne(s => s.CaseRegister)
                .HasForeignKey(s => s.CaseRegisterId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.SpecialReleases)
                .WithOne(sr => sr.CaseRegister)
                .HasForeignKey(sr => sr.CaseRegisterId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.ArrestWarrants)
                .WithOne(aw => aw.CaseRegister)
                .HasForeignKey(aw => aw.CaseRegisterId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.CourtHearings)
                .WithOne(ch => ch.CaseRegister)
                .HasForeignKey(ch => ch.CaseRegisterId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ClosureChecklist)
                .WithOne(cc => cc.CaseRegister)
                .HasForeignKey<CaseClosureChecklist>(cc => cc.CaseRegisterId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Weighing)
                .WithMany()
                .HasForeignKey(e => new { e.WeighingId, e.OrganizationId })
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureCaseSubfile(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CaseSubfile>(entity =>
        {
            entity.ToTable("case_subfiles");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.CaseRegisterId)
                .HasColumnName("case_register_id")
                .IsRequired();

            entity.Property(e => e.SubfileTypeId)
                .HasColumnName("subfile_type_id")
                .IsRequired();

            entity.Property(e => e.SubfileName)
                .HasColumnName("subfile_name")
                .HasMaxLength(100);

            entity.Property(e => e.DocumentType)
                .HasColumnName("document_type")
                .HasMaxLength(100);

            entity.Property(e => e.Content)
                .HasColumnName("content")
                .HasColumnType("text");

            entity.Property(e => e.FilePath)
                .HasColumnName("file_path")
                .HasMaxLength(500);

            entity.Property(e => e.FileUrl)
                .HasColumnName("file_url")
                .HasMaxLength(500);

            entity.Property(e => e.MimeType)
                .HasColumnName("mime_type")
                .HasMaxLength(100);

            entity.Property(e => e.FileSizeBytes)
                .HasColumnName("file_size_bytes")
                .HasColumnType("bigint");

            entity.Property(e => e.Checksum)
                .HasColumnName("checksum")
                .HasMaxLength(64);

            entity.Property(e => e.UploadedById)
                .HasColumnName("uploaded_by_id");

            entity.Property(e => e.UploadedAt)
                .HasColumnName("uploaded_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.Property(e => e.Metadata)
                .HasColumnName("metadata")
                .HasColumnType("jsonb");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.Property(e => e.OrganizationId)
                .HasColumnName("organization_id");

            entity.Property(e => e.StationId)
                .HasColumnName("station_id");

            // Indexes
            entity.HasIndex(e => new { e.CaseRegisterId, e.SubfileTypeId })
                .HasDatabaseName("idx_case_subfiles_case_type");

            entity.HasIndex(e => e.UploadedAt)
                .HasDatabaseName("idx_case_subfiles_uploaded");

            // ContentEmbedding: always mapped with HNSW index
            entity.Property(e => e.ContentEmbedding).HasColumnType("vector(384)");
            entity.HasIndex(e => e.ContentEmbedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");

            // Relationships
            entity.HasOne(e => e.SubfileType)
                .WithMany(st => st.CaseSubfiles)
                .HasForeignKey(e => e.SubfileTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSpecialRelease(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SpecialRelease>(entity =>
        {
            entity.ToTable("special_releases");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.CaseRegisterId)
                .HasColumnName("case_register_id")
                .IsRequired();

            entity.Property(e => e.CertificateNo)
                .HasColumnName("certificate_no")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.ReleaseTypeId)
                .HasColumnName("release_type_id")
                .IsRequired();

            entity.Property(e => e.OverloadKg)
                .HasColumnName("overload_kg");

            entity.Property(e => e.RedistributionAllowed)
                .HasColumnName("redistribution_allowed")
                .HasDefaultValue(false);

            entity.Property(e => e.ReweighRequired)
                .HasColumnName("reweigh_required")
                .HasDefaultValue(false);

            entity.Property(e => e.ReweighWeighingId)
                .HasColumnName("reweigh_weighing_id");

            entity.Property(e => e.ComplianceAchieved)
                .HasColumnName("compliance_achieved")
                .HasDefaultValue(false);

            entity.Property(e => e.Reason)
                .HasColumnName("reason")
                .HasColumnType("text")
                .IsRequired();

            entity.Property(e => e.AuthorizedById)
                .HasColumnName("authorized_by_id")
                .IsRequired();

            entity.Property(e => e.IssuedAt)
                .HasColumnName("issued_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.Property(e => e.OrganizationId)
                .HasColumnName("organization_id");

            entity.Property(e => e.StationId)
                .HasColumnName("station_id");

            // Indexes
            entity.HasIndex(e => e.CaseRegisterId)
                .HasDatabaseName("idx_special_releases_case")
                .IsUnique();

            entity.HasIndex(e => e.CertificateNo)
                .HasDatabaseName("idx_special_releases_cert")
                .IsUnique();

            entity.HasIndex(e => e.IssuedAt)
                .HasDatabaseName("idx_special_releases_issued");

            // Relationships
            entity.HasOne(e => e.ReleaseType)
                .WithMany(rt => rt.SpecialReleases)
                .HasForeignKey(e => e.ReleaseTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureArrestWarrant(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ArrestWarrant>(entity =>
        {
            entity.ToTable("arrest_warrants");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.CaseRegisterId)
                .HasColumnName("case_register_id")
                .IsRequired();

            entity.Property(e => e.WarrantNo)
                .HasColumnName("warrant_no")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.IssuedBy)
                .HasColumnName("issued_by")
                .HasMaxLength(255);

            entity.Property(e => e.AccusedName)
                .HasColumnName("accused_name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.AccusedIdNo)
                .HasColumnName("accused_id_no")
                .HasMaxLength(50);

            entity.Property(e => e.OffenceDescription)
                .HasColumnName("offence_description")
                .HasColumnType("text");

            entity.Property(e => e.WarrantStatusId)
                .HasColumnName("warrant_status_id")
                .IsRequired();

            entity.Property(e => e.IssuedAt)
                .HasColumnName("issued_at")
                .HasColumnType("timestamp with time zone")
                .IsRequired();

            entity.Property(e => e.ExecutedAt)
                .HasColumnName("executed_at")
                .HasColumnType("timestamp with time zone");

            entity.Property(e => e.DroppedAt)
                .HasColumnName("dropped_at")
                .HasColumnType("timestamp with time zone");

            entity.Property(e => e.ExecutionDetails)
                .HasColumnName("execution_details")
                .HasColumnType("text");

            entity.Property(e => e.DroppedReason)
                .HasColumnName("dropped_reason")
                .HasColumnType("text");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.Property(e => e.OrganizationId)
                .HasColumnName("organization_id");

            entity.Property(e => e.StationId)
                .HasColumnName("station_id");

            // Indexes
            entity.HasIndex(e => e.CaseRegisterId)
                .HasDatabaseName("idx_arrest_warrants_case");

            entity.HasIndex(e => e.WarrantNo)
                .HasDatabaseName("idx_arrest_warrants_warrant_no")
                .IsUnique();

            entity.HasIndex(e => new { e.WarrantStatusId, e.IssuedAt })
                .HasDatabaseName("idx_arrest_warrants_status");

            entity.HasIndex(e => e.AccusedIdNo)
                .HasDatabaseName("idx_arrest_warrants_accused")
                .HasFilter("accused_id_no IS NOT NULL");

            // Relationships
            entity.HasOne(e => e.WarrantStatus)
                .WithMany(ws => ws.ArrestWarrants)
                .HasForeignKey(e => e.WarrantStatusId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCourtHearing(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CourtHearing>(entity =>
        {
            entity.ToTable("court_hearings");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.CaseRegisterId)
                .HasColumnName("case_register_id")
                .IsRequired();

            entity.Property(e => e.CourtId)
                .HasColumnName("court_id");

            entity.Property(e => e.HearingDate)
                .HasColumnName("hearing_date")
                .HasColumnType("date")
                .IsRequired();

            entity.Property(e => e.HearingTime)
                .HasColumnName("hearing_time")
                .HasColumnType("time");

            entity.Property(e => e.HearingTypeId)
                .HasColumnName("hearing_type_id");

            entity.Property(e => e.HearingStatusId)
                .HasColumnName("hearing_status_id");

            entity.Property(e => e.HearingOutcomeId)
                .HasColumnName("hearing_outcome_id");

            entity.Property(e => e.MinuteNotes)
                .HasColumnName("minute_notes")
                .HasColumnType("text");

            entity.Property(e => e.NextHearingDate)
                .HasColumnName("next_hearing_date")
                .HasColumnType("date");

            entity.Property(e => e.AdjournmentReason)
                .HasColumnName("adjournment_reason")
                .HasColumnType("text");

            entity.Property(e => e.PresidingOfficer)
                .HasColumnName("presiding_officer")
                .HasMaxLength(255);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.Property(e => e.OrganizationId)
                .HasColumnName("organization_id");

            entity.Property(e => e.StationId)
                .HasColumnName("station_id");

            // Indexes
            entity.HasIndex(e => new { e.CaseRegisterId, e.HearingDate })
                .HasDatabaseName("idx_court_hearings_case_date");

            entity.HasIndex(e => new { e.HearingStatusId, e.HearingDate })
                .HasDatabaseName("idx_court_hearings_status_date");

            entity.HasIndex(e => new { e.CourtId, e.HearingDate })
                .HasDatabaseName("idx_court_hearings_court")
                .HasFilter("court_id IS NOT NULL");

            // MinuteNotesEmbedding: always mapped with HNSW index
            entity.Property(e => e.MinuteNotesEmbedding).HasColumnType("vector(384)");
            entity.HasIndex(e => e.MinuteNotesEmbedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");

            // Relationships
            entity.HasOne(e => e.HearingType)
                .WithMany(ht => ht.CourtHearings)
                .HasForeignKey(e => e.HearingTypeId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.HearingStatus)
                .WithMany(hs => hs.CourtHearings)
                .HasForeignKey(e => e.HearingStatusId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.HearingOutcome)
                .WithMany(ho => ho.CourtHearings)
                .HasForeignKey(e => e.HearingOutcomeId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureCaseClosureChecklist(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CaseClosureChecklist>(entity =>
        {
            entity.ToTable("case_closure_checklists");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            entity.Property(e => e.CaseRegisterId)
                .HasColumnName("case_register_id")
                .IsRequired();

            entity.Property(e => e.ClosureTypeId)
                .HasColumnName("closure_type_id");

            entity.Property(e => e.LegalSectionId)
                .HasColumnName("legal_section_id");

            entity.Property(e => e.SubfileAComplete)
                .HasColumnName("subfile_a_complete")
                .HasDefaultValue(false);

            entity.Property(e => e.SubfileBComplete)
                .HasColumnName("subfile_b_complete")
                .HasDefaultValue(false);

            entity.Property(e => e.SubfileCComplete)
                .HasColumnName("subfile_c_complete")
                .HasDefaultValue(false);

            entity.Property(e => e.SubfileDComplete)
                .HasColumnName("subfile_d_complete")
                .HasDefaultValue(false);

            entity.Property(e => e.SubfileEComplete)
                .HasColumnName("subfile_e_complete")
                .HasDefaultValue(false);

            entity.Property(e => e.SubfileFComplete)
                .HasColumnName("subfile_f_complete")
                .HasDefaultValue(false);

            entity.Property(e => e.SubfileGComplete)
                .HasColumnName("subfile_g_complete")
                .HasDefaultValue(false);

            entity.Property(e => e.SubfileHComplete)
                .HasColumnName("subfile_h_complete")
                .HasDefaultValue(false);

            entity.Property(e => e.SubfileIComplete)
                .HasColumnName("subfile_i_complete")
                .HasDefaultValue(false);

            entity.Property(e => e.SubfileJComplete)
                .HasColumnName("subfile_j_complete")
                .HasDefaultValue(false);

            entity.Property(e => e.AllSubfilesVerified)
                .HasColumnName("all_subfiles_verified")
                .HasDefaultValue(false);

            entity.Property(e => e.ReviewStatusId)
                .HasColumnName("review_status_id");

            entity.Property(e => e.ReviewRequestedAt)
                .HasColumnName("review_requested_at")
                .HasColumnType("timestamp with time zone");

            entity.Property(e => e.ReviewRequestedById)
                .HasColumnName("review_requested_by_id");

            entity.Property(e => e.ReviewNotes)
                .HasColumnName("review_notes")
                .HasColumnType("text");

            entity.Property(e => e.ApprovedById)
                .HasColumnName("approved_by_id");

            entity.Property(e => e.ApprovedAt)
                .HasColumnName("approved_at")
                .HasColumnType("timestamp with time zone");

            entity.Property(e => e.VerifiedById)
                .HasColumnName("verified_by_id");

            entity.Property(e => e.VerifiedAt)
                .HasColumnName("verified_at")
                .HasColumnType("timestamp with time zone");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .IsRequired();

            entity.Property(e => e.OrganizationId)
                .HasColumnName("organization_id");

            entity.Property(e => e.StationId)
                .HasColumnName("station_id");

            // Index
            entity.HasIndex(e => e.CaseRegisterId)
                .HasDatabaseName("idx_case_closure_checklists_case")
                .IsUnique();

            // Relationships
            entity.HasOne(e => e.ClosureType)
                .WithMany(ct => ct.CaseClosureChecklists)
                .HasForeignKey(e => e.ClosureTypeId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.LegalSection)
                .WithMany()
                .HasForeignKey(e => e.LegalSectionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ReviewStatus)
                .WithMany(rs => rs.CaseClosureChecklists)
                .HasForeignKey(e => e.ReviewStatusId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
