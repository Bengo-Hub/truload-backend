# KenloadV2 vs TruLoad - Comprehensive System Comparison

**Document Version:** 1.0
**Last Updated:** January 22, 2026
**Authors:** System Audit Team
**Purpose:** Identify gaps, strengths, and recommended improvements for TruLoad based on KenloadV2 reference implementation

---

## Executive Summary

This document provides a comprehensive comparison between KenloadV2 (existing KeNHA system) and TruLoad (next-generation weighing and enforcement solution). The analysis covers:

1. **Weighing Process & Axle Workflows**
2. **Prosecution & Case Handling**
3. **UI/UX Design Patterns**
4. **Technical Architecture**
5. **Regulatory Compliance**
6. **Identified Gaps & Recommended Improvements**

### Key Findings

| Area | KenloadV2 Advantage | TruLoad Advantage | Recommendation |
|------|---------------------|-------------------|----------------|
| **Axle Grouping** | ✅ Complete dual-system (Deck + Type) | ❌ Missing group aggregation | Adopt KenloadV2 approach |
| **Fee Calculation** | ✅ Per-axle-type fee schedules | ❌ Basic fee structure | Implement comprehensive fee tables |
| **Demerit Points** | ✅ Full NTSA integration | ❌ Not implemented | Add demerit points system |
| **PDF Calculation** | ✅ Fourth Power Law implemented | ✅ Model defined, needs calculation | Complete implementation |
| **Weight Ticket Format** | ✅ Regulatory compliant | ❌ Missing dual-table display | Adopt KenloadV2 format |
| **Offline Support** | ❌ Limited | ✅ PWA with IndexedDB | Keep TruLoad approach |
| **Modern Architecture** | ❌ Legacy .NET Framework | ✅ .NET 8+ LTS | Keep TruLoad approach |
| **Vector Search/AI** | ❌ Not available | ✅ ONNX + pgvector | Keep TruLoad approach |
| **Case Management** | ✅ Comprehensive subfiles (A-J) | ⚠️ Partial implementation | Enhance TruLoad with subfiles |
| **UI Framework** | ❌ Vue.js 2 + Bootstrap | ✅ Next.js 15 + Shadcn | Keep TruLoad approach |
| **Multi-tenancy** | ❌ Single tenant | ✅ Multi-tenant ready | Keep TruLoad approach |

---

## 1. Weighing Process Comparison

### 1.1 Axle Grouping System

#### KenloadV2 Implementation (Reference)

KenloadV2 uses a **dual classification system**:

**System 1: Deck Grouping (A, B, C, D)**
- Purpose: Visual configuration and weight aggregation for multi-deck weighbridges
- Groups: A (Front), B (Mid-Front), C (Mid-Rear), D (Rear)
- Stored in: `axleweightxreff.axle_deckgrouping`

```csharp
// KenloadV2 - AxleWeightConfigController.cs
var group = new String[4] { "A", "B", "C", "D" };
for (int k = 0; k < len; k++)
{
    if (AxleWeight[k].axle_deckgrouping == "A")
        groupA += AxleWeight[k].axle_legalweight;
    else if (AxleWeight[k].axle_deckgrouping == "B")
        groupB += AxleWeight[k].axle_legalweight;
    // ...
}
```

**System 2: Axle Type Classification**
- Purpose: Fee calculation and regulatory compliance
- Types: Steering, Single Drive, Tandem, Tridem
- Stored in: `axleweightxreff.axle_typeoftyres` + spacing logic

```csharp
// KenloadV2 - EACActController.cs
public ActionResult GetEACActCharges(
    double GVWOverloadkg,
    double steeringaxle,      // Steering axle overload
    double singledriveaxle,   // Single drive axle overload
    double tandemaxle,        // Tandem group overload
    double tridemaxle)        // Tridem group overload
```

#### TruLoad Current Implementation

TruLoad has a **5-tier hierarchical model**:

1. **TyreType** → S (Single), D (Dual), W (Wide)
2. **AxleGroup** → S1, SA4, SA6, TAG8, etc.
3. **AxleConfiguration** → 2*, 2A, 3A, 4B, 5C, 6D, 7A, 7B
4. **AxleWeightReference** → Position-specific weights
5. **WeighingAxle** → Actual measurements

