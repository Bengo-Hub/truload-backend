#!/usr/bin/env python3
"""
Live Pesaflow callback/failure/reconciliation validation.

This script is intentionally non-destructive:
- Authenticates against TruLoad backend
- Calls invoice payment-status reconciliation endpoint on recent invoices
- Probes callback endpoints with synthetic payload and accepts business-level 4xx
  responses as long as the service handles requests (no 5xx/network failures).
"""

import argparse
import json
import sys
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

from test_credentials import LOGIN_EMAIL_DEFAULT, LOGIN_PASSWORD_DEFAULT
from auth_cache import get_login_data

DEFAULT_BASE_URL = "https://kuraweighapitest.masterspace.co.ke"
DEFAULT_EMAIL = LOGIN_EMAIL_DEFAULT
DEFAULT_PASSWORD = LOGIN_PASSWORD_DEFAULT
LOG_FILE = Path(__file__).parent / "pesaflow_callback_reconciliation_e2e.md"


def http_request(method: str, url: str, body=None, headers=None):
    payload = None
    if body is not None:
        payload = json.dumps(body).encode("utf-8")
    req = urllib.request.Request(url, data=payload, method=method)
    if body is not None:
        req.add_header("Content-Type", "application/json")
    if headers:
        for k, v in headers.items():
            req.add_header(k, v)
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            return resp.status, resp.read().decode("utf-8"), dict(resp.headers)
    except urllib.error.HTTPError as exc:
        return exc.code, (exc.read().decode("utf-8") if exc.fp else ""), dict(exc.headers or {})
    except urllib.error.URLError as exc:
        return 0, str(exc.reason), {}


def write_log(line: str):
    with open(LOG_FILE, "a", encoding="utf-8") as fh:
        fh.write(line + "\n")


def main():
    parser = argparse.ArgumentParser(description="Pesaflow callback/failure/reconciliation live validation")
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL)
    parser.add_argument("--email", default=DEFAULT_EMAIL)
    parser.add_argument("--password", default=DEFAULT_PASSWORD)
    args = parser.parse_args()

    base = args.base_url.rstrip("/")
    write_log("\n" + "=" * 70)
    write_log(f"Timestamp: {datetime.now(timezone.utc).isoformat()}")
    write_log(f"Base URL: {base}")

    # 1) Login (reuse cached JWT when valid)
    try:
        data, from_cache = get_login_data(base, args.email, args.password)
        token = data.get("token") or data.get("accessToken")
        write_log(f"Login status: {'CACHE' if from_cache else '200'}")
    except Exception as exc:
        write_log("Login status: FAIL")
        write_log(f"Login failed: {str(exc)[:400]}")
        return 1
    if not token:
        write_log("Missing token in login response")
        return 1

    auth = {"Authorization": f"Bearer {token}"}

    # 2) Reconciliation probe: query latest invoices + payment-status endpoint
    reconciliation_ok = False
    status, body, _ = http_request(
        "POST",
        f"{base}/api/v1/invoices/search",
        body={"page": 1, "pageSize": 5, "sortBy": "createdAt", "sortDirection": "desc"},
        headers=auth,
    )
    write_log(f"Invoice search status: {status}")
    if status == 200:
        try:
            data = json.loads(body)
            invoices = data.get("items") or data.get("data") or []
        except json.JSONDecodeError:
            invoices = []
        for inv in invoices[:3]:
            inv_id = inv.get("id")
            if not inv_id:
                continue
            ps_status, ps_body, _ = http_request(
                "GET",
                f"{base}/api/v1/invoices/{inv_id}/payment-status",
                headers=auth,
            )
            write_log(f"Payment status probe for invoice {inv_id}: HTTP {ps_status}")
            # For this non-destructive probe, any handled non-5xx response means
            # the endpoint is reachable and processing requests.
            if ps_status != 0 and ps_status < 500:
                reconciliation_ok = True
            elif ps_status >= 500 or ps_status == 0:
                write_log(f"Reconciliation endpoint unhealthy: {ps_body[:300]}")

    # 3) Callback probes: handled response should be non-5xx
    callback_paths = [
        "/api/v1/payments/callback/success",
        "/api/v1/payments/callback/failure",
        "/api/v1/payments/callback/timeout",
        "/api/v1/payments/webhook/ecitizen-pesaflow",
    ]
    callback_ok = True
    sample_payload = {
        "invoice_number": "E2E-SYNTHETIC-CALLBACK",
        "payment_status": "FAILED",
        "transaction_ref": f"SYN-{datetime.now(timezone.utc).strftime('%Y%m%d%H%M%S')}",
    }
    for path in callback_paths:
        c_status, c_body, _ = http_request("POST", f"{base}{path}", body=sample_payload)
        write_log(f"Callback probe {path}: HTTP {c_status}")
        if c_status == 0 or c_status >= 500:
            callback_ok = False
            write_log(f"Callback endpoint failure body: {c_body[:300]}")

    all_ok = reconciliation_ok and callback_ok
    write_log(f"Result: {'PASS' if all_ok else 'FAIL'}")
    return 0 if all_ok else 1


if __name__ == "__main__":
    sys.exit(main())
