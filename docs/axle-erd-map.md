# Axle System Entity Relationship Map

## Overview
The axle system implements a **5-tier hierarchical model** with **reference tables** (master data) and **transaction tables** (weighing events).

---

## Entity Relationship Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    REFERENCE TABLES (Master Data)                │
└─────────────────────────────────────────────────────────────────┘

┌──────────────┐
│  TyreType    │ (3 records: S, D, W)
│  - Code      │ Single source for tyre classification
│  - Name      │ S=Single(7500kg), D=Dual(10000kg), W=Wide(8000kg)
│  - MaxWeight │
└──────┬───────┘
       │
       │ (Optional FK, SetNull on delete)
       ↓
┌──────────────┐
│  AxleGroup   │ (10 records: S1, SA4, SA6, TAG8, QAG16, etc.)
│  - Code      │ Defines axle grouping rules & spacing
│  - Weight    │ Typical: 6000-10000 kg
│  - Spacing   │ MinSpacingFeet, MaxSpacingFeet (nullable)
│  - AxleCount │ Number of axles in group (1-4)
└──────┬───────┘
       │
       │ (Required FK, Restrict on delete)
       ↓
┌────────────────────┐
│ AxleConfiguration  │ (48 standard + user-created derived)
│ - AxleCode         │ UNIQUE: "2*", "3A", "5*S|DD|DD|"
│ - AxleNumber       │ Total axles (2-7)
│ - GvwPermissibleKg │ Gross Vehicle Weight limit
│ - IsStandard       │ TRUE = EAC immutable, FALSE = user custom
│ - LegalFramework   │ EAC | TRAFFIC_ACT | BOTH
│ - CreatedByUserId  │ NULL for standard, User GUID for derived
└────────┬───────────┘
         │
         │ (Parent-Child: 1-to-Many, Cascade delete)
         │ UNIQUE constraint: (AxleConfigurationId, AxlePosition)
         ↓
┌────────────────────────┐
│ AxleWeightReference    │ (48+ records, position-specific)
│ - AxlePosition         │ Sequential: 1, 2, 3, ... N
│ - LegalWeightKg        │ Permissible weight for this axle
│ - AxleGroupId     (FK) │ → AxleGroup (REQUIRED, Restrict)
│ - TyreTypeId      (FK) │ → TyreType (OPTIONAL, SetNull)
│ - AxleGrouping         │ A=Front, B=Coupling, C=Mid, D=Rear
└────────┬───────────────┘
         │
         │ (Referenced by business logic, no direct FK)
         ↓
┌────────────────────────┐
│  AxleFeeSchedule       │ (10 tiered schedules)
│  - LegalFramework      │ EAC | TRAFFIC_ACT
│  - FeeType             │ GVW | AXLE
│  - OverloadMinKg       │ Range start (e.g., 1, 1001, 2001)
│  - OverloadMaxKg       │ Range end (NULL = unlimited)
│  - FeePerKgUsd         │ Fee calculation rate
│  - DemeritPoints       │ 3-20 points based on severity
│  - EffectiveFrom/To    │ Versioning support
└────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                   TRANSACTION TABLES (Live Data)                 │
└─────────────────────────────────────────────────────────────────┘

┌────────────────────────┐
│      Weighing          │ (Future implementation)
│      - VehicleId       │ → vehicles(id)
│      - DriverId        │ → drivers(id) [PLANNED]
│      - StationId       │ → stations(id)
│      - GVW             │ Total measured weight
│      - TicketNo        │ UNIQUE weight ticket number
└────────┬───────────────┘
         │
         │ (Parent-Child: 1-to-Many, Cascade delete)
         │ UNIQUE constraint: (WeighingId, AxleNumber)
         ↓
