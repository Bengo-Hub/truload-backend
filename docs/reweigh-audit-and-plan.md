# Reweigh Scenarios: Audit & Implementation Plan

**Date**: 2026-04-17
**Scope**: Enforcement reweigh (existing), commercial reweigh (new)

---

## Part 1: Current Implementation Audit

### 1.1 Enforcement Reweigh -- What Exists

The enforcement reweigh workflow is fully implemented and tested end-to-end. Below is a complete map of every file, method, and line involved.

#### Backend: Core Reweigh Logic

| File | Lines | What It Does |
|------|-------|--------------|
| `Services/Implementations/Weighing/WeighingService.cs` | 244-303 | `InitiateReweighAsync()` -- creates a new WeighingTransaction linked to the original via `OriginalWeighingId`, increments `ReweighCycleNo`, enforces max cycles, updates LoadCorrectionMemo with relief truck info |
| `Services/Implementations/Weighing/WeighingService.cs` | 854-964 | Auto-close cascade on compliant reweigh: closes case (COMPLIANCE_ACHIEVED disposition), releases vehicle from yard, marks LoadCorrectionMemo compliance, generates ComplianceCertificate |
| `Services/Interfaces/Weighing/IWeighingService.cs` | 48-49 | Interface: `InitiateReweighAsync(Guid originalTransactionId, string? ticketNumber, Guid userId, string? reliefTruckRegNumber, int? reliefTruckEmptyWeightKg)` |
| `Controllers/WeighingOperations/WeighingController.cs` | 655-703 | `POST /api/v1/weighing-transactions/reweigh` endpoint with `[Authorize(Policy = "Permission:weighing.create")]` |

#### Backend: Data Model

| File | Lines | Field |
|------|-------|-------|
| `Models/Weighing/WeighingTransaction.cs` | 142 | `ReweighCycleNo` (int, default 0) -- 0 = original, 1+ = reweigh |
| `Models/Weighing/WeighingTransaction.cs` | 147 | `OriginalWeighingId` (Guid?) -- FK to root transaction |
| `Models/Weighing/WeighingTransaction.cs` | 243 | `ReweighLimit` (int, default 8) -- max allowed cycles |

#### Backend: LoadCorrectionMemo (Reweigh Tracking)

| File | Lines | Field |
|------|-------|-------|
| `Models/CaseManagement/LoadCorrectionMemo.cs` | 41 | `ReweighScheduledAt` -- when reweigh was scheduled |
| `Models/CaseManagement/LoadCorrectionMemo.cs` | 46 | `ReweighWeighingId` -- FK to reweigh transaction |
| `Models/CaseManagement/LoadCorrectionMemo.cs` | 51 | `ComplianceAchieved` -- set true when reweigh passes |
| `Models/CaseManagement/LoadCorrectionMemo.cs` | 56-61 | `ReliefTruckRegNumber`, `ReliefTruckEmptyWeightKg` -- relief truck details for offload method |

#### Backend: ComplianceCertificate (Post-Reweigh)

| File | Lines | What |
|------|-------|------|
| `Models/CaseManagement/ComplianceCertificate.cs` | 1-48 | Certificate entity: `CertificateNo`, `CaseRegisterId`, `WeighingId`, `LoadCorrectionMemoId`, `IssuedById`, `IssuedAt` |
| `Services/Implementations/CaseManagement/ComplianceCertificateService.cs` | 1-78 | Read-only service: GetById, GetByCaseId, GetByWeighingId |
| `Services/Implementations/Infrastructure/PdfDocuments/ComplianceCertificateDocument.cs` | 1-50+ | QuestPDF document: official clearance after reweigh, issued under Kenya Traffic Act / EAC Vehicle Load Control Act |

#### Backend: Configuration

| File | Lines | Setting |
|------|-------|---------|
| `Data/Seeders/SystemConfiguration/SystemConfigurationSeeder.cs` | 563-569 | `WeighingMaxReweighCycles` seeded as `"8"` (Integer, Category: Weighing) |

#### Backend: Reports

