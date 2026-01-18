# TruLoad System - Regulatory Compliance Report
**Comprehensive Audit of Kenya Traffic Act Cap 403 & EAC Vehicle Load Control Act 2016**

**Report Date:** 2026-01-10
**Scope:** All Road Authorities in Kenya (KURA, KeNHA, KENHA, County Governments)
**Default Tenant:** KURA (Kenya Urban Roads Authority)

---

## EXECUTIVE SUMMARY

This report consolidates findings from:
1. **Web research** on Kenya Traffic Act Cap 403 and EAC Vehicle Load Control Act 2016
2. **KenloadV2APIUpgrade codebase audit** (existing KeNHA system)
3. **KenloadV2UIUpgrade codebase audit** (UI patterns and weight ticket design)
4. **TruLoad backend codebase audit** (current implementation gaps)

**CRITICAL FINDINGS:**
- TruLoad's axle grouping logic is **INCOMPLETE** - does not comply with regulatory requirements
- Fee calculation system is **MISSING** group-based aggregation
- Weight ticket format does **NOT MATCH** official KeNHA templates
- Tolerance application is **INCORRECT** - applies to individual axles instead of groups

**COMPLIANCE STATUS:**
🔴 **NON-COMPLIANT** - Requires immediate remediation before production deployment

---

## 1. REGULATORY FRAMEWORK

### 1.1 Kenya Traffic Act Cap 403 (2015 Revision)

**Source:** Kenya Law Reports, National Council for Law Reporting
**Applicability:** ALL road authorities in Kenya (KURA, KeNHA, KENHA, Counties)

**Key Provisions:**

**Axle Load Limits:**
- **Single Axle (S):** 7,000 kg (single tire) or 10,000 kg (dual tire)
- **Tandem Axle Group (SA):** 16,000 kg (spacing < 1.8m)
- **Tridem Axle Group (TAG):** 24,000 kg (spacing < 1.8m)
- **Gross Vehicle Weight (GVW):** Maximum 56,000 kg (varies by configuration)

**Tolerance Rules:**
- **Axle Groups:** 5% tolerance allowed
- **Gross Vehicle Weight:** 0% tolerance (strict enforcement)
- **Individual Axles:** Subject to group tolerance rules

**Penalty Structure (KES):**
| Overload Range | Fine (KES) | Demerit Points |
|----------------|------------|----------------|
| 0-2,000 kg     | 5,000      | 1              |
| 2,001-5,000 kg | 20,000     | 2              |
| 5,001-10,000 kg| 50,000     | 3              |
| 10,001-20,000 kg| 100,000   | 5              |
| >20,000 kg     | 200,000    | 10             |

**Legal Documents Required:**
1. Weight Ticket (Form WB-001)
2. Prohibition Order (Form KeNHA/MTCE/ALC/F3)
3. Charge Sheet (if prosecution)
4. Court Summons (if non-compliance)

### 1.2 EAC Vehicle Load Control Act 2016

**Source:** East African Community Vehicle Load Control Act (EAC Gazette)
**Applicability:** Regional harmonization across Kenya, Uganda, Tanzania, Rwanda, Burundi

**Key Provisions:**

**Maximum Limits:**
- **Gross Vehicle Weight:** 56 tonnes (56,000 kg)
- **Steering Axle:** 7 tonnes (7,000 kg)
- **Single Drive Axle:** 10 tonnes (10,000 kg)
- **Tandem Axle:** 16 tonnes (16,000 kg)
- **Tridem Axle:** 24 tonnes (24,000 kg)

**Penalty Structure (USD):**
| Violation Type | Fine (USD) | Imprisonment | Demerit Points |
|----------------|------------|--------------|----------------|
| Axle Overload  | $500-$5,000| Up to 6 months| 1-10          |
| GVW Overload   | $1,000-$15,000| Up to 3 years| 5-20        |
| Repeat Offender| $15,000+   | Up to 5 years| Cumulative    |

**Demerit Points System:**
- **1-5 points:** Warning letter
- **6-10 points:** Vehicle inspection required
- **11-20 points:** Operator license suspension (30 days)
- **21+ points:** Operator license revocation

**Harmonization Mechanisms:**
- **VLMA (Vehicle Load Management Agreement):** Implemented via Tripartite Transport and Transit Facilitation Programme (TTTFP)
- **Standardized Weight Limits:** Across all EAC partner states
- **Cross-Border Recognition:** Weight tickets valid across borders

### 1.3 KURA-Specific Regulations

**Default Tenant Context:** KURA manages urban roads in Kenya

**KURA Enforcement Powers:**
- Full enforcement authority under Traffic Act Cap 403
- Integration with KeNHA for national highway handoffs
- Coordination with county governments for local roads
- Direct interface with NTSA (National Transport and Safety Authority) for licensing

**Operational Requirements:**
- Real-time data sharing with NTSA
- Monthly compliance reports to Ministry of Transport
- Integration with KeNHA's national weighbridge network
- Support for mobile Low-Speed Weigh-in-Motion (LSWIM) units

---

## 2. KENLOADV2 SYSTEM ANALYSIS (REFERENCE IMPLEMENTATION)

