#!/usr/bin/env python3
"""
Pesaflow Invoice E2E Test Script
=================================
Tests the full invoice -> Pesaflow flow through the TruLoad backend API.

Steps:
  1. Authenticate with backend (POST /api/v1/auth/login)
  2. Search for existing invoices (POST /api/v1/invoices/search)
  3. Push invoice to Pesaflow (POST /api/v1/invoices/{id}/pesaflow)
  4. Query payment status (GET /api/v1/invoices/{id}/payment-status)
  5. Log full response/error with details

Usage:
    python pesaflow_invoice_e2e.py
    python pesaflow_invoice_e2e.py --base-url http://localhost:4000
    python pesaflow_invoice_e2e.py --email admin@example.com --password MyPass123!
"""

import argparse
import json
import sys
import urllib.request
import urllib.error
import urllib.parse
from datetime import datetime, timezone
from pathlib import Path
import io

# ─── Defaults ─────────────────────────────────────────────────────────────

BACKEND_BASE_URL = "http://localhost:4000"
DEFAULT_EMAIL = "gadmin@masterspace.co.ke"
DEFAULT_PASSWORD = "ChangeMe123!"

LOG_FILE = Path(__file__).parent / "pesaflow_invoice_e2e.md"


# ─── Utility Functions ─────────────────────────────────────────────────────


class Tee:
    """Write to multiple file-like objects."""
    def __init__(self, *files):
        self._files = files

    def write(self, data):
        for f in self._files:
            try:
                f.write(data)
            except Exception:
                pass

    def flush(self):
        for f in self._files:
            try:
                f.flush()
            except Exception:
                pass


def http_request(method: str, url: str, body=None, headers=None):
    """Make HTTP request and return (status_code, response_body_str, resp_headers)."""
    if body:
        data = json.dumps(body).encode("utf-8")
        req = urllib.request.Request(url, data=data, method=method)
        req.add_header("Content-Type", "application/json")
    else:
        req = urllib.request.Request(url, method=method)

    if headers:
        for k, v in headers.items():
            req.add_header(k, v)

    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            body_str = resp.read().decode("utf-8")
            return resp.status, body_str, dict(resp.headers)
    except urllib.error.HTTPError as e:
        body_str = e.read().decode("utf-8") if e.fp else ""
        return e.code, body_str, dict(e.headers) if e.headers else {}
    except urllib.error.URLError as e:
        return 0, str(e.reason), {}


def print_sep(title: str):
    print(f"\n{'=' * 70}")
    print(f"  {title}")
    print(f"{'=' * 70}")


def print_json(label: str, data):
    """Pretty-print JSON data with a label."""
    if isinstance(data, str):
        try:
            data = json.loads(data)
        except json.JSONDecodeError:
            print(f"  {label}: {data[:500]}")
            return
    print(f"  {label}:")
    for line in json.dumps(data, indent=2).split("\n"):
        print(f"    {line}")


# ─── Test Steps ────────────────────────────────────────────────────────────


def step_1_authenticate(base_url: str, email: str, password: str) -> str | None:
    """Authenticate with backend and return JWT token."""
    print_sep("STEP 1: Authenticate with Backend")

    url = f"{base_url}/api/v1/auth/login"
    payload = {"email": email, "password": password}

    print(f"  URL:      {url}")
    print(f"  Email:    {email}")
    print(f"  Password: {'*' * len(password)}")
    print()

    status, body, _ = http_request("POST", url, body=payload)

    print(f"  HTTP Status: {status}")

    if status == 200:
        try:
            data = json.loads(body)
            token = data.get("token") or data.get("accessToken")
            user = data.get("user", {})
            is_super = data.get("isSuperUser", user.get("isSuperUser", "N/A"))

            print(f"\n  [PASS] Login successful")
            print(f"         User:        {user.get('fullName', 'N/A')} ({user.get('email', email)})")
            print(f"         isSuperUser: {is_super}")
            print(f"         Token:       {token[:50]}..." if token else "         Token: MISSING")

            if token:
                return token
            else:
                print(f"\n  [FAIL] No token in response. Keys: {list(data.keys())}")
                print_json("Full response", data)
                return None
        except json.JSONDecodeError:
            print(f"\n  [FAIL] Response is not JSON: {body[:300]}")
            return None
    else:
        print(f"\n  [FAIL] Login failed with HTTP {status}")
        print(f"         Body: {body[:500]}")
        return None


