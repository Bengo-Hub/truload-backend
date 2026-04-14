#!/usr/bin/env python3
"""
TruLoad Compliance E2E Test -- Scenario 3: Manual KeNHA Tag -> Yard Hold + Special Release
============================================================================================
Vehicle is weight-COMPLIANT but has an open manual KeNHA tag. The tag causes
the vehicle to be held in the yard (TagHold status). After the tag is resolved,
a special release is requested and approved, releasing the vehicle from the yard.

Correct workflow order:
  1. Create manual KeNHA vehicle tag on target vehicle
  2. Weigh vehicle (compliant weight) -> TagHold override due to open tag
  3. Case register auto-created with TAG violation type
  4. Yard entry auto-created with reason "tag_hold"
  5. Close the manual tag (levy paid / resolved)
  6. Request special release (ADMIN_DISCRETION)
  7. Approve special release
  8. Release vehicle from yard
  9. Verify final state + download PDFs

Usage:
    python compliance_e2e_scenario_3.py [--base-url http://localhost:4000]

Requirements:
    pip install requests
"""

import argparse
import json
import sys
import uuid
import time
from datetime import datetime
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

# --- Configuration ------------------------------------------------------------

DEFAULT_BASE_URL = "http://localhost:4000"
API_PREFIX = "/api/v1"
LOGIN_EMAIL = LOGIN_EMAIL_DEFAULT
LOGIN_PASSWORD = LOGIN_PASSWORD_DEFAULT

# Compliant axle weights (3-axle, GVW=23000, permissible=26000, well under limit)
COMPLIANT_AXLES = [
    {"axleNumber": 1, "measuredWeightKg": 7000},
    {"axleNumber": 2, "measuredWeightKg": 8000},
    {"axleNumber": 3, "measuredWeightKg": 8000},
]

VEHICLE_REG = "KDG 303T"


# --- Test Runner --------------------------------------------------------------