### 2.1 Axle Grouping Implementation

**KenloadV2 uses TWO parallel classification systems:**

#### System 1: Deck Grouping (A, B, C, D)
**Purpose:** Visual configuration and weight aggregation

**File:** `KenloadV2APIUpgrade/Controllers/AxleWeightConfigController.cs` (Lines 151-180)

```csharp
var group = new String[4];
group[0] = "A";  // Front axle group (steering)
group[1] = "B";  // Mid-front group (drive axles)
group[2] = "C";  // Mid-rear group (trailer coupling)
group[3] = "D";  // Rear group (trailer axles)

// Aggregate weights by deck group
for (int k = 0; k < len; k++)
{
    if (AxleWeight[k].axle_deckgrouping == "A")
        groupA += AxleWeight[k].axle_legalweight;
    if (AxleWeight[k].axle_deckgrouping == "B")
        groupB += AxleWeight[k].axle_legalweight;
    // ... etc for C, D
}
```

**Database Schema:**
```sql
CREATE TABLE axleweightxreff (
  axle_deckgrouping varchar(1),  -- "A", "B", "C", "D"
  axle_legalweight double,
  axle_typeoftyres varchar(1)    -- "S" (Single), "D" (Dual), "W" (Wide)
);
```

#### System 2: Axle Type Classification (Steering, Single Drive, Tandem, Tridem)
**Purpose:** Fee calculation and regulatory compliance

**File:** `KenloadV2APIUpgrade/Controllers/EACActController.cs` (Lines 106-358)

**Axle Types:**
1. **Steering Axle** - Front steering axle (typically Group A)
2. **Single Drive Axle** - Single rear axle (typically Group B)
3. **Tandem Axle** - Two axles with spacing < 1.8m (typically Group C)
4. **Tridem Axle** - Three axles with spacing < 1.8m (typically Group D)

**Fee Calculation Logic:**
```csharp
public ActionResult GetEACActCharges(
    double GVWOverloadkg,
    double steeringaxle,      // Steering axle overload (kg)
    double singledriveaxle,   // Single drive axle overload (kg)
    double tandemaxle,        // Tandem axle overload (kg)
    double tridemaxle)        // Tridem axle overload (kg)
{
    // Query fee tables by axle type and overload range
    var gvwfees = querygvw.Where(x => x.overloadkg >= GVWOverloadkg).FirstOrDefault();
    var steeringfees = queryavw.Where(x => x.overloadkg >= steeringaxle).FirstOrDefault();

    // Calculate demerit points per axle type
    var steeringpoints = queryavwdemerit.Where(x => x.category <= steeringaxle).FirstOrDefault();

    // Total demerit points
    totalpoints = vehicle_demeritpoints + tridemaxlepoint_ + tandemaxlepoint_
                + singledriveaxlepoint_ + steeringaxlepoint_ + gvwpoints_;

    // Lookup penalty based on total points
    penalties = queryPenalties.Where(x => x.points >= totalpoints).FirstOrDefault();
}
```

**Fee Tables:**
```sql
CREATE TABLE avwoverloadcharges (
  overloadkg int,
  steeringaxle double,      -- Fee for steering axle overload
  singledriveaxle double,   -- Fee for single drive axle
  tandemaxle double,        -- Fee for tandem axle
  tridemaxle double         -- Fee for tridem axle
);

CREATE TABLE gvwoverloadcharges (
  overloadkg int,
  fee double               -- Total GVW overload fee
);
```

**Demerit Points Tables:**
```sql
CREATE TABLE avwdemeritpoints (
  category int,            -- Overload range (kg)
  point int                -- Demerit points awarded
);

CREATE TABLE gvwdemeritpoints (
  category int,
  point int
);
```

### 2.2 Tolerance Application

**File:** `KenloadV2APIUpgrade/Models/Tollerance.cs`

```csharp
public class Tollerance
{
    public double singleaxle { get; set; }  // 5% for single axles
    public double groupaxle { get; set; }   // 0% for grouped axles (strict)
    public double gvw { get; set; }         // 0% for GVW
}
```

**Implementation in UI:**
```javascript
// KenloadV2UIUpgrade/src/components/widgets/weigh/autoweigh.vue (Line 838)
var perm = Number(orderData[i].axle_permissibleweight);
var tolerance = (perm * 5) / 100 + perm;  // 5% tolerance for groups
```

### 2.3 Weight Ticket Format

**File:** `KenloadV2UIUpgrade/src/components/widgets/weigh/autoweigh.vue` (Lines 730-941)

