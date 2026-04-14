#!/usr/bin/env python3
"""
TruLoad Compliance E2E Test -- Scenario 6
==========================================
Full Court Case Lifecycle: escalation -> investigation -> hearings ->
subfiles -> diary -> arrest warrants -> review -> closure.

Workflow:
  1. Login
  2. Setup metadata (driver, transporter, cargo, locations)
  3. Scale test
  4. Autoweigh overloaded vehicle (KDG 606L, GVW=27500)
  5. Update metadata on transaction
  6. Capture weights (triggers compliance + case/yard auto-triggers)
  7. Verify case auto-created
  8. Verify yard entry auto-created
  9. Escalate to court
 10. Create court record
 11. Assign IO (initial assignment)
 12. Add case parties (IO + defendant driver)
 13. Upload Subfile B (document evidence)
 14. Upload Subfile D (witness statement)
 15. Upload Subfile F (investigation diary)
 16. Upload Subfile G (charge sheet)
 17. Check subfile completion
 18. Schedule first hearing (mention)
 19. Adjourn first hearing
 20. Verify next hearing auto-created
 21. Issue arrest warrant
 22. Execute warrant
 23. Complete second hearing (plea with conviction)
 24. Create closure checklist
 25. Request closure review
 26. Approve closure review
 27. Close the case
 28. Verify final state

Usage:
    python compliance_e2e_scenario_6.py [--base-url http://localhost:4000]

Requirements:
    pip install requests
"""

import argparse
import json
import sys
import uuid
import time
from datetime import datetime, timedelta
from pathlib import Path
from typing import Any, Optional

try:
    import requests
except ImportError:
    print("ERROR: 'requests' package required. Install with: pip install requests")
    sys.exit(1)

E2E_ROOT = Path(__file__).resolve().parent.parent
if str(E2E_ROOT) not in sys.path:
    sys.path.append(str(E2E_ROOT))

from test_credentials import LOGIN_EMAIL_DEFAULT, LOGIN_PASSWORD_DEFAULT

# ─── Configuration ──────────────────────────────────────────────────────────

DEFAULT_BASE_URL = "http://localhost:4000"
API_PREFIX = "/api/v1"
LOGIN_EMAIL = LOGIN_EMAIL_DEFAULT
LOGIN_PASSWORD = LOGIN_PASSWORD_DEFAULT

# Overloaded axle weights (3-axle, GVW=27500, clearly overloaded)
OVERLOADED_AXLES = [
    {"axleNumber": 1, "measuredWeightKg": 8500},
    {"axleNumber": 2, "measuredWeightKg": 9500},
    {"axleNumber": 3, "measuredWeightKg": 9500},
]