| File | Lines | What |
|------|-------|------|
| `Services/Implementations/Reporting/Modules/WeighingReportGenerator.cs` | 73-74 | `reweigh-statement` report definition |
| `Services/Implementations/Reporting/Modules/WeighingReportGenerator.cs` | 784-882 | `GenerateReweighStatementAsync()` -- PDF/CSV/XLSX report tracking reweigh cycles, weight reduction, success rate |

#### Backend: E2E Tests

| File | What |
|------|------|
| `Tests/e2e/compliancee2e/compliance_e2e_scenario_1.py` | Full lifecycle: overload -> case -> prosecution -> invoice -> payment -> memo -> reweigh -> compliance cert -> auto-close. Steps 15-18 cover reweigh initiation, compliant weight capture, case closure verification, and certificate verification. |
| `Tests/e2e/compliancee2e/compliance_e2e_scenario_6.py` | Reweigh-related scenario |
| `Tests/e2e/compliancee2e/compliance_e2e_scenario_7.py` | Reweigh-related scenario |

#### Frontend: Reweigh UI

| File | What |
|------|------|
| `src/hooks/useWeighing.ts` (lines 467-520) | `initiateReweighTransaction()` -- calls API, creates new session with incremented cycle, preserves vehicle details |
| `src/hooks/useWeighing.ts` (lines 474-477) | Client-side max check: `reweighCycle >= 8` |
| `src/lib/api/weighing.ts` (lines 314-317, 480-486) | `InitiateReweighRequest` type and `initiateReweigh()` API call to `POST /weighing-transactions/reweigh` |
| `src/components/weighing/DecisionPanel.tsx` (lines 28-50, 299-307) | "Re-weigh" button shown when `canReweigh && reweighCycleNo < 8`, displays cycle counter |
| `src/components/weighing/steps/WeighingDecisionStep.tsx` (lines 14-71) | Props include `canReweigh`, `onReweigh`, `reweighCycleNo` |
| `src/types/weighing.ts` (line 111) | `reweighCycleNo: number` on WeighingTransaction type |
| `src/app/[orgSlug]/weighing/special-release/page.tsx` (lines 85, 132, 149) | Special release form includes `requiresReweigh` flag per release type |

---

### 1.2 How the Enforcement Reweigh Flow Works (Step by Step)

1. **Initial overload detected**: `CalculateComplianceAsync()` determines vehicle is overloaded -> auto-creates CaseRegister, YardEntry, ProhibitionOrder.

2. **Prosecution and payment**: Officer escalates to prosecution -> charge sheet generated -> invoice created -> driver pays fine via eCitizen.

3. **Load Correction Memo**: Auto-created after payment receipt. Records overload amount, redistribution type.

4. **Reweigh initiation**: Officer calls `POST /weighing-transactions/reweigh` with:
   - `originalWeighingId` (required)
   - `reweighTicketNumber` (optional -- auto-generated via DocumentNumberService if omitted)
   - `reliefTruckRegNumber` (optional -- for offload method)
   - `reliefTruckEmptyWeightKg` (optional)

5. **New transaction created**: `InitiateReweighAsync()`:
   - Checks `ReweighCycleNo < maxReweighCycles` (default 8)
   - Creates new `WeighingTransaction` with `OriginalWeighingId` pointing to root transaction and `ReweighCycleNo = original + 1`
   - Copies station, vehicle, driver, transporter from original
   - Updates `LoadCorrectionMemo.ReweighWeighingId` and relief truck info

6. **Weight capture**: Standard `CaptureWeightsAsync()` -> `CalculateComplianceAsync()` runs on the reweigh transaction.

7. **Compliant reweigh cascade** (lines 854-964 of WeighingService):
   - If `transaction.IsCompliant && transaction.OriginalWeighingId.HasValue`:
     - Closes case with `COMPLIANCE_ACHIEVED` disposition and rich closing narration (includes payment/receipt info)
     - Releases vehicle from yard
     - Marks `LoadCorrectionMemo.ComplianceAchieved = true`
     - Generates `ComplianceCertificate` with number `COMP-{year}-{sequence}`

8. **Still overloaded**: If reweigh result is still non-compliant, standard overload flow triggers again (new case/yard NOT created since original exists). Driver must offload more and attempt another reweigh (up to max cycles).

### 1.3 What the Original Transaction Gets