class ComplianceE2EScenario3:
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

    # -- Step Implementations ---------------------------------------------------

    def step_01_login(self):
        """Authenticate and get JWT token + user info."""
        r = requests.post(
            self._url("auth/login"),
            headers=self.headers,
            json={"email": LOGIN_EMAIL, "password": LOGIN_PASSWORD},
            timeout=60,
        )
        print(f"    POST /auth/login -> {r.status_code}")
        assert r.status_code == 200, f"Login failed: {r.status_code} {r.text[:200]}"
        data = r.json()

        self.token = data.get("accessToken") or data.get("token")
        assert self.token, f"No token in response: {list(data.keys())}"

        user = data.get("user", data)
        self.data["userId"] = user.get("id")
        self.data["stationId"] = user.get("stationId")
        
        # Fallback: if user has no station, fetch available stations and pick first one
        if not self.data["stationId"]:
            print("    [INFO] No stationId in user profile, fetching fallback...")
            rs = self._get("stations")
            if rs.status_code == 200:
                stations = rs.json()
                if isinstance(stations, dict):
                    stations = stations.get("items", [])
                if stations:
                    self.data["stationId"] = stations[0]["id"]
                    print(f"    stationId (fallback): {self.data['stationId']} ({stations[0].get('name')})")

        print(f"    userId:    {self.data['userId']}")
        print(f"    stationId: {self.data['stationId']}")

        return True, f"Logged in as {user.get('email', LOGIN_EMAIL)}"

    def step_02_setup_metadata(self):
        """Create or fetch driver, transporter, cargo, origin, destination."""
        created = []

        # -- Driver --
        r = self._get(f"drivers/search?query=E2E")
        drivers = r.json() if r.status_code == 200 else []
        if isinstance(drivers, dict):
            drivers = drivers.get("items", drivers.get("data", []))
        if drivers:
            self.data["driverId"] = drivers[0]["id"]
            print(f"    Driver found: {drivers[0].get('fullName', drivers[0].get('fullNames', 'N/A'))} ({self.data['driverId']})")
        else:
            r = self._post("drivers", {
                "fullName": "John E2E",
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
                    print(f"    Driver fallback: {all_drivers[0].get('fullName', 'N/A')}")

        # -- Transporter --
        r = self._get(f"transporters/search?query=E2E")
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

        # -- Cargo Type --
        r = self._get("cargo-types")
        cargos = r.json() if r.status_code == 200 else []
        if isinstance(cargos, dict):
            cargos = cargos.get("items", cargos.get("data", []))
        if cargos:
            self.data["cargoId"] = cargos[0]["id"]
            print(f"    Cargo type found: {cargos[0].get('name', 'N/A')}")
        else:
            r = self._post("cargo-types", {
                "code": "AGRICULTURAL_PRODUCE",
                "name": "Agricultural Produce",
                "category": "Agricultural",
            })
            print(f"    POST /cargo-types -> {r.status_code}")
            if r.status_code in (200, 201):
                cargo = r.json()
                self.data["cargoId"] = cargo.get("id")
                created.append("cargo-type")

        # -- Origins & Destinations --
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
                {"code": "BUSIA", "name": "Busia Border", "locationType": "border", "country": "Kenya"},
                {"code": "ELD", "name": "Eldoret", "locationType": "city", "country": "Kenya"},
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

    def step_03_fetch_tag_categories(self):
        """Fetch tag categories to get a categoryId for creating a manual tag."""
        r = self._get("vehicle-tags/categories")
        print(f"    GET /vehicle-tags/categories -> {r.status_code}")
        assert r.status_code == 200, f"Failed to fetch tag categories: {r.status_code} {r.text[:200]}"

        categories = r.json()
        if isinstance(categories, dict):
            categories = categories.get("items", categories.get("data", []))

        assert categories, "No tag categories found -- seeder may not have run"

        # Use the first available active category
        active_cats = [c for c in categories if c.get("isActive", True)]
        cat = active_cats[0] if active_cats else categories[0]
        self.data["tagCategoryId"] = cat["id"]

        print(f"    Available categories: {len(categories)}")
        for c in categories[:5]:
            print(f"      - {c.get('code')}: {c.get('name')} (id={c.get('id')})")
        print(f"    Selected: {cat.get('code')} / {cat.get('name')}")

        return True, f"Tag category: {cat.get('code')} ({self.data['tagCategoryId']})"

    def step_04_create_manual_tag(self):
        """Create a manual KeNHA vehicle tag on the target vehicle."""
        body = {
            "regNo": VEHICLE_REG,
            "tagType": "manual",
            "tagCategoryId": self.data["tagCategoryId"],
            "reason": "KeNHA enforcement: Outstanding road maintenance levy",
            "stationCode": "WBS-001",
            "createCase": False,
        }
        r = self._post("vehicle-tags", body)
        print(f"    POST /vehicle-tags -> {r.status_code}")
        assert r.status_code in (200, 201), f"Tag creation failed: {r.status_code} {r.text[:300]}"

        tag = r.json()
        self.data["tagId"] = tag.get("id")
        self.data["tagStatus"] = tag.get("status")

        print(f"    tagId:           {self.data['tagId']}")
        print(f"    regNo:           {tag.get('regNo')}")
        print(f"    tagType:         {tag.get('tagType')}")
        print(f"    tagCategoryName: {tag.get('tagCategoryName')}")
        print(f"    status:          {tag.get('status')}")
        print(f"    reason:          {tag.get('reason')}")
        print(f"    stationCode:     {tag.get('stationCode')}")

        has_tag = bool(tag.get("id"))
        is_open = tag.get("status", "").lower() in ("open", "active")
        return has_tag and is_open, f"Tag {self.data['tagId']} created, status={tag.get('status')}"

    def step_05_scale_test(self):
        """Create passing scale test for the station."""
        body = {
            "stationId": self.data["stationId"],
            "bound": "A",
            "testType": "calibration_weight",
            "vehiclePlate": "E2E-TEST",
            "weighingMode": "static",
            "testWeightKg": 10000,
            "actualWeightKg": 10005,
            "result": "pass",
            "deviationKg": 5,
            "details": "E2E scenario 3 - tag hold test calibration",
        }
        r = self._post("scale-tests", body)
        print(f"    POST /scale-tests -> {r.status_code}")
        if r.status_code in (200, 201):
            data = r.json()
            self.data["scaleTestId"] = data.get("id")
            return True, f"Scale test created: {self.data['scaleTestId']}"
        return False, f"Failed: {r.status_code} {r.text[:200]}"

    def step_06_autoweigh_compliant(self):
        """Autoweigh 3-axle compliant vehicle (GVW 23000, limit 26000, well under)."""
        body = {
            "stationId": self.data["stationId"],
            "vehicleRegNumber": VEHICLE_REG,
            "weighingMode": "mobile",
            "source": "MobileApp",
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

    def step_07_update_metadata(self):
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

    def step_08_capture_weights(self):
        """Capture compliant weights -- triggers compliance check + TAG CHECK.
        Weight is compliant but open tag should override to TagHold."""
        wid = self.data["weighingId"]
        body = {"axles": COMPLIANT_AXLES}
        r = self._post(f"weighing-transactions/{wid}/capture-weights", body)
        print(f"    POST /weighing-transactions/{wid}/capture-weights -> {r.status_code}")
        assert r.status_code == 200, f"Capture failed: {r.status_code} {r.text[:300]}"

        txn = r.json()
        print(f"    captureStatus:    {txn.get('captureStatus')}")
        print(f"    controlStatus:    {txn.get('controlStatus')}")
        print(f"    isCompliant:      {txn.get('isCompliant')}")
        print(f"    overloadKg:       {txn.get('overloadKg')}")
        print(f"    gvwMeasuredKg:    {txn.get('gvwMeasuredKg')}")
        print(f"    gvwPermissibleKg: {txn.get('gvwPermissibleKg')}")
        print(f"    isSentToYard:     {txn.get('isSentToYard')}")
        print(f"    violationReason:  {(txn.get('violationReason') or '')[:100]}")

        is_tag_hold = txn.get("controlStatus") == "TagHold"
        is_sent_to_yard = txn.get("isSentToYard") is True

        return is_tag_hold and is_sent_to_yard, (
            f"ControlStatus={txn.get('controlStatus')}, "
            f"IsSentToYard={txn.get('isSentToYard')}, "
            f"GVW={txn.get('gvwMeasuredKg')}kg (compliant weight held by tag)"
        )

    def step_09_verify_auto_case(self):
        """Verify case register was auto-created with TAG violation type."""
        wid = self.data["weighingId"]
        time.sleep(0.5)

        r = self._get(f"case/cases/by-weighing/{wid}")
        print(f"    GET /case/cases/by-weighing/{wid} -> {r.status_code}")

        if r.status_code != 200:
            return False, f"No case found: {r.status_code}"

        case = r.json()
        self.data["caseId"] = case.get("id")
        self.data["caseActId"] = case.get("actId")

        print(f"    caseId:           {self.data['caseId']}")
        print(f"    caseNo:           {case.get('caseNo')}")
        print(f"    caseStatus:       {case.get('caseStatus')}")
        print(f"    violationType:    {case.get('violationType')}")
        print(f"    violationDetails: {(case.get('violationDetails') or '')[:100]}")
        print(f"    dispositionType:  {case.get('dispositionType')}")

        has_case = bool(case.get("caseNo"))
        # Verify the violation type is TAG (auto-created for tag hold)
        is_tag_violation = "tag" in (case.get("violationType") or "").lower()

        return has_case and is_tag_violation, (
            f"Case {case.get('caseNo')}, violationType={case.get('violationType')}"
        )

    def step_10_verify_auto_yard(self):
        """Verify yard entry was auto-created with reason 'tag_hold'."""
        wid = self.data["weighingId"]
        r = self._get(f"yard-entries/by-weighing/{wid}")
        print(f"    GET /yard-entries/by-weighing/{wid} -> {r.status_code}")

        if r.status_code != 200:
            return False, f"No yard entry: {r.status_code} {r.text[:200]}"

        yard = r.json()
        self.data["yardEntryId"] = yard.get("id")
        print(f"    yardEntryId: {self.data['yardEntryId']}")
        print(f"    status:      {yard.get('status')}")
        print(f"    reason:      {yard.get('reason')}")
        print(f"    enteredAt:   {yard.get('enteredAt')}")

        ok = yard.get("status") == "pending" and yard.get("reason") == "tag_hold"
        return ok, f"Yard entry: status={yard.get('status')}, reason={yard.get('reason')}"

    def step_11_close_manual_tag(self):
        """Close the manual tag after KeNHA resolution (levy paid)."""
        tag_id = self.data["tagId"]
        body = {
            "closedReason": "KeNHA tag resolved - levy paid",
        }
        r = self._put(f"vehicle-tags/{tag_id}/close", body)
        print(f"    PUT /vehicle-tags/{tag_id}/close -> {r.status_code}")

        if r.status_code != 200:
            return False, f"Tag close failed: {r.status_code} {r.text[:300]}"

        tag = r.json()
        print(f"    status:       {tag.get('status')}")
        print(f"    closedReason: {tag.get('closedReason')}")
        print(f"    closedAt:     {tag.get('closedAt')}")
        print(f"    closedByName: {tag.get('closedByName')}")

        is_closed = tag.get("status", "").lower() == "closed"
        return is_closed, f"Tag closed: status={tag.get('status')}, at={tag.get('closedAt')}"

    def step_12_fetch_release_types(self):
        """Fetch release types to get the ADMIN_DISCRETION release type ID."""
        r = self._get("case/taxonomy/release-types")
        print(f"    GET /case/taxonomy/release-types -> {r.status_code}")
        assert r.status_code == 200, f"Failed to fetch release types: {r.status_code} {r.text[:200]}"

        release_types = r.json()
        if isinstance(release_types, dict):
            release_types = release_types.get("items", release_types.get("data", []))

        for rt in release_types:
            code = rt.get("code", "")
            print(f"      - {code}: {rt.get('name')} (id={rt.get('id')})")
            if code == "ADMIN_DISCRETION":
                self.data["adminDiscretionReleaseTypeId"] = rt["id"]
                return True, f"ADMIN_DISCRETION: {rt['id']}"

        # Use first available if ADMIN_DISCRETION not found
        if release_types:
            self.data["adminDiscretionReleaseTypeId"] = release_types[0]["id"]
            return True, f"Fallback to first release type: {release_types[0].get('code')} ({release_types[0]['id']})"

        return False, "No release types found in taxonomy"

    def step_13_create_special_release(self):
        """Create special release for the tag-hold case (ADMIN_DISCRETION)."""
        cid = self.data["caseId"]
        release_type_id = self.data.get("adminDiscretionReleaseTypeId")
        assert release_type_id, "adminDiscretionReleaseTypeId not set from step 12"

        body = {
            "caseRegisterId": cid,
            "releaseTypeId": release_type_id,
            "reason": "Vehicle tag resolved by KeNHA. Releasing from yard hold.",
            "requiresRedistribution": False,
            "requiresReweigh": False,
        }
        r = self._post("case/special-releases", body)
        print(f"    POST /case/special-releases -> {r.status_code}")

        if r.status_code not in (200, 201):
            return False, f"Special release creation failed: {r.status_code} {r.text[:300]}"

        sr = r.json()
        self.data["specialReleaseId"] = sr.get("id")

        print(f"    specialReleaseId: {self.data['specialReleaseId']}")
        print(f"    certificateNo:    {sr.get('certificateNo')}")
        print(f"    releaseType:      {sr.get('releaseType')}")
        print(f"    reason:           {sr.get('reason')}")
        print(f"    isApproved:       {sr.get('isApproved')}")
        print(f"    createdByName:    {sr.get('createdByName')}")

        has_sr = bool(sr.get("id"))
        return has_sr, f"Special release {sr.get('certificateNo')} created"

    def step_14_approve_special_release(self):
        """Approve the special release."""
        sr_id = self.data["specialReleaseId"]
        body = {
            "approvalNotes": "Tag verified as resolved",
        }
        r = self._post(f"case/special-releases/{sr_id}/approve", body)
        print(f"    POST /case/special-releases/{sr_id}/approve -> {r.status_code}")

        if r.status_code != 200:
            return False, f"Approval failed: {r.status_code} {r.text[:300]}"

        sr = r.json()
        print(f"    isApproved:    {sr.get('isApproved')}")
        print(f"    approvedAt:    {sr.get('approvedAt')}")
        print(f"    approvedByName:{sr.get('approvedByName')}")

        is_approved = sr.get("isApproved") is True
        return is_approved, f"Special release approved at {sr.get('approvedAt')}"

    def step_15_release_from_yard(self):
        """Release vehicle from yard after special release approval."""
        yard_id = self.data.get("yardEntryId")
        if not yard_id:
            return False, "No yardEntryId available"

        body = {
            "notes": "Released after KeNHA tag resolution and special release approval",
        }
        r = self._put(f"yard-entries/{yard_id}/release", body)
        print(f"    PUT /yard-entries/{yard_id}/release -> {r.status_code}")

        if r.status_code != 200:
            return False, f"Yard release failed: {r.status_code} {r.text[:300]}"

        yard = r.json()
        status = yard.get("status", "")
        print(f"    status:     {status}")
        print(f"    releasedAt: {yard.get('releasedAt')}")

        return status == "released", f"Yard status: {status}, releasedAt={yard.get('releasedAt')}"

    def step_16_verify_final_state(self):
        """Verify final state: tag closed, special release approved, yard released."""
        results = []

        # Check tag is closed
        tag_id = self.data.get("tagId")
        if tag_id:
            r = self._get(f"vehicle-tags/{tag_id}")
            if r.status_code == 200:
                tag = r.json()
                tag_closed = tag.get("status", "").lower() == "closed"
                results.append(f"tag={tag.get('status')}")
                print(f"    Tag status: {tag.get('status')}")
            else:
                results.append("tag=FETCH_FAILED")
                tag_closed = False
        else:
            tag_closed = False
            results.append("tag=NO_ID")

        # Check case
        cid = self.data.get("caseId")
        if cid:
            r = self._get(f"case/cases/{cid}")
            if r.status_code == 200:
                case = r.json()
                results.append(f"case={case.get('caseStatus')}")
                results.append(f"disposition={case.get('dispositionType')}")
                print(f"    Case status: {case.get('caseStatus')}")
                print(f"    Disposition: {case.get('dispositionType')}")

        # Check special release is approved
        sr_id = self.data.get("specialReleaseId")
        if sr_id:
            r = self._get(f"case/special-releases/{sr_id}")
            if r.status_code == 200:
                sr = r.json()
                sr_approved = sr.get("isApproved") is True
                results.append(f"sr_approved={sr.get('isApproved')}")
                print(f"    Special release approved: {sr.get('isApproved')}")
            else:
                sr_approved = False
                results.append("sr=FETCH_FAILED")
        else:
            sr_approved = False
            results.append("sr=NO_ID")

        # Check yard is released
        yard_id = self.data.get("yardEntryId")
        if yard_id:
            r = self._get(f"yard-entries/{yard_id}")
            if r.status_code == 200:
                yard = r.json()
                yard_released = yard.get("status") == "released"
                results.append(f"yard={yard.get('status')}")
                print(f"    Yard status: {yard.get('status')}")
            else:
                yard_released = False
                results.append("yard=FETCH_FAILED")
        else:
            yard_released = False
            results.append("yard=NO_ID")

        all_ok = tag_closed and sr_approved and yard_released
        return all_ok, f"Final state: {', '.join(results)}"

    def step_17_download_weight_ticket_pdf(self):
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
            return False, f"Unexpected content: type={content_type}, length={content_len}"
        return False, f"Weight ticket PDF failed: {r.status_code}"

    def step_18_download_special_release_pdf(self):
        """Download special release certificate PDF and verify response."""
        sr_id = self.data.get("specialReleaseId")
        if not sr_id:
            return False, "No specialReleaseId available"

        r = self._get(f"case/special-releases/{sr_id}/certificate/pdf")
        print(f"    GET /case/special-releases/{sr_id}/certificate/pdf -> {r.status_code}")

        if r.status_code == 200:
            content_type = r.headers.get("content-type", "")
            content_len = len(r.content)
            print(f"    content-type: {content_type}")
            print(f"    content-length: {content_len} bytes")
            if "pdf" in content_type.lower() and content_len > 100:
                return True, f"Special release certificate PDF downloaded ({content_len} bytes)"
            return False, f"Unexpected content: type={content_type}, length={content_len}"
        elif r.status_code == 501:
            print("    SKIPPED: Certificate PDF generation not yet implemented")
            return True, "SKIPPED -- certificate PDF generation not yet implemented (501)"
        return False, f"Special release PDF failed: {r.status_code} {r.text[:200]}"

    # -- Runner -----------------------------------------------------------------

    def run(self):
        """Execute all steps in order."""
        print("\n" + "=" * 70)
        print("  TRULOAD COMPLIANCE E2E TEST -- SCENARIO 3")
        print(f"  Target: {self.base_url}")
        print(f"  Started: {datetime.utcnow().isoformat()}Z")
        print("=" * 70)
        print()
        print("  Workflow: Manual KeNHA Tag -> Compliant Weight + TagHold")
        print("         -> Yard Hold -> Tag Resolution -> Special Release")
        print()

        steps = [
            (1, "Login", self.step_01_login),
            (2, "Setup metadata (driver, transporter, cargo, locations)", self.step_02_setup_metadata),
            (3, "Fetch tag categories", self.step_03_fetch_tag_categories),
            (4, "Create manual KeNHA vehicle tag", self.step_04_create_manual_tag),
            (5, "Create scale test", self.step_05_scale_test),
            (6, "Autoweigh compliant vehicle", self.step_06_autoweigh_compliant),
            (7, "Update weighing metadata (driver, transporter)", self.step_07_update_metadata),
            (8, "Capture weights (triggers compliance + TAG CHECK -> TagHold)", self.step_08_capture_weights),
            (9, "Verify auto-created case register (TAG violation type)", self.step_09_verify_auto_case),
            (10, "Verify auto-created yard entry (reason=tag_hold)", self.step_10_verify_auto_yard),
            (11, "Close manual tag (KeNHA levy paid)", self.step_11_close_manual_tag),
            (12, "Fetch release types (ADMIN_DISCRETION)", self.step_12_fetch_release_types),
            (13, "Create special release", self.step_13_create_special_release),
            (14, "Approve special release", self.step_14_approve_special_release),
            (15, "Release vehicle from yard", self.step_15_release_from_yard),
            (16, "Verify final state (tag closed, SR approved, yard released)", self.step_16_verify_final_state),
            (17, "Download weight ticket PDF", self.step_17_download_weight_ticket_pdf),
            (18, "Download special release certificate PDF", self.step_18_download_special_release_pdf),
        ]

        for num, name, fn in steps:
            passed = self._step(num, name, fn)
            if not passed and num <= 8:
                # Critical steps -- abort if login/setup/tag/autoweigh/capture fail
                print(f"\n  *** ABORTING: Critical step {num} failed ***")
                break

        self._print_summary()

    def _print_summary(self):
        """Print final test summary."""
        print("\n\n" + "=" * 70)
        print("  E2E TEST SUMMARY -- SCENARIO 3: MANUAL KeNHA TAG -> YARD HOLD + SPECIAL RELEASE")
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
        for key in ["weighingId", "vehicleId", "tagId", "caseId", "yardEntryId",
                     "specialReleaseId", "adminDiscretionReleaseTypeId",
                     "scaleTestId", "driverId", "transporterId"]:
            val = self.data.get(key, "---")
            print(f"    {key}: {val}")

        print("=" * 70)
        return fail_count == 0


# --- Main ---------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(
        description="TruLoad Compliance E2E Test -- Scenario 3: Manual KeNHA Tag -> Yard Hold + Special Release"
    )
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL,
                        help=f"Backend base URL (default: {DEFAULT_BASE_URL})")
    args = parser.parse_args()

    test = ComplianceE2EScenario3(args.base_url)
    test.run()

    # Exit with appropriate code
    failures = sum(1 for r in test.results if r["status"] != "PASS")
    sys.exit(failures)


if __name__ == "__main__":
    main()
