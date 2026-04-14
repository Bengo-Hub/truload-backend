# TruLoad E2E Compliance Test Results

**Date**: 2026-04-14 19:41:33
**Environment**: https://kuraweighapitest.masterspace.co.ke

---

## Scenario 1: Standard Overload Workflow

```

======================================================================
  TRULOAD COMPLIANCE E2E TEST
  Target: https://kuraweighapitest.masterspace.co.ke
  Started: 2026-04-14T16:41:34.323640Z
======================================================================

  Workflow: Overload -> Case+Yard -> Prosecution -> Invoice
         -> Payment -> Memo -> Reweigh -> Cert + Close


======================================================================
  STEP 1: Login
======================================================================
    POST /auth/login -> 401

  [ERROR] Login
    -> Login failed: 401 {"message":"Account is locked out"}

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
  Started: 2026-04-14T16:41:35.793760Z
======================================================================

  Workflow: Within-Tolerance Overload -> Warning -> Auto Special Release -> Case Closed


======================================================================
  STEP 1: Login
======================================================================
    POST /auth/login -> 401

  [ERROR] Login
    -> Login failed: 401 {"message":"Account is locked out"}

  *** ABORTING: Critical step 1 failed ***


======================================================================
  E2E TEST SUMMARY -- SCENARIO 2: WITHIN-TOLERANCE AUTO SPECIAL RELEASE
======================================================================
  Step  Status   Name
  ---   ------   --------------------------------------------------
  1     [FAIL]   Login

  ============================================================
  TOTAL: 1  |  PASS: 0  |  FAIL: 1

  1 STEP(S) FAILED

  Collected IDs:
    weighingId: ---
    vehicleId: ---
    caseId: ---
    specialReleaseId: ---
    scaleTestId: ---
    driverId: ---
    transporterId: ---
======================================================================

STDERR:
  print(f"  Started: {datetime.utcnow().isoformat()}Z")```

---

## Scenario 3: Tag-Hold and Yard Release

```

======================================================================
  TRULOAD COMPLIANCE E2E TEST -- SCENARIO 3
  Target: https://kuraweighapitest.masterspace.co.ke
  Started: 2026-04-14T16:41:37.275470Z
======================================================================

  Workflow: Manual KeNHA Tag -> Compliant Weight + TagHold
         -> Yard Hold -> Tag Resolution -> Special Release


======================================================================
  STEP 1: Login
======================================================================
    POST /auth/login -> 401

  [ERROR] Login
    -> Login failed: 401 {"message":"Account is locked out"}

  *** ABORTING: Critical step 1 failed ***


======================================================================
  E2E TEST SUMMARY -- SCENARIO 3: MANUAL KeNHA TAG -> YARD HOLD + SPECIAL RELEASE
======================================================================
  Step  Status   Name
  ---   ------   --------------------------------------------------
  1     [FAIL]   Login

  ============================================================
  TOTAL: 1  |  PASS: 0  |  FAIL: 1

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
    transporterId: ---
======================================================================

STDERR:
  print(f"  Started: {datetime.utcnow().isoformat()}Z")```

---

## Scenario 4: Permit-Based Exemption

```

======================================================================
  TRULOAD COMPLIANCE E2E TEST -- SCENARIO 4
  Target: https://kuraweighapitest.masterspace.co.ke
  Started: 2026-04-14T16:41:38.602243Z
======================================================================

  Workflow: Compliant Vehicle -> Weight Ticket Only (No Case, No Yard, No Prosecution)


======================================================================
  STEP 1: Login
======================================================================
    POST /auth/login -> 401

  [ERROR] Login
    -> Login failed: 401 {"message":"Account is locked out"}

  *** ABORTING: Critical step 1 failed ***


======================================================================
  E2E TEST SUMMARY -- SCENARIO 4: COMPLIANT VEHICLE
======================================================================
  Step  Status   Name
  ---   ------   --------------------------------------------------
  1     [FAIL]   Login

  ============================================================
  TOTAL: 1  |  PASS: 0  |  FAIL: 1

  1 STEP(S) FAILED

  Collected IDs:
    weighingId: ---
    vehicleId: ---
    scaleTestId: ---
    driverId: ---
    transporterId: ---
======================================================================

STDERR:
  print(f"  Started: {datetime.utcnow().isoformat()}Z")```