The original transaction itself is **not modified** during reweigh. The linkage is one-directional:
- Reweigh transaction -> `OriginalWeighingId` -> original transaction
- `LoadCorrectionMemo.ReweighWeighingId` -> latest reweigh transaction
- Case closure references the reweigh ticket number in the closing narration

### 1.4 Special Release Interaction

Special releases interact with reweigh through the `SpecialRelease` model:
- `RequiresReweigh` (bool) -- set by the release type configuration
- `RequiresRedistribution` (bool) -- whether load redistribution is needed
- Within-tolerance overloads auto-create special release with `ReweighRequired = false`
- Overloaded vehicles requiring offload get special releases with `ReweighRequired = true`

### 1.5 Commercial Mode -- Current State

There is **zero** reweigh logic for commercial weighing mode. The `CalculateComplianceAsync()` method early-returns for commercial tenants at line 661 (`isCommercialMode` check), and the compliant-reweigh cascade (lines 854-964) only fires for enforcement mode. The `InitiateReweighAsync()` method itself has no commercial/enforcement branching -- it would technically work for commercial tenants but was designed exclusively for enforcement.

---

## Part 2: Enforcement Reweigh -- Gaps & Regulatory Requirements

### 2.1 What Regulations Require

Based on research into Kenya Traffic Act (Cap 403), EAC Vehicle Load Control Act 2013/2016, and KeNHA Axle Load Control procedures:

| Requirement | Source | Status |
|-------------|--------|--------|
| Vehicle must offload excess cargo before reweigh | Kenya Traffic Act / KeNHA | Implemented (LoadCorrectionMemo tracks redistribution) |
| Relief truck must be documented for offload | KeNHA procedure | Implemented (relief truck reg + empty weight on memo) |
| Maximum reweigh cycles configurable | KeNHA / FRD | Implemented (default 8, configurable via `WeighingMaxReweighCycles`) |
| Compliance certificate on passing reweigh | Kenya Traffic Act | Implemented (auto-generated `COMP-{year}-{seq}`) |
| 5% axle tolerance for redistribution | KeNHA policy | Partially implemented (tolerance system exists but redistribution-specific tolerance not distinguished) |
| Fees/penalties recalculated on reweigh | EAC Act | **NOT IMPLEMENTED** -- fees from original transaction are used; no recalculation on failed reweigh |
| Weighbridge report form as compliance permit | KeNHA regulations | Implemented (compliance certificate PDF) |
| Reweigh if vehicle suspected of reloading post-control | KeNHA regulations | **NOT IMPLEMENTED** -- no "spot check reweigh" capability |
| Full audit trail per reweigh cycle | FRD | Implemented (separate transactions linked via OriginalWeighingId) |
| Time limit for offloading/reweigh | Various | **NOT IMPLEMENTED** -- no deadline enforcement |

### 2.2 Identified Gaps in Enforcement Reweigh

#### GAP 1: No Fee Recalculation on Failed Reweigh

When a vehicle is reweighed and is **still overloaded**, the current system triggers the standard overload flow but does not recalculate fees based on the new (reduced) overload amount. In some jurisdictions, the fee should be recalculated based on the new excess weight.

**Recommendation**: Add a `RecalculateFeesOnReweigh` setting. When enabled, if a reweigh shows reduced but still non-compliant weight, recalculate fees based on the new overload delta.

#### GAP 2: No Time Deadline for Offloading/Reweigh

There is no enforcement of time limits between the original overload detection and the reweigh. Some regulations require offloading within a specified period (e.g., 24 hours).

**Recommendation**: Add `ReweighDeadlineHours` to `LoadCorrectionMemo`. The frontend should display a countdown, and the system should flag overdue memos in reports.

#### GAP 3: No Spot-Check Reweigh

KeNHA regulations allow reweighing a vehicle if there is "reason to believe that the vehicle subsequent to control has been reloaded or tampered with." The current system only supports reweigh as part of the overload correction flow.

**Recommendation**: Add a `ReweighType` enum (`correction` | `spot_check`) to the reweigh initiation. Spot-check reweighs would not require a LoadCorrectionMemo or prior overload.

#### GAP 4: No Axle-Level Redistribution Tracking

