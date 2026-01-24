# Sprint 12: Prosecution & Case Management Enhancement

**Sprint Duration:** 2 weeks
**Target Start:** February 5, 2026
**Priority:** P1 - High
**Status:** 🚧 PLANNED

---

## Sprint Goal

Enhance the prosecution and case management module to match KenloadV2's comprehensive subfile system (A-J) and implement court hearing tracking. This sprint addresses gaps identified in the [KenloadV2 vs TruLoad Comparison](../KENLOAD_VS_TRULOAD_COMPARISON.md) Section 4.

---

## Background

### Current State (Sprint 10 Complete)
- ✅ CaseRegister with auto-creation from weighing
- ✅ SpecialRelease workflow with certificates
- ✅ Basic case status tracking (Open → Closed)
- ✅ PDF generation for Load Correction Memo, Compliance Certificate
- ❌ **GAP:** Limited subfile support (only basic structure)
- ❌ **GAP:** No court hearing tracking
- ❌ **GAP:** No warrant management workflow
- ❌ **GAP:** No prosecutor assignment workflow

### KenloadV2 Case Subfile System (Reference)

KenloadV2 implements comprehensive case documentation through 10 subfiles:

| Subfile | Description | Purpose |
|---------|-------------|---------|
| A | Case Register | Initial case details, violation info |
| B | Document Evidence | Weight tickets, photos, ANPR, permits |
| C | Expert Reports | Engineering/forensic reports |
| D | Witness Statements | Inspector, driver, witness statements |
| E | Accused Statements | Defendant statement, reweigh docs |
| F | Investigation Diary | Investigation timeline and findings |
| G | Court Documents | Charge sheets, bonds, NTAC, warrants |
| H | Accused Records | Prior offenses, ID documents |
| I | Covering Report | Prosecutorial summary |
| J | Minute Sheets | Court minutes, adjournments |

---

## Deliverables

### 1. Enhanced Case Subfile Models

The ERD already defines `case_subfiles` table. This sprint implements the full subfile workflow.

**Subfile Type Enum:**
```csharp
public enum CaseSubfileType
{
    A_CaseDetails,           // Initial case (= case_registers)
    B_DocumentEvidence,      // Evidence files
    C_ExpertReports,         // Technical reports
    D_WitnessStatements,     // Witness testimony
    E_AccusedStatements,     // Defendant statements
    F_InvestigationDiary,    // Investigation diary
    G_CourtDocuments,        // Court documents
    H_AccusedRecords,        // Prior offenses
    I_CoveringReport,        // Prosecutorial summary
    J_MinuteSheets           // Court minutes
}
```

**Enhanced CaseSubfile Model:**
```csharp
public class CaseSubfile : BaseEntity
{
    public Guid CaseRegisterId { get; set; }
    public CaseRegister CaseRegister { get; set; }

    public CaseSubfileType SubfileType { get; set; }
    public string SubfileCode { get; set; }  // "A", "B", etc.
    public string Title { get; set; }
    public string? Description { get; set; }

    // File attachment
    public string? FileUrl { get; set; }
    public string? MimeType { get; set; }
    public long? FileSizeBytes { get; set; }

    // Metadata
    public DateTime DocumentDate { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; }

    // Verification
    public bool IsVerified { get; set; }
    public Guid? VerifiedByUserId { get; set; }
    public DateTime? VerifiedAt { get; set; }
}
```

### 2. Evidence Management (Subfile B)

**Model:** `CaseEvidence`
```csharp
public class CaseEvidence : BaseEntity
{
    public Guid CaseRegisterId { get; set; }
    public Guid? CaseSubfileId { get; set; }  // Links to subfile B

    public string EvidenceType { get; set; }  // "PHOTO", "VIDEO", "DOCUMENT", "ANPR_IMAGE"
    public string Description { get; set; }
    public string FileUrl { get; set; }
    public string MimeType { get; set; }
    public long FileSizeBytes { get; set; }

    // Chain of custody
    public string? ChainOfCustody { get; set; }
    public Guid CollectedByUserId { get; set; }
    public DateTime CollectedAt { get; set; }
    public string? CollectionLocation { get; set; }

    // Verification
    public bool IsAuthenticated { get; set; }
    public string? AuthenticationNotes { get; set; }
}
```