**Gap Identified:** TruLoad's `AxleGrouping` field (A/B/C/D) exists but **group aggregation logic is incomplete**.

#### Recommended Approach for TruLoad

```csharp
// RECOMMENDED: Unified Group Aggregation Service
public class AxleGroupAggregationService
{
    public async Task<List<AxleGroupResult>> AggregateAxleGroups(
        ICollection<WeighingAxle> axles,
        ToleranceSettings tolerance)
    {
        var groups = axles.GroupBy(a => a.AxleGrouping).ToList();
        var results = new List<AxleGroupResult>();

        foreach (var group in groups)
        {
            var groupLabel = group.Key;  // "A", "B", "C", "D"
            var groupWeight = group.Sum(a => a.MeasuredWeightKg);
            var groupPermissible = group.Sum(a => a.PermissibleWeightKg);
            var axleCount = group.Count();

            // Determine axle type based on count and spacing
            var axleType = DetermineAxleType(axleCount, group.ToList());

            // Apply tolerance: 5% for single axles, 0% for groups (per Traffic Act)
            var toleranceKg = axleCount == 1
                ? (int)(groupPermissible * 0.05m)
                : 0;

            var effectiveLimit = groupPermissible + toleranceKg;
            var groupOverload = Math.Max(0, groupWeight - effectiveLimit);

            // Calculate Pavement Damage Factor (Fourth Power Law)
            var pdf = Math.Pow((double)groupWeight / groupPermissible, 4);

            results.Add(new AxleGroupResult
            {
                GroupLabel = groupLabel,
                AxleType = axleType,
                AxleCount = axleCount,
                GroupWeightKg = groupWeight,
                GroupPermissibleKg = groupPermissible,
                ToleranceKg = toleranceKg,
                EffectiveLimitKg = effectiveLimit,
                OverloadKg = groupOverload,
                PavementDamageFactor = (decimal)pdf,
                Status = DetermineStatus(groupOverload, tolerance.OperationalToleranceKg)
            });
        }

        return results;
    }

    private AxleType DetermineAxleType(int axleCount, List<WeighingAxle> axles)
    {
        return axleCount switch
        {
            1 when axles[0].AxleGrouping == "A" => AxleType.Steering,
            1 => AxleType.SingleDrive,
            2 => AxleType.Tandem,
            3 => AxleType.Tridem,
            4 => AxleType.Quad,
            _ => AxleType.Unknown
        };
    }
}
```

### 1.2 Weight Capture Modes

#### KenloadV2 Modes

| Mode | Description | Hardware | UI Component |
|------|-------------|----------|--------------|
| **Multi-Deck** | 4 decks + GVW | Static weighbridge | `deckweights.vue` |
| **LSWIM** | Low-Speed Weigh-in-Motion | Haenni scales | `axleweights.vue` |
| **HSWIM** | High-Speed WIM | Embedded sensors | `virtualticket.vue` |
| **Manual** | Offline/backup | Any scale | `addmanualticket.vue` |

#### TruLoad Modes

| Mode | Description | Hardware | Status |
|------|-------------|----------|--------|
| **Static Multi-Deck** | TruConnect streams | Static weighbridge | ✅ Designed |
| **WIM** | Auto-capture | WIM sensors | ✅ Designed |
| **Mobile/Axle-by-Axle** | Manual assignment | Mobile scales | ✅ Designed |

**Advantage:** TruLoad uses a **unified backend endpoint** - all modes send identical payload structure.

```typescript
// TruLoad - Unified Weight Capture Payload
POST /api/v1/weighing-transactions/{id}/capture-weights
{
  weighingAxles: [
    { axleNumber: 1, measuredWeightKg: 6800, axleConfigurationId: "uuid" },
    { axleNumber: 2, measuredWeightKg: 10500, axleConfigurationId: "uuid" },
    // ...
  ]
}
```

### 1.3 Tolerance Rules

#### Kenya Traffic Act Cap 403 Requirements

| Element | Tolerance | Notes |
|---------|-----------|-------|
| Single Axle | 5% | Applied to individual axle |
| Axle Group (Tandem/Tridem) | 0% | Strict enforcement on group total |
| Gross Vehicle Weight (GVW) | 0% | No tolerance allowed |
| Operational Tolerance | ≤200 kg | Auto special release threshold |