The LoadCorrectionMemo records the total overload in kg but does not track which specific axle groups need redistribution. For complex multi-axle vehicles, the officer should be able to specify which axles are overloaded and what the target redistribution should be.

**Recommendation**: Add a `LoadCorrectionDetail` child table under LoadCorrectionMemo with per-axle-group targets.

#### GAP 5: No Reweigh Notification to Transporter

No SMS/email notification is sent to the transporter when a reweigh is initiated or when compliance is achieved.

**Recommendation**: Wire `INotificationService.SendExternalNotificationAsync()` into `InitiateReweighAsync()` and the compliance cascade.

#### GAP 6: No Photo/Evidence Attachment on Reweigh

The reweigh transaction does not capture photographic evidence of the offloaded cargo or the redistributed load. This is important for audit trail completeness.

**Recommendation**: Allow media attachments on the reweigh transaction (the infrastructure exists for weighing transactions but is not prompted during reweigh).

#### GAP 7: ReweighLimit is on WeighingTransaction but Not Used

`WeighingTransaction.ReweighLimit` (line 243) is stored per transaction (default 8) but never read by the enforcement logic. The actual limit comes from `SettingKeys.WeighingMaxReweighCycles` application setting. The model field is only used in the reweigh-statement report display.

**Recommendation**: Either populate `ReweighLimit` from the setting when creating the transaction (for historical record), or remove the field from the model and rely solely on the setting.

---

## Part 3: Commercial Reweigh -- What Needs to Be Built

Commercial weighing has fundamentally different reweigh scenarios than enforcement. There is no overload/prosecution workflow. Instead, reweighs are triggered by business disputes, quality concerns, equipment issues, or process requirements.

### 3.1 Commercial Reweigh Scenarios

#### Scenario A: Disputed Weight Reading

**When**: Customer/transporter disagrees with the measured weight.
**Trigger**: Manual -- operator or customer initiates.
**Process**:
1. Original transaction flagged as "disputed"
2. Vehicle repositioned on scale (or moved to a different certified scale)
3. New weight captured as a reweigh
4. If weights differ beyond tolerance, a supervisor review is triggered
5. Final weight determined (original, reweigh, or average depending on policy)
6. Dispute resolution recorded with justification

**Data needed**:
- `DisputeReason` (text) on the reweigh transaction
- `DisputeResolution` enum: `original_upheld`, `reweigh_accepted`, `average_used`, `third_party`
- `DisputedById` -- who initiated the dispute
- `ResolvedById`, `ResolvedAt` -- who resolved it

#### Scenario B: Tare Weight Re-verification

**When**: Periodic re-verification of stored/preset tare weights, or when tare is suspected to be stale.
**Trigger**: Automatic (tare expiry) or manual (operator suspicion).
**Process**:
1. Empty vehicle placed on scale
2. New tare weight captured
3. Compared against stored tare weight
4. If difference exceeds tolerance, all recent transactions using that stored tare should be flagged
5. Vehicle's tare record updated in `vehicle_tare_history`
6. Optionally: recalculate net weights on recent transactions affected by stale tare

**Data needed**:
- `TareDiscrepancyKg` on the verification record
- `AffectedTransactionIds` -- list of transactions that used the old tare
- Link to `vehicle_tare_history` table (already planned in commercial schema)

#### Scenario C: Equipment Malfunction / Scale Error

**When**: Scale test fails, equipment error detected, or calibration issue identified.
**Trigger**: System (failed scale test) or manual (operator observation).
**Process**:
1. All transactions since last passing scale test are flagged
2. Affected vehicles recalled for reweigh (notification sent)
3. Reweigh transactions linked to original with reason `equipment_error`
4. Corrected weights issued on new tickets
5. Audit record of which scale test was associated and why it failed

**Data needed**:
- `ReweighReason` enum: `equipment_error`, `calibration_failure`, `operator_error`
- `AffectedScaleTestId` -- FK to the failed scale test
- Bulk reweigh initiation capability (batch create reweigh requests)

#### Scenario D: Quality Re-inspection Requiring Reweigh