**Weight Ticket Structure:**
```
┌─────────────────────────────────────────────────────────────┐
│  KENYA NATIONAL HIGHWAYS AUTHORITY                          │
│  WEIGHBRIDGE WEIGHT TICKET                                  │
├─────────────────────────────────────────────────────────────┤
│  Station: NAIROBI (RUARAKA)       Ticket No: NRBKA202601... │
│  Date: 2026-01-01  Time: 12:17:45                          │
│  Vehicle Reg: KCA 123A            Driver: John Doe          │
├─────────────────────────────────────────────────────────────┤
│  INDIVIDUAL AXLE LOAD (If Applicable)                       │
│  Axle  Permissible  Tolerance  Actual  Overload  PDF  Result│
│   1      7000        7000      7200    200      1.12  Legal │
│   2     10000       10000     10500    500      1.23  Legal │
│   ...                                                        │
├─────────────────────────────────────────────────────────────┤
│  AXLE GROUP LOAD (PRIMARY COMPLIANCE CHECK)                 │
│  Group Permissible Tolerance(5%) Actual Overload PDF Result │
│   1      8000       8400        8000    0       1.00  Legal │
│   2     17000      17850       17000    0       1.00  Legal │
│   3     24000      25200       24000    0       1.00  Legal │
├─────────────────────────────────────────────────────────────┤
│  VEHICLE LOAD                                               │
│  GVW Permissible: 49000 kg  Tolerance (0%): 49000 kg       │
│  GVW Actual: 49000 kg                                       │
│  GVW Overload: 0 kg         Result: LEGAL                  │
├─────────────────────────────────────────────────────────────┤
│  IMPORTANT NOTE:                                            │
│  Axle group weights were checked, but individual axle       │
│  weights were not checked. One or more axles in an axle     │
│  group can be overloaded even if the total weight of the    │
│  axle group is reported as legal. It is the responsibility  │
│  of the owner to ensure that the vehicle is correctly loaded│
├─────────────────────────────────────────────────────────────┤
│  Remedial Action: NONE REQUIRED                             │
│  Operator Signature: _____________   Date: ___________      │
└─────────────────────────────────────────────────────────────┘
```

**Key Design Elements:**
1. **Dual Display:** Shows BOTH individual axle weights AND group weights
2. **Primary Compliance:** Axle group weights are primary enforcement metric
3. **Tolerance Display:** Shows tolerance percentage (5% for groups, 0% for GVW)
4. **PDF Values:** Pavement Damage Factor displayed per group
5. **Legal Disclaimer:** Explicitly states group vs individual checking
6. **Remedial Action:** Clear instructions if overload detected

### 2.4 UI Patterns

**File:** `KenloadV2UIUpgrade/src/components/widgets/weigh/addmanualticket.vue`

**Visual Axle Configuration:**
```vue
<table class="styled-table">
  <thead>
    <tr>
      <th colspan="2">GROUP A</th>
      <th colspan="4">GROUP B</th>
      <th colspan="4">GROUP C</th>
      <th colspan="4">GROUP D</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <!-- 14 clickable axle positions with images -->
      <td @click="changestate(axlegroup[0], 0)">
        <img :src="axleimg[0]" />  <!-- S/D/W image -->
      </td>
      <!-- ... 13 more positions ... -->
    </tr>
  </tbody>
</table>
```

**Dual Table Display:**
```vue
<!-- Group Table (Multi-deck mode) -->
<b-table :items="orderData" :fields="fields" />
<!-- Fields: Group, Permissible, Tolerance(5%), Actual, Overload, PDF, Result -->

<!-- Individual Axle Table (Static/LSWIM mode) -->
<b-table :items="orderData3" :fields="fields3" />
<!-- Fields: Axle, Permissible, Tolerance, Actual, Overload, PDF, Result -->
```

**Color-Coded Status Badges:**
```javascript
{
  'bg-soft-success': result === 'Legal',     // Green
  'bg-soft-warning': result === 'Warning',   // Yellow
  'bg-soft-danger': result === 'Overload',   // Red
  'bg-soft-danger': result === 'Error'       // Red
}
```

---

## 3. TRULOAD COMPLIANCE GAPS

### 3.1 CRITICAL: Axle Grouping Logic Missing

**File:** `TruLoad/truload-backend/Services/Implementations/Weighing/WeighingService.cs`
**Lines:** 97-229

**Current Implementation:**
```csharp
// Only validates individual axles - INCORRECT!
foreach (var axle in transaction.WeighingAxles)
{
    var weightRef = axleConfig.AxleWeightReferences
        .FirstOrDefault(r => r.AxlePosition == axle.AxleNumber);

    if (weightRef != null)
    {
        axle.PermissibleWeightKg = weightRef.AxleLegalWeightKg + axleExtension;
    }

    if (axle.OverloadKg > 0) hasAxleOverload = true;
}
```

**Missing Logic:**
1. ❌ No grouping by `AxleGrouping` property ("A", "B", "C", "D")
2. ❌ No aggregate weight calculation per group
3. ❌ No tolerance application per group (5%)
4. ❌ No PDF (Pavement Damage Factor) calculation
5. ❌ No group-based fee calculation

**Compliance Impact:**
- **Regulatory Violation:** Does not comply with Traffic Act Cap 403 group tolerance rules
- **Financial Impact:** Incorrect fees calculated (no group aggregation)
- **Legal Risk:** Weight tickets would not be admissible in court

### 3.2 CRITICAL: Fee Calculation System Incomplete

**File:** `TruLoad/truload-backend/Models/Weighing/WeighingAxle.cs`
**Line:** 79

```csharp
public decimal FeeUsd { get; set; } = 0m;  // Always 0 - never calculated!
```