#### KenloadV2 Implementation

```javascript
// KenloadV2 - autoweigh.vue (Line 838)
var perm = Number(orderData[i].axle_permissibleweight);
var tolerance = (perm * 5) / 100 + perm;  // 5% tolerance
```

#### TruLoad Current State

⚠️ **Gap:** Tolerance logic exists in model but not properly enforced in compliance calculation.

#### Recommended TruLoad Implementation

```csharp
// TruLoad - ToleranceService.cs
public class ToleranceService
{
    private readonly IToleranceRepository _toleranceRepo;

    public ToleranceResult CalculateTolerance(
        int axleCount,
        int permissibleKg,
        string legalFramework)
    {
        var settings = _toleranceRepo.GetActiveSettings(legalFramework);

        // 5% tolerance for single axles only (Traffic Act Cap 403)
        var percentageTolerance = axleCount == 1
            ? (int)(permissibleKg * settings.SingleAxleTolerancePercent)
            : 0;

        return new ToleranceResult
        {
            PercentageToleranceKg = percentageTolerance,
            OperationalToleranceKg = settings.OperationalToleranceKg, // ≤200 kg
            EffectiveLimitKg = permissibleKg + percentageTolerance,
            ToleranceApplied = axleCount == 1
        };
    }
}
```

---

## 2. Fee Calculation Comparison

### 2.1 KenloadV2 Fee Structure

#### EAC Act Fee Tables

| Overload (kg) | Steering ($) | Single Drive ($) | Tandem ($) | Tridem ($) | GVW ($) |
|---------------|--------------|------------------|------------|------------|---------|
| 0-500 | 25 | 30 | 40 | 50 | 100 |
| 501-1000 | 50 | 60 | 80 | 100 | 200 |
| 1001-2000 | 100 | 120 | 160 | 200 | 400 |
| 2001-5000 | 200 | 240 | 320 | 400 | 800 |
| 5001-10000 | 400 | 480 | 640 | 800 | 1500 |
| >10000 | 800 | 960 | 1280 | 1600 | 2500 |

#### Traffic Act Fee Tables (KES)

| Overload (kg) | Fine (KES) | Demerit Points |
|---------------|------------|----------------|
| 0-2,000 | 5,000 | 1 |
| 2,001-5,000 | 20,000 | 2 |
| 5,001-10,000 | 50,000 | 3 |
| 10,001-20,000 | 100,000 | 5 |
| >20,000 | 200,000 | 10 |

### 2.2 TruLoad Fee Structure

**Current Status:** Basic `AxleFeeSchedule` table exists with 10 entries (5 EAC + 5 Traffic Act).

**Gap:** Missing axle-type-specific fee columns.

### 2.3 Recommended Fee System for TruLoad

```csharp
// RECOMMENDED: Models/Fees/AxleOverloadFeeSchedule.cs
public class AxleOverloadFeeSchedule : BaseEntity
{
    public int OverloadMinKg { get; set; }
    public int? OverloadMaxKg { get; set; }

    // Per-axle-type fees (USD)
    public decimal SteeringAxleFeeUsd { get; set; }
    public decimal SingleDriveAxleFeeUsd { get; set; }
    public decimal TandemAxleFeeUsd { get; set; }
    public decimal TridemAxleFeeUsd { get; set; }

    public string LegalFramework { get; set; }  // "EAC_ACT" or "TRAFFIC_ACT"
    public DateTime EffectiveFrom { get; set; }
}

public class GvwOverloadFeeSchedule : BaseEntity
{
    public int OverloadMinKg { get; set; }
    public int? OverloadMaxKg { get; set; }
    public decimal FeeUsd { get; set; }
    public decimal FeeKes { get; set; }
    public string LegalFramework { get; set; }
    public DateTime EffectiveFrom { get; set; }
}
```

---

## 3. Demerit Points System

### 3.1 NTSA Demerit Points Overview

The National Transport and Safety Authority (NTSA) operates a demerit points system:

| Points Accumulated | Consequence |
|--------------------|-------------|
| 10-13 points | License disqualification for 6 months |
| 14-19 points | License disqualification for 1 year |
| 20+ points | License disqualification for 2 years |

### 3.2 KenloadV2 Implementation