**When**: Quality inspection reveals issues (moisture content changed, foreign matter found, cargo contamination) that affect the weight calculation.
**Trigger**: Quality inspector flags the shipment.
**Process**:
1. Original net weight is disputed based on quality findings
2. Vehicle may need to be reweighed after quality adjustments (drying, cleaning)
3. New `quality_deduction_kg` calculated and applied
4. Adjusted net weight recalculated: `net_weight_kg - quality_deduction_kg`
5. Updated weight ticket issued

**Data needed**:
- `QualityReinspectionId` -- link to quality inspection record
- Updated `quality_deduction_kg` and `adjusted_net_weight_kg` on the reweigh transaction
- `industry_metadata` JSONB for moisture%, foreign matter%, grade changes

#### Scenario E: Origin-Destination Weight Discrepancy

**When**: Weight measured at origin weighbridge differs significantly from destination weighbridge.
**Trigger**: Automatic (destination weight capture detects variance) or manual.
**Process**:
1. Destination weighing flags that net weight differs from origin ticket by more than tolerance
2. Investigation triggered: was cargo pilfered, was there spillage, or is it a calibration difference?
3. If needed, vehicle reweighed at destination on a different (or the same) scale
4. Discrepancy resolution recorded with root cause
5. Commercial tolerance applied (typically +/- 1-2% between different weighbridges)

**Data needed**:
- `OriginWeighingId`, `OriginNetWeightKg` on destination transaction
- `WeightDiscrepancyKg` (already planned in schema)
- `DiscrepancyReason` enum: `calibration_difference`, `spillage`, `pilferage`, `moisture_loss`, `other`
- Commercial tolerance setting per cargo type (already planned as `commercial_tolerance_settings`)

#### Scenario F: Regulatory / Compliance Reweigh

**When**: Regulatory body requests a reweigh (e.g., SOLAS VGM verification for container shipping).
**Trigger**: External -- regulatory request.
**Process**:
1. Container/vehicle identified for verification
2. Weighed on certified scale
3. Weight compared against declared VGM
4. Certificate of weight issued
5. Compliance/non-compliance recorded

**Data needed**:
- `RegulatoryReferenceNo` -- the regulatory request number
- `DeclaredWeightKg` -- what was declared (e.g., SOLAS VGM)
- `CertificateOfWeight` -- output document

### 3.2 Commercial Reweigh Tolerance Standards

Based on international standards (OIML R76, NIST Handbook 44) and commercial practice:

| Context | Tolerance | Basis |
|---------|-----------|-------|
| Same scale, same day | +/- 0.1% of capacity or 20 kg (whichever is greater) | Equipment accuracy |
| Same scale, different day | +/- 0.5% of reading | Calibration drift |
| Different scales | +/- 1% each = +/- 2% total | Inter-scale variance |
| Dispute reweigh acceptance | +/- 0.5% of original reading | Industry standard |
| Tare verification | +/- 50 kg or 0.5% (whichever is greater) | Fuel/fluids/mud variation |

### 3.3 Commercial Reweigh Record Keeping Requirements

For commercial audit trail, each reweigh must record:
1. Original transaction ID and weight
2. Reweigh transaction ID, weight, and timestamp
3. Reason for reweigh (dispute, quality, equipment, regulatory, tare verification)
4. Who initiated and who resolved
5. Weight difference and whether it falls within tolerance
6. Resolution outcome (which weight is accepted)
7. Both weight tickets (original and reweigh) must be retained
8. Supervisor sign-off required if discrepancy exceeds tolerance

---

## Part 4: Recommendations for the Plan

### 4.1 Enforcement Reweigh Enhancements (Add to Current Plan)

These can be added to an existing sprint as the changes are incremental:

1. **Populate `ReweighLimit` on transaction creation** from the `WeighingMaxReweighCycles` setting (1 line in `InitiateReweighAsync`)
2. **Add `ReweighDeadlineAt` to LoadCorrectionMemo** (nullable DateTime) and set it from a new `ReweighDeadlineHours` setting
3. **Add `ReweighType` field** to WeighingTransaction (`"correction"` | `"spot_check"`) -- allows spot-check reweighs without the full overload workflow
4. **Send SMS/email notification** on reweigh initiation and compliance achievement (wire existing `INotificationService`)
5. **Fee recalculation on failed reweigh** -- add `RecalculateFeesOnReweigh` setting, recalculate when reweigh is still non-compliant