---

## Scenario 5: Court Escalation Path

```

======================================================================
  TRULOAD COMPLIANCE E2E TEST -- SCENARIO 5
  Target: https://kuraweighapitest.masterspace.co.ke
  Started: 2026-04-14T16:41:39.873619Z
======================================================================

  Workflow: Overload -> Case+Yard -> Prosecution+Invoice
         -> Court Escalation (No Payment, No Release)


======================================================================
  STEP 1: Login
======================================================================
    POST /auth/login -> 401

  [ERROR] Login
    -> Login failed: 401 {"message":"Account is locked out"}

  *** ABORTING: Critical step 1 failed ***


======================================================================
  E2E TEST SUMMARY -- SCENARIO 5: COURT ESCALATION
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
    courtEscalationDispositionId: ---
    prohibitionOrderId: ---
    driverId: ---
    transporterId: ---
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
  Started: 2026-04-14T16:41:41.195434Z
======================================================================

  Workflow: Metadata -> Autoweigh -> Overload -> Case+Yard
         -> Escalate -> Court -> IO -> Parties -> Subfiles
         -> Hearings -> Warrants -> Closure -> Review -> Close


======================================================================
  STEP 1: Login
======================================================================
    POST /auth/login -> 401

  [ERROR] Login
    -> Login failed: 401 {"message":"Account is locked out"}

  *** ABORTING: Critical step 1 failed ***


======================================================================
  E2E TEST SUMMARY -- SCENARIO 6: Full Court Case Lifecycle
======================================================================
  Step  Status   Name
  ---   ------   --------------------------------------------------
  1     [FAIL]   Login

  ============================================================
  TOTAL: 1  |  PASS: 0  |  FAIL: 1

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
    driverId: ---
    transporterId: ---
    userId: ---
======================================================================

STDERR:
  print(f"  Started: {datetime.utcnow().isoformat()}Z")```

---

## Scenario 7: Repeat Offender Multiple Overloads

```

======================================================================
  TRULOAD E2E SCENARIO 7: REPEAT OFFENDER -- MULTIPLE OVERLOADS
  Target: https://kuraweighapitest.masterspace.co.ke
  Started: 2026-04-14T16:41:42.507165Z
======================================================================

  Vehicle: KDG 999R
  Workflow: 3x (Autoweigh -> Capture -> Case -> Prosecution)
         -> Verify top offenders + axle violations


======================================================================
  STEP 1: Login
======================================================================
    POST /auth/login -> 401

  [ERROR] Login
    -> Login failed: 401 {"message":"Account is locked out"}

  *** ABORTING: Critical step 1 failed ***


======================================================================
  E2E SCENARIO 7 TEST SUMMARY: REPEAT OFFENDER
======================================================================
  Step  Status   Name
  ---   ------   --------------------------------------------------
  1     [FAIL]   Login

  ============================================================
  TOTAL: 1  |  PASS: 0  |  FAIL: 1

  1 STEP(S) FAILED

  Shared IDs:
    userId: ---
    stationId: ---
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
| Scenario 2: Compliant Vehicle (No Overload) | 1 | 0 | 1 | FAIL |
| Scenario 3: Tag-Hold and Yard Release | 1 | 0 | 1 | FAIL |
| Scenario 4: Permit-Based Exemption | 1 | 0 | 1 | FAIL |
| Scenario 5: Court Escalation Path | 1 | 0 | 1 | FAIL |
| Scenario 6: Full Court Case Lifecycle | 1 | 0 | 1 | FAIL |
| Scenario 7: Repeat Offender Multiple Overloads | 1 | 0 | 1 | FAIL |
| **TOTAL** | **7** | **0** | **7** | **7 FAILURES** |

### 7 FAILURES DETECTED
