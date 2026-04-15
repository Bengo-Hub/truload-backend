# TruLoad E2E Compliance Test Results

**Date**: 2026-04-14 19:57:38
**Environment**: https://kuraweighapitest.masterspace.co.ke

---

## Scenario 1: Standard Overload Workflow

```

======================================================================
  TRULOAD COMPLIANCE E2E TEST
  Target: https://kuraweighapitest.masterspace.co.ke
  Started: 2026-04-14T16:57:38.653666Z
======================================================================

  Workflow: Overload -> Case+Yard -> Prosecution -> Invoice
         -> Payment -> Memo -> Reweigh -> Cert + Close


======================================================================
  STEP 1: Login
======================================================================

  [ERROR] Login
    -> Login failed: 500 {"error":{"code":"INTERNAL_SERVER_ERROR","message":"An unexpected error occurred","details":null,"traceId":"0HNKQ8OR3017N:00000001","timestamp":"2026-04-14T16:57:43.6395011Z"}}

  *** ABORTING: Critical step 1 failed ***


======================================================================
  E2E TEST SUMMARY
======================================================================
  Step  Status   Name
  ---   ------   --------------------------------------------------
  1     [FAIL]   Login

  ============================================================
  TOTAL: 1  |  PASS: 0  |  FAIL: 1

  1 STEP(S) FAILED

  Collected IDs:
    weighingId: ---
    caseId: ---
    yardEntryId: ---
    prosecutionId: ---
    invoiceId: ---
    receiptId: ---
    reweighId: ---
    driverId: ---
    transporterId: ---
======================================================================

STDERR:
  print(f"  Started: {datetime.utcnow().isoformat()}Z")```

---

## Scenario 2: Compliant Vehicle (No Overload)

```

======================================================================
  TRULOAD COMPLIANCE E2E TEST -- SCENARIO 2
  Target: https://kuraweighapitest.masterspace.co.ke
  Started: 2026-04-14T16:57:44.046537Z
======================================================================

  Workflow: Within-Tolerance Overload -> Warning -> Auto Special Release -> Case Closed


======================================================================
  STEP 1: Login
======================================================================
    POST /auth/login -> 200
    userId:    019cae52-0015-7924-b0a8-53e78d1187d7
    stationId: 4d7169a1-7f43-46bf-813c-5641c1ce8c6b

  [PASS] Login
    -> Logged in as gadmin@masterspace.co.ke

======================================================================
  STEP 2: Setup metadata (driver, transporter, cargo, locations)
======================================================================
    POST /drivers -> 400
    Driver fallback: N/A
    POST /transporters -> 201
    Cargo type found: Agricultural Produce
    Origin: ATHI RIVER
    Destination: BERIKA

  [PASS] Setup metadata (driver, transporter, cargo, locations)
    -> Metadata: driverId=OK, transporterId=OK, cargoId=OK, originId=OK, destinationId=OK

======================================================================
  STEP 3: Create scale test
======================================================================
    POST /scale-tests -> 500

  [FAIL] Create scale test
    -> Failed: 500 

  *** ABORTING: Critical step 3 failed ***


======================================================================
  E2E TEST SUMMARY -- SCENARIO 2: WITHIN-TOLERANCE AUTO SPECIAL RELEASE
======================================================================
  Step  Status   Name
  ---   ------   --------------------------------------------------
  1     [PASS]   Login
  2     [PASS]   Setup metadata (driver, transporter, cargo, locations)
  3     [FAIL]   Create scale test

  ============================================================
  TOTAL: 3  |  PASS: 2  |  FAIL: 1

  1 STEP(S) FAILED

  Collected IDs:
    weighingId: ---
    vehicleId: ---
    caseId: ---
    specialReleaseId: ---
    scaleTestId: ---
    driverId: ba54a2f1-6148-4baf-8e86-04cc4785236c
    transporterId: 526b1ebe-68e0-43dc-aa23-73f6356dd8cc
======================================================================

STDERR:
  print(f"  Started: {datetime.utcnow().isoformat()}Z")```

---

## Scenario 3: Tag-Hold and Yard Release