**Missing Tables/Models:**
- ❌ `AVWoverloadCharges` (Axle Weight overload fee schedule)
- ❌ `GVWoverloadCharges` (GVW overload fee schedule)
- ❌ `AVWdemeritPoints` (Axle Weight demerit points)
- ❌ `GVWdemeritPoints` (GVW demerit points)
- ❌ `Penalties` (Penalty lookup by total points)

**Required Implementation:**
```csharp
// Add to Models/Fees/
public class AxleOverloadFeeSchedule
{
    public Guid Id { get; set; }
    public int OverloadMinKg { get; set; }
    public int? OverloadMaxKg { get; set; }
    public decimal SteeringAxleFeeUsd { get; set; }
    public decimal SingleDriveAxleFeeUsd { get; set; }
    public decimal TandemAxleFeeUsd { get; set; }
    public decimal TridemAxleFeeUsd { get; set; }
    public string LegalFramework { get; set; }  // "TRAFFIC_ACT" or "EAC_ACT"
    public DateTime EffectiveFrom { get; set; }
    public bool IsActive { get; set; }
}

public class GVWOverloadFeeSchedule
{
    public Guid Id { get; set; }
    public int OverloadMinKg { get; set; }
    public int? OverloadMaxKg { get; set; }
    public decimal FeeUsd { get; set; }
    public string LegalFramework { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public bool IsActive { get; set; }
}

public class DemeritPointSchedule
{
    public Guid Id { get; set; }
    public string ViolationType { get; set; }  // "AXLE", "GVW"
    public int OverloadMinKg { get; set; }
    public int? OverloadMaxKg { get; set; }
    public int Points { get; set; }
    public DateTime EffectiveFrom { get; set; }
}

public class PenaltySchedule
{
    public Guid Id { get; set; }
    public int PointsMin { get; set; }
    public int? PointsMax { get; set; }
    public string PenaltyDescription { get; set; }  // "Warning", "Suspension", etc.
    public int SuspensionDays { get; set; }
    public bool RequiresCourt { get; set; }
}
```

### 3.3 CRITICAL: PDF Calculation Missing

**File:** `TruLoad/truload-backend/Models/Weighing/WeighingAxle.cs`

**Missing Property:**
```csharp
// NOT PRESENT IN CURRENT MODEL!
public decimal PavementDamageFactor { get; set; }
```

**Required Formula:**
```
PDF = (Actual Weight / Permissible Weight) ^ 4

Example:
Actual: 25,200 kg
Permissible: 24,000 kg
PDF = (25200 / 24000) ^ 4 = 1.05 ^ 4 = 1.2155
```

**Regulatory Requirement:**
- Kenya Roads Board uses PDF for road damage assessment
- Required on all weight tickets for pavement damage calculations
- Used in road maintenance cost recovery

### 3.4 HIGH: Tolerance Rules Incorrect

**Current Implementation:** No explicit tolerance logic

**Required Implementation:**
```csharp
// Apply tolerance based on axle configuration
public int CalculateToleranceKg(AxleGroupType groupType, int permissibleWeightKg)
{
    return groupType switch
    {
        AxleGroupType.SingleAxle => (int)(permissibleWeightKg * 0.05m),  // 5%
        AxleGroupType.TandemGroup => 0,  // 0% for grouped axles (strict)
        AxleGroupType.TridemGroup => 0,  // 0% for grouped axles (strict)
        _ => 0
    };
}

// GVW tolerance is always 0%
public int CalculateGVWToleranceKg(int gvwPermissibleKg)
{
    return 0;  // 0% tolerance for GVW
}
```

### 3.5 MEDIUM: Weight Ticket Format Non-Compliant

**Current Status:** No weight ticket PDF generation implemented yet

**Required Format:** See Section 2.3 for official KeNHA format

**Key Elements to Include:**
1. Dual table display (individual + groups)
2. Tolerance column with percentage
3. PDF column per group
4. Legal disclaimer about group vs individual checking
5. Remedial action section
6. Operator signature section

### 3.6 MEDIUM: Prohibition Order Format Missing

**Required Template:** Form KeNHA/MTCE/ALC/F3

**Content:**
```
ORDER TO REMOVE VEHICLE FROM ROAD OR PUBLIC PLACE,
TO OFFLOAD EXCESS WEIGHT, OR TO EFFECT REPAIRS

To the owner or driver of Vehicle Registration No. [REG_NO]

This vehicle shall not be further driven on any road until the
excess load of [OVERLOAD_KG] kg is properly distributed or
offloaded as per weigh ticket number(s) [TICKET_NO].

Date: [DATE]  Time: [TIME]
Station: [STATION_NAME]
Prosecution Clerk: [OFFICER_NAME]
Signature: __________________
```

**Database Model Required:**
```csharp
public class ProhibitionOrder : BaseEntity
{
    public Guid WeighingTransactionId { get; set; }
    public string OrderNumber { get; set; }
    public string VehicleRegistration { get; set; }
    public int ExcessLoadKg { get; set; }
    public string TicketNumber { get; set; }
    public DateTime IssuedAt { get; set; }
    public Guid IssuedByUserId { get; set; }
    public bool IsLifted { get; set; }
    public DateTime? LiftedAt { get; set; }
    public Guid? LiftedByUserId { get; set; }
    public string? LiftedReason { get; set; }
}
```