┌────────────────────────────────┐
│      WeighingAxle              │ Per-axle measurement records
│      - AxleNumber              │ Position during weighing (1, 2, 3...)
│      - MeasuredWeightKg        │ Actual scale reading
│      - PermissibleWeightKg     │ Legal limit (from reference)
│      - OverloadKg (computed)   │ = Measured - Permissible
│      - AxleConfigurationId (FK)│ → AxleConfiguration (Restrict)
│      - AxleWeightReferenceId   │ → AxleWeightReference (SetNull)
│      - AxleGroupId        (FK) │ → AxleGroup (Restrict)
│      - TyreTypeId        (FK) │ → TyreType (SetNull)
│      - FeeUsd (calculated)     │ From AxleFeeSchedule lookup
│      - CapturedAt              │ Timestamp of measurement
└────────────────────────────────┘
```

---

## Relationship Details

### 1. TyreType → AxleWeightReference / WeighingAxle
- **Cardinality**: 1-to-Many (Optional)
- **Delete Behavior**: SET NULL (preserve historical data)
- **Purpose**: Classify tyre configuration affecting weight capacity
- **Validation**: None required (nullable relationship)

### 2. AxleGroup → AxleWeightReference / WeighingAxle
- **Cardinality**: 1-to-Many (Required)
- **Delete Behavior**: RESTRICT (cannot delete if in use)
- **Purpose**: Define axle classification with spacing rules
- **Validation**: 
  - Must be active (`is_active = TRUE`)
  - Spacing rules apply only if `AxleCountInGroup > 1`

### 3. AxleConfiguration → AxleWeightReference
- **Cardinality**: 1-to-Many (Required, Composite Unique)
- **Delete Behavior**: CASCADE (references deleted with config)
- **Purpose**: Template defining vehicle axle pattern
- **Validation**:
  - Must have exactly `AxleNumber` references
  - Each reference position must be 1 through `AxleNumber`
  - Standard configs (`IsStandard = TRUE`) cannot be deleted
  - Derived configs with weighing history → soft delete only

### 4. AxleConfiguration → WeighingAxle
- **Cardinality**: 1-to-Many (Required)
- **Delete Behavior**: RESTRICT (preserve audit trail)
- **Purpose**: Link weighing to template configuration
- **Validation**: Must reference active configuration

### 5. AxleWeightReference → WeighingAxle
- **Cardinality**: 1-to-Many (Optional)
- **Delete Behavior**: SET NULL (preserve weighing data)
- **Purpose**: Link to specific position specification
- **Validation**: Position must match within configuration

### 6. User → AxleConfiguration (Derived only)
- **Cardinality**: 1-to-Many (Optional)
- **Delete Behavior**: RESTRICT (preserve creator reference)
- **Purpose**: Track who created custom configurations
- **Validation**: 
  - `IsStandard = FALSE` → `CreatedByUserId` MUST be set
  - `IsStandard = TRUE` → `CreatedByUserId` MUST be NULL

---

## Data Flow: Weighing Interface Workflow

### Step 1: Configuration Selection
```
Frontend: Officer selects vehicle axle pattern
↓
GET /api/axle-configurations?standard=true&activeOnly=true
↓
Returns: List of AxleConfiguration with AxleNumber, GVW, LegalFramework
```

### Step 2: Load Weight References
```
Backend: Fetch AxleWeightReference for selected config
↓
SELECT * FROM axle_weight_references 
WHERE axle_configuration_id = @configId
ORDER BY axle_position
↓
Returns: N references (N = AxleNumber) with:
  - AxlePosition
  - LegalWeightKg (permissible)
  - AxleGroupId, TyreTypeId, AxleGrouping
```

### Step 3: Display Weighing Interface
```
Frontend UI shows:
Axle 1 (Front - Single):    [_____ kg] / 8,000 kg (S1)
Axle 2 (Coupling - Dual):   [_____ kg] / 9,000 kg (TAG8)
Axle 3 (Coupling - Dual):   [_____ kg] / 9,000 kg (TAG8)
                            ─────────────────────────────