### 4.2 Commercial Reweigh (Add to Plan as New Sprint)

This should be a dedicated sprint (suggest Sprint 5 or adding to Sprint 2 as section 2.6) in the `giggly-discovering-island.md` plan.

#### New Service: `ICommercialReweighService`

**File**: `Services/Interfaces/Weighing/ICommercialReweighService.cs`

Methods:
- `InitiateDisputeReweighAsync(Guid originalTxId, string reason, Guid initiatedById)` -- Scenario A
- `InitiateTareVerificationAsync(Guid vehicleId, Guid stationId, Guid userId)` -- Scenario B
- `InitiateEquipmentErrorReweighAsync(Guid scaleTestId, List<Guid> affectedTxIds)` -- Scenario C (batch)
- `InitiateQualityReweighAsync(Guid originalTxId, string qualityNotes, Guid userId)` -- Scenario D
- `ResolveDisputeAsync(Guid reweighTxId, string resolution, Guid resolvedById)` -- close dispute
- `GetReweighHistoryAsync(Guid originalTxId)` -- all reweighs linked to a transaction

#### New Fields on WeighingTransaction

```
commercial_reweigh_reason  varchar(50)?   -- "dispute", "tare_verification", "equipment_error", "quality", "regulatory"
dispute_reason             text?          -- free text reason
dispute_resolution         varchar(50)?   -- "original_upheld", "reweigh_accepted", "average_used"
dispute_resolved_by_id     uuid?          -- FK to user
dispute_resolved_at        timestamptz?
```

#### New Table: `weighing.commercial_reweigh_disputes`

For complex dispute tracking (optional, can be JSONB on transaction instead):
```
id                  uuid PK
original_weighing_id uuid FK -> weighing_transactions
reweigh_weighing_id  uuid FK -> weighing_transactions  
initiated_by_id      uuid FK -> users
reason              text
resolution          varchar(50)
resolved_by_id      uuid FK -> users
resolved_at         timestamptz
weight_difference_kg int
within_tolerance    boolean
supervisor_approved boolean
supervisor_id       uuid?
notes               text
created_at          timestamptz
```

#### Frontend Changes

Add to the commercial weighing decision step:
- "Request Reweigh" button (for dispute)
- "Verify Tare" action on the vehicle details panel
- Dispute resolution dialog (supervisor workflow)
- Reweigh history tab on transaction detail

#### Reports

Add to `CommercialReportGenerator.cs`:
- **Reweigh/Dispute Report** -- all commercial reweighs with reasons, resolutions, tolerance analysis
- **Tare Verification Report** -- tare weight changes over time per vehicle, flagging anomalies
- **Weight Discrepancy Report** (already planned) -- enhance with reweigh resolution data

### 4.3 Key Design Differences: Enforcement vs Commercial Reweigh

| Aspect | Enforcement | Commercial |
|--------|-------------|------------|
| Trigger | Overload detection + offloading | Business dispute, quality, equipment, regulatory |
| Prerequisite | Payment of fine + LoadCorrectionMemo | None (or supervisor approval for disputes) |
| Fee impact | Fees from original; no recalculation (gap) | No fines; may affect invoice for cargo weight |
| Max cycles | 8 (configurable) | No hard limit (but audited) |
| Outcome document | Compliance Certificate | Updated Weight Ticket |
| Case management | Integrated (case register, prosecution) | None |
| Yard management | Vehicle held until compliant | N/A |
| Who initiates | Enforcement officer | Operator, customer, or system |
| Tolerance context | Regulatory (5% axle, operational allowance) | Commercial (0.1-2% depending on context) |
| Audit requirement | Legal (court evidence) | Business (financial reconciliation) |

### 4.4 Specific Changes to `giggly-discovering-island.md`

The following items should be added to the commercial weighing plan at `C:\Users\bob\.claude\plans\giggly-discovering-island.md`:

1. **Sprint 2, Section 2.6**: Add "Commercial Reweigh Service" with the `ICommercialReweighService` interface and implementation described in 4.2 above.

2. **Sprint 1, Section 1.2**: Add the `commercial_reweigh_reason`, `dispute_reason`, `dispute_resolution`, `dispute_resolved_by_id`, `dispute_resolved_at` columns to the weighing_transactions schema changes.