**API Endpoints:**
| Method | Route | Permission | Description |
|--------|-------|------------|-------------|
| GET | `/api/v1/cases/{id}/evidence` | case.read | List case evidence |
| POST | `/api/v1/cases/{id}/evidence` | case.subfile_manage | Upload evidence |
| DELETE | `/api/v1/cases/{id}/evidence/{evidenceId}` | case.subfile_manage | Remove evidence |

### 3. Witness Statements (Subfile D)

**Model:** `WitnessStatement`
```csharp
public class WitnessStatement : BaseEntity
{
    public Guid CaseRegisterId { get; set; }
    public Guid? CaseSubfileId { get; set; }  // Links to subfile D

    public string WitnessName { get; set; }
    public string WitnessType { get; set; }  // "OFFICER", "DRIVER", "OWNER", "BYSTANDER"
    public string? WitnessIdNumber { get; set; }
    public string? WitnessPhone { get; set; }
    public string? WitnessAddress { get; set; }

    public string StatementText { get; set; }
    public DateTime StatementDate { get; set; }
    public string? StatementLocation { get; set; }

    // Attachments
    public string? SignatureUrl { get; set; }
    public string? StatementFileUrl { get; set; }

    // Recording officer
    public Guid RecordedByUserId { get; set; }
    public User RecordedByUser { get; set; }
}
```

### 4. Court Hearing Enhancement

The ERD already defines `court_hearings` table. This sprint enhances the workflow.

**Enhanced CourtHearing Model:**
```csharp
public class CourtHearing : BaseEntity
{
    public Guid CaseRegisterId { get; set; }
    public CaseRegister CaseRegister { get; set; }

    public Guid CourtId { get; set; }
    public Court Court { get; set; }

    public DateTime HearingDate { get; set; }
    public TimeOnly? HearingTime { get; set; }
    public string HearingType { get; set; }  // "MENTION", "TRIAL", "RULING", "JUDGMENT"

    public string? JudgeName { get; set; }
    public string? ProsecutorName { get; set; }
    public string? DefenseCounsel { get; set; }

    public string Status { get; set; }  // "SCHEDULED", "IN_PROGRESS", "ADJOURNED", "COMPLETED"
    public string? Outcome { get; set; }
    public string? OutcomeDetails { get; set; }

    // Next hearing
    public DateTime? NextHearingDate { get; set; }
    public string? AdjournmentReason { get; set; }

    // Minute notes
    public string? MinuteNotes { get; set; }
    public string? MinuteNotesFileUrl { get; set; }

    // Vector embedding for semantic search
    public Vector? MinuteNotesEmbedding { get; set; }
}
```

**Court Hearing Status Flow:**
```
SCHEDULED → IN_PROGRESS → COMPLETED
                ↓
           ADJOURNED → SCHEDULED (new hearing)
```

**API Endpoints:**
| Method | Route | Permission | Description |
|--------|-------|------------|-------------|
| GET | `/api/v1/cases/{id}/hearings` | case.read | List case hearings |
| POST | `/api/v1/cases/{id}/hearings` | case.court_hearing | Schedule hearing |
| PUT | `/api/v1/cases/{id}/hearings/{hearingId}` | case.court_hearing | Update hearing |
| POST | `/api/v1/cases/{id}/hearings/{hearingId}/adjourn` | case.court_hearing | Adjourn with reason |
| POST | `/api/v1/cases/{id}/hearings/{hearingId}/complete` | case.court_hearing | Mark completed |

### 5. Warrant Management