```csharp
// KenloadV2 - EACActController.cs
var steeringpoints = queryavwdemerit.Where(x => x.category <= steeringaxle).FirstOrDefault();
var tandempoints = queryavwdemerit.Where(x => x.category <= tandemaxle).FirstOrDefault();

totalpoints = vehicle_demeritpoints + tridemaxlepoint_ + tandemaxlepoint_
            + singledriveaxlepoint_ + steeringaxlepoint_ + gvwpoints_;

penalties = queryPenalties.Where(x => x.points >= totalpoints).FirstOrDefault();
```

### 3.3 Recommended TruLoad Implementation

```csharp
// Models/Enforcement/DemeritPointSchedule.cs
public class DemeritPointSchedule : BaseEntity
{
    public string ViolationType { get; set; }  // "STEERING", "TANDEM", "TRIDEM", "GVW"
    public int OverloadMinKg { get; set; }
    public int? OverloadMaxKg { get; set; }
    public int Points { get; set; }
    public string LegalFramework { get; set; }
    public DateTime EffectiveFrom { get; set; }
}

public class PenaltySchedule : BaseEntity
{
    public int PointsMin { get; set; }
    public int? PointsMax { get; set; }
    public string PenaltyDescription { get; set; }
    public int SuspensionDays { get; set; }
    public bool RequiresCourt { get; set; }
    public decimal AdditionalFineUsd { get; set; }
}

// Services/DemeritPointsService.cs
public class DemeritPointsService
{
    public async Task<DemeritPointsResult> CalculateDemeritPoints(
        List<AxleGroupResult> groupResults,
        int gvwOverloadKg,
        string legalFramework)
    {
        int totalPoints = 0;
        var breakdown = new List<DemeritPointBreakdown>();

        // Calculate points per axle group
        foreach (var group in groupResults.Where(g => g.OverloadKg > 0))
        {
            var schedule = await GetDemeritSchedule(
                group.AxleType.ToString(),
                group.OverloadKg,
                legalFramework);

            if (schedule != null)
            {
                totalPoints += schedule.Points;
                breakdown.Add(new DemeritPointBreakdown
                {
                    ViolationType = group.AxleType.ToString(),
                    OverloadKg = group.OverloadKg,
                    Points = schedule.Points
                });
            }
        }

        // Add GVW points if overloaded
        if (gvwOverloadKg > 0)
        {
            var gvwSchedule = await GetDemeritSchedule("GVW", gvwOverloadKg, legalFramework);
            if (gvwSchedule != null)
            {
                totalPoints += gvwSchedule.Points;
                breakdown.Add(new DemeritPointBreakdown
                {
                    ViolationType = "GVW",
                    OverloadKg = gvwOverloadKg,
                    Points = gvwSchedule.Points
                });
            }
        }

        // Determine penalty based on total points
        var penalty = await GetPenalty(totalPoints);

        return new DemeritPointsResult
        {
            TotalPoints = totalPoints,
            Breakdown = breakdown,
            Penalty = penalty,
            RequiresCourt = penalty?.RequiresCourt ?? false,
            SuspensionDays = penalty?.SuspensionDays ?? 0
        };
    }
}
```

---

## 4. Prosecution & Case Handling Comparison

### 4.1 Case Management Workflow

#### KenloadV2 Case System

Two parallel systems:
1. **Legacy CaseDetails** - Basic case tracking
2. **New CaseMgt** - Comprehensive with subfiles

**Case Status Flow:**
```
Open → In Yard → PBC (Pending Before Court) → Paid/Closed
```

**Case Subfiles (A-J):**
| Subfile | Description | KenloadV2 Model |
|---------|-------------|-----------------|
| A | Initial case details | CaseRegister |
| B | Document Evidence | CaseEvidenceFiles |
| C | Expert Reports | CaseExpertReports |
| D | Witness Statements | caseWitnessStatements |
| E | Accused Statements | AccusedStatement |
| F | Investigation Diary | CaseDiary |
| G | Charge Sheets, NTAC | CaseCoverPage |
| H | Accused Records | CaseAccussedRecords |
| I | Covering Report | CaseCoveringReport |
| J | Minute Sheets | CaseMinuteSheet |

#### TruLoad Case System