VEHICLE_REG = "KDG 606L"


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
        return requests.get(self._url(path), headers=self._auth_headers(), timeout=60, **kwargs)

    def _post(self, path: str, body: Any = None, **kwargs) -> requests.Response:
        return requests.post(self._url(path), headers=self._auth_headers(),
                             json=body, timeout=60, **kwargs)

    def _put(self, path: str, body: Any = None, **kwargs) -> requests.Response:
        return requests.put(self._url(path), headers=self._auth_headers(),
                            json=body, timeout=60, **kwargs)

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

    # ── Helper: find taxonomy item by code ─────────────────────────────────

    def _find_taxonomy(self, taxonomy_path: str, code: str) -> Optional[dict]:
        """GET a taxonomy list and find the item matching the given code."""
        r = self._get(taxonomy_path)
        print(f"    GET /{taxonomy_path} -> {r.status_code}")
        if r.status_code != 200:
            return None
        items = r.json()
        if isinstance(items, dict):
            items = items.get("items", items.get("data", []))
        for item in items:
            if item.get("code", "").upper() == code.upper():
                return item
        # Fallback: partial match
        for item in items:
            if code.upper() in item.get("code", "").upper():
                return item
        # Last resort: return first item if any
        if items:
            print(f"    WARNING: Code '{code}' not found in {taxonomy_path}, using first item: {items[0].get('code')}")
            return items[0]
        return None

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
                "idNumber": "E2E-ID-006",
                "drivingLicenseNo": "E2E-DL-006",
                "licenseClass": "BCE",
                "nationality": "Kenyan",
                "phoneNumber": "+254700000006",
            })
            print(f"    POST /drivers -> {r.status_code}")
            if r.status_code in (200, 201):
                drv = r.json()
                self.data["driverId"] = drv.get("id")
                created.append("driver")
            else:
                # Fallback: search for any driver
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
                "code": "E2E-TRANS-006",
                "name": "E2E Test Transporters S6 Ltd",
                "registrationNo": "PVT-E2E-006",
                "phone": "+254700000062",
                "email": "e2e-transport-s6@test.co.ke",
                "address": "Mombasa Road, Nairobi",
                "ntacNo": "NTAC-E2E-006",
            })
            print(f"    POST /transporters -> {r.status_code}")
            if r.status_code in (200, 201):
                trans = r.json()
                self.data["transporterId"] = trans.get("id")
                created.append("transporter")
            else:
                # Fallback
                r2 = self._get("transporters/search?query=")
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
        elif len(locations) == 1:
            self.data["originId"] = locations[0]["id"]
            self.data["destinationId"] = locations[0]["id"]

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
            "notes": "E2E scenario 6 calibration",
        }
        r = self._post("scale-tests", body)
        print(f"    POST /scale-tests -> {r.status_code}")
        if r.status_code in (200, 201):
            data = r.json()
            self.data["scaleTestId"] = data.get("id")
            return True, f"Scale test created: {self.data['scaleTestId']}"
        return False, f"Failed: {r.status_code} {r.text[:200]}"

    def step_04_autoweigh_overloaded(self):
        """Autoweigh 3-axle overloaded vehicle (KDG 606L, GVW=27500)."""
        body = {
            "stationId": self.data["stationId"],
            "vehicleRegNumber": VEHICLE_REG,
            "weighingMode": "static",
            "source": "Middleware",
            "axles": OVERLOADED_AXLES,
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
        """Capture weights -- triggers compliance check + case/yard auto-triggers."""
        wid = self.data["weighingId"]
        body = {"axles": OVERLOADED_AXLES}
        r = self._post(f"weighing-transactions/{wid}/capture-weights", body)
        print(f"    POST /weighing-transactions/{wid}/capture-weights -> {r.status_code}")
        assert r.status_code == 200, f"Capture failed: {r.status_code} {r.text[:300]}"

        txn = r.json()
        print(f"    captureStatus: {txn.get('captureStatus')}")
        print(f"    controlStatus: {txn.get('controlStatus')}")
        print(f"    isCompliant:   {txn.get('isCompliant')}")
        print(f"    overloadKg:    {txn.get('overloadKg')}")
        print(f"    gvwMeasuredKg: {txn.get('gvwMeasuredKg')}")

        is_overloaded = txn.get("controlStatus") == "Overloaded" or txn.get("isCompliant") is False
        return is_overloaded, f"ControlStatus={txn.get('controlStatus')}, GVW={txn.get('gvwMeasuredKg')}kg"

    def step_07_verify_case_auto_created(self):
        """Verify case register was auto-created on overload detection."""
        wid = self.data["weighingId"]
        time.sleep(1)

        r = self._get(f"case/cases/by-weighing/{wid}")
        print(f"    GET /case/cases/by-weighing/{wid} -> {r.status_code}")
        assert r.status_code == 200, f"No case found: {r.status_code} {r.text[:200]}"

        case = r.json()
        self.data["caseId"] = case.get("id")
        self.data["caseNo"] = case.get("caseNo")
        print(f"    caseId:     {self.data['caseId']}")
        print(f"    caseNo:     {case.get('caseNo')}")
        print(f"    caseStatus: {case.get('caseStatus')}")

        has_case = bool(self.data["caseId"])
        return has_case, f"Case auto-created: {case.get('caseNo')}"

    def step_08_verify_yard_entry(self):
        """Verify yard entry was auto-created."""
        wid = self.data["weighingId"]

        r = self._get(f"yard-entries/by-weighing/{wid}")
        print(f"    GET /yard-entries/by-weighing/{wid} -> {r.status_code}")

        if r.status_code != 200:
            return False, f"No yard entry found: {r.status_code}"

        yard = r.json()
        self.data["yardEntryId"] = yard.get("id")
        print(f"    yardEntryId: {self.data['yardEntryId']}")
        print(f"    status:      {yard.get('status')}")

        return True, f"Yard entry exists: {self.data['yardEntryId']}"

    def step_09_escalate_to_court(self):
        """Escalate case to court via PUT case/cases/{caseId} with disposition type."""
        cid = self.data["caseId"]

        # Find COURT_ESCALATION disposition type
        disp = self._find_taxonomy("case/taxonomy/disposition-types", "COURT_ESCALATION")
        if not disp:
            return False, "Could not find COURT_ESCALATION disposition type"

        self.data["courtDispositionTypeId"] = disp["id"]
        print(f"    Disposition type: {disp.get('code')} -> {disp['id']}")

        body = {
            "dispositionTypeId": disp["id"],
        }
        r = self._put(f"case/cases/{cid}", body)
        print(f"    PUT /case/cases/{cid} -> {r.status_code}")
        assert r.status_code == 200, f"Escalation failed: {r.status_code} {r.text[:300]}"

        case = r.json()
        print(f"    dispositionType: {case.get('dispositionType')}")
        return True, f"Escalated to court, disposition={case.get('dispositionType', disp.get('code'))}"

    def step_10_create_court_record(self):
        """Create a court record for hearings."""
        unique_suffix = str(uuid.uuid4())[:8].upper()
        body = {
            "code": f"E2E-MCT-{unique_suffix}",
            "name": f"E2E Magistrates Court {unique_suffix}",
            "location": "Nairobi",
            "courtType": "magistrate",
        }

        r = self._post("courts", body)
        print(f"    POST /courts -> {r.status_code}")

        if r.status_code not in (200, 201):
            # Try to find an existing court
            r2 = self._get("courts")
            courts = r2.json() if r2.status_code == 200 else []
            if isinstance(courts, dict):
                courts = courts.get("items", courts.get("data", []))
            if courts:
                self.data["courtId"] = courts[0]["id"]
                print(f"    Using existing court: {courts[0].get('name')} ({self.data['courtId']})")
                return True, f"Using existing court: {courts[0].get('name')}"
            return False, f"Create court failed: {r.status_code} {r.text[:300]}"

        court = r.json()
        self.data["courtId"] = court.get("id")
        print(f"    courtId: {self.data['courtId']}")
        print(f"    code:    {court.get('code')}")
        print(f"    name:    {court.get('name')}")

        return True, f"Court created: {court.get('name')} ({self.data['courtId']})"

    def step_11_assign_io(self):
        """Assign Investigating Officer (IO) to the case."""
        cid = self.data["caseId"]
        user_id = self.data["userId"]

        body = {
            "newOfficerId": user_id,
            "assignmentType": "initial",
            "reason": "Initial IO assignment for court case",
            "officerRank": "Inspector",
        }

        r = self._post(f"cases/{cid}/assignments", body)
        print(f"    POST /cases/{cid}/assignments -> {r.status_code}")
        assert r.status_code in (200, 201), f"Assignment failed: {r.status_code} {r.text[:300]}"

        assignment = r.json()
        self.data["assignmentId"] = assignment.get("id")
        is_current = assignment.get("isCurrent", False)
        print(f"    assignmentId: {self.data['assignmentId']}")
        print(f"    isCurrent:    {is_current}")

        return is_current, f"IO assigned, isCurrent={is_current}"

    def step_12_add_case_parties(self):
        """Add case parties: investigating officer and defendant driver."""
        cid = self.data["caseId"]
        user_id = self.data["userId"]
        driver_id = self.data.get("driverId")

        parties_added = 0

        # Party 1: Investigating Officer
        body_io = {
            "partyRole": "investigating_officer",
            "userId": user_id,
        }
        r = self._post(f"cases/{cid}/parties", body_io)
        print(f"    POST /cases/{cid}/parties (IO) -> {r.status_code}")
        if r.status_code in (200, 201):
            parties_added += 1
            party = r.json()
            print(f"    IO party id: {party.get('id')}")
        else:
            print(f"    IO party response: {r.text[:200]}")

        # Party 2: Defendant Driver
        if driver_id:
            body_driver = {
                "partyRole": "defendant_driver",
                "driverId": driver_id,
            }
            r = self._post(f"cases/{cid}/parties", body_driver)
            print(f"    POST /cases/{cid}/parties (driver) -> {r.status_code}")
            if r.status_code in (200, 201):
                parties_added += 1
                party = r.json()
                print(f"    Driver party id: {party.get('id')}")
            else:
                print(f"    Driver party response: {r.text[:200]}")

        # Verify total parties
        r = self._get(f"cases/{cid}/parties")
        total_parties = 0
        if r.status_code == 200:
            parties_list = r.json()
            if isinstance(parties_list, list):
                total_parties = len(parties_list)
            elif isinstance(parties_list, dict):
                items = parties_list.get("items", parties_list.get("data", []))
                total_parties = len(items)
        print(f"    Total parties on case: {total_parties}")

        return total_parties >= 2, f"Added {parties_added} parties, total on case: {total_parties}"

    def step_13_upload_subfile_b(self):
        """Upload Subfile B (Document Evidence)."""
        cid = self.data["caseId"]

        # Find subfile type EVIDENCE (maps to Subfile B - Document Evidence)
        subfile_type = self._find_taxonomy("case/taxonomy/subfile-types", "EVIDENCE")
        if not subfile_type:
            return False, "Could not find subfile type B"

        self.data["subfileTypeB"] = subfile_type["id"]
        print(f"    Subfile type B: {subfile_type.get('code')} -> {subfile_type['id']}")

        body = {
            "caseRegisterId": cid,
            "subfileTypeId": subfile_type["id"],
            "subfileName": "Weight Ticket Evidence",
            "documentType": "evidence",
            "content": "Digital weight ticket attached. Vehicle KDG 606L recorded GVW of 27500kg, exceeding permissible limit.",
        }

        r = self._post("case/subfiles", body)
        print(f"    POST /case/subfiles (type B) -> {r.status_code}")
        assert r.status_code in (200, 201), f"Subfile B failed: {r.status_code} {r.text[:300]}"

        sf = r.json()
        self.data["subfileBId"] = sf.get("id")
        print(f"    subfileId: {self.data['subfileBId']}")

        return True, f"Subfile B (Evidence) created: {self.data['subfileBId']}"

    def step_14_upload_subfile_d(self):
        """Upload Subfile D (Witness Statement)."""
        cid = self.data["caseId"]

        subfile_type = self._find_taxonomy("case/taxonomy/subfile-types", "DRIVER_DOCS")
        if not subfile_type:
            return False, "Could not find subfile type D"

        self.data["subfileTypeD"] = subfile_type["id"]
        print(f"    Subfile type D: {subfile_type.get('code')} -> {subfile_type['id']}")

        body = {
            "caseRegisterId": cid,
            "subfileTypeId": subfile_type["id"],
            "subfileName": "Witness Statement - Weighbridge Operator",
            "documentType": "witness_statement",
            "content": "I, the weighbridge operator, witnessed vehicle KDG 606L being weighed. The GVW reading was 27500kg.",
        }

        r = self._post("case/subfiles", body)
        print(f"    POST /case/subfiles (type D) -> {r.status_code}")
        assert r.status_code in (200, 201), f"Subfile D failed: {r.status_code} {r.text[:300]}"

        sf = r.json()
        self.data["subfileDId"] = sf.get("id")
        print(f"    subfileId: {self.data['subfileDId']}")

        return True, f"Subfile D (Witness Statement) created: {self.data['subfileDId']}"

    def step_15_upload_subfile_f(self):
        """Upload Subfile F (Investigation Diary)."""
        cid = self.data["caseId"]

        subfile_type = self._find_taxonomy("case/taxonomy/subfile-types", "LEGAL_NOTICES")
        if not subfile_type:
            return False, "Could not find subfile type F"

        self.data["subfileTypeF"] = subfile_type["id"]
        print(f"    Subfile type F: {subfile_type.get('code')} -> {subfile_type['id']}")

        body = {
            "caseRegisterId": cid,
            "subfileTypeId": subfile_type["id"],
            "subfileName": "Investigation Diary Entry",
            "documentType": "diary",
            "content": f"Investigation diary for case. "
                       f"Date: {datetime.utcnow().strftime('%Y-%m-%d')}. "
                       f"Investigating officer confirmed overloading violation. "
                       f"Vehicle impounded. Driver cautioned.",
        }

        r = self._post("case/subfiles", body)
        print(f"    POST /case/subfiles (type F) -> {r.status_code}")
        assert r.status_code in (200, 201), f"Subfile F failed: {r.status_code} {r.text[:300]}"

        sf = r.json()
        self.data["subfileFId"] = sf.get("id")
        print(f"    subfileId: {self.data['subfileFId']}")

        return True, f"Subfile F (Investigation Diary) created: {self.data['subfileFId']}"

    def step_16_upload_subfile_g(self):
        """Upload Subfile G (Charge Sheet)."""
        cid = self.data["caseId"]

        subfile_type = self._find_taxonomy("case/taxonomy/subfile-types", "COURT_FILINGS")
        if not subfile_type:
            return False, "Could not find subfile type G"

        self.data["subfileTypeG"] = subfile_type["id"]
        print(f"    Subfile type G: {subfile_type.get('code')} -> {subfile_type['id']}")

        body = {
            "caseRegisterId": cid,
            "subfileTypeId": subfile_type["id"],
            "subfileName": "Charge Sheet - Overloading",
            "documentType": "charge_sheet",
            "content": "CHARGE SHEET: The accused is charged with contravening Section 56 of the Traffic Act "
                       "by operating vehicle KDG 606L with a GVW of 27500kg exceeding the permissible limit.",
        }

        r = self._post("case/subfiles", body)
        print(f"    POST /case/subfiles (type G) -> {r.status_code}")
        assert r.status_code in (200, 201), f"Subfile G failed: {r.status_code} {r.text[:300]}"

        sf = r.json()
        self.data["subfileGId"] = sf.get("id")
        print(f"    subfileId: {self.data['subfileGId']}")

        return True, f"Subfile G (Charge Sheet) created: {self.data['subfileGId']}"

    def step_17_check_subfile_completion(self):
        """Check subfile completion status for the case."""
        cid = self.data["caseId"]

        r = self._get(f"case/subfiles/by-case/{cid}/completion")
        print(f"    GET /case/subfiles/by-case/{cid}/completion -> {r.status_code}")

        if r.status_code != 200:
            # Fallback: count subfiles directly
            r2 = self._get(f"case/subfiles/by-case/{cid}")
            if r2.status_code == 200:
                subfiles = r2.json()
                if isinstance(subfiles, list):
                    count = len(subfiles)
                elif isinstance(subfiles, dict):
                    items = subfiles.get("items", subfiles.get("data", []))
                    count = len(items)
                else:
                    count = 0
                print(f"    Subfiles count (fallback): {count}")
                return count >= 4, f"Subfile count: {count} (fallback, completion endpoint returned {r.status_code})"
            return False, f"Completion check failed: {r.status_code}"

        completion = r.json()
        completed_types = completion.get("completedTypes", 0)
        total_types = completion.get("totalTypes", 0)
        print(f"    completedTypes: {completed_types}")
        print(f"    totalTypes:     {total_types}")

        return completed_types >= 4, f"Completed {completed_types}/{total_types} subfile types"

    def step_18_schedule_first_hearing(self):
        """Schedule first hearing (mention) for the case."""
        cid = self.data["caseId"]
        court_id = self.data.get("courtId")
        if not court_id:
            return False, "No court ID available (step 10 may have failed)"

        # Find MENTION hearing type
        hearing_type = self._find_taxonomy("case/taxonomy/hearing-types", "MENTION")
        if not hearing_type:
            return False, "Could not find MENTION hearing type"

        self.data["mentionTypeId"] = hearing_type["id"]
        print(f"    Hearing type: {hearing_type.get('code')} -> {hearing_type['id']}")

        tomorrow = (datetime.utcnow() + timedelta(days=1)).strftime("%Y-%m-%dT10:00:00Z")

        body = {
            "courtId": court_id,
            "hearingDate": tomorrow,
            "hearingTypeId": hearing_type["id"],
            "presidingOfficer": "Hon. Magistrate E2E",
        }

        r = self._post(f"cases/{cid}/hearings", body)
        print(f"    POST /cases/{cid}/hearings -> {r.status_code}")
        assert r.status_code in (200, 201), f"Schedule hearing failed: {r.status_code} {r.text[:300]}"

        hearing = r.json()
        self.data["hearingId1"] = hearing.get("id")
        print(f"    hearingId: {self.data['hearingId1']}")
        print(f"    date:      {hearing.get('hearingDate')}")
        print(f"    type:      {hearing.get('hearingTypeName', hearing.get('hearingType'))}")

        return True, f"First hearing (mention) scheduled: {self.data['hearingId1']}"

    def step_19_adjourn_first_hearing(self):
        """Adjourn the first hearing."""
        hearing_id = self.data["hearingId1"]

        next_week = (datetime.utcnow() + timedelta(days=7)).strftime("%Y-%m-%dT10:00:00Z")

        body = {
            "adjournmentReason": "Accused needs legal representation",
            "nextHearingDate": next_week,
        }

        r = self._post(f"hearings/{hearing_id}/adjourn", body)
        print(f"    POST /hearings/{hearing_id}/adjourn -> {r.status_code}")
        assert r.status_code in (200, 201), f"Adjourn failed: {r.status_code} {r.text[:300]}"

        hearing = r.json()
        status_name = hearing.get("hearingStatusName", hearing.get("status", ""))
        print(f"    hearingStatusName: {status_name}")

        adjourned = "adjourned" in status_name.lower() if status_name else False
        return adjourned, f"Hearing adjourned, status={status_name}"

    def step_20_verify_next_hearing_auto_created(self):
        """Verify that a next hearing was auto-created after adjournment."""
        cid = self.data["caseId"]

        r = self._get(f"cases/{cid}/hearings")
        print(f"    GET /cases/{cid}/hearings -> {r.status_code}")
        assert r.status_code == 200, f"Fetch hearings failed: {r.status_code}"

        hearings = r.json()
        if isinstance(hearings, dict):
            hearings = hearings.get("items", hearings.get("data", []))

        print(f"    Total hearings: {len(hearings)}")
        for i, h in enumerate(hearings):
            print(f"    Hearing {i+1}: id={h.get('id')}, type={h.get('hearingTypeName', h.get('hearingType'))}, status={h.get('hearingStatusName', h.get('status'))}")

        # Save the second hearing for later use
        if len(hearings) >= 2:
            # Find the hearing that is NOT the first one (scheduled/pending)
            for h in hearings:
                if h.get("id") != self.data["hearingId1"]:
                    self.data["hearingId2"] = h["id"]
                    break
            if not self.data.get("hearingId2"):
                self.data["hearingId2"] = hearings[1]["id"]
            print(f"    Second hearing saved: {self.data['hearingId2']}")

        return len(hearings) >= 2, f"Found {len(hearings)} hearings (expected >= 2)"

    def step_21_issue_arrest_warrant(self):
        """Issue an arrest warrant for the case."""
        cid = self.data["caseId"]

        body = {
            "caseRegisterId": cid,
            "accusedName": "E2E Test Driver",
            "accusedIdNo": "12345678",
            "offenceDescription": "Overloading contrary to Traffic Act Section 56",
            "issuedBy": "Nairobi Law Courts",
        }

        r = self._post("case/warrants", body)
        print(f"    POST /case/warrants -> {r.status_code}")
        assert r.status_code in (200, 201), f"Warrant creation failed: {r.status_code} {r.text[:300]}"

        warrant = r.json()
        self.data["warrantId"] = warrant.get("id")
        warrant_no = warrant.get("warrantNo", "")
        print(f"    warrantId: {self.data['warrantId']}")
        print(f"    warrantNo: {warrant_no}")
        print(f"    status:    {warrant.get('warrantStatusName', warrant.get('status'))}")

        starts_with_war = warrant_no.startswith("WAR-") if warrant_no else False
        return starts_with_war or bool(self.data["warrantId"]), f"Warrant issued: {warrant_no}"

    def step_22_execute_warrant(self):
        """Execute the arrest warrant."""
        warrant_id = self.data["warrantId"]

        body = {
            "executionDetails": "Accused arrested and brought to court",
        }

        r = requests.post(self._url(f"case/warrants/{warrant_id}/execute"),
                          headers=self._auth_headers(), json=body, timeout=60)
        print(f"    POST /case/warrants/{warrant_id}/execute -> {r.status_code}")
        assert r.status_code in (200, 201), f"Warrant execution failed: {r.status_code} {r.text[:300]}"

        warrant = r.json()
        status_name = warrant.get("warrantStatusName", warrant.get("status", ""))
        print(f"    warrantStatusName: {status_name}")

        executed = "executed" in status_name.lower() if status_name else False
        return executed, f"Warrant status: {status_name}"

    def step_23_complete_second_hearing(self):
        """Complete the second hearing (plea) with conviction."""
        hearing_id = self.data.get("hearingId2")
        if not hearing_id:
            return False, "No second hearing ID available"

        # Find PLEA hearing type
        plea_type = self._find_taxonomy("case/taxonomy/hearing-types", "PLEA")
        if plea_type:
            print(f"    Plea type: {plea_type.get('code')} -> {plea_type['id']}")

        # Find CONVICTED hearing outcome from taxonomy
        convicted = self._find_taxonomy("case/taxonomy/hearing-outcomes", "CONVICTED")
        if not convicted:
            return False, "Could not find CONVICTED hearing outcome"

        self.data["convictedOutcomeId"] = convicted["id"]
        print(f"    Convicted outcome: {convicted.get('code')} -> {convicted['id']}")

        body = {
            "hearingOutcomeId": convicted["id"],
            "minuteNotes": "Accused pleaded guilty. Fined KES 50,000",
            "fineAmount": 50000,
            "sentenceDetails": "Fine of KES 50,000 or 3 months imprisonment",
        }

        # If plea type found, include it
        if plea_type:
            body["hearingTypeId"] = plea_type["id"]

        r = self._post(f"hearings/{hearing_id}/complete", body)
        print(f"    POST /hearings/{hearing_id}/complete -> {r.status_code}")
        assert r.status_code in (200, 201), f"Complete hearing failed: {r.status_code} {r.text[:300]}"

        hearing = r.json()
        print(f"    outcome:  {hearing.get('hearingOutcomeName', hearing.get('outcome'))}")
        print(f"    status:   {hearing.get('hearingStatusName', hearing.get('status'))}")

        return True, f"Second hearing completed with conviction, fine=KES 50,000"

    def step_24_create_closure_checklist(self):
        """Create/update closure checklist with all subfiles marked complete."""
        cid = self.data["caseId"]

        # Look up closure type CONVICTION
        closure_type = self._find_taxonomy("case/taxonomy/closure-types", "CONVICTION")
        if closure_type:
            self.data["closureTypeId"] = closure_type["id"]
            print(f"    Closure type: {closure_type.get('code')} -> {closure_type['id']}")

        body = {
            "subfileAComplete": True,
            "subfileBComplete": True,
            "subfileCComplete": True,
            "subfileDComplete": True,
            "subfileEComplete": True,
            "subfileFComplete": True,
            "subfileGComplete": True,
            "subfileHComplete": True,
            "subfileIComplete": True,
            "subfileJComplete": True,
        }

        if self.data.get("closureTypeId"):
            body["closureTypeId"] = self.data["closureTypeId"]

        r = self._put(f"cases/{cid}/closure-checklist", body)
        print(f"    PUT /cases/{cid}/closure-checklist -> {r.status_code}")
        assert r.status_code in (200, 201), f"Closure checklist failed: {r.status_code} {r.text[:300]}"

        checklist = r.json()
        all_verified = checklist.get("allSubfilesVerified", False)
        print(f"    allSubfilesVerified: {all_verified}")

        return all_verified, f"Closure checklist created, allSubfilesVerified={all_verified}"

    def step_25_request_closure_review(self):
        """Request closure review for the case."""
        cid = self.data["caseId"]

        body = {
            "reviewNotes": "All subfiles complete, case ready for closure",
        }

        r = self._post(f"cases/{cid}/closure-checklist/request-review", body)
        print(f"    POST /cases/{cid}/closure-checklist/request-review -> {r.status_code}")
        assert r.status_code in (200, 201), f"Request review failed: {r.status_code} {r.text[:300]}"

        review = r.json()
        review_status = review.get("reviewStatusName", review.get("reviewStatus", ""))
        print(f"    reviewStatusName: {review_status}")

        requested = ("pending" in review_status.lower() or "requested" in review_status.lower()) if review_status else False
        return requested, f"Review status: {review_status}"

    def step_26_approve_closure_review(self):
        """Approve the closure review."""
        cid = self.data["caseId"]

        body = {
            "reviewNotes": "Reviewed and approved for closure",
        }

        r = self._post(f"cases/{cid}/closure-checklist/approve-review", body)
        print(f"    POST /cases/{cid}/closure-checklist/approve-review -> {r.status_code}")
        assert r.status_code in (200, 201), f"Approve review failed: {r.status_code} {r.text[:300]}"

        review = r.json()
        review_status = review.get("reviewStatusName", review.get("reviewStatus", ""))
        print(f"    reviewStatusName: {review_status}")

        approved = "approved" in review_status.lower() if review_status else False
        return approved, f"Review status: {review_status}"

    def step_27_close_case(self):
        """Close the case with conviction disposition."""
        cid = self.data["caseId"]

        # Use the court disposition type (from disposition_types table, NOT closure_types)
        disposition_id = self.data.get("courtDispositionTypeId")

        body = {
            "closingReason": "Case concluded. Accused convicted and fined.",
        }
        if disposition_id:
            body["dispositionTypeId"] = disposition_id

        r = self._post(f"case/cases/{cid}/close", body)
        print(f"    POST /case/cases/{cid}/close -> {r.status_code}")
        assert r.status_code in (200, 201), f"Close case failed: {r.status_code} {r.text[:300]}"

        case = r.json()
        case_status = case.get("caseStatus", "")
        print(f"    caseStatus: {case_status}")

        is_closed = "closed" in case_status.lower() if case_status else False
        return is_closed, f"Case status: {case_status}"

    def step_28_verify_final_state(self):
        """Verify the complete final state of the closed case."""
        cid = self.data["caseId"]
        checks = []
        all_pass = True

        # 1. Verify case status is Closed
        r = self._get(f"case/cases/{cid}")
        print(f"    GET /case/cases/{cid} -> {r.status_code}")
        if r.status_code == 200:
            case = r.json()
            case_status = case.get("caseStatus", "")
            is_closed = "closed" in case_status.lower()
            checks.append(f"caseStatus={'PASS' if is_closed else 'FAIL'} ({case_status})")
            if not is_closed:
                all_pass = False
            print(f"    Case status: {case_status} -> {'PASS' if is_closed else 'FAIL'}")
        else:
            checks.append("caseStatus=FAIL (fetch error)")
            all_pass = False

        # 2. Verify parties >= 2
        r = self._get(f"cases/{cid}/parties")
        if r.status_code == 200:
            parties = r.json()
            if isinstance(parties, dict):
                parties = parties.get("items", parties.get("data", []))
            party_count = len(parties) if isinstance(parties, list) else 0
            party_ok = party_count >= 2
            checks.append(f"parties={'PASS' if party_ok else 'FAIL'} ({party_count})")
            if not party_ok:
                all_pass = False
            print(f"    Parties: {party_count} -> {'PASS' if party_ok else 'FAIL'}")
        else:
            checks.append("parties=FAIL (fetch error)")
            all_pass = False

        # 3. Verify subfiles >= 4
        r = self._get(f"case/subfiles/by-case/{cid}")
        if r.status_code == 200:
            subfiles = r.json()
            if isinstance(subfiles, dict):
                subfiles = subfiles.get("items", subfiles.get("data", []))
            sf_count = len(subfiles) if isinstance(subfiles, list) else 0
            sf_ok = sf_count >= 4
            checks.append(f"subfiles={'PASS' if sf_ok else 'FAIL'} ({sf_count})")
            if not sf_ok:
                all_pass = False
            print(f"    Subfiles: {sf_count} -> {'PASS' if sf_ok else 'FAIL'}")
        else:
            checks.append("subfiles=FAIL (fetch error)")
            all_pass = False

        # 4. Verify hearings >= 2
        r = self._get(f"cases/{cid}/hearings")
        if r.status_code == 200:
            hearings = r.json()
            if isinstance(hearings, dict):
                hearings = hearings.get("items", hearings.get("data", []))
            h_count = len(hearings) if isinstance(hearings, list) else 0
            h_ok = h_count >= 2
            checks.append(f"hearings={'PASS' if h_ok else 'FAIL'} ({h_count})")
            if not h_ok:
                all_pass = False
            print(f"    Hearings: {h_count} -> {'PASS' if h_ok else 'FAIL'}")
        else:
            checks.append("hearings=FAIL (fetch error)")
            all_pass = False

        # 5. Verify warrants >= 1
        r = self._get(f"case/warrants/by-case/{cid}")
        if r.status_code == 200:
            warrants = r.json()
            if isinstance(warrants, dict):
                warrants = warrants.get("items", warrants.get("data", []))
            w_count = len(warrants) if isinstance(warrants, list) else 0
            w_ok = w_count >= 1
            checks.append(f"warrants={'PASS' if w_ok else 'FAIL'} ({w_count})")
            if not w_ok:
                all_pass = False
            print(f"    Warrants: {w_count} -> {'PASS' if w_ok else 'FAIL'}")
        else:
            checks.append("warrants=FAIL (fetch error)")
            all_pass = False

        # 6. Verify closure checklist allSubfilesVerified == true
        r = self._get(f"cases/{cid}/closure-checklist")
        if r.status_code == 200:
            checklist = r.json()
            verified = checklist.get("allSubfilesVerified", False)
            checks.append(f"closureChecklist={'PASS' if verified else 'FAIL'} (verified={verified})")
            if not verified:
                all_pass = False
            print(f"    Closure checklist verified: {verified} -> {'PASS' if verified else 'FAIL'}")
        else:
            checks.append("closureChecklist=FAIL (fetch error)")
            all_pass = False

        # 7. Verify assignments >= 1
        r = self._get(f"cases/{cid}/assignments")
        if r.status_code == 200:
            assignments = r.json()
            if isinstance(assignments, dict):
                assignments = assignments.get("items", assignments.get("data", []))
            a_count = len(assignments) if isinstance(assignments, list) else 0
            a_ok = a_count >= 1
            checks.append(f"assignments={'PASS' if a_ok else 'FAIL'} ({a_count})")
            if not a_ok:
                all_pass = False
            print(f"    Assignments: {a_count} -> {'PASS' if a_ok else 'FAIL'}")
        else:
            checks.append("assignments=FAIL (fetch error)")
            all_pass = False

        summary = "; ".join(checks)
        return all_pass, f"Final state: {summary}"

    # ── Runner ───────────────────────────────────────────────────────────

    def run(self):
        """Execute all steps in order."""
        print("\n" + "=" * 70)
        print("  TRULOAD COMPLIANCE E2E TEST -- SCENARIO 6")
        print("  Full Court Case Lifecycle")
        print(f"  Target: {self.base_url}")
        print(f"  Started: {datetime.utcnow().isoformat()}Z")
        print("=" * 70)
        print()
        print("  Workflow: Metadata -> Autoweigh -> Overload -> Case+Yard")
        print("         -> Escalate -> Court -> IO -> Parties -> Subfiles")
        print("         -> Hearings -> Warrants -> Closure -> Review -> Close")
        print()

        steps = [
            (1,  "Login",                                              self.step_01_login),
            (2,  "Setup metadata (driver, transporter, cargo, locs)",  self.step_02_setup_metadata),
            (3,  "Create scale test",                                  self.step_03_scale_test),
            (4,  "Autoweigh overloaded vehicle (KDG 606L, GVW=27500)", self.step_04_autoweigh_overloaded),
            (5,  "Update weighing metadata (driver, transporter)",     self.step_05_update_metadata),
            (6,  "Capture weights (triggers compliance + auto-case)",  self.step_06_capture_weights),
            (7,  "Verify case auto-created",                           self.step_07_verify_case_auto_created),
            (8,  "Verify yard entry auto-created",                     self.step_08_verify_yard_entry),
            (9,  "Escalate to court",                                  self.step_09_escalate_to_court),
            (10, "Create court record",                                self.step_10_create_court_record),
            (11, "Assign IO (initial assignment)",                     self.step_11_assign_io),
            (12, "Add case parties (IO + defendant driver)",           self.step_12_add_case_parties),
            (13, "Upload Subfile B (document evidence)",               self.step_13_upload_subfile_b),
            (14, "Upload Subfile D (witness statement)",               self.step_14_upload_subfile_d),
            (15, "Upload Subfile F (investigation diary)",             self.step_15_upload_subfile_f),
            (16, "Upload Subfile G (charge sheet)",                    self.step_16_upload_subfile_g),
            (17, "Check subfile completion",                           self.step_17_check_subfile_completion),
            (18, "Schedule first hearing (mention)",                   self.step_18_schedule_first_hearing),
            (19, "Adjourn first hearing",                              self.step_19_adjourn_first_hearing),
            (20, "Verify next hearing auto-created",                   self.step_20_verify_next_hearing_auto_created),
            (21, "Issue arrest warrant",                               self.step_21_issue_arrest_warrant),
            (22, "Execute warrant",                                    self.step_22_execute_warrant),
            (23, "Complete second hearing (plea with conviction)",     self.step_23_complete_second_hearing),
            (24, "Create closure checklist",                           self.step_24_create_closure_checklist),
            (25, "Request closure review",                             self.step_25_request_closure_review),
            (26, "Approve closure review",                             self.step_26_approve_closure_review),
            (27, "Close the case",                                     self.step_27_close_case),
            (28, "Verify final state",                                 self.step_28_verify_final_state),
        ]

        for num, name, fn in steps:
            passed = self._step(num, name, fn)
            if not passed and num <= 8:
                # Critical steps -- abort if login/setup/weighing/case/yard fail
                print(f"\n  *** ABORTING: Critical step {num} failed ***")
                break

        self._print_summary()

    def _print_summary(self):
        """Print final test summary."""
        print("\n\n" + "=" * 70)
        print("  E2E TEST SUMMARY -- SCENARIO 6: Full Court Case Lifecycle")
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
        for key in ["weighingId", "vehicleId", "caseId", "caseNo", "yardEntryId",
                     "courtId", "assignmentId", "hearingId1", "hearingId2",
                     "warrantId", "subfileBId", "subfileDId", "subfileFId",
                     "subfileGId", "closureTypeId", "courtDispositionTypeId",
                     "convictedOutcomeId", "scaleTestId",
                     "driverId", "transporterId", "userId"]:
            val = self.data.get(key, "---")
            print(f"    {key}: {val}")

        print("=" * 70)
        return fail_count == 0


# ─── Main ───────────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description="TruLoad Compliance E2E Test - Scenario 6: Full Court Case Lifecycle")
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