**Model:** `ArrestWarrant` (Enhanced)
```csharp
public class ArrestWarrant : BaseEntity
{
    public Guid CaseRegisterId { get; set; }
    public CaseRegister CaseRegister { get; set; }

    public string WarrantNo { get; set; }
    public string WarrantType { get; set; }  // "ARREST", "BENCH", "SEARCH"

    // Subject
    public string SubjectName { get; set; }
    public string? SubjectIdNumber { get; set; }
    public string? SubjectAddress { get; set; }
    public string? SubjectPhone { get; set; }

    // Issuing authority
    public Guid CourtId { get; set; }
    public Court Court { get; set; }
    public string? IssuingJudge { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    // Status
    public string Status { get; set; }  // "ACTIVE", "EXECUTED", "EXPIRED", "CANCELLED"
    public DateTime? ExecutedAt { get; set; }
    public Guid? ExecutedByUserId { get; set; }
    public string? ExecutionNotes { get; set; }

    // Document
    public string? WarrantFileUrl { get; set; }
}
```

**Warrant Status Flow:**
```
ACTIVE → EXECUTED (arrest made)
   ↓
EXPIRED (time limit passed)
   ↓
CANCELLED (court order)
```

### 6. Prosecutor Assignment Workflow

**Model:** `CaseProsecutor`
```csharp
public class CaseProsecutor : BaseEntity
{
    public Guid CaseRegisterId { get; set; }
    public CaseRegister CaseRegister { get; set; }

    public Guid ProsecutorUserId { get; set; }
    public User ProsecutorUser { get; set; }

    public string Role { get; set; }  // "LEAD", "ASSISTANT", "CONSULTANT"
    public DateTime AssignedAt { get; set; }
    public Guid AssignedByUserId { get; set; }

    public bool IsActive { get; set; }
    public DateTime? RemovedAt { get; set; }
    public string? RemovalReason { get; set; }
}
```

**API Endpoints:**
| Method | Route | Permission | Description |
|--------|-------|------------|-------------|
| GET | `/api/v1/cases/{id}/prosecutors` | case.read | List assigned prosecutors |
| POST | `/api/v1/cases/{id}/prosecutors` | case.assign | Assign prosecutor |
| DELETE | `/api/v1/cases/{id}/prosecutors/{userId}` | case.assign | Remove prosecutor |

### 7. Case Closure Checklist Enhancement

**CaseClosureChecklist Fields:**
```csharp
public class CaseClosureChecklist : BaseEntity
{
    public Guid CaseRegisterId { get; set; }

    // Subfile completeness
    public bool HasSubfileA { get; set; }  // Case details
    public bool HasSubfileB { get; set; }  // Evidence
    public bool HasSubfileC { get; set; }  // Expert reports (optional)
    public bool HasSubfileD { get; set; }  // Witness statements
    public bool HasSubfileE { get; set; }  // Accused statement
    public bool HasSubfileF { get; set; }  // Investigation diary
    public bool HasSubfileG { get; set; }  // Court documents
    public bool HasSubfileH { get; set; }  // Accused records
    public bool HasSubfileI { get; set; }  // Covering report
    public bool HasSubfileJ { get; set; }  // Minute sheets

    // Closure requirements by disposition
    public string DispositionType { get; set; }
    public bool RequiredSubfilesComplete { get; set; }

    // Approval
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public bool IsApproved { get; set; }
}
```

**Closure Requirements by Disposition:**
| Disposition | Required Subfiles |
|-------------|-------------------|
| Withdrawn | A, B, I |
| Discharged | A, B, D, I, J |
| Charged & Paid | A, B, G, I |
| Charged & Jailed | A, B, D, E, F, G, H, I, J |
| Special Release | A, B, I |

---

## Database Migration