**Current Implementation (Sprint 10):**
- ✅ CaseRegister with auto-creation from weighing
- ✅ SpecialRelease workflow
- ✅ Basic PDF documents (Load Correction Memo, Compliance Certificate)

**Missing Elements:**
- ❌ Full subfile system (B-J)
- ❌ Court hearing tracking
- ❌ Warrant management
- ❌ Prosecutor assignment

### 4.2 Recommended TruLoad Case Enhancement

```csharp
// RECOMMENDED: Complete Case Subfile System

// Models/Case/CaseSubfile.cs
public abstract class CaseSubfileBase : BaseEntity
{
    public Guid CaseRegisterId { get; set; }
    public CaseRegister CaseRegister { get; set; }
    public string SubfileCode { get; set; }  // "A", "B", "C", etc.
    public DateTime DocumentDate { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string? Notes { get; set; }
}

// Subfile B: Evidence Files
public class CaseEvidence : CaseSubfileBase
{
    public string EvidenceType { get; set; }  // "PHOTO", "DOCUMENT", "VIDEO"
    public string Description { get; set; }
    public string FileUrl { get; set; }
    public string MimeType { get; set; }
    public long FileSizeBytes { get; set; }
    public string? ChainOfCustody { get; set; }
}

// Subfile C: Expert Reports
public class CaseExpertReport : CaseSubfileBase
{
    public string ExpertName { get; set; }
    public string ExpertiseArea { get; set; }
    public string ReportSummary { get; set; }
    public string FileUrl { get; set; }
    public DateTime ReportDate { get; set; }
}

// Subfile D: Witness Statements
public class CaseWitnessStatement : CaseSubfileBase
{
    public string WitnessName { get; set; }
    public string WitnessType { get; set; }  // "OFFICER", "DRIVER", "BYSTANDER"
    public string StatementText { get; set; }
    public DateTime StatementDate { get; set; }
    public string? WitnessSignatureUrl { get; set; }
}

// Subfile F: Investigation Diary
public class CaseDiaryEntry : CaseSubfileBase
{
    public DateTime EntryDateTime { get; set; }
    public string Activity { get; set; }
    public string Location { get; set; }
    public string Findings { get; set; }
    public Guid InvestigatorUserId { get; set; }
}

// Subfile G: Court Documents
public class CaseCourtDocument : CaseSubfileBase
{
    public string DocumentType { get; set; }  // "CHARGE_SHEET", "SUMMONS", "WARRANT"
    public string DocumentNumber { get; set; }
    public DateTime IssueDate { get; set; }
    public string FileUrl { get; set; }
    public string CourtName { get; set; }
    public string? JudgeName { get; set; }
}

// Court Hearing Tracking
public class CourtHearing : BaseEntity
{
    public Guid CaseRegisterId { get; set; }
    public CaseRegister CaseRegister { get; set; }
    public DateTime HearingDate { get; set; }
    public string CourtName { get; set; }
    public string HearingType { get; set; }  // "MENTION", "TRIAL", "JUDGMENT"
    public string? JudgeName { get; set; }
    public string? Outcome { get; set; }
    public DateTime? NextHearingDate { get; set; }
    public string? Notes { get; set; }
}
```

---

## 5. UI/UX Comparison

### 5.1 Weight Display Design

#### KenloadV2 Design Patterns

1. **Digital LCD Display**
   - Black background with yellow/green digital font
   - Real-time weight streaming (1000ms interval)
   - Audio alerts (multilingual: Swahili/English)

2. **Dual Table Layout**
   - Table 1: Individual axle weights (diagnostics)
   - Table 2: Axle group weights (primary compliance)

3. **Color-Coded Status**
   - Green: LEGAL
   - Yellow: WARNING
   - Red: OVERLOAD

4. **Visual Axle Configuration**
   - Clickable axle positions with tyre type images (S/D/W)
   - Deck grouping indicators (A, B, C, D)

#### TruLoad Planned Design (Superior Approach)

1. **Digital Twin Visualization**
   - Dynamic SVG vehicle diagram
   - Live weight indicators with "pressure" visualization
   - Color gradient (Green < 95%, Yellow > 95%, Red > 100%)

2. **Unified Hierarchical Grid**
   - Single table with expandable rows
   - Group rows (bold) → Axle children (lighter)
   - Better information hierarchy than KenloadV2's dual tables