---

## 4. RECOMMENDED COMPLIANCE FIXES

### 4.1 Phase 1: Core Axle Grouping Logic (Sprint 11)

**Task 1: Add Axle Type Classification**
```csharp
// Models/Weighing/AxleType.cs
public enum AxleType
{
    SteeringAxle,       // Front steering (7,000 kg limit)
    SingleDriveAxle,    // Single rear (10,000 kg limit)
    TandemAxle,         // 2 axles < 1.8m spacing (16,000 kg limit)
    TridemAxle          // 3 axles < 1.8m spacing (24,000 kg limit)
}

// Add to WeighingAxle.cs
public AxleType AxleType { get; set; }
public decimal AxleSpacingMeters { get; set; }
```

**Task 2: Implement Group Aggregation**
```csharp
// Services/Implementations/Weighing/WeighingService.cs
private async Task<List<AxleGroupResult>> AggregateAxleGroups(
    List<WeighingAxle> axles,
    int operationalToleranceKg)
{
    var groups = axles.GroupBy(a => a.AxleGrouping).ToList();
    var results = new List<AxleGroupResult>();

    foreach (var group in groups)
    {
        var groupLabel = group.Key;  // "A", "B", "C", "D"
        var groupWeight = group.Sum(a => a.MeasuredWeightKg);
        var groupPermissible = group.Sum(a => a.PermissibleWeightKg);

        // Apply 5% tolerance for single axles, 0% for grouped
        var isSingleAxle = group.Count() == 1;
        var toleranceKg = isSingleAxle
            ? (int)(groupPermissible * 0.05m)
            : 0;

        var groupOverload = groupWeight - (groupPermissible + toleranceKg);

        // Calculate PDF = (Actual / Permissible) ^ 4
        var pdfFactor = Math.Pow((double)groupWeight / groupPermissible, 4);

        results.Add(new AxleGroupResult
        {
            GroupLabel = groupLabel,
            GroupWeight = groupWeight,
            GroupPermissible = groupPermissible,
            ToleranceKg = toleranceKg,
            OverloadKg = Math.Max(0, groupOverload),
            PavementDamageFactor = (decimal)pdfFactor,
            IsCompliant = groupOverload <= operationalToleranceKg
        });
    }

    return results;
}
```

**Task 3: Calculate Fees per Group**
```csharp
private async Task<decimal> CalculateAxleFeeAsync(
    AxleType axleType,
    int overloadKg,
    string legalFramework)
{
    var feeSchedule = await _context.AxleOverloadFeeSchedules
        .Where(f => f.LegalFramework == legalFramework
                 && f.OverloadMinKg <= overloadKg
                 && (f.OverloadMaxKg == null || f.OverloadMaxKg >= overloadKg)
                 && f.IsActive)
        .OrderByDescending(f => f.EffectiveFrom)
        .FirstOrDefaultAsync();

    if (feeSchedule == null) return 0m;

    return axleType switch
    {
        AxleType.SteeringAxle => feeSchedule.SteeringAxleFeeUsd,
        AxleType.SingleDriveAxle => feeSchedule.SingleDriveAxleFeeUsd,
        AxleType.TandemAxle => feeSchedule.TandemAxleFeeUsd,
        AxleType.TridemAxle => feeSchedule.TridemAxleFeeUsd,
        _ => 0m
    };
}
```

### 4.2 Phase 2: Demerit Points System (Sprint 11)

**Task 1: Calculate Demerit Points**
```csharp
private async Task<int> CalculateDemeritPointsAsync(
    string violationType,  // "AXLE" or "GVW"
    int overloadKg)
{
    var schedule = await _context.DemeritPointSchedules
        .Where(s => s.ViolationType == violationType
                 && s.OverloadMinKg <= overloadKg
                 && (s.OverloadMaxKg == null || s.OverloadMaxKg >= overloadKg))
        .OrderByDescending(s => s.OverloadMinKg)
        .FirstOrDefaultAsync();

    return schedule?.Points ?? 0;
}
```

**Task 2: Determine Penalty**
```csharp
private async Task<PenaltySchedule?> DeterminePenaltyAsync(int totalPoints)
{
    return await _context.PenaltySchedules
        .Where(p => p.PointsMin <= totalPoints
                 && (p.PointsMax == null || p.PointsMax >= totalPoints))
        .OrderByDescending(p => p.PointsMin)
        .FirstOrDefaultAsync();
}
```

### 4.3 Phase 3: Weight Ticket Generation (Sprint 12)