def step_2_find_invoice(base_url: str, token: str) -> dict | None:
    """Search for an existing invoice (preferably pending Pesaflow sync)."""
    print_sep("STEP 2: Find Invoice to Push to Pesaflow")

    headers = {"Authorization": f"Bearer {token}"}

    # Search for invoices with pending or failed Pesaflow sync status
    url = f"{base_url}/api/v1/invoices/search"
    search_payload = {
        "page": 1,
        "pageSize": 10,
        "sortBy": "createdAt",
        "sortDirection": "desc"
    }

    print(f"  URL:     {url}")
    print(f"  Search:  All invoices (most recent first)")
    print()

    status, body, _ = http_request("POST", url, body=search_payload, headers=headers)

    print(f"  HTTP Status: {status}")

    if status == 200:
        try:
            data = json.loads(body)
            items = data.get("items") or data.get("data") or []
            total = data.get("totalCount") or data.get("total") or len(items)

            print(f"  Total invoices found: {total}")

            if not items:
                print(f"\n  [WARN] No invoices found in database")
                return None

            # Prefer invoices that haven't been synced yet
            pending = [i for i in items if i.get("pesaflowSyncStatus") in ("pending", "failed", None)]
            target = pending[0] if pending else items[0]

            print(f"\n  Selected invoice:")
            print(f"    ID:               {target.get('id')}")
            print(f"    Invoice No:       {target.get('invoiceNo')}")
            print(f"    Amount Due:       {target.get('amountDue')} {target.get('currency', 'KES')}")
            print(f"    Status:           {target.get('status')}")
            print(f"    Pesaflow Status:  {target.get('pesaflowSyncStatus', 'N/A')}")
            print(f"    Pesaflow Invoice: {target.get('pesaflowInvoiceNumber', 'N/A')}")

            return target
        except json.JSONDecodeError:
            print(f"\n  [FAIL] Response is not JSON: {body[:300]}")
            return None
    else:
        print(f"\n  [FAIL] Invoice search failed with HTTP {status}")
        print(f"         Body: {body[:500]}")
        return None


def step_3_push_to_pesaflow(base_url: str, token: str, invoice: dict) -> dict | None:
    """Push invoice to Pesaflow via backend API."""
    print_sep("STEP 3: Push Invoice to Pesaflow")

    invoice_id = invoice.get("id")
    url = f"{base_url}/api/v1/invoices/{invoice_id}/pesaflow"

    headers = {"Authorization": f"Bearer {token}"}

    push_payload = {
        "localInvoiceId": invoice_id,
        "clientName": "E2E Test Client",
        "clientEmail": "e2e-test@truload.co.ke",
        "clientMsisdn": "254700000000",
        "clientIdNumber": "E2E-TEST-001",
        "sendStk": False
    }

    print(f"  URL:        {url}")
    print(f"  Invoice ID: {invoice_id}")
    print(f"  Invoice No: {invoice.get('invoiceNo')}")
    print(f"  Amount:     {invoice.get('amountDue')} {invoice.get('currency', 'KES')}")
    print()

    status, body, _ = http_request("POST", url, body=push_payload, headers=headers)

    print(f"  HTTP Status: {status}")

    try:
        data = json.loads(body)
        print_json("Response", data)
    except json.JSONDecodeError:
        data = None
        print(f"  Response (raw): {body[:800]}")

    if status == 200 and data:
        success = data.get("success", False)
        if success:
            print(f"\n  [PASS] Invoice pushed to Pesaflow successfully")
            print(f"         Pesaflow Invoice: {data.get('pesaflowInvoiceNumber')}")
            print(f"         Payment Link:     {data.get('paymentLink')}")
            print(f"         Gateway Fee:      {data.get('gatewayFee')}")
            print(f"         Amount Net:       {data.get('amountNet')}")
        else:
            print(f"\n  [FAIL] Pesaflow returned error: {data.get('message', 'Unknown error')}")
        return data
    else:
        print(f"\n  [FAIL] Push to Pesaflow failed with HTTP {status}")
        if data:
            error_msg = data.get("message") or data.get("error") or data.get("title")
            if error_msg:
                print(f"         Error: {error_msg}")
        return data