3. **Decision Panel**
   - Primary status badge (LEGAL/WARNING/PROHIBITED)
   - Action buttons: Print, Tag, Send to Yard, Special Release

### 5.2 Recommended UI Improvements

```tsx
// RECOMMENDED: Unified Hierarchical Weight Grid Component
interface WeightGridRow {
  type: 'group' | 'axle';
  label: string;
  permissibleKg: number;
  toleranceKg?: number;  // Groups only
  measuredKg: number;
  overloadKg: number;
  pdf: number;
  status: 'LEGAL' | 'WARNING' | 'OVERLOAD';
  children?: WeightGridRow[];  // For group rows
}

const WeightHierarchyGrid: React.FC<{ data: WeightGridRow[] }> = ({ data }) => {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Group/Axle</TableHead>
          <TableHead>Type</TableHead>
          <TableHead>Permissible (kg)</TableHead>
          <TableHead>Tolerance (kg)</TableHead>
          <TableHead>Measured (kg)</TableHead>
          <TableHead>Overload (kg)</TableHead>
          <TableHead>PDF</TableHead>
          <TableHead>Status</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {data.map((row, index) => (
          <React.Fragment key={index}>
            {/* Group Row (Bold, Primary) */}
            {row.type === 'group' && (
              <TableRow className="font-bold bg-muted/50">
                <TableCell>{row.label}</TableCell>
                <TableCell>{getAxleTypeLabel(row)}</TableCell>
                <TableCell>{row.permissibleKg.toLocaleString()}</TableCell>
                <TableCell>{row.toleranceKg?.toLocaleString() ?? '-'}</TableCell>
                <TableCell>{row.measuredKg.toLocaleString()}</TableCell>
                <TableCell className={getOverloadClass(row.overloadKg)}>
                  {row.overloadKg.toLocaleString()}
                </TableCell>
                <TableCell>{row.pdf.toFixed(2)}</TableCell>
                <TableCell>
                  <StatusBadge status={row.status} />
                </TableCell>
              </TableRow>
            )}

            {/* Axle Children (Lighter, Indented) */}
            {row.children?.map((child, childIndex) => (
              <TableRow key={childIndex} className="text-muted-foreground">
                <TableCell className="pl-8">↳ {child.label}</TableCell>
                <TableCell>-</TableCell>
                <TableCell>{child.permissibleKg.toLocaleString()}</TableCell>
                <TableCell>-</TableCell>
                <TableCell>{child.measuredKg.toLocaleString()}</TableCell>
                <TableCell>{child.overloadKg.toLocaleString()}</TableCell>
                <TableCell>{child.pdf.toFixed(2)}</TableCell>
                <TableCell>-</TableCell>
              </TableRow>
            ))}
          </React.Fragment>
        ))}

        {/* GVW Summary Row (Highlighted) */}
        <TableRow className="font-bold bg-primary/10 border-t-2">
          <TableCell>GVW TOTAL</TableCell>
          <TableCell>-</TableCell>
          <TableCell>{gvwPermissible.toLocaleString()}</TableCell>
          <TableCell>0 (No Tolerance)</TableCell>
          <TableCell>{gvwMeasured.toLocaleString()}</TableCell>
          <TableCell className={getOverloadClass(gvwOverload)}>
            {gvwOverload.toLocaleString()}
          </TableCell>
          <TableCell>-</TableCell>
          <TableCell>
            <StatusBadge status={gvwStatus} size="lg" />
          </TableCell>
        </TableRow>
      </TableBody>
    </Table>
  );
};
```

---

## 6. Technical Architecture Comparison

### 6.1 Backend Technology

| Aspect | KenloadV2 | TruLoad | Winner |
|--------|-----------|---------|--------|
| Framework | .NET Framework 4.x | .NET 8 LTS | ✅ TruLoad |
| Database | SQL Server | PostgreSQL 16 + pgvector | ✅ TruLoad |
| Caching | None | Redis 7+ | ✅ TruLoad |
| Message Queue | None | RabbitMQ + MassTransit | ✅ TruLoad |
| AI/ML | None | ONNX Runtime + pgvector | ✅ TruLoad |
| PDF Generation | Client-side (jsPDF) | Backend (QuestPDF) | ✅ TruLoad |
| API Documentation | None | Swagger/OpenAPI 3.0 | ✅ TruLoad |
| Authentication | Basic/Session | JWT + ASP.NET Core Identity | ✅ TruLoad |