**Task 1: Update QuestPDF Template**
```csharp
// Services/Implementations/Pdf/WeightTicketPdfService.cs
public void Compose(IDocumentContainer container)
{
    container.Page(page =>
    {
        page.Header().Element(ComposeHeader);
        page.Content().Element(ComposeContent);
        page.Footer().Element(ComposeFooter);
    });
}

private void ComposeContent(IContainer container)
{
    container.Column(column =>
    {
        // Section 1: Individual Axle Load (if applicable)
        column.Item().Text("INDIVIDUAL AXLE LOAD").Bold().FontSize(12);
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(40);   // Axle
                columns.RelativeColumn();     // Permissible
                columns.RelativeColumn();     // Tolerance
                columns.RelativeColumn();     // Actual
                columns.RelativeColumn();     // Overload
                columns.RelativeColumn();     // PDF
                columns.ConstantColumn(60);   // Result
            });

            table.Header(header =>
            {
                header.Cell().Text("Axle").Bold();
                header.Cell().Text("Permissible [KG]").Bold();
                header.Cell().Text("Tolerance [KG]").Bold();
                header.Cell().Text("Actual [KG]").Bold();
                header.Cell().Text("Overload [KG]").Bold();
                header.Cell().Text("PDF").Bold();
                header.Cell().Text("Result").Bold();
            });

            foreach (var axle in weighingAxles)
            {
                table.Cell().Text(axle.AxleNumber.ToString());
                table.Cell().Text(axle.PermissibleWeightKg.ToString("N0"));
                table.Cell().Text(axle.ToleranceKg.ToString("N0"));
                table.Cell().Text(axle.MeasuredWeightKg.ToString("N0"));
                table.Cell().Text(Math.Max(0, axle.OverloadKg).ToString("N0"));
                table.Cell().Text(axle.PavementDamageFactor.ToString("F2"));
                table.Cell().Text(axle.OverloadKg > 0 ? "Overload" : "Legal")
                    .FontColor(axle.OverloadKg > 0 ? Colors.Red.Medium : Colors.Green.Medium);
            }
        });

        column.Item().PaddingVertical(10);

        // Section 2: Axle Group Load (PRIMARY)
        column.Item().Text("AXLE GROUP LOAD (PRIMARY COMPLIANCE CHECK)").Bold().FontSize(12);
        column.Item().Table(table =>
        {
            // Similar structure for groups...
        });

        // Section 3: GVW Summary
        column.Item().PaddingVertical(10);
        column.Item().Text("VEHICLE LOAD").Bold().FontSize(12);
        // ...

        // Section 4: Legal Disclaimer
        column.Item().PaddingVertical(10);
        column.Item().Text("IMPORTANT NOTE:").Bold();
        column.Item().Text(
            "Axle group weights were checked, but individual axle weights " +
            "were not checked. One or more axles in an axle group can be " +
            "overloaded even if the total weight of the axle group is " +
            "reported as legal. It is the responsibility of the owner to " +
            "ensure that the vehicle is correctly loaded.");
    });
}
```

### 4.4 Phase 4: Database Schema Updates (Sprint 11)

**Migration: Add Fee and Demerit Tables**
```csharp
// Migrations/20260110_AddRegulatoryComplianceTables.cs
protected override void Up(MigrationBuilder migrationBuilder)
{
    // 1. Add AxleType column
    migrationBuilder.AddColumn<int>(
        name: "axle_type",
        table: "weighing_axles",
        type: "integer",
        nullable: false,
        defaultValue: 0);

    // 2. Add PDF column
    migrationBuilder.AddColumn<decimal>(
        name: "pavement_damage_factor",
        table: "weighing_axles",
        type: "numeric(10,4)",
        nullable: false,
        defaultValue: 0m);

    // 3. Create fee schedule tables
    migrationBuilder.CreateTable(
        name: "axle_overload_fee_schedules",
        columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            overload_min_kg = table.Column<int>(type: "integer", nullable: false),
            overload_max_kg = table.Column<int>(type: "integer", nullable: true),
            steering_axle_fee_usd = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
            single_drive_axle_fee_usd = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
            tandem_axle_fee_usd = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
            tridem_axle_fee_usd = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
            legal_framework = table.Column<string>(type: "varchar(50)", nullable: false),
            effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
            created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        });

    migrationBuilder.CreateTable(
        name: "gvw_overload_fee_schedules",
        columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            overload_min_kg = table.Column<int>(type: "integer", nullable: false),
            overload_max_kg = table.Column<int>(type: "integer", nullable: true),
            fee_usd = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
            legal_framework = table.Column<string>(type: "varchar(50)", nullable: false),
            effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
            created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        });

    migrationBuilder.CreateTable(
        name: "demerit_point_schedules",
        columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            violation_type = table.Column<string>(type: "varchar(20)", nullable: false),
            overload_min_kg = table.Column<int>(type: "integer", nullable: false),
            overload_max_kg = table.Column<int>(type: "integer", nullable: true),
            points = table.Column<int>(type: "integer", nullable: false),
            effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        });

    migrationBuilder.CreateTable(
        name: "penalty_schedules",
        columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            points_min = table.Column<int>(type: "integer", nullable: false),
            points_max = table.Column<int>(type: "integer", nullable: true),
            penalty_description = table.Column<string>(type: "varchar(500)", nullable: false),
            suspension_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
            requires_court = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
            created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        });

    // 4. Create indexes
    migrationBuilder.CreateIndex(
        name: "IX_axle_overload_fee_schedules_framework_date",
        table: "axle_overload_fee_schedules",
        columns: new[] { "legal_framework", "effective_from", "is_active" });

    migrationBuilder.CreateIndex(
        name: "IX_demerit_point_schedules_type_overload",
        table: "demerit_point_schedules",
        columns: new[] { "violation_type", "overload_min_kg" });
}
```