Total GVW:                  [_____ kg] / 26,000 kg
```

### Step 4: Capture Measurements
```
Officer enters actual weights from scale:
Axle 1: 8,500 kg  → Overload: +500 kg
Axle 2: 9,800 kg  → Overload: +800 kg
Axle 3: 9,200 kg  → Overload: +200 kg
Total:  27,500 kg → GVW Overload: +1,500 kg
```

### Step 5: Calculate Fees
```
For each axle:
  1. Lookup AxleFeeSchedule:
     WHERE legal_framework = @framework
       AND fee_type = 'AXLE'
       AND overload_kg BETWEEN overload_min_kg AND overload_max_kg
  2. Calculate: overloadKg * feePerKgUsd
  3. Accumulate demerit points

For GVW:
  1. Lookup AxleFeeSchedule (fee_type = 'GVW')
  2. Calculate: gvwOverloadKg * feePerKgUsd

Total Fee = MAX(GVW fee, SUM(axle fees)) [EAC rule]
```

### Step 6: Create WeighingAxle Records
```
For each measured axle:
  INSERT INTO weighing_axles (
    weighing_id,
    axle_number,
    measured_weight_kg,
    permissible_weight_kg,        ← from AxleWeightReference
    axle_configuration_id,
    axle_weight_reference_id,
    axle_group_id,                ← from AxleWeightReference
    axle_grouping,                ← from AxleWeightReference
    tyre_type_id,                 ← from AxleWeightReference (nullable)
    fee_usd,                      ← calculated from AxleFeeSchedule
    captured_at
  )
```

---

## Validation Rules

### Configuration Level
- `AxleCode` must be UNIQUE across all configurations
- `AxleNumber` must match count of `AxleWeightReferences`
- `LegalFramework` must be in ('EAC', 'TRAFFIC_ACT', 'BOTH')
- `GvwPermissibleKg` must be ≥ SUM(AxleWeightReference.LegalWeightKg)
- Standard configs: `IsStandard = TRUE` AND `CreatedByUserId IS NULL`
- Derived configs: `IsStandard = FALSE` AND `CreatedByUserId IS NOT NULL`

### Reference Level
- `AxlePosition` must be UNIQUE within `AxleConfigurationId`
- `AxlePosition` must be between 1 and parent `AxleConfiguration.AxleNumber`
- `AxleGrouping` must be in ('A', 'B', 'C', 'D')
- `AxleLegalWeightKg` must be > 0
- `AxleLegalWeightKg` should not exceed `AxleGroup.TypicalWeightKg` by >10%

### Weighing Level
- `(WeighingId, AxleNumber)` must be UNIQUE
- `AxleNumber` must match count in `AxleConfiguration.AxleNumber`
- `MeasuredWeightKg` must be > 0
- `PermissibleWeightKg` must match `AxleWeightReference.LegalWeightKg`
- `AxleConfigurationId` must reference active configuration

---

## Legal Framework Integration

### EAC (East African Community) — USD Fees
- **Tolerance**: DB-driven via ToleranceSetting (default 5% for axle, configurable)
- **Axle-type fees**: From `AVWoverloadCharges` table — per-axle-type flat USD fees
  - Steering, SingleDrive, Tandem, Tridem each have different rates at 100kg thresholds
- **GVW fees**: From `GVWoverloadCharges` table — flat USD fee per threshold
- **KES conversion**: At runtime using DollarRate
- **Demerit Points**: 3-15 points per violation type

### Kenya Traffic Act Cap 403 — Native KES Fees
- **Tolerance**: DB-driven via ToleranceSetting (configurable per-framework)
- **GVW fee structure**: Flat KES penalties (NOT per-kg rates):
  - 1,000 kg → KSh 10,000
  - 2,000 kg → KSh 20,000
  - 3,000 kg → KSh 30,000
  - 5,000 kg → KSh 60,000
  - 10,000 kg → KSh 350,000
  - 10,001+ kg → KSh 400,000 (max)
- **Axle-type fees**: Traffic Act does NOT differentiate by axle type — all types get same flat KES fee per band
- **Source**: KenloadV2 `trafficoverloadCharges` table
- **Demerit Points**: 1-12 points (per violation type)

### Permissible Weight Limits (Both Acts)
| Tyre Type | Steering (Group A) | Single Drive | Tandem (per axle) | Description |
|-----------|-------------------|--------------|-------------------|-------------|
| S (Single) | 8,000 kg | 7,500 kg | 7,500 kg | Single tyre per axle end |
| D (Dual) | 10,000 kg | 10,000 kg | 9,000 kg | Dual/twin tyres per axle end |
| W (Wide) | 8,000 kg | 8,000 kg | 8,000 kg | Wide single super tyre |

### Axle Group Limits
| Group Type | Axle Count | Max Combined Weight |
|-----------|-----------|-------------------|
| Steering (S1) | 1 | 8,000-10,000 kg |
| Single Drive (SA4) | 1 | 10,000 kg |
| Tandem (TAG8) | 2 | 18,000 kg |
| Tridem (TAG12) | 3 | 24,000 kg |
| Quad (QAG16) | 4 | 32,000 kg |
| **Max GVW** | — | **56,000 kg** |

---

## Index Strategy (Performance Optimization)

### Hot Path Queries
```sql
-- Configuration lookup
CREATE INDEX idx_axle_configurations_code ON axle_configurations(axle_code);
CREATE INDEX idx_axle_configurations_standard ON axle_configurations(is_standard, is_active);