**Migration:** `20260205_EnhanceProsecutionModule.cs`

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // 1. Create case_evidence table
    migrationBuilder.CreateTable(
        name: "case_evidence",
        columns: table => new
        {
            id = table.Column<Guid>(),
            case_register_id = table.Column<Guid>(),
            case_subfile_id = table.Column<Guid>(nullable: true),
            evidence_type = table.Column<string>(maxLength: 50),
            description = table.Column<string>(maxLength: 500),
            file_url = table.Column<string>(maxLength: 2000),
            mime_type = table.Column<string>(maxLength: 100),
            file_size_bytes = table.Column<long>(),
            chain_of_custody = table.Column<string>(nullable: true),
            collected_by_user_id = table.Column<Guid>(),
            collected_at = table.Column<DateTime>(),
            collection_location = table.Column<string>(nullable: true),
            is_authenticated = table.Column<bool>(defaultValue: false),
            authentication_notes = table.Column<string>(nullable: true),
            is_active = table.Column<bool>(defaultValue: true),
            created_at = table.Column<DateTime>(),
            updated_at = table.Column<DateTime>(),
            deleted_at = table.Column<DateTime>(nullable: true)
        });

    // 2. Create witness_statements table
    migrationBuilder.CreateTable(
        name: "witness_statements",
        columns: table => new
        {
            id = table.Column<Guid>(),
            case_register_id = table.Column<Guid>(),
            case_subfile_id = table.Column<Guid>(nullable: true),
            witness_name = table.Column<string>(maxLength: 255),
            witness_type = table.Column<string>(maxLength: 50),
            witness_id_number = table.Column<string>(nullable: true),
            witness_phone = table.Column<string>(nullable: true),
            witness_address = table.Column<string>(nullable: true),
            statement_text = table.Column<string>(),
            statement_date = table.Column<DateTime>(),
            statement_location = table.Column<string>(nullable: true),
            signature_url = table.Column<string>(nullable: true),
            statement_file_url = table.Column<string>(nullable: true),
            recorded_by_user_id = table.Column<Guid>(),
            is_active = table.Column<bool>(defaultValue: true),
            created_at = table.Column<DateTime>(),
            updated_at = table.Column<DateTime>(),
            deleted_at = table.Column<DateTime>(nullable: true)
        });

    // 3. Create case_prosecutors table
    migrationBuilder.CreateTable(
        name: "case_prosecutors",
        columns: table => new
        {
            id = table.Column<Guid>(),
            case_register_id = table.Column<Guid>(),
            prosecutor_user_id = table.Column<Guid>(),
            role = table.Column<string>(maxLength: 50),
            assigned_at = table.Column<DateTime>(),
            assigned_by_user_id = table.Column<Guid>(),
            is_active = table.Column<bool>(defaultValue: true),
            removed_at = table.Column<DateTime>(nullable: true),
            removal_reason = table.Column<string>(nullable: true),
            created_at = table.Column<DateTime>(),
            updated_at = table.Column<DateTime>(),
            deleted_at = table.Column<DateTime>(nullable: true)
        });

    // 4. Enhance court_hearings table
    migrationBuilder.AddColumn<string>(
        name: "prosecutor_name",
        table: "court_hearings",
        maxLength: 255,
        nullable: true);

    migrationBuilder.AddColumn<string>(
        name: "defense_counsel",
        table: "court_hearings",
        maxLength: 255,
        nullable: true);

    migrationBuilder.AddColumn<string>(
        name: "adjournment_reason",
        table: "court_hearings",
        maxLength: 500,
        nullable: true);

    // 5. Enhance arrest_warrants table
    migrationBuilder.AddColumn<DateTime>(
        name: "expires_at",
        table: "arrest_warrants",
        nullable: true);

    migrationBuilder.AddColumn<Guid>(
        name: "executed_by_user_id",
        table: "arrest_warrants",
        nullable: true);

    // 6. Enhance case_closure_checklists table
    migrationBuilder.AddColumn<bool>(
        name: "has_subfile_c",
        table: "case_closure_checklists",
        defaultValue: false);

    migrationBuilder.AddColumn<bool>(
        name: "has_subfile_f",
        table: "case_closure_checklists",
        defaultValue: false);

    // 7. Create indexes
    migrationBuilder.CreateIndex(
        name: "idx_case_evidence_case",
        table: "case_evidence",
        column: "case_register_id");

    migrationBuilder.CreateIndex(
        name: "idx_witness_statements_case",
        table: "witness_statements",
        column: "case_register_id");

    migrationBuilder.CreateIndex(
        name: "idx_case_prosecutors_case",
        table: "case_prosecutors",
        column: "case_register_id");

    migrationBuilder.CreateIndex(
        name: "idx_case_prosecutors_user",
        table: "case_prosecutors",
        column: "prosecutor_user_id");
}
```

---

## API Endpoints Summary

### Case Subfiles
| Method | Route | Permission |
|--------|-------|------------|
| GET | `/api/v1/cases/{id}/subfiles` | case.read |
| GET | `/api/v1/cases/{id}/subfiles/{type}` | case.read |
| POST | `/api/v1/cases/{id}/subfiles` | case.subfile_manage |
| PUT | `/api/v1/cases/{id}/subfiles/{subfileId}` | case.subfile_manage |
| DELETE | `/api/v1/cases/{id}/subfiles/{subfileId}` | case.subfile_manage |

### Evidence
| Method | Route | Permission |
|--------|-------|------------|
| GET | `/api/v1/cases/{id}/evidence` | case.read |
| POST | `/api/v1/cases/{id}/evidence` | case.subfile_manage |
| PUT | `/api/v1/cases/{id}/evidence/{evidenceId}` | case.subfile_manage |
| DELETE | `/api/v1/cases/{id}/evidence/{evidenceId}` | case.subfile_manage |

### Witness Statements
| Method | Route | Permission |
|--------|-------|------------|
| GET | `/api/v1/cases/{id}/witnesses` | case.read |
| POST | `/api/v1/cases/{id}/witnesses` | case.subfile_manage |
| PUT | `/api/v1/cases/{id}/witnesses/{witnessId}` | case.subfile_manage |

### Court Hearings
| Method | Route | Permission |
|--------|-------|------------|
| GET | `/api/v1/cases/{id}/hearings` | case.read |
| POST | `/api/v1/cases/{id}/hearings` | case.court_hearing |
| PUT | `/api/v1/cases/{id}/hearings/{hearingId}` | case.court_hearing |
| POST | `/api/v1/cases/{id}/hearings/{hearingId}/adjourn` | case.court_hearing |
| POST | `/api/v1/cases/{id}/hearings/{hearingId}/complete` | case.court_hearing |

### Warrants
| Method | Route | Permission |
|--------|-------|------------|
| GET | `/api/v1/cases/{id}/warrants` | case.read |
| POST | `/api/v1/cases/{id}/warrants` | case.arrest_warrant |
| PUT | `/api/v1/cases/{id}/warrants/{warrantId}` | case.arrest_warrant |
| POST | `/api/v1/cases/{id}/warrants/{warrantId}/execute` | case.arrest_warrant |

### Prosecutors
| Method | Route | Permission |
|--------|-------|------------|
| GET | `/api/v1/cases/{id}/prosecutors` | case.read |
| POST | `/api/v1/cases/{id}/prosecutors` | case.assign |
| DELETE | `/api/v1/cases/{id}/prosecutors/{userId}` | case.assign |

---

## Testing Requirements

### Unit Tests (30 tests)
- [ ] CaseSubfileService tests (8)
- [ ] CaseEvidenceService tests (6)
- [ ] WitnessStatementService tests (4)
- [ ] CourtHearingService tests (6)
- [ ] ArrestWarrantService tests (4)
- [ ] CaseClosureService tests (2)

### Integration Tests (15 tests)
- [ ] Full case lifecycle (create → evidence → hearing → closure)
- [ ] Subfile completeness validation
- [ ] Warrant execution workflow
- [ ] Prosecutor assignment workflow

---

## Acceptance Criteria

1. ✅ All 10 case subfile types can be created and managed
2. ✅ Evidence files uploaded with chain of custody tracking
3. ✅ Witness statements recorded with signature support
4. ✅ Court hearings scheduled with adjournment workflow
5. ✅ Warrants issued, executed, and tracked
6. ✅ Prosecutors assigned to cases with role distinction
7. ✅ Case closure checklist validates required subfiles by disposition
8. ✅ All APIs protected with appropriate permissions
9. ✅ Existing tests continue to pass
10. ✅ New tests achieve 85%+ code coverage

---

## Dependencies

- **Sprint 10:** Case Register (completed) - provides base case infrastructure
- **Sprint 11:** Axle Grouping (in progress) - provides accurate violation data

---

**Document Version:** 1.0
**Last Updated:** January 22, 2026
**Author:** System Audit Team