3. **Sprint 2, Section 2.3**: Add commercial reweigh endpoints to the `CommercialWeighingController`:
   - `POST /{id}/dispute-reweigh` -- initiate dispute reweigh
   - `POST /{id}/resolve-dispute` -- resolve dispute
   - `POST /tare-verification/{vehicleId}` -- initiate tare verification
   - `GET /{id}/reweigh-history` -- get reweigh chain for a transaction

4. **Sprint 3**: Add reweigh UI components to the commercial weighing frontend (dispute button, tare verification action, resolution dialog).

5. **Sprint 2, Section 2.5 (Reports)**: Add "Reweigh/Dispute Report" and "Tare Verification Report" to the commercial report generator.

6. **Enforcement polish (any sprint)**: Add the 5 enforcement reweigh enhancements listed in section 4.1 as a sub-task.

---

## Appendix: File Reference Index

### Backend Files with Reweigh Logic

| Path | Role |
|------|------|
| `Services/Implementations/Weighing/WeighingService.cs` | Core reweigh initiation + compliance cascade |
| `Services/Interfaces/Weighing/IWeighingService.cs` | Interface definition |
| `Controllers/WeighingOperations/WeighingController.cs` | HTTP endpoint |
| `Models/Weighing/WeighingTransaction.cs` | Data model (ReweighCycleNo, OriginalWeighingId, ReweighLimit) |
| `Models/CaseManagement/LoadCorrectionMemo.cs` | Reweigh scheduling + relief truck tracking |
| `Models/CaseManagement/ComplianceCertificate.cs` | Post-reweigh certificate |
| `Services/Implementations/CaseManagement/ComplianceCertificateService.cs` | Certificate queries |
| `Services/Implementations/CaseManagement/SpecialReleaseService.cs` | Special release (RequiresReweigh flag) |
| `Services/Implementations/CaseManagement/LoadCorrectionMemoService.cs` | Memo queries |
| `Services/Implementations/Infrastructure/PdfDocuments/ComplianceCertificateDocument.cs` | Certificate PDF |
| `Services/Implementations/Infrastructure/PdfDocuments/SpecialReleaseCertificateDocument.cs` | Special release PDF |
| `Services/Implementations/Infrastructure/PdfDocuments/LoadCorrectionMemoDocument.cs` | Memo PDF |
| `Services/Implementations/Reporting/Modules/WeighingReportGenerator.cs` | Reweigh Statement report |
| `Data/Seeders/SystemConfiguration/SystemConfigurationSeeder.cs` | WeighingMaxReweighCycles = 8 |
| `Services/Implementations/Infrastructure/DocumentNumberService.cs` | ReweighTicket number generation |

### Frontend Files with Reweigh Logic

| Path | Role |
|------|------|
| `src/hooks/useWeighing.ts` | Reweigh session management + API call |
| `src/lib/api/weighing.ts` | `initiateReweigh()` API function + `InitiateReweighRequest` type |
| `src/types/weighing.ts` | `reweighCycleNo` on transaction type |
| `src/components/weighing/DecisionPanel.tsx` | "Re-weigh" button + cycle counter |
| `src/components/weighing/steps/WeighingDecisionStep.tsx` | Reweigh props and handlers |
| `src/app/[orgSlug]/weighing/special-release/page.tsx` | `requiresReweigh` flag |
| `src/components/case/subfiles/SubfileEntryForms.tsx` | Case subfile reweigh references |

### E2E Test Files

| Path | Coverage |
|------|----------|
| `Tests/e2e/compliancee2e/compliance_e2e_scenario_1.py` | Full overload -> reweigh -> compliance lifecycle |
| `Tests/e2e/compliancee2e/compliance_e2e_scenario_3.py` | Special release with reweigh |
| `Tests/e2e/compliancee2e/compliance_e2e_scenario_4.py` | Reweigh scenario variant |
| `Tests/e2e/compliancee2e/compliance_e2e_scenario_5.py` | Reweigh scenario variant |
| `Tests/e2e/compliancee2e/compliance_e2e_scenario_6.py` | Reweigh scenario variant |
| `Tests/e2e/compliancee2e/compliance_e2e_scenario_7.py` | Reweigh scenario variant |
