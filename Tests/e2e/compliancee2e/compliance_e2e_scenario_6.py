#!/usr/bin/env python3
"""
TruLoad Compliance E2E Test -- Scenario 6
==========================================
Full Court Case Lifecycle: escalation -> investigation -> hearings ->
subfiles -> diary -> arrest warrants -> review -> closure.

Workflow:
  1. Overload detected -> Case + Yard auto-created
  2. Escalate to court
  3. Create court record, assign IO, add case parties
  4. Upload subfiles (B, D, F, G) for evidence and investigation
  5. Schedule hearings (mention -> adjourn -> plea)
  6. Issue and execute arrest warrant
  7. Complete second hearing with conviction
  8. Closure checklist -> request review -> approve -> close case
  9. Verify final state (parties, subfiles, hearings, warrants, assignments)

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
from typing import Any, Optional

try:
    import requests
except ImportError:
    print("ERROR: 'requests' package required. Install with: pip install requests")
    sys.exit(1)

# --- Configuration -----------------------------------------------------------

DEFAULT_BASE_URL = "http://localhost:4000"
API_PREFIX = "/api/v1"
LOGIN_EMAIL = "gadmin@masterspace.co.ke"
LOGIN_PASSWORD = "ChangeMe123!"

# Overloaded axle weights (3-axle, GVW=27500, clearly overloaded)
OVERLOADED_AXLES = [
    {"axleNumber": 1, "measuredWeightKg": 8500},
    {"axleNumber": 2, "measuredWeightKg": 9500},
    {"axleNumber": 3, "measuredWeightKg": 9500},
]

VEHICLE_REG = "KDG 606L"


# --- Test Runner --------------------------------------------------------------

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

    # -- Helper: find taxonomy item by code ------------------------------------

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

    # -- Step Implementations --------------------------------------------------

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

    def step_02_lookup_metadata(self):
        """Lookup vehicle, driver, and station IDs for the test."""
        found = []

        # -- Vehicle --
        r = self._get(f"vehicles/search?query={VEHICLE_REG}")
        print(f"    GET /vehicles/search?query={VEHICLE_REG} -> {r.status_code}")
        vehicles = r.json() if r.status_code == 200 else []
        if isinstance(vehicles, dict):
            vehicles = vehicles.get("items", vehicles.get("data", []))
        if vehicles:
            self.data["vehicleId"] = vehicles[0]["id"]
            print(f"    Vehicle found: {vehicles[0].get('registrationNumber', 'N/A')} ({self.data['vehicleId']})")
            found.append("vehicle")
        else:
            # Try broader search
            r2 = self._get("vehicles")
            all_v = r2.json() if r2.status_code == 200 else []
            if isinstance(all_v, dict):
                all_v = all_v.get("items", all_v.get("data", []))
            if all_v:
                self.data["vehicleId"] = all_v[0]["id"]
                found.append("vehicle(fallback)")

        # -- Driver --
        r = self._get("drivers/search?query=E2E")
        print(f"    GET /drivers/search?query=E2E -> {r.status_code}")
        drivers = r.json() if r.status_code == 200 else []
        if isinstance(drivers, dict):
            drivers = drivers.get("items", drivers.get("data", []))
        if drivers:
            self.data["driverId"] = drivers[0]["id"]
            self.data["driverLicenseNo"] = drivers[0].get("drivingLicenseNo", "E2E-DL-001")
            print(f"    Driver found: {drivers[0].get('fullNames', 'N/A')} ({self.data['driverId']})")
            found.append("driver")
        else:
            # Fallback: search for any driver
            r2 = self._get("drivers/search?query=")
            all_d = r2.json() if r2.status_code == 200 else []
            if isinstance(all_d, dict):
                all_d = all_d.get("items", all_d.get("data", []))
            if all_d:
                self.data["driverId"] = all_d[0]["id"]
                self.data["driverLicenseNo"] = all_d[0].get("drivingLicenseNo", "DL-001")
                found.append("driver(fallback)")

        # -- Station (from login) --
        if self.data.get("stationId"):
            r = self._get(f"stations/{self.data['stationId']}")
            print(f"    GET /stations/{self.data['stationId']} -> {r.status_code}")
            if r.status_code == 200:
                station = r.json()
                print(f"    Station: {station.get('name', 'N/A')}")
                found.append("station")
        else:
            r = self._get("stations")
            stations = r.json() if r.status_code == 200 else []
            if isinstance(stations, dict):
                stations = stations.get("items", stations.get("data", []))
            if stations:
                self.data["stationId"] = stations[0]["id"]
                found.append("station(fallback)")

        # -- Axle Config --
        r = self._get("axle-configurations")
        print(f"    GET /axle-configurations -> {r.status_code}")
        axle_configs = r.json() if r.status_code == 200 else []
        if isinstance(axle_configs, dict):
            axle_configs = axle_configs.get("items", axle_configs.get("data", []))
        if axle_configs:
            # Try to find a 3-axle config
            for cfg in axle_configs:
                if cfg.get("axleCount") == 3 or "3" in str(cfg.get("code", "")):
                    self.data["axleConfigId"] = cfg["id"]
                    break
            if not self.data.get("axleConfigId"):
                self.data["axleConfigId"] = axle_configs[0]["id"]
            found.append("axleConfig")

        # -- Cargo Type --
        r = self._get("cargo-types")
        print(f"    GET /cargo-types -> {r.status_code}")
        cargos = r.json() if r.status_code == 200 else []
        if isinstance(cargos, dict):
            cargos = cargos.get("items", cargos.get("data", []))
        if cargos:
            self.data["cargoTypeId"] = cargos[0]["id"]
            found.append("cargoType")

        # -- Origins & Destinations --
        r = self._get("origins-destinations")
        print(f"    GET /origins-destinations -> {r.status_code}")
        locations = r.json() if r.status_code == 200 else []
        if isinstance(locations, dict):
            locations = locations.get("items", locations.get("data", []))
        if len(locations) >= 2:
            self.data["originId"] = locations[0]["id"]
            self.data["destinationId"] = locations[1]["id"]
            found.append("origin+destination")
        elif len(locations) == 1:
            self.data["originId"] = locations[0]["id"]
            self.data["destinationId"] = locations[0]["id"]
            found.append("origin(single)")

        summary = ", ".join(found)
        all_ok = all(self.data.get(k) for k in ["stationId", "driverId"])
        return all_ok, f"Found: {summary}"

    def step_03_start_weighing(self):
        """Start a weighing session for the vehicle."""
        body = {
            "stationId": self.data["stationId"],
            "vehicleRegNumber": VEHICLE_REG,
            "driverLicenseNo": self.data.get("driverLicenseNo", "E2E-DL-001"),
        }
        if self.data.get("axleConfigId"):
            body["axleConfigId"] = self.data["axleConfigId"]
        if self.data.get("cargoTypeId"):
            body["cargoTypeId"] = self.data["cargoTypeId"]
        if self.data.get("originId"):
            body["originId"] = self.data["originId"]
        if self.data.get("destinationId"):
            body["destinationId"] = self.data["destinationId"]

        r = self._post("weighing/start", body)
        print(f"    POST /weighing/start -> {r.status_code}")

        if r.status_code not in (200, 201):
            # Try alternate endpoint
            r = self._post("weighing-transactions/start", body)
            print(f"    POST /weighing-transactions/start -> {r.status_code}")

        assert r.status_code in (200, 201), f"Start weighing failed: {r.status_code} {r.text[:300]}"

        data = r.json()
        txn = data.get("transaction", data)
        self.data["weighingId"] = txn.get("weighingId") or txn.get("id")
        print(f"    weighingId: {self.data['weighingId']}")

        return True, f"Weighing started: {self.data['weighingId']}"

    def step_04_complete_weighing_overloaded(self):
        """Complete weighing with overloaded axle weights. Assert ControlStatus == 'Overloaded'."""
        wid = self.data["weighingId"]
        body = {
            "axles": OVERLOADED_AXLES,
        }

        r = self._post(f"weighing/{wid}/complete", body)
        print(f"    POST /weighing/{wid}/complete -> {r.status_code}")

        if r.status_code not in (200, 201):
            # Try alternate endpoints
            r = self._post(f"weighing-transactions/{wid}/capture-weights", body)
            print(f"    POST /weighing-transactions/{wid}/capture-weights -> {r.status_code}")

        assert r.status_code in (200, 201), f"Complete weighing failed: {r.status_code} {r.text[:300]}"

        txn = r.json()
        control_status = txn.get("controlStatus", "")
        print(f"    controlStatus: {control_status}")
        print(f"    isCompliant:   {txn.get('isCompliant')}")
        print(f"    gvwMeasuredKg: {txn.get('gvwMeasuredKg')}")
        print(f"    overloadKg:    {txn.get('overloadKg')}")

        is_overloaded = control_status == "Overloaded" or txn.get("isCompliant") is False
        return is_overloaded, f"ControlStatus={control_status}, GVW={txn.get('gvwMeasuredKg')}kg"

    def step_05_verify_case_auto_created(self):
        """Verify case register was auto-created on overload detection."""
        wid = self.data["weighingId"]
        time.sleep(1)

        r = self._get(f"cases/by-weighing/{wid}")
        print(f"    GET /cases/by-weighing/{wid} -> {r.status_code}")

        if r.status_code != 200:
            # Try alternate path
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

    def step_06_verify_yard_entry_auto_created(self):
        """Verify yard entry was auto-created."""
        wid = self.data["weighingId"]

        r = self._get(f"yard/by-weighing/{wid}")
        print(f"    GET /yard/by-weighing/{wid} -> {r.status_code}")

        if r.status_code != 200:
            # Try alternate paths
            r = self._get(f"yard-entries/by-weighing/{wid}")
            print(f"    GET /yard-entries/by-weighing/{wid} -> {r.status_code}")

        if r.status_code != 200:
            return False, f"No yard entry found: {r.status_code}"

        yard = r.json()
        self.data["yardEntryId"] = yard.get("id")
        print(f"    yardEntryId: {self.data['yardEntryId']}")
        print(f"    status:      {yard.get('status')}")

        return True, f"Yard entry exists: {self.data['yardEntryId']}"

    def step_07_escalate_to_court(self):
        """Escalate case to court via PUT cases/{caseId} with disposition type."""
        cid = self.data["caseId"]

        # Find COURT_ESCALATION disposition type
        disp = self._find_taxonomy("taxonomy/disposition-types", "COURT_ESCALATION")
        if not disp:
            return False, "Could not find COURT_ESCALATION disposition type"

        self.data["courtDispositionTypeId"] = disp["id"]
        print(f"    Disposition type: {disp.get('code')} -> {disp['id']}")

        body = {
            "dispositionTypeId": disp["id"],
        }
        r = self._put(f"cases/{cid}", body)
        print(f"    PUT /cases/{cid} -> {r.status_code}")

        if r.status_code not in (200, 201):
            # Try alternate path
            r = self._put(f"case/cases/{cid}", body)
            print(f"    PUT /case/cases/{cid} -> {r.status_code}")

        assert r.status_code == 200, f"Escalation failed: {r.status_code} {r.text[:300]}"

        case = r.json()
        print(f"    dispositionType: {case.get('dispositionType')}")
        return True, f"Escalated to court, disposition={case.get('dispositionType', disp.get('code'))}"

    def step_08_create_court_record(self):
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

    def step_09_assign_io(self):
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

        if r.status_code not in (200, 201):
            # Try alternate path
            r = self._post(f"case/cases/{cid}/assignments", body)
            print(f"    POST /case/cases/{cid}/assignments -> {r.status_code}")

        assert r.status_code in (200, 201), f"Assignment failed: {r.status_code} {r.text[:300]}"

        assignment = r.json()
        self.data["assignmentId"] = assignment.get("id")
        is_current = assignment.get("isCurrent", False)
        print(f"    assignmentId: {self.data['assignmentId']}")
        print(f"    isCurrent:    {is_current}")

        return is_current, f"IO assigned, isCurrent={is_current}"

    def step_10_add_case_parties(self):
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

    def step_11_upload_subfile_b(self):
        """Upload Subfile B (Document Evidence)."""
        cid = self.data["caseId"]

        # Find subfile type B
        subfile_type = self._find_taxonomy("taxonomy/subfile-types", "B")
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

    def step_12_upload_subfile_d(self):
        """Upload Subfile D (Witness Statement)."""
        cid = self.data["caseId"]

        subfile_type = self._find_taxonomy("taxonomy/subfile-types", "D")
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

    def step_13_upload_subfile_f(self):
        """Upload Subfile F (Investigation Diary)."""
        cid = self.data["caseId"]

        subfile_type = self._find_taxonomy("taxonomy/subfile-types", "F")
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

    def step_14_upload_subfile_g(self):
        """Upload Subfile G (Charge Sheet)."""
        cid = self.data["caseId"]

        subfile_type = self._find_taxonomy("taxonomy/subfile-types", "G")
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

    def step_15_check_subfile_completion(self):
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

    def step_16_schedule_first_hearing(self):
        """Schedule first hearing (mention) for the case."""
        cid = self.data["caseId"]
        court_id = self.data["courtId"]

        # Find MENTION hearing type
        hearing_type = self._find_taxonomy("taxonomy/hearing-types", "MENTION")
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

        if r.status_code not in (200, 201):
            # Try alternate path
            r = self._post(f"case/cases/{cid}/hearings", body)
            print(f"    POST /case/cases/{cid}/hearings -> {r.status_code}")

        assert r.status_code in (200, 201), f"Schedule hearing failed: {r.status_code} {r.text[:300]}"

        hearing = r.json()
        self.data["hearingId1"] = hearing.get("id")
        print(f"    hearingId: {self.data['hearingId1']}")
        print(f"    date:      {hearing.get('hearingDate')}")
        print(f"    type:      {hearing.get('hearingTypeName', hearing.get('hearingType'))}")

        return True, f"First hearing (mention) scheduled: {self.data['hearingId1']}"

    def step_17_adjourn_first_hearing(self):
        """Adjourn the first hearing."""
        hearing_id = self.data["hearingId1"]

        next_week = (datetime.utcnow() + timedelta(days=7)).strftime("%Y-%m-%dT10:00:00Z")

        body = {
            "adjournmentReason": "Accused needs legal representation",
            "nextHearingDate": next_week,
        }

        r = self._post(f"hearings/{hearing_id}/adjourn", body)
        print(f"    POST /hearings/{hearing_id}/adjourn -> {r.status_code}")

        if r.status_code not in (200, 201):
            # Try alternate path
            r = self._post(f"case/hearings/{hearing_id}/adjourn", body)
            print(f"    POST /case/hearings/{hearing_id}/adjourn -> {r.status_code}")

        assert r.status_code in (200, 201), f"Adjourn failed: {r.status_code} {r.text[:300]}"

        hearing = r.json()
        status_name = hearing.get("hearingStatusName", hearing.get("status", ""))
        print(f"    hearingStatusName: {status_name}")

        adjourned = "adjourned" in status_name.lower() if status_name else False
        return adjourned, f"Hearing adjourned, status={status_name}"

    def step_18_verify_next_hearing_auto_created(self):
        """Verify that a next hearing was auto-created after adjournment."""
        cid = self.data["caseId"]

        r = self._get(f"cases/{cid}/hearings")
        print(f"    GET /cases/{cid}/hearings -> {r.status_code}")

        if r.status_code != 200:
            # Try alternate path
            r = self._get(f"case/cases/{cid}/hearings")
            print(f"    GET /case/cases/{cid}/hearings -> {r.status_code}")

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

    def step_19_issue_arrest_warrant(self):
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

    def step_20_execute_warrant(self):
        """Execute the arrest warrant."""
        warrant_id = self.data["warrantId"]

        body = {
            "executionDetails": "Accused arrested and brought to court",
        }

        r = self._post(f"case/warrants/{warrant_id}/execute", body)
        print(f"    POST /case/warrants/{warrant_id}/execute -> {r.status_code}")

        assert r.status_code in (200, 201), f"Warrant execution failed: {r.status_code} {r.text[:300]}"

        warrant = r.json()
        status_name = warrant.get("warrantStatusName", warrant.get("status", ""))
        print(f"    warrantStatusName: {status_name}")

        executed = "executed" in status_name.lower() if status_name else False
        return executed, f"Warrant status: {status_name}"

    def step_21_complete_second_hearing(self):
        """Complete the second hearing (plea) with conviction."""
        hearing_id = self.data.get("hearingId2")
        if not hearing_id:
            return False, "No second hearing ID available"

        # Find PLEA hearing type
        plea_type = self._find_taxonomy("taxonomy/hearing-types", "PLEA")
        if plea_type:
            print(f"    Plea type: {plea_type.get('code')} -> {plea_type['id']}")

        # Find CONVICTED hearing outcome
        convicted = self._find_taxonomy("taxonomy/hearing-outcomes", "CONVICTED")
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

        if r.status_code not in (200, 201):
            # Try alternate path
            r = self._post(f"case/hearings/{hearing_id}/complete", body)
            print(f"    POST /case/hearings/{hearing_id}/complete -> {r.status_code}")

        assert r.status_code in (200, 201), f"Complete hearing failed: {r.status_code} {r.text[:300]}"

        hearing = r.json()
        print(f"    outcome:  {hearing.get('hearingOutcomeName', hearing.get('outcome'))}")
        print(f"    status:   {hearing.get('hearingStatusName', hearing.get('status'))}")

        return True, f"Second hearing completed with conviction, fine=KES 50,000"

    def step_22_create_closure_checklist(self):
        """Create/update closure checklist with all subfiles marked complete."""
        cid = self.data["caseId"]

        # Look up closure type CONVICTED
        closure_type = self._find_taxonomy("taxonomy/closure-types", "CONVICTED")
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

        if r.status_code not in (200, 201):
            # Try alternate path
            r = self._put(f"case/cases/{cid}/closure-checklist", body)
            print(f"    PUT /case/cases/{cid}/closure-checklist -> {r.status_code}")

        assert r.status_code in (200, 201), f"Closure checklist failed: {r.status_code} {r.text[:300]}"

        checklist = r.json()
        all_verified = checklist.get("allSubfilesVerified", False)
        print(f"    allSubfilesVerified: {all_verified}")

        return all_verified, f"Closure checklist created, allSubfilesVerified={all_verified}"

    def step_23_request_closure_review(self):
        """Request closure review for the case."""
        cid = self.data["caseId"]

        body = {
            "reviewNotes": "All subfiles complete, case ready for closure",
        }

        r = self._post(f"cases/{cid}/closure-checklist/request-review", body)
        print(f"    POST /cases/{cid}/closure-checklist/request-review -> {r.status_code}")

        if r.status_code not in (200, 201):
            # Try alternate path
            r = self._post(f"case/cases/{cid}/closure-checklist/request-review", body)
            print(f"    POST /case/cases/{cid}/closure-checklist/request-review -> {r.status_code}")

        assert r.status_code in (200, 201), f"Request review failed: {r.status_code} {r.text[:300]}"

        review = r.json()
        review_status = review.get("reviewStatusName", review.get("reviewStatus", ""))
        print(f"    reviewStatusName: {review_status}")

        requested = "requested" in review_status.lower() if review_status else False
        return requested, f"Review status: {review_status}"

    def step_24_approve_closure_review(self):
        """Approve the closure review."""
        cid = self.data["caseId"]

        body = {
            "reviewNotes": "Reviewed and approved for closure",
        }

        r = self._post(f"cases/{cid}/closure-checklist/approve-review", body)
        print(f"    POST /cases/{cid}/closure-checklist/approve-review -> {r.status_code}")

        if r.status_code not in (200, 201):
            # Try alternate path
            r = self._post(f"case/cases/{cid}/closure-checklist/approve-review", body)
            print(f"    POST /case/cases/{cid}/closure-checklist/approve-review -> {r.status_code}")

        assert r.status_code in (200, 201), f"Approve review failed: {r.status_code} {r.text[:300]}"

        review = r.json()
        review_status = review.get("reviewStatusName", review.get("reviewStatus", ""))
        print(f"    reviewStatusName: {review_status}")

        approved = "approved" in review_status.lower() if review_status else False
        return approved, f"Review status: {review_status}"

    def step_25_close_case(self):
        """Close the case with conviction disposition."""
        cid = self.data["caseId"]

        disposition_id = self.data.get("closureTypeId") or self.data.get("courtDispositionTypeId")

        body = {
            "closingReason": "Case concluded. Accused convicted and fined.",
        }
        if disposition_id:
            body["dispositionTypeId"] = disposition_id

        r = self._post(f"cases/{cid}/close", body)
        print(f"    POST /cases/{cid}/close -> {r.status_code}")

        if r.status_code not in (200, 201):
            # Try alternate path
            r = self._post(f"case/cases/{cid}/close", body)
            print(f"    POST /case/cases/{cid}/close -> {r.status_code}")

        assert r.status_code in (200, 201), f"Close case failed: {r.status_code} {r.text[:300]}"

        case = r.json()
        case_status = case.get("caseStatus", "")
        print(f"    caseStatus: {case_status}")

        is_closed = "closed" in case_status.lower() if case_status else False
        return is_closed, f"Case status: {case_status}"

    def step_26_verify_final_state(self):
        """Verify the complete final state of the closed case."""
        cid = self.data["caseId"]
        checks = []
        all_pass = True

        # 1. Verify case status is Closed
        r = self._get(f"cases/{cid}")
        if r.status_code != 200:
            r = self._get(f"case/cases/{cid}")
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
        if r.status_code != 200:
            r = self._get(f"case/cases/{cid}/hearings")
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
        if r.status_code != 200:
            r = self._get(f"case/cases/{cid}/closure-checklist")
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
        if r.status_code != 200:
            r = self._get(f"case/cases/{cid}/assignments")
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

    # -- Runner ----------------------------------------------------------------

    def run(self):
        """Execute all steps in order."""
        print("\n" + "=" * 70)
        print("  TRULOAD COMPLIANCE E2E TEST -- SCENARIO 6")
        print("  Full Court Case Lifecycle")
        print(f"  Target: {self.base_url}")
        print(f"  Started: {datetime.utcnow().isoformat()}Z")
        print("=" * 70)
        print()
        print("  Workflow: Overload -> Case+Yard -> Escalate -> Court -> IO")
        print("         -> Parties -> Subfiles -> Hearings -> Warrants")
        print("         -> Closure Checklist -> Review -> Close")
        print()

        steps = [
            (1,  "Login",                                              self.step_01_login),
            (2,  "Lookup vehicle/driver/station metadata",             self.step_02_lookup_metadata),
            (3,  "Start weighing",                                     self.step_03_start_weighing),
            (4,  "Complete weighing with overload",                    self.step_04_complete_weighing_overloaded),
            (5,  "Verify case auto-created",                           self.step_05_verify_case_auto_created),
            (6,  "Verify yard entry auto-created",                     self.step_06_verify_yard_entry_auto_created),
            (7,  "Escalate to court",                                  self.step_07_escalate_to_court),
            (8,  "Create court record",                                self.step_08_create_court_record),
            (9,  "Assign IO (initial assignment)",                     self.step_09_assign_io),
            (10, "Add case parties (IO + defendant driver)",           self.step_10_add_case_parties),
            (11, "Upload Subfile B (document evidence)",               self.step_11_upload_subfile_b),
            (12, "Upload Subfile D (witness statement)",               self.step_12_upload_subfile_d),
            (13, "Upload Subfile F (investigation diary)",             self.step_13_upload_subfile_f),
            (14, "Upload Subfile G (charge sheet)",                    self.step_14_upload_subfile_g),
            (15, "Check subfile completion",                           self.step_15_check_subfile_completion),
            (16, "Schedule first hearing (mention)",                   self.step_16_schedule_first_hearing),
            (17, "Adjourn first hearing",                              self.step_17_adjourn_first_hearing),
            (18, "Verify next hearing auto-created",                   self.step_18_verify_next_hearing_auto_created),
            (19, "Issue arrest warrant",                               self.step_19_issue_arrest_warrant),
            (20, "Execute warrant",                                    self.step_20_execute_warrant),
            (21, "Complete second hearing (plea with conviction)",     self.step_21_complete_second_hearing),
            (22, "Create closure checklist",                           self.step_22_create_closure_checklist),
            (23, "Request closure review",                             self.step_23_request_closure_review),
            (24, "Approve closure review",                             self.step_24_approve_closure_review),
            (25, "Close the case",                                     self.step_25_close_case),
            (26, "Verify final state",                                 self.step_26_verify_final_state),
        ]

        for num, name, fn in steps:
            passed = self._step(num, name, fn)
            if not passed and num <= 5:
                # Critical steps -- abort if login/setup/weighing/case fail
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
        for key in ["weighingId", "caseId", "caseNo", "yardEntryId", "courtId",
                     "assignmentId", "hearingId1", "hearingId2", "warrantId",
                     "subfileBId", "subfileDId", "subfileFId", "subfileGId",
                     "closureTypeId", "courtDispositionTypeId",
                     "driverId", "vehicleId", "userId"]:
            val = self.data.get(key, "---")
            print(f"    {key}: {val}")

        print("=" * 70)
        return fail_count == 0


# --- Main --------------------------------------------------------------------

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
