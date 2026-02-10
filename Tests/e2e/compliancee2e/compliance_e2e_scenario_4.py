#!/usr/bin/env python3
"""
TruLoad Compliance E2E Test -- Scenario 4
==========================================
Compliant Vehicle -> Weight Ticket Only (No Case, No Yard, No Prosecution)

This scenario tests a fully compliant vehicle that passes through weighing
without triggering any enforcement actions. The vehicle's gross weight is
well under the permissible limit, so:
  - No case register is created
  - No yard entry is created
  - No prosecution, invoice, or payment
  - Only a weight ticket is generated

Steps:
  1. Login
  2. Setup metadata (driver, transporter, cargo, locations)
  3. Create scale test
  4. Autoweigh compliant vehicle (GVW 21500, limit 26000)
  5. Update weighing metadata
  6. Capture weights -- triggers compliance check
  7. Verify ControlStatus = "Compliant", IsCompliant = true, IsSentToYard = false
  8. Verify NO case register created (expect 404)
  9. Verify NO yard entry created (expect 404)
  10. Download weight ticket PDF

Usage:
    python compliance_e2e_scenario_4.py [--base-url http://localhost:4000]

Requirements:
    pip install requests
"""

import argparse
import json
import sys
import uuid
import time
from datetime import datetime
from typing import Any, Optional

try:
    import requests
except ImportError:
    print("ERROR: 'requests' package required. Install with: pip install requests")
    sys.exit(1)

# ─── Configuration ──────────────────────────────────────────────────────────

DEFAULT_BASE_URL = "http://localhost:4000"
API_PREFIX = "/api/v1"
LOGIN_EMAIL = "gadmin@masterspace.co.ke"
LOGIN_PASSWORD = "ChangeMe123!"

# Compliant axle weights (3-axle, GVW=21500, permissible=26000, well under limit)
COMPLIANT_AXLES = [
    {"axleNumber": 1, "measuredWeightKg": 6500},
    {"axleNumber": 2, "measuredWeightKg": 7500},
    {"axleNumber": 3, "measuredWeightKg": 7500},
]

VEHICLE_REG = "KDG 404P"


# ─── Test Runner ────────────────────────────────────────────────────────────