### 6.2 Frontend Technology

| Aspect | KenloadV2 | TruLoad | Winner |
|--------|-----------|---------|--------|
| Framework | Vue.js 2 + Bootstrap Vue | Next.js 15 + React 19 | ✅ TruLoad |
| Styling | Bootstrap 4 CSS | Tailwind CSS + Shadcn | ✅ TruLoad |
| State Management | Vuex | Zustand + TanStack Query | ✅ TruLoad |
| Forms | Manual validation | React Hook Form + Zod | ✅ TruLoad |
| Offline Support | Limited localStorage | PWA + IndexedDB (Dexie) | ✅ TruLoad |
| TypeScript | None | Full TypeScript | ✅ TruLoad |

### 6.3 Scale Integration

| Aspect | KenloadV2 | TruLoad | Notes |
|--------|-----------|---------|-------|
| Integration App | iConnect (Java) | TruConnect (Node.js/Electron) | Both functional |
| Protocol | HTTP polling (localhost) | HTTP + WebSocket ready | ✅ TruLoad |
| Multi-scale | Yes (4 decks) | Yes (4 decks) | Equal |
| Haenni Support | Yes | Planned | KenloadV2 leads |
| RDU Communication | Serial/TCP | Serial/TCP | Equal |

---

## 7. Summary of Gaps & Action Items

### 7.1 Critical Gaps (Must Fix)

| Gap | Impact | Priority | Sprint |
|-----|--------|----------|--------|
| Axle group aggregation logic | Non-compliant weights | P0 | Sprint 11 |
| Per-axle-type fee calculation | Incorrect charges | P0 | Sprint 11 |
| Demerit points system | Incomplete enforcement | P0 | Sprint 11 |
| Weight ticket dual-table format | Court admissibility | P0 | Sprint 12 |

### 7.2 High Priority Gaps

| Gap | Impact | Priority | Sprint |
|-----|--------|----------|--------|
| Case subfiles (B-J) | Incomplete prosecution | P1 | Sprint 12-13 |
| Court hearing tracking | No case lifecycle | P1 | Sprint 13 |
| Warrant management | Limited enforcement | P1 | Sprint 13 |
| NTSA integration | No license tracking | P1 | Sprint 14 |

### 7.3 Medium Priority Gaps

| Gap | Impact | Priority | Sprint |
|-----|--------|----------|--------|
| Haenni scale support | Limited mobile weighing | P2 | Sprint 14 |
| Multi-language audio alerts | User experience | P2 | Sprint 15 |
| Export to Excel (multi-format) | Reporting flexibility | P2 | Sprint 15 |

### 7.4 TruLoad Advantages to Preserve

1. **Modern Architecture** - .NET 8, PostgreSQL, Redis
2. **Offline-First PWA** - IndexedDB with background sync
3. **Vector Search/AI** - Natural language queries
4. **Multi-Tenancy** - KURA, KeNHA, Counties
5. **Unified Backend** - Mode-agnostic weight capture
6. **Superior UI Framework** - Next.js 15, Shadcn, TypeScript

---

## 8. Recommended Implementation Roadmap

### Phase 1: Regulatory Compliance (Sprint 11)
1. Implement axle group aggregation service
2. Add per-axle-type fee calculation
3. Add demerit points calculation
4. Update WeighingService with PDF calculation
5. Add database migrations for fee/demerit tables

### Phase 2: Document Generation (Sprint 12)
1. Update weight ticket PDF with dual-table format
2. Add legal disclaimer section
3. Add tolerance display columns
4. Implement prohibition order template

### Phase 3: Prosecution Enhancement (Sprint 12-13)
1. Add case subfile models (B-J)
2. Implement court hearing tracking
3. Add warrant management
4. Implement prosecutor assignment

### Phase 4: Integration & Polish (Sprint 14-15)
1. NTSA integration for demerit points sync
2. Haenni scale support in TruConnect
3. Multi-language audio alerts
4. Advanced reporting exports

---

**Document Prepared By:** System Audit Team
**Reviewed By:** Technical Lead
**Approved By:** Project Manager
**Next Review Date:** February 22, 2026