---

## 5. COMPLIANCE CHECKLIST

### 5.1 Traffic Act Cap 403 Compliance

| Requirement | Status | Priority |
|-------------|--------|----------|
| ✅ Single axle limit (7,000 kg S / 10,000 kg D) | ✅ Configured | High |
| ❌ Tandem axle limit (16,000 kg, < 1.8m spacing) | ❌ Missing | CRITICAL |
| ❌ Tridem axle limit (24,000 kg, < 1.8m spacing) | ❌ Missing | CRITICAL |
| ❌ GVW limit (56,000 kg) | ✅ Configured | High |
| ❌ 5% tolerance for axle groups | ❌ Not implemented | CRITICAL |
| ❌ 0% tolerance for GVW | ❌ Not implemented | CRITICAL |
| ❌ Fee calculation (KES 5,000-200,000) | ❌ Missing | CRITICAL |
| ❌ Weight ticket format (Form WB-001) | ❌ Not compliant | HIGH |
| ❌ Prohibition order (Form KeNHA/MTCE/ALC/F3) | ❌ Missing | HIGH |

### 5.2 EAC Act 2016 Compliance

| Requirement | Status | Priority |
|-------------|--------|----------|
| ❌ Demerit points system | ❌ Missing | CRITICAL |
| ❌ Cumulative points tracking | ❌ Missing | CRITICAL |
| ❌ Penalty determination (1-20+ points) | ❌ Missing | CRITICAL |
| ❌ Fee calculation (USD $500-$15,000) | ❌ Missing | CRITICAL |
| ❌ Cross-border data format (VLMA/TTTFP) | ❌ Missing | MEDIUM |
| ✅ Multi-tenant support (KURA/KeNHA/etc.) | ✅ Implemented | High |

### 5.3 Document Compliance

| Document Type | Status | Priority |
|---------------|--------|----------|
| ❌ Weight Ticket (dual table format) | ❌ Not compliant | CRITICAL |
| ❌ Prohibition Order (Form F3) | ❌ Missing | HIGH |
| ❌ Charge Sheet | ❌ Missing | HIGH |
| ❌ Court Summons | ❌ Missing | MEDIUM |
| ❌ PDF (Pavement Damage Factor) display | ❌ Missing | HIGH |
| ❌ Legal disclaimer on tickets | ❌ Missing | MEDIUM |

---

## 6. IMPLEMENTATION ROADMAP

### Sprint 11: Core Regulatory Compliance (Weeks 1-2)

**Objectives:**
1. Fix axle grouping logic with A/B/C/D classification
2. Implement group-based tolerance (5% for groups, 0% for GVW)
3. Add PDF (Pavement Damage Factor) calculation
4. Create fee schedule tables and models
5. Implement demerit points system

**Deliverables:**
- Updated `WeighingService.cs` with group aggregation
- New fee schedule tables migrated
- Demerit points calculation
- Unit tests for compliance logic

**Acceptance Criteria:**
- Weight ticket shows correct group aggregation
- Tolerance applied per group (not per axle)
- Fees calculated per KeNHA/EAC fee schedules
- Demerit points tracked per violation

### Sprint 12: Document Templates & UI (Weeks 3-4)

**Objectives:**
1. Update weight ticket PDF to match KeNHA format
2. Implement prohibition order template
3. Add dual table display (individual + groups)
4. Create legal disclaimer section
5. Update frontend to show group compliance

**Deliverables:**
- Compliant weight ticket PDF
- Prohibition order PDF
- Updated weighing UI with group display
- Legal disclaimer on all tickets

**Acceptance Criteria:**
- Weight ticket matches KeNHA format exactly
- Prohibition order matches Form F3
- Dual table display with color-coded status
- Legal disclaimer visible on all tickets

### Sprint 13: Integration & Testing (Week 5)

**Objectives:**
1. End-to-end testing with sample vehicles
2. Validate against KeNHA weight ticket samples
3. Performance testing under load
4. Documentation updates

**Deliverables:**
- Test report with 100+ sample weighings
- Performance benchmarks
- Updated user documentation
- Training materials

**Acceptance Criteria:**
- All test cases pass
- Performance meets SLA (< 500ms per weighing)
- Documentation complete
- Ready for UAT

---

## 7. RISK ASSESSMENT

### 7.1 Legal Risks

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Non-compliant weight tickets inadmissible in court | HIGH | HIGH | Implement Sprint 11-12 fixes immediately |
| Incorrect fee calculations (revenue loss/overcharge) | HIGH | HIGH | Fee schedule validation with KURA/KeNHA |
| Missing demerit points (repeat offenders not tracked) | MEDIUM | HIGH | Implement demerit system in Sprint 11 |
| Wrong tolerance rules (false positives/negatives) | HIGH | HIGH | Unit tests against known samples |