```

======================================================================
  TRULOAD COMPLIANCE E2E TEST -- SCENARIO 3
  Target: https://kuraweighapitest.masterspace.co.ke
  Started: 2026-04-14T16:58:08.225429Z
======================================================================

  Workflow: Manual KeNHA Tag -> Compliant Weight + TagHold
         -> Yard Hold -> Tag Resolution -> Special Release


======================================================================
  STEP 1: Login
======================================================================
    POST /auth/login -> CACHE
    userId:    019cae52-0015-7924-b0a8-53e78d1187d7
    stationId: 4d7169a1-7f43-46bf-813c-5641c1ce8c6b

  [PASS] Login
    -> Logged in as gadmin@masterspace.co.ke

======================================================================
  STEP 2: Setup metadata (driver, transporter, cargo, locations)
======================================================================
    POST /drivers -> 400
    Driver fallback: N/A
    Transporter found: E2E Test Transporters Ltd
    Cargo type found: Agricultural Produce
    Origin: ATHI RIVER
    Destination: BERIKA

  [PASS] Setup metadata (driver, transporter, cargo, locations)
    -> Metadata: driverId=OK, transporterId=OK, cargoId=OK, originId=OK, destinationId=OK

======================================================================
  STEP 3: Fetch tag categories
======================================================================
    GET /vehicle-tags/categories -> 200
    Available categories: 10
      - COURT_ORDER: Court Order Hold (id=23bb2324-c765-4817-afc0-ade0e0f440f8)
      - CUSTOMS_HOLD: Customs Hold (id=42a6edd8-72a8-4a95-95f2-e4961276a079)
      - HABITUAL_OFFENDER: Habitual Offender (id=c2e028ea-e170-4fb5-b055-e1ca88039519)
      - INSPECTION_DUE: Inspection Due (id=0346fc83-7a67-4bc0-a7b5-e11179c129e9)
      - INSURANCE_EXPIRED: Insurance Expired (id=ba901752-0cba-4941-bb1b-a1e0c4875648)
    Selected: COURT_ORDER / Court Order Hold

  [PASS] Fetch tag categories
    -> Tag category: COURT_ORDER (23bb2324-c765-4817-afc0-ade0e0f440f8)

======================================================================
  STEP 4: Create manual KeNHA vehicle tag
======================================================================
    POST /vehicle-tags -> 500

  [ERROR] Create manual KeNHA vehicle tag
    -> Tag creation failed: 500 

  *** ABORTING: Critical step 4 failed ***


======================================================================
  E2E TEST SUMMARY -- SCENARIO 3: MANUAL KeNHA TAG -> YARD HOLD + SPECIAL RELEASE
======================================================================
  Step  Status   Name
  ---   ------   --------------------------------------------------
  1     [PASS]   Login
  2     [PASS]   Setup metadata (driver, transporter, cargo, locations)
  3     [PASS]   Fetch tag categories
  4     [FAIL]   Create manual KeNHA vehicle tag

  ============================================================
  TOTAL: 4  |  PASS: 3  |  FAIL: 1

  1 STEP(S) FAILED

  Collected IDs:
    weighingId: ---
    vehicleId: ---
    tagId: ---
    caseId: ---
    yardEntryId: ---
    specialReleaseId: ---
    adminDiscretionReleaseTypeId: ---
    scaleTestId: ---
    driverId: ba54a2f1-6148-4baf-8e86-04cc4785236c
    transporterId: 526b1ebe-68e0-43dc-aa23-73f6356dd8cc
======================================================================

STDERR:
  print(f"  Started: {datetime.utcnow().isoformat()}Z")```

---

## Scenario 4: Permit-Based Exemption

```

======================================================================
  TRULOAD COMPLIANCE E2E TEST -- SCENARIO 4
  Target: https://kuraweighapitest.masterspace.co.ke
  Started: 2026-04-14T16:58:19.247897Z
======================================================================

  Workflow: Compliant Vehicle -> Weight Ticket Only (No Case, No Yard, No Prosecution)


======================================================================
  STEP 1: Login
======================================================================
    POST /auth/login -> CACHE
    userId:    019cae52-0015-7924-b0a8-53e78d1187d7
    stationId: 4d7169a1-7f43-46bf-813c-5641c1ce8c6b

  [PASS] Login
    -> Logged in as gadmin@masterspace.co.ke

======================================================================
  STEP 2: Setup metadata (driver, transporter, cargo, locations)
======================================================================
    POST /drivers -> 400
    Driver fallback: N/A
    Transporter found: E2E Test Transporters Ltd
    Cargo type found: Agricultural Produce
    Origin: ATHI RIVER
    Destination: BERIKA

  [PASS] Setup metadata (driver, transporter, cargo, locations)
    -> Metadata: driverId=OK, transporterId=OK, cargoId=OK, originId=OK, destinationId=OK

======================================================================
  STEP 3: Create scale test
======================================================================
    POST /scale-tests -> 500

  [FAIL] Create scale test
    -> Failed: 500 

  *** ABORTING: Critical step 3 failed ***


======================================================================
  E2E TEST SUMMARY -- SCENARIO 4: COMPLIANT VEHICLE
======================================================================
  Step  Status   Name
  ---   ------   --------------------------------------------------
  1     [PASS]   Login
  2     [PASS]   Setup metadata (driver, transporter, cargo, locations)
  3     [FAIL]   Create scale test

  ============================================================
  TOTAL: 3  |  PASS: 2  |  FAIL: 1

  1 STEP(S) FAILED

  Collected IDs:
    weighingId: ---
    vehicleId: ---
    scaleTestId: ---
    driverId: ba54a2f1-6148-4baf-8e86-04cc4785236c
    transporterId: 526b1ebe-68e0-43dc-aa23-73f6356dd8cc
======================================================================

STDERR:
  print(f"  Started: {datetime.utcnow().isoformat()}Z")```

---

## Scenario 5: Court Escalation Path

```

======================================================================
  TRULOAD COMPLIANCE E2E TEST -- SCENARIO 5
  Target: https://kuraweighapitest.masterspace.co.ke
  Started: 2026-04-14T16:58:29.767681Z
======================================================================

  Workflow: Overload -> Case+Yard -> Prosecution+Invoice
         -> Court Escalation (No Payment, No Release)


======================================================================
  STEP 1: Login
======================================================================
    POST /auth/login -> CACHE
    userId:    019cae52-0015-7924-b0a8-53e78d1187d7
    stationId: 4d7169a1-7f43-46bf-813c-5641c1ce8c6b

  [PASS] Login
    -> Logged in as gadmin@masterspace.co.ke

======================================================================
  STEP 2: Setup metadata (driver, transporter, cargo, locations)
======================================================================
    POST /drivers -> 201
    Transporter found: E2E Test Transporters Ltd
    Cargo type found: Agricultural Produce
    Origin: ATHI RIVER
    Destination: BERIKA

  [PASS] Setup metadata (driver, transporter, cargo, locations)
    -> Metadata: driverId=OK, transporterId=OK, cargoId=OK, originId=OK, destinationId=OK

======================================================================
  STEP 3: Create scale test
======================================================================
    POST /scale-tests -> 500

  [FAIL] Create scale test
    -> Failed: 500 

  *** ABORTING: Critical step 3 failed ***


======================================================================
  E2E TEST SUMMARY -- SCENARIO 5: COURT ESCALATION
======================================================================
  Step  Status   Name
  ---   ------   --------------------------------------------------
  1     [PASS]   Login
  2     [PASS]   Setup metadata (driver, transporter, cargo, locations)
  3     [FAIL]   Create scale test

  ============================================================
  TOTAL: 3  |  PASS: 2  |  FAIL: 1

  1 STEP(S) FAILED

  Collected IDs:
    weighingId: ---
    caseId: ---
    yardEntryId: ---
    prosecutionId: ---
    invoiceId: ---
    courtEscalationDispositionId: ---
    prohibitionOrderId: ---
    driverId: 53152151-f602-472c-85d8-942ec9c61ffd
    transporterId: 526b1ebe-68e0-43dc-aa23-73f6356dd8cc
======================================================================

STDERR:
  print(f"  Started: {datetime.utcnow().isoformat()}Z")```

---

## Scenario 6: Full Court Case Lifecycle

```

======================================================================
  TRULOAD COMPLIANCE E2E TEST -- SCENARIO 6
  Full Court Case Lifecycle
  Target: https://kuraweighapitest.masterspace.co.ke
  Started: 2026-04-14T16:58:40.702310Z
======================================================================

  Workflow: Metadata -> Autoweigh -> Overload -> Case+Yard
         -> Escalate -> Court -> IO -> Parties -> Subfiles
         -> Hearings -> Warrants -> Closure -> Review -> Close


======================================================================
  STEP 1: Login
======================================================================
    POST /auth/login -> CACHE
    userId:    019cae52-0015-7924-b0a8-53e78d1187d7
    stationId: 4d7169a1-7f43-46bf-813c-5641c1ce8c6b

  [PASS] Login
    -> Logged in as gadmin@masterspace.co.ke

======================================================================
  STEP 2: Setup metadata (driver, transporter, cargo, locs)
======================================================================
    Driver found: John E2E (53152151-f602-472c-85d8-942ec9c61ffd)
    Transporter found: E2E Test Transporters Ltd
    Cargo type found: Agricultural Produce
    Origin: ATHI RIVER
    Destination: BERIKA

  [PASS] Setup metadata (driver, transporter, cargo, locs)
    -> Metadata: driverId=OK, transporterId=OK, cargoId=OK, originId=OK, destinationId=OK

======================================================================
  STEP 3: Create scale test
======================================================================
    POST /scale-tests -> 500

  [FAIL] Create scale test
    -> Failed: 500 

  *** ABORTING: Critical step 3 failed ***


======================================================================
  E2E TEST SUMMARY -- SCENARIO 6: Full Court Case Lifecycle
======================================================================
  Step  Status   Name
  ---   ------   --------------------------------------------------
  1     [PASS]   Login
  2     [PASS]   Setup metadata (driver, transporter, cargo, locs)
  3     [FAIL]   Create scale test

  ============================================================
  TOTAL: 3  |  PASS: 2  |  FAIL: 1

  1 STEP(S) FAILED

  Collected IDs:
    weighingId: ---
    vehicleId: ---
    caseId: ---
    caseNo: ---
    yardEntryId: ---
    courtId: ---
    assignmentId: ---
    hearingId1: ---
    hearingId2: ---
    warrantId: ---
    subfileBId: ---
    subfileDId: ---
    subfileFId: ---
    subfileGId: ---
    closureTypeId: ---
    courtDispositionTypeId: ---
    convictedOutcomeId: ---
    scaleTestId: ---
    driverId: 53152151-f602-472c-85d8-942ec9c61ffd
    transporterId: 526b1ebe-68e0-43dc-aa23-73f6356dd8cc
    userId: 019cae52-0015-7924-b0a8-53e78d1187d7
======================================================================

STDERR:
  print(f"  Started: {datetime.utcnow().isoformat()}Z")```

---

## Scenario 7: Repeat Offender Multiple Overloads

```

======================================================================
  TRULOAD E2E SCENARIO 7: REPEAT OFFENDER -- MULTIPLE OVERLOADS
  Target: https://kuraweighapitest.masterspace.co.ke
  Started: 2026-04-14T16:58:47.961578Z
======================================================================

  Vehicle: KDG 999R
  Workflow: 3x (Autoweigh -> Capture -> Case -> Prosecution)
         -> Verify top offenders + axle violations


======================================================================
  STEP 1: Login
======================================================================
    POST /auth/login -> CACHE
    userId:    019cae52-0015-7924-b0a8-53e78d1187d7
    stationId: 4d7169a1-7f43-46bf-813c-5641c1ce8c6b

  [PASS] Login
    -> Logged in as gadmin@masterspace.co.ke

======================================================================
  STEP 2: Create scale test
======================================================================
    POST /scale-tests -> 500

  [FAIL] Create scale test
    -> Failed: 500 

  *** ABORTING: Critical step 2 failed ***


======================================================================
  E2E SCENARIO 7 TEST SUMMARY: REPEAT OFFENDER
======================================================================
  Step  Status   Name
  ---   ------   --------------------------------------------------
  1     [PASS]   Login
  2     [FAIL]   Create scale test

  ============================================================
  TOTAL: 2  |  PASS: 1  |  FAIL: 1

  1 STEP(S) FAILED

  Shared IDs:
    userId: 019cae52-0015-7924-b0a8-53e78d1187d7
    stationId: 4d7169a1-7f43-46bf-813c-5641c1ce8c6b
    scaleTestId: ---
    driverId: ---
    transporterId: ---
    vehicleId: ---

  Cycle #1 IDs:
    weighingId: ---
    caseId: ---
    prosecutionId: ---
    demeritPoints: ---
    overloadKg: ---

  Cycle #2 IDs:
    weighingId: ---
    caseId: ---
    prosecutionId: ---
    demeritPoints: ---
    overloadKg: ---

  Cycle #3 IDs:
    weighingId: ---
    caseId: ---
    prosecutionId: ---
    demeritPoints: ---
    overloadKg: ---
======================================================================

STDERR:
  print(f"  Started: {datetime.utcnow().isoformat()}Z")```

---

## Overall Summary

| Scenario | Total | Pass | Fail | Status |
|----------|-------|------|------|--------|
| Scenario 1: Standard Overload Workflow | 1 | 0 | 1 | FAIL |
| Scenario 2: Compliant Vehicle (No Overload) | 3 | 2 | 1 | FAIL |
| Scenario 3: Tag-Hold and Yard Release | 4 | 3 | 1 | FAIL |
| Scenario 4: Permit-Based Exemption | 3 | 2 | 1 | FAIL |
| Scenario 5: Court Escalation Path | 3 | 2 | 1 | FAIL |
| Scenario 6: Full Court Case Lifecycle | 3 | 2 | 1 | FAIL |
| Scenario 7: Repeat Offender Multiple Overloads | 2 | 1 | 1 | FAIL |
| **TOTAL** | **19** | **12** | **7** | **7 FAILURES** |

### 7 FAILURES DETECTED