def step_4_query_payment_status(base_url: str, token: str, invoice_id: str):
    """Query payment status for the invoice."""
    print_sep("STEP 4: Query Payment Status")

    url = f"{base_url}/api/v1/invoices/{invoice_id}/payment-status"
    headers = {"Authorization": f"Bearer {token}"}

    print(f"  URL:        {url}")
    print(f"  Invoice ID: {invoice_id}")
    print()

    status, body, _ = http_request("GET", url, headers=headers)

    print(f"  HTTP Status: {status}")

    try:
        data = json.loads(body)
        print_json("Payment Status", data)
    except json.JSONDecodeError:
        print(f"  Response (raw): {body[:500]}")

    if status == 200:
        print(f"\n  [PASS] Payment status retrieved")
    elif status == 204:
        print(f"\n  [INFO] No payment status available yet (invoice not yet paid)")
    else:
        print(f"\n  [INFO] Payment status query returned HTTP {status}")


# ─── Main ──────────────────────────────────────────────────────────────────


def main():
    parser = argparse.ArgumentParser(description="Pesaflow Invoice E2E Test via Backend API")
    parser.add_argument("--base-url", default=BACKEND_BASE_URL,
                        help=f"Backend base URL (default: {BACKEND_BASE_URL})")
    parser.add_argument("--email", default=DEFAULT_EMAIL,
                        help=f"Login email (default: {DEFAULT_EMAIL})")
    parser.add_argument("--password", default=DEFAULT_PASSWORD,
                        help=f"Login password (default: {DEFAULT_PASSWORD})")
    args = parser.parse_args()

    # Open log file and tee stdout/stderr to both console and markdown log
    log_fh = open(LOG_FILE, "a", encoding="utf-8")
    orig_stdout = sys.stdout
    orig_stderr = sys.stderr
    sys.stdout = Tee(orig_stdout, log_fh)
    sys.stderr = Tee(orig_stderr, log_fh)

    print("=" * 70)
    print("  PESAFLOW INVOICE E2E TEST (via Backend API)")
    print(f"  Backend URL:   {args.base_url}")
    print(f"  Email:         {args.email}")
    print(f"  Timestamp:     {datetime.now(timezone.utc).isoformat()}")
    print("=" * 70)

    results = {}

    try:
        # Step 1: Authenticate
        token = step_1_authenticate(args.base_url, args.email, args.password)
        results["auth"] = "PASS" if token else "FAIL"

        if not token:
            print(f"\n  [ABORT] Cannot proceed without authentication")
            print_summary(results)
            return 1

        # Step 2: Find an invoice
        invoice = step_2_find_invoice(args.base_url, token)
        results["find_invoice"] = "PASS" if invoice else "FAIL"

        if not invoice:
            print(f"\n  [ABORT] No invoice found to push to Pesaflow")
            print(f"         Create a prosecution + invoice first, then re-run this test")
            print_summary(results)
            return 1

        # Step 3: Push invoice to Pesaflow
        pesaflow_result = step_3_push_to_pesaflow(args.base_url, token, invoice)
        if pesaflow_result and pesaflow_result.get("success"):
            results["push_pesaflow"] = "PASS"
        else:
            results["push_pesaflow"] = "FAIL"

        # Step 4: Query payment status
        invoice_id = invoice.get("id")
        step_4_query_payment_status(args.base_url, token, invoice_id)
        results["payment_status"] = "TESTED"

        print_summary(results)
        return 0 if results.get("push_pesaflow") == "PASS" else 1

    finally:
        try:
            sys.stdout = orig_stdout
            sys.stderr = orig_stderr
        except Exception:
            pass
        try:
            log_fh.close()
        except Exception:
            pass


def print_summary(results: dict):
    print("\n" + "=" * 70)
    print("  SUMMARY")
    print("=" * 70)
    for step, status in results.items():
        icon = ("[PASS]" if status == "PASS"
                else "[FAIL]" if status == "FAIL"
                else "[TEST]")
        print(f"  {icon} {step}: {status}")
    print("=" * 70)


if __name__ == "__main__":
    sys.exit(main())