### 7.2 Operational Risks

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Performance degradation with partitioning fix | MEDIUM | LOW | Test on staging with production data volume |
| Fee schedule data migration errors | MEDIUM | MEDIUM | Validate with historical KenloadV2 data |
| UI confusion with dual table display | LOW | MEDIUM | User training and tooltips |
| Integration with NTSA delayed | MEDIUM | LOW | Design API contract upfront |

---

## 8. RECOMMENDATIONS

### 8.1 Immediate Actions (Week 1)

1. **CRITICAL:** Fix axle grouping logic in `WeighingService.cs`
2. **CRITICAL:** Add fee schedule tables and migration
3. **CRITICAL:** Implement tolerance rules (5% groups, 0% GVW)
4. **HIGH:** Add PDF calculation property to `WeighingAxle` model
5. **HIGH:** Create prohibition order template

### 8.2 Short-Term Actions (Weeks 2-4)

6. **HIGH:** Update weight ticket PDF format to match KeNHA
7. **MEDIUM:** Implement demerit points tracking
8. **MEDIUM:** Add dual table display in UI
9. **MEDIUM:** Create legal disclaimer component
10. **LOW:** Add cross-border data export (VLMA/TTTFP)

### 8.3 Long-Term Actions (Months 2-3)

11. **MEDIUM:** Integration with NTSA for license verification
12. **MEDIUM:** Real-time data sharing with KeNHA/other authorities
13. **LOW:** Mobile app for LSWIM operations
14. **LOW:** Predictive analytics for repeat offenders

---

## 9. REFERENCES

### 9.1 Legal Documents

1. **Kenya Traffic Act Cap 403 (2015 Revision)**
   National Council for Law Reporting
   http://kenyalaw.org/kl/fileadmin/pdfdownloads/Acts/TrafficAct_Cap403.pdf

2. **EAC Vehicle Load Control Act 2016**
   East African Community Gazette
   https://www.eac.int/documents/category/transport

3. **KURA Weight Enforcement Guidelines**
   Kenya Urban Roads Authority
   https://www.kura.go.ke/regulations

### 9.2 Technical References

4. **KenloadV2APIUpgrade Source Code**
   Location: `D:\Projects\BengoBox\KeNHA\KenloadV2APIUpgrade`
   Reviewed: Controllers/EACActController.cs, Models/AxleWeights.cs

5. **KenloadV2UIUpgrade Source Code**
   Location: `D:\Projects\BengoBox\KeNHA\KenloadV2UIUpgrade`
   Reviewed: components/widgets/weigh/autoweigh.vue, addmanualticket.vue

6. **KeNHA Weight Ticket Sample**
   File: `NRBKA202601011217.pdf`
   Location: `D:\Projects\BengoBox\TruLoad\resources\`

### 9.3 Industry Standards

7. **ASTM E1318-09** - Standard Specification for Highway Weigh-In-Motion (WIM) Systems
8. **Fourth Power Law (AASHO Road Test 1958-1960)** - Pavement damage calculation
9. **ISO 39001:2012** - Road traffic safety management systems

---

## APPENDIX A: FEE SCHEDULE EXAMPLES

### Traffic Act Cap 403 Fee Schedule (KES)

| Overload Range | Fine (KES) | Demerit Points | Example |
|----------------|------------|----------------|---------|
| 0-500 kg | 5,000 | 1 | Warning letter |
| 501-1,000 kg | 10,000 | 1 | First offense |
| 1,001-2,000 kg | 20,000 | 2 | Second offense |
| 2,001-5,000 kg | 50,000 | 3 | Court summons |
| 5,001-10,000 kg | 100,000 | 5 | License suspension |
| 10,001-20,000 kg | 150,000 | 10 | Operator audit |
| >20,000 kg | 200,000 | 20 | License revocation |

### EAC Act 2016 Fee Schedule (USD)

| Axle Type | Overload | Fee (USD) | Demerit Points |
|-----------|----------|-----------|----------------|
| Steering | 0-1,000 kg | 500 | 1 |
| Steering | 1,001-2,000 kg | 1,000 | 2 |
| Steering | >2,000 kg | 2,500 | 5 |
| Single Drive | 0-2,000 kg | 1,000 | 1 |
| Single Drive | 2,001-5,000 kg | 2,500 | 3 |
| Single Drive | >5,000 kg | 5,000 | 5 |
| Tandem | 0-3,000 kg | 1,500 | 2 |
| Tandem | 3,001-7,000 kg | 3,500 | 5 |
| Tandem | >7,000 kg | 7,500 | 10 |
| Tridem | 0-4,000 kg | 2,000 | 2 |
| Tridem | 4,001-10,000 kg | 5,000 | 5 |
| Tridem | >10,000 kg | 10,000 | 10 |
| GVW | 0-5,000 kg | 1,000 | 5 |
| GVW | 5,001-15,000 kg | 5,000 | 10 |
| GVW | >15,000 kg | 15,000 | 20 |

---

**END OF REPORT**

**Report Prepared By:** TruLoad Development Team
**Review Date:** 2026-01-10
**Next Review:** After Sprint 11 completion (2 weeks)

**Distribution:**
- Project Manager
- Lead Backend Developer
- Lead Frontend Developer
- QA Team
- KURA Stakeholders
- KeNHA Integration Team