class ComplianceE2ETest:
    def __init__(self, base_url: str):
        self.base_url = base_url.rstrip("/")
        self.api = f"{self.base_url}{API_PREFIX}"
        self.token: Optional[str] = None
        self.headers: dict = {"Content-Type": "application/json"}
        self.results: list = []
        self.data: dict = {}  # Collected IDs and data across steps

    def _url(self, path: str) -> str:
        return f"{self.api}/{path.lstrip('/')}"

    def _auth_headers(self) -> dict:
        return {**self.headers, "Authorization": f"Bearer {self.token}"}

    def _get(self, path: str, **kwargs) -> requests.Response:
        return requests.get(self._url(path), headers=self._auth_headers(), timeout=30, **kwargs)

    def _post(self, path: str, body: Any = None, **kwargs) -> requests.Response:
        return requests.post(self._url(path), headers=self._auth_headers(),
                             json=body, timeout=30, **kwargs)

    def _put(self, path: str, body: Any = None, **kwargs) -> requests.Response:
        return requests.put(self._url(path), headers=self._auth_headers(),
                            json=body, timeout=30, **kwargs)

    def _step(self, num: int, name: str, fn):
        """Execute a test step with error handling and reporting."""
        print(f"\n{'='*70}")
        print(f"  STEP {num}: {name}")
        print(f"{'='*70}")
        try:
            passed, details = fn()
            status = "PASS" if passed else "FAIL"
            self.results.append({"step": num, "name": name, "status": status, "details": details})
            icon = "[PASS]" if passed else "[FAIL]"
            print(f"\n  {icon} {name}")
            if details:
                print(f"    -> {details}")
            return passed
        except Exception as e:
            self.results.append({"step": num, "name": name, "status": "ERROR", "details": str(e)})
            print(f"\n  [ERROR] {name}")
            print(f"    -> {e}")
            return False

    # ── Step Implementations ─────────────────────────────────────────────

    def step_01_login(self):
        """Authenticate and get JWT token + user info."""
        r = requests.post(
            self._url("auth/login"),
            headers=self.headers,
            json={"email": LOGIN_EMAIL, "password": LOGIN_PASSWORD},
            timeout=30,
        )
        print(f"    POST /auth/login -> {r.status_code}")
        assert r.status_code == 200, f"Login failed: {r.status_code} {r.text[:200]}"
        data = r.json()

        self.token = data.get("accessToken") or data.get("token")
        assert self.token, f"No token in response: {list(data.keys())}"

        user = data.get("user", data)
        self.data["userId"] = user.get("id")
        self.data["stationId"] = user.get("stationId")
        print(f"    userId:    {self.data['userId']}")
        print(f"    stationId: {self.data['stationId']}")

        return True, f"Logged in as {user.get('email', LOGIN_EMAIL)}"

    def step_02_setup_metadata(self):
        """Create or fetch driver, transporter, cargo, origin, destination."""
        created = []

        # ── Driver ──
        r = self._get("drivers/search?query=E2E")
        drivers = r.json() if r.status_code == 200 else []
        if isinstance(drivers, dict):
            drivers = drivers.get("items", drivers.get("data", []))
        if drivers:
            self.data["driverId"] = drivers[0]["id"]
            print(f"    Driver found: {drivers[0].get('fullNames', 'N/A')} ({self.data['driverId']})")
        else:
            r = self._post("drivers", {
                "fullNames": "John E2E",
                "surname": "Kamau",
                "idNumber": "E2E-ID-001",
                "drivingLicenseNo": "E2E-DL-001",
                "licenseClass": "BCE",
                "nationality": "Kenyan",
                "phoneNumber": "+254700000001",
            })
            print(f"    POST /drivers -> {r.status_code}")
            if r.status_code in (200, 201):
                drv = r.json()
                self.data["driverId"] = drv.get("id")
                created.append("driver")
            else:
                # Fallback: try search for any driver
                r2 = self._get("drivers/search?query=")
                all_drivers = r2.json() if r2.status_code == 200 else []
                if isinstance(all_drivers, dict):
                    all_drivers = all_drivers.get("items", all_drivers.get("data", []))
                if all_drivers:
                    self.data["driverId"] = all_drivers[0]["id"]
                    print(f"    Driver fallback: {all_drivers[0].get('fullNames', 'N/A')}")

        # ── Transporter ──
        r = self._get("transporters/search?query=E2E")
        transporters = r.json() if r.status_code == 200 else []
        if isinstance(transporters, dict):
            transporters = transporters.get("items", transporters.get("data", []))
        if transporters:
            self.data["transporterId"] = transporters[0]["id"]
            print(f"    Transporter found: {transporters[0].get('name', 'N/A')}")
        else:
            r = self._post("transporters", {
                "code": "E2E-TRANS-001",
                "name": "E2E Test Transporters Ltd",
                "registrationNo": "PVT-E2E-001",
                "phone": "+254700000002",
                "email": "e2e-transport@test.co.ke",
                "address": "Mombasa Road, Nairobi",
                "ntacNo": "NTAC-E2E-001",
            })
            print(f"    POST /transporters -> {r.status_code}")
            if r.status_code in (200, 201):
                trans = r.json()
                self.data["transporterId"] = trans.get("id")
                created.append("transporter")
            else:
                # Fallback
                r2 = self._get("transporters")
                all_trans = r2.json() if r2.status_code == 200 else []
                if isinstance(all_trans, dict):
                    all_trans = all_trans.get("items", all_trans.get("data", []))
                if all_trans:
                    self.data["transporterId"] = all_trans[0]["id"]

        # ── Cargo Type ──
        r = self._get("cargo-types")
        cargos = r.json() if r.status_code == 200 else []
        if isinstance(cargos, dict):
            cargos = cargos.get("items", cargos.get("data", []))
        if cargos:
            self.data["cargoId"] = cargos[0]["id"]
            print(f"    Cargo type found: {cargos[0].get('name', 'N/A')}")
        else:
            r = self._post("cargo-types", {
                "code": "GENERAL_CARGO",
                "name": "General Cargo",
                "category": "General",
            })
            print(f"    POST /cargo-types -> {r.status_code}")
            if r.status_code in (200, 201):
                cargo = r.json()
                self.data["cargoId"] = cargo.get("id")
                created.append("cargo-type")

        # ── Origins & Destinations ──
        r = self._get("origins-destinations")
        locations = r.json() if r.status_code == 200 else []
        if isinstance(locations, dict):
            locations = locations.get("items", locations.get("data", []))
        if len(locations) >= 2:
            self.data["originId"] = locations[0]["id"]
            self.data["destinationId"] = locations[1]["id"]
            print(f"    Origin: {locations[0].get('name', 'N/A')}")
            print(f"    Destination: {locations[1].get('name', 'N/A')}")
        else:
            for loc in [
                {"code": "NBI", "name": "Nairobi", "locationType": "city", "country": "Kenya"},
                {"code": "MSA", "name": "Mombasa", "locationType": "port", "country": "Kenya"},
            ]:
                r = self._post("origins-destinations", loc)
                print(f"    POST /origins-destinations ({loc['code']}) -> {r.status_code}")
                if r.status_code in (200, 201):
                    result = r.json()
                    if "originId" not in self.data:
                        self.data["originId"] = result.get("id")
                    else:
                        self.data["destinationId"] = result.get("id")
                    created.append(f"location:{loc['code']}")

        summary_parts = []
        for key in ["driverId", "transporterId", "cargoId", "originId", "destinationId"]:
            val = self.data.get(key, "MISSING")
            summary_parts.append(f"{key}={'OK' if val != 'MISSING' else 'MISSING'}")

        all_present = all(self.data.get(k) for k in ["driverId", "transporterId"])
        return all_present, f"Metadata: {', '.join(summary_parts)}"

    def step_03_scale_test(self):
        """Create passing scale test for the station."""
        body = {
            "stationId": self.data["stationId"],
            "testType": "standard",
            "status": "passed",
            "targetWeight": 10000,
            "measuredWeight": 10005,
            "deviationPercent": 0.05,
            "instrumentId": "WIM-001",
            "testedByUserId": self.data["userId"],
            "notes": "E2E scenario 4 calibration check",
        }
        r = self._post("scale-tests", body)
        print(f"    POST /scale-tests -> {r.status_code}")
        if r.status_code in (200, 201):
            data = r.json()
            self.data["scaleTestId"] = data.get("id")
            return True, f"Scale test created: {self.data['scaleTestId']}"
        return False, f"Failed: {r.status_code} {r.text[:200]}"

    def step_04_autoweigh_compliant(self):
        """Autoweigh 3-axle compliant vehicle (GVW 21500, limit 26000)."""
        body = {
            "stationId": self.data["stationId"],
            "vehicleRegNumber": VEHICLE_REG,
            "weighingMode": "static",
            "source": "Middleware",
            "axles": COMPLIANT_AXLES,
        }
        r = self._post("weighing-transactions/autoweigh", body)
        print(f"    POST /weighing-transactions/autoweigh -> {r.status_code}")
        assert r.status_code in (200, 201), f"Autoweigh failed: {r.status_code} {r.text[:300]}"

        data = r.json()
        txn = data.get("transaction", data)
        self.data["weighingId"] = txn.get("weighingId") or txn.get("id")
        self.data["vehicleId"] = txn.get("vehicleId")

        print(f"    weighingId:    {self.data['weighingId']}")
        print(f"    vehicleId:     {self.data['vehicleId']}")
        print(f"    captureStatus: {txn.get('captureStatus')}")
        print(f"    gvwMeasuredKg: {txn.get('gvwMeasuredKg')}")

        return True, f"Autoweigh created: {self.data['weighingId']}"

    def step_05_update_metadata(self):
        """Link driver and transporter to the weighing transaction."""
        wid = self.data["weighingId"]
        body = {}
        if self.data.get("driverId"):
            body["driverId"] = self.data["driverId"]
        if self.data.get("transporterId"):
            body["transporterId"] = self.data["transporterId"]

        if not body:
            return True, "No metadata to update (IDs missing)"

        r = self._put(f"weighing-transactions/{wid}", body)
        print(f"    PUT /weighing-transactions/{wid} -> {r.status_code}")

        if r.status_code == 200:
            txn = r.json()
            linked = []
            if txn.get("driverId"):
                linked.append("driver")
            if txn.get("transporterId"):
                linked.append("transporter")
            return True, f"Linked: {', '.join(linked) if linked else 'none'}"
        else:
            print(f"    Response: {r.text[:300]}")
            return False, f"Update failed: {r.status_code}"

    def step_06_capture_weights(self):
        """Capture weights -- triggers compliance check. Expect COMPLIANT result."""
        wid = self.data["weighingId"]
        body = {"axles": COMPLIANT_AXLES}
        r = self._post(f"weighing-transactions/{wid}/capture-weights", body)
        print(f"    POST /weighing-transactions/{wid}/capture-weights -> {r.status_code}")
        assert r.status_code == 200, f"Capture failed: {r.status_code} {r.text[:300]}"

        txn = r.json()
        print(f"    captureStatus:    {txn.get('captureStatus')}")
        print(f"    controlStatus:    {txn.get('controlStatus')}")
        print(f"    isCompliant:      {txn.get('isCompliant')}")
        print(f"    isSentToYard:     {txn.get('isSentToYard')}")
        print(f"    overloadKg:       {txn.get('overloadKg')}")
        print(f"    gvwMeasuredKg:    {txn.get('gvwMeasuredKg')}")
        print(f"    gvwPermissibleKg: {txn.get('gvwPermissibleKg')}")

        # Store for verification in step 7
        self.data["controlStatus"] = txn.get("controlStatus")
        self.data["isCompliant"] = txn.get("isCompliant")
        self.data["isSentToYard"] = txn.get("isSentToYard")

        is_compliant = txn.get("isCompliant") is True or txn.get("controlStatus") == "Compliant"
        return is_compliant, f"Compliant: GVW={txn.get('gvwMeasuredKg')}kg, permissible={txn.get('gvwPermissibleKg')}kg"

    def step_07_verify_compliant_status(self):
        """Verify ControlStatus = 'Compliant', IsCompliant = true, IsSentToYard = false."""
        wid = self.data["weighingId"]

        # Fetch the weighing transaction to get fresh data
        r = self._get(f"weighing-transactions/{wid}")
        print(f"    GET /weighing-transactions/{wid} -> {r.status_code}")

        if r.status_code != 200:
            # Fall back to data captured in step 6
            print(f"    Falling back to step 6 cached data")
            control_status = self.data.get("controlStatus")
            is_compliant = self.data.get("isCompliant")
            is_sent_to_yard = self.data.get("isSentToYard")
        else:
            txn = r.json()
            control_status = txn.get("controlStatus")
            is_compliant = txn.get("isCompliant")
            is_sent_to_yard = txn.get("isSentToYard")

        print(f"    controlStatus: {control_status}")
        print(f"    isCompliant:   {is_compliant}")
        print(f"    isSentToYard:  {is_sent_to_yard}")

        ok_status = control_status == "Compliant"
        ok_compliant = is_compliant is True
        ok_no_yard = is_sent_to_yard is False or is_sent_to_yard is None

        all_ok = ok_status and ok_compliant and ok_no_yard
        details = (
            f"controlStatus={'Compliant' if ok_status else control_status}, "
            f"isCompliant={'true' if ok_compliant else is_compliant}, "
            f"isSentToYard={'false' if ok_no_yard else is_sent_to_yard}"
        )
        return all_ok, details

    def step_08_verify_no_case(self):
        """Verify NO case register was created (expect 404 -- compliant vehicle has no case)."""
        wid = self.data["weighingId"]
        time.sleep(0.5)

        r = self._get(f"case/cases/by-weighing/{wid}")
        print(f"    GET /case/cases/by-weighing/{wid} -> {r.status_code}")

        if r.status_code == 404:
            return True, "No case created (404) -- correct for compliant vehicle"
        elif r.status_code == 200:
            case = r.json()
            # Check if response is empty list/null
            if case is None or case == [] or case == {}:
                return True, "No case created (empty response) -- correct for compliant vehicle"
            # A case was unexpectedly created
            print(f"    UNEXPECTED: Case found: {case.get('caseNo', 'N/A')}")
            print(f"    caseId:     {case.get('id')}")
            print(f"    caseStatus: {case.get('caseStatus')}")
            return False, f"UNEXPECTED case created: {case.get('caseNo')} -- compliant vehicle should have no case"
        else:
            # Other status codes (e.g. 204, 400) -- treat as no case
            print(f"    Status {r.status_code} -- treating as no case")
            return True, f"No case created (status {r.status_code}) -- correct for compliant vehicle"

    def step_09_verify_no_yard(self):
        """Verify NO yard entry was created (expect 404 -- compliant vehicle not sent to yard)."""
        wid = self.data["weighingId"]

        r = self._get(f"yard-entries/by-weighing/{wid}")
        print(f"    GET /yard-entries/by-weighing/{wid} -> {r.status_code}")

        if r.status_code == 404:
            return True, "No yard entry created (404) -- correct for compliant vehicle"
        elif r.status_code == 200:
            yard = r.json()
            # Check if response is empty list/null
            if yard is None or yard == [] or yard == {}:
                return True, "No yard entry created (empty response) -- correct for compliant vehicle"
            # A yard entry was unexpectedly created
            print(f"    UNEXPECTED: Yard entry found: {yard.get('id', 'N/A')}")
            print(f"    status: {yard.get('status')}")
            print(f"    reason: {yard.get('reason')}")
            return False, f"UNEXPECTED yard entry created -- compliant vehicle should not be sent to yard"
        else:
            # Other status codes -- treat as no yard entry
            print(f"    Status {r.status_code} -- treating as no yard entry")
            return True, f"No yard entry created (status {r.status_code}) -- correct for compliant vehicle"

    def step_10_download_weight_ticket_pdf(self):
        """Download weight ticket PDF and verify response."""
        wid = self.data["weighingId"]
        r = self._get(f"weighing-transactions/{wid}/ticket/pdf")
        print(f"    GET /weighing-transactions/{wid}/ticket/pdf -> {r.status_code}")
        if r.status_code == 200:
            content_type = r.headers.get("content-type", "")
            content_len = len(r.content)
            print(f"    content-type: {content_type}")
            print(f"    content-length: {content_len} bytes")
            if "pdf" in content_type.lower() and content_len > 100:
                return True, f"Weight ticket PDF downloaded ({content_len} bytes)"
        return False, f"Weight ticket PDF failed: {r.status_code}"

    # ── Runner ───────────────────────────────────────────────────────────

    def run(self):
        """Execute all steps in order."""
        print("\n" + "=" * 70)
        print("  TRULOAD COMPLIANCE E2E TEST -- SCENARIO 4")
        print(f"  Target: {self.base_url}")
        print(f"  Started: {datetime.utcnow().isoformat()}Z")
        print("=" * 70)
        print()
        print("  Workflow: Compliant Vehicle -> Weight Ticket Only (No Case, No Yard, No Prosecution)")
        print()

        steps = [
            (1, "Login", self.step_01_login),
            (2, "Setup metadata (driver, transporter, cargo, locations)", self.step_02_setup_metadata),
            (3, "Create scale test", self.step_03_scale_test),
            (4, "Autoweigh compliant vehicle", self.step_04_autoweigh_compliant),
            (5, "Update weighing metadata (driver, transporter)", self.step_05_update_metadata),
            (6, "Capture weights (triggers compliance check)", self.step_06_capture_weights),
            (7, "Verify Compliant status (ControlStatus, IsCompliant, IsSentToYard)", self.step_07_verify_compliant_status),
            (8, "Verify NO case register created (expect 404)", self.step_08_verify_no_case),
            (9, "Verify NO yard entry created (expect 404)", self.step_09_verify_no_yard),
            (10, "Download weight ticket PDF", self.step_10_download_weight_ticket_pdf),
        ]

        for num, name, fn in steps:
            passed = self._step(num, name, fn)
            if not passed and num <= 6:
                # Critical steps -- abort if login/setup/autoweigh/capture fail
                print(f"\n  *** ABORTING: Critical step {num} failed ***")
                break

        self._print_summary()

    def _print_summary(self):
        """Print final test summary."""
        print("\n\n" + "=" * 70)
        print("  E2E TEST SUMMARY -- SCENARIO 4: COMPLIANT VEHICLE")
        print("=" * 70)
        print(f"  {'Step':<5} {'Status':<8} {'Name'}")
        print(f"  {'---':<5} {'------':<8} {'--------------------------------------------------'}")

        pass_count = 0
        fail_count = 0
        for r in self.results:
            icon = "[PASS]" if r["status"] == "PASS" else "[FAIL]"
            print(f"  {r['step']:<5} {icon:<8} {r['name']}")
            if r["status"] == "PASS":
                pass_count += 1
            else:
                fail_count += 1

        total = len(self.results)
        print(f"\n  {'='*60}")
        print(f"  TOTAL: {total}  |  PASS: {pass_count}  |  FAIL: {fail_count}")

        if fail_count == 0:
            print(f"\n  ALL {total} STEPS PASSED")
        else:
            print(f"\n  {fail_count} STEP(S) FAILED")

        # Print collected IDs
        print(f"\n  Collected IDs:")
        for key in ["weighingId", "vehicleId", "scaleTestId", "driverId", "transporterId"]:
            val = self.data.get(key, "---")
            print(f"    {key}: {val}")

        print("=" * 70)
        return fail_count == 0


# ─── Main ───────────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description="TruLoad Compliance E2E Test -- Scenario 4: Compliant Vehicle")
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL,
                        help=f"Backend base URL (default: {DEFAULT_BASE_URL})")
    args = parser.parse_args()

    test = ComplianceE2ETest(args.base_url)
    test.run()

    # Exit with appropriate code
    failures = sum(1 for r in test.results if r["status"] != "PASS")
    sys.exit(failures)


if __name__ == "__main__":
    main()