-- Reference lookup by config
CREATE INDEX idx_axle_weight_ref_config ON axle_weight_references(axle_configuration_id);
CREATE INDEX idx_axle_weight_ref_config_position ON axle_weight_references(axle_configuration_id, axle_position);

-- Weighing axle queries
CREATE INDEX idx_weighing_axles_weighing ON weighing_axles(weighing_id);
CREATE INDEX idx_weighing_axles_configuration ON weighing_axles(axle_configuration_id);

-- Fee schedule lookup
CREATE INDEX idx_axle_fee_schedule_lookup ON axle_fee_schedules(legal_framework, fee_type, overload_min_kg);
CREATE INDEX idx_axle_fee_schedule_effective ON axle_fee_schedules(effective_from, effective_to);
```

---

## Future Extensions

### Planned: Driver Integration
```
┌────────────────┐
│    Driver      │ (NEW - demerit points tracking)
│    - NtsaId    │
│    - LicenseNo │
│    - DemeritPts│ Accumulation from violations
└────────┬───────┘
         │
         │ (1-to-Many)
         ↓
┌──────────────────────┐
│ DriverDemeritRecord  │ (NEW - violation history)
│ - DriverId      (FK) │
│ - CaseRegisterId     │
│ - PointsAssigned     │ From AxleFeeSchedule.DemeritPoints
│ - ViolationDate      │
│ - PointsExpiryDate   │ +36 months
└──────────────────────┘
```

### Planned: Permit Integration
```
┌────────────────┐
│    Permit      │ (Extends PermitType reference)
│    - PermitNo  │
│    - VehicleId │
│    - ValidFrom │
│    - ValidTo   │
└────────────────┘
```

---

## Summary: 5-Tier Architecture

1. **TyreType** (Foundation) → Tyre classification
2. **AxleGroup** (Classification) → Grouping rules with spacing
3. **AxleConfiguration** (Template) → Vehicle axle patterns
4. **AxleWeightReference** (Specification) → Position-specific limits
5. **WeighingAxle** (Transaction) → Actual measurements

**Key Design Principles:**
- ✅ Unified table for standard + derived configurations
- ✅ Referential integrity with appropriate delete behaviors
- ✅ Dual legal framework support (EAC + Traffic Act)
- ✅ Immutable standard configs, auditable derived configs
- ✅ Performance-optimized with strategic indexes
- ✅ Extensible for future driver/permit integration