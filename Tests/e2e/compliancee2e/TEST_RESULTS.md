# TruLoad E2E Compliance Test Results

**Date**: 2026-03-01 21:53:26
**Environment**: localhost:4000 (fresh database)

---

## Scenario 1: Standard Overload Workflow

```

======================================================================
  TRULOAD COMPLIANCE E2E TEST
  Target: http://localhost:4000
  Started: 2026-03-01T18:53:26.417836Z
======================================================================

  Workflow: Overload -> Case+Yard -> Prosecution -> Invoice
         -> Payment -> Memo -> Reweigh -> Cert + Close


======================================================================
  STEP 1: Login
======================================================================
    POST /auth/login -> 200
    [INFO] No stationId in user profile, fetching fallback...
    stationId (fallback): ef26c390-1365-425e-bcff-e81968763ac7 (Nairobi Mobile Unit 01)
    userId:    019caaba-1808-70a8-8328-71baa828065b
    stationId: ef26c390-1365-425e-bcff-e81968763ac7

  [PASS] Login
    -> Logged in as gadmin@masterspace.co.ke

======================================================================
  STEP 2: Setup metadata (driver, transporter, cargo, locations)
======================================================================
    POST /drivers -> 400
    POST /transporters -> 201
    Cargo type found: Agricultural Produce
    Origin: Busia Border
    Destination: Eldoret

  [FAIL] Setup metadata (driver, transporter, cargo, locations)
    -> Metadata: driverId=MISSING, transporterId=OK, cargoId=OK, originId=OK, destinationId=OK

  *** ABORTING: Critical step 2 failed ***


======================================================================
  E2E TEST SUMMARY
======================================================================
  Step  Status   Name
  ---   ------   --------------------------------------------------
  1     [PASS]   Login
  2     [FAIL]   Setup metadata (driver, transporter, cargo, locations)

  ============================================================
  TOTAL: 2  |  PASS: 1  |  FAIL: 1

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
    transporterId: d59bb5ec-e5ca-4b3e-8d8b-4c850a75f0bd
======================================================================

STDERR:
  print(f"  Started: {datetime.utcnow().isoformat()}Z")```

---

## Scenario 2: Compliant Vehicle (No Overload)

```

======================================================================
  TRULOAD COMPLIANCE E2E TEST -- SCENARIO 2
  Target: http://localhost:4000
  Started: 2026-03-01T18:53:31.813419Z
======================================================================

  Workflow: Within-Tolerance Overload -> Warning -> Auto Special Release -> Case Closed


======================================================================
  STEP 1: Login
======================================================================
    POST /auth/login -> 200
    [INFO] No stationId in user profile, fetching fallback...
    stationId (fallback): ef26c390-1365-425e-bcff-e81968763ac7 (Nairobi Mobile Unit 01)
    userId:    019caaba-1808-70a8-8328-71baa828065b
    stationId: ef26c390-1365-425e-bcff-e81968763ac7

  [PASS] Login
    -> Logged in as gadmin@masterspace.co.ke

======================================================================
  STEP 2: Setup metadata (driver, transporter, cargo, locations)
======================================================================
    POST /drivers -> 400
    Transporter found: E2E Test Transporters Ltd
    Cargo type found: Agricultural Produce
    Origin: Busia Border
    Destination: Eldoret

  [FAIL] Setup metadata (driver, transporter, cargo, locations)
    -> Metadata: driverId=MISSING, transporterId=OK, cargoId=OK, originId=OK, destinationId=OK

  *** ABORTING: Critical step 2 failed ***


======================================================================
  E2E TEST SUMMARY -- SCENARIO 2: WITHIN-TOLERANCE AUTO SPECIAL RELEASE
======================================================================
  Step  Status   Name
  ---   ------   --------------------------------------------------
  1     [PASS]   Login
  2     [FAIL]   Setup metadata (driver, transporter, cargo, locations)

  ============================================================
  TOTAL: 2  |  PASS: 1  |  FAIL: 1

  1 STEP(S) FAILED

  Collected IDs:
    weighingId: ---
    vehicleId: ---
    caseId: ---
    specialReleaseId: ---
    scaleTestId: ---
    driverId: ---
    transporterId: d59bb5ec-e5ca-4b3e-8d8b-4c850a75f0bd
======================================================================

STDERR:
  print(f"  Started: {datetime.utcnow().isoformat()}Z")```

---

## Scenario 3: Tag-Hold and Yard Release

```

======================================================================
  TRULOAD COMPLIANCE E2E TEST -- SCENARIO 3
  Target: http://localhost:4000
  Started: 2026-03-01T18:53:32.514183Z
======================================================================

  Workflow: Manual KeNHA Tag -> Compliant Weight + TagHold
         -> Yard Hold -> Tag Resolution -> Special Release


======================================================================
  STEP 1: Login
======================================================================
    POST /auth/login -> 200
    [INFO] No stationId in user profile, fetching fallback...
    stationId (fallback): ef26c390-1365-425e-bcff-e81968763ac7 (Nairobi Mobile Unit 01)
    userId:    019caaba-1808-70a8-8328-71baa828065b
    stationId: ef26c390-1365-425e-bcff-e81968763ac7

  [PASS] Login
    -> Logged in as gadmin@masterspace.co.ke

======================================================================
  STEP 2: Setup metadata (driver, transporter, cargo, locations)
======================================================================
    POST /drivers -> 400
    Transporter found: E2E Test Transporters Ltd
    Cargo type found: Agricultural Produce
    Origin: Busia Border
    Destination: Eldoret

  [FAIL] Setup metadata (driver, transporter, cargo, locations)
    -> Metadata: driverId=MISSING, transporterId=OK, cargoId=OK, originId=OK, destinationId=OK

  *** ABORTING: Critical step 2 failed ***


======================================================================
  E2E TEST SUMMARY -- SCENARIO 3: MANUAL KeNHA TAG -> YARD HOLD + SPECIAL RELEASE
======================================================================
  Step  Status   Name
  ---   ------   --------------------------------------------------
  1     [PASS]   Login
  2     [FAIL]   Setup metadata (driver, transporter, cargo, locations)

  ============================================================
  TOTAL: 2  |  PASS: 1  |  FAIL: 1

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
    driverId: ---
    transporterId: d59bb5ec-e5ca-4b3e-8d8b-4c850a75f0bd
======================================================================

STDERR:
  print(f"  Started: {datetime.utcnow().isoformat()}Z")```

---

## Scenario 4: Permit-Based Exemption

```

======================================================================
  TRULOAD COMPLIANCE E2E TEST -- SCENARIO 4
  Target: http://localhost:4000
  Started: 2026-03-01T18:53:33.224026Z
======================================================================

  Workflow: Compliant Vehicle -> Weight Ticket Only (No Case, No Yard, No Prosecution)


======================================================================
  STEP 1: Login
======================================================================
    POST /auth/login -> 200
    [INFO] No stationId in user profile, fetching fallback...
    stationId (fallback): ef26c390-1365-425e-bcff-e81968763ac7 (Nairobi Mobile Unit 01)
    userId:    019caaba-1808-70a8-8328-71baa828065b
    stationId: ef26c390-1365-425e-bcff-e81968763ac7

  [PASS] Login
    -> Logged in as gadmin@masterspace.co.ke

======================================================================
  STEP 2: Setup metadata (driver, transporter, cargo, locations)
======================================================================
    POST /drivers -> 400
    Transporter found: E2E Test Transporters Ltd
    Cargo type found: Agricultural Produce
    Origin: Busia Border
    Destination: Eldoret

  [FAIL] Setup metadata (driver, transporter, cargo, locations)
    -> Metadata: driverId=MISSING, transporterId=OK, cargoId=OK, originId=OK, destinationId=OK

  *** ABORTING: Critical step 2 failed ***


======================================================================
  E2E TEST SUMMARY -- SCENARIO 4: COMPLIANT VEHICLE
======================================================================
  Step  Status   Name
  ---   ------   --------------------------------------------------
  1     [PASS]   Login
  2     [FAIL]   Setup metadata (driver, transporter, cargo, locations)

  ============================================================
  TOTAL: 2  |  PASS: 1  |  FAIL: 1

  1 STEP(S) FAILED

  Collected IDs:
    weighingId: ---
    vehicleId: ---
    scaleTestId: ---
    driverId: ---
    transporterId: d59bb5ec-e5ca-4b3e-8d8b-4c850a75f0bd
======================================================================

STDERR:
  print(f"  Started: {datetime.utcnow().isoformat()}Z")```

---

## Scenario 5: Court Escalation Path

```

======================================================================
  TRULOAD COMPLIANCE E2E TEST -- SCENARIO 5
  Target: http://localhost:4000
  Started: 2026-03-01T18:54:28.252310Z
======================================================================

  Workflow: Overload -> Case+Yard -> Prosecution+Invoice
         -> Court Escalation (No Payment, No Release)


======================================================================
  STEP 1: Login
======================================================================
    POST /auth/login -> 200
    userId:    019caaba-1808-70a8-8328-71baa828065b
    stationId: None

  [PASS] Login
    -> Logged in as gadmin@masterspace.co.ke

======================================================================
  STEP 2: Setup metadata (driver, transporter, cargo, locations)
======================================================================
    POST /drivers -> 201
    Transporter found: E2E Test Transporters Ltd
    Cargo type found: Agricultural Produce
    Origin: Busia Border
    Destination: Eldoret

  [PASS] Setup metadata (driver, transporter, cargo, locations)
    -> Metadata: driverId=OK, transporterId=OK, cargoId=OK, originId=OK, destinationId=OK

======================================================================
  STEP 3: Create scale test
======================================================================
    POST /scale-tests -> 400

  [FAIL] Create scale test
    -> Failed: 400 {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"request":["The request field is required."],"$.stationId":["The J

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
    driverId: 56163410-7e58-4545-ae73-65f1c779ad60
    transporterId: d59bb5ec-e5ca-4b3e-8d8b-4c850a75f0bd
======================================================================

STDERR:
  print(f"  Started: {datetime.utcnow().isoformat()}Z")```

---

## Scenario 6: Full Court Case Lifecycle

```

======================================================================
  TRULOAD COMPLIANCE E2E TEST -- SCENARIO 6
  Full Court Case Lifecycle
  Target: http://localhost:4000
  Started: 2026-03-01T18:54:29.032762Z
======================================================================

  Workflow: Metadata -> Autoweigh -> Overload -> Case+Yard
         -> Escalate -> Court -> IO -> Parties -> Subfiles
         -> Hearings -> Warrants -> Closure -> Review -> Close


======================================================================
  STEP 1: Login
======================================================================
    POST /auth/login -> 200
    userId:    019caaba-1808-70a8-8328-71baa828065b
    stationId: None

  [PASS] Login
    -> Logged in as gadmin@masterspace.co.ke

======================================================================
  STEP 2: Setup metadata (driver, transporter, cargo, locs)
======================================================================
    Driver found: John E2E (56163410-7e58-4545-ae73-65f1c779ad60)
    Transporter found: E2E Test Transporters Ltd
    Cargo type found: Agricultural Produce
    Origin: Busia Border
    Destination: Eldoret

  [PASS] Setup metadata (driver, transporter, cargo, locs)
    -> Metadata: driverId=OK, transporterId=OK, cargoId=OK, originId=OK, destinationId=OK

======================================================================
  STEP 3: Create scale test
======================================================================
    POST /scale-tests -> 400

  [FAIL] Create scale test
    -> Failed: 400 {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"request":["The request field is required."],"$.stationId":["The J

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
    driverId: 56163410-7e58-4545-ae73-65f1c779ad60
    transporterId: d59bb5ec-e5ca-4b3e-8d8b-4c850a75f0bd
    userId: 019caaba-1808-70a8-8328-71baa828065b
======================================================================

STDERR:
  print(f"  Started: {datetime.utcnow().isoformat()}Z")```

---

## Scenario 7: Repeat Offender Multiple Overloads

```

======================================================================
  TRULOAD E2E SCENARIO 7: REPEAT OFFENDER -- MULTIPLE OVERLOADS
  Target: http://localhost:4000
  Started: 2026-03-01T18:54:30.576435Z
======================================================================

  Vehicle: KDG 999R
  Workflow: 3x (Autoweigh -> Capture -> Case -> Prosecution)
         -> Verify top offenders + axle violations


======================================================================
  STEP 1: Login
======================================================================
    POST /auth/login -> 200
    userId:    019caaba-1808-70a8-8328-71baa828065b
    stationId: None

  [PASS] Login
    -> Logged in as gadmin@masterspace.co.ke

======================================================================
  STEP 2: Create scale test
======================================================================
    POST /scale-tests -> 400

  [FAIL] Create scale test
    -> Failed: 400 {"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"request":["The request field is required."],"$.stationId":["The J

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
    userId: 019caaba-1808-70a8-8328-71baa828065b
    stationId: None
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
| Scenario 1: Standard Overload Workflow | 2 | 1 | 1 | FAIL |
| Scenario 2: Compliant Vehicle (No Overload) | 2 | 1 | 1 | FAIL |
| Scenario 3: Tag-Hold and Yard Release | 2 | 1 | 1 | FAIL |
| Scenario 4: Permit-Based Exemption | 2 | 1 | 1 | FAIL |
| Scenario 5: Court Escalation Path | 3 | 2 | 1 | FAIL |
| Scenario 6: Full Court Case Lifecycle | 3 | 2 | 1 | FAIL |
| Scenario 7: Repeat Offender Multiple Overloads | 2 | 1 | 1 | FAIL |
| **TOTAL** | **16** | **9** | **7** | **7 FAILURES** |

### 7 FAILURES DETECTED
