#!/usr/bin/env python3
"""
Pesaflow API Direct Test Script
================================
Tests the Pesaflow (eCitizen) payment API directly using configured credentials
from appsettings.Development.json.

Steps:
  1. Get OAuth Bearer token from Pesaflow (cached to file, reused until expiry)
  2. Create invoice via iframe endpoint (returns invoice_number, invoice_link, commission, etc.)
  3. Save local invoice with complete Pesaflow details
  4. Query payment status for the created invoice

Usage:
    python pesaflow_api_test.py
    python pesaflow_api_test.py --retries 3
    python pesaflow_api_test.py --clear-token     # Force new token
"""

import argparse
import base64
import hashlib
import hmac
import json
import os
import sys
import time
import urllib.request
import urllib.error
import urllib.parse
from datetime import datetime, timezone
from pathlib import Path
import io

# ─── Pesaflow Test Credentials (from appsettings.Development.json) ──────────
PESAFLOW_BASE_URL = "https://test.pesaflow.com"
API_KEY = "hkW0lc/+xu9GA5Di"       # Consumer Key (HMAC key + Basic auth user)
API_SECRET = "tgia2h6QEcwqPmJ1Uxv3V9I7cqf6Ub7X"  # Consumer Secret (Basic auth pass)
API_CLIENT_ID = "588"                #Account ID
SERVICE_ID= "235330" 

# Webhook/Callback URLs (for invoice payments)
NOTIFICATION_URL = "http://localhost:4000/api/v1/payments/webhook/ecitizen-pesaflow"
CALLBACK_SUCCESS_URL = "http://localhost:4000/api/v1/payments/callback/success"
CALLBACK_FAILURE_URL = "http://localhost:4000/api/v1/payments/callback/failure"
CALLBACK_TIMEOUT_URL = "http://localhost:4000/api/v1/payments/callback/timeout"

# Token cache file (next to this script)
TOKEN_CACHE_FILE = Path(__file__).parent / ".pesaflow_token_cache.json"

# Local test artifacts
LOG_FILE = Path(__file__).parent / "pesaflow_api_test.md"
LOCAL_INVOICE_DIR = Path(__file__).parent

MAX_RETRIES = 1

# ─── Utility Functions ─────────────────────────────────────────────────────


def compute_secure_hash(data_string: str, key: str) -> str:
    """Compute Pesaflow secure hash: Base64(hex(HMAC-SHA256(key, data)))"""
    key_bytes = key.encode("utf-8")
    data_bytes = data_string.encode("utf-8")
    hash_bytes = hmac.new(key_bytes, data_bytes, hashlib.sha256).digest()
    hex_hash = hash_bytes.hex()
    return base64.b64encode(hex_hash.encode("utf-8")).decode("utf-8")


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


def http_request(method: str, url: str, body=None, headers=None,
                 form_data=None, raw_body: bytes = None):
    """Make HTTP request and return (status_code, response_body_str, resp_headers)."""
    if raw_body is not None:
        req = urllib.request.Request(url, data=raw_body, method=method)
    elif form_data:
        encoded = urllib.parse.urlencode(form_data).encode("utf-8")
        req = urllib.request.Request(url, data=encoded, method=method)
        req.add_header("Content-Type", "application/x-www-form-urlencoded")
    elif body:
        data = json.dumps(body).encode("utf-8")
        req = urllib.request.Request(url, data=data, method=method)
        req.add_header("Content-Type", "application/json")
    else:
        req = urllib.request.Request(url, method=method)

    if headers:
        for k, v in headers.items():
            req.add_header(k, v)

    # Save POST payloads to a JSON file (overwrite each time) for debugging/inspection
    try:
        if method and method.upper() == "POST":
            record = {
                "url": url,
                "method": method.upper(),
                "headers": headers or {},
                "payload": None
            }
            if raw_body is not None:
                try:
                    record["payload"] = raw_body.decode("utf-8")
                except Exception:
                    record["payload"] = base64.b64encode(raw_body).decode("ascii")
            elif form_data is not None:
                record["payload"] = form_data
            elif body is not None:
                record["payload"] = body

            out_path = LOCAL_INVOICE_DIR / "pesaflow_last_post_payload.json"
            # Overwrite the file with the latest POST payload (single JSON object)
            try:
                record["timestamp"] = datetime.now(timezone.utc).isoformat()
                out_path.write_text(json.dumps(record, indent=2))
            except Exception:
                pass
    except Exception:
        # don't fail the request flow if saving the payload fails
        pass

    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            body_str = resp.read().decode("utf-8")
            return resp.status, body_str, dict(resp.headers)
    except urllib.error.HTTPError as e:
        body_str = e.read().decode("utf-8") if e.fp else ""
        return e.code, body_str, dict(e.headers) if e.headers else {}
    except urllib.error.URLError as e:
        return 0, str(e.reason), {}


def http_retry(method, url, **kwargs):
    """Wrapper that retries on 502/503 errors."""
    last_status, last_body, last_headers = 0, "", {}
    for attempt in range(MAX_RETRIES):
        last_status, last_body, last_headers = http_request(method, url, **kwargs)
        if last_status not in (502, 503) or attempt == MAX_RETRIES - 1:
            return last_status, last_body, last_headers
        wait = 3 * (attempt + 1)
        print(f"  [RETRY] HTTP {last_status}, retrying in {wait}s "
              f"(attempt {attempt + 2}/{MAX_RETRIES})...")
        time.sleep(wait)
    return last_status, last_body, last_headers


def print_sep(title: str):
    print(f"\n{'=' * 70}")
    print(f"  {title}")
    print(f"{'=' * 70}")


# ─── Token Cache ───────────────────────────────────────────────────────────


def load_cached_token() -> str | None:
    """Load token from cache file if still valid."""
    if not TOKEN_CACHE_FILE.exists():
        return None
    try:
        data = json.loads(TOKEN_CACHE_FILE.read_text())
        expires_at = data.get("expires_at", 0)
        now = time.time()
        if now < expires_at:
            remaining = int(expires_at - now)
            print(f"  [CACHE] Reusing cached token (expires in {remaining}s)")
            return data["token"]
        else:
            print(f"  [CACHE] Cached token expired, requesting new one")
            return None
    except (json.JSONDecodeError, KeyError):
        return None


def save_token_to_cache(token: str, expires_in: int):
    """Save token to cache file with expiry timestamp."""
    expires_at = time.time() + expires_in - 60  # 60s safety buffer
    TOKEN_CACHE_FILE.write_text(json.dumps({
        "token": token,
        "expires_at": expires_at,
        "expires_in": expires_in,
        "cached_at": datetime.now(timezone.utc).isoformat(),
    }, indent=2))
    print(f"  [CACHE] Token cached to {TOKEN_CACHE_FILE.name} "
          f"(valid for {expires_in - 60}s)")


def clear_token_cache():
    """Delete cached token file."""
    if TOKEN_CACHE_FILE.exists():
        TOKEN_CACHE_FILE.unlink()
        print(f"  [CACHE] Token cache cleared")


# ─── Test Steps ────────────────────────────────────────────────────────────


def step_1_get_oauth_token() -> str | None:
    """
    Get OAuth Bearer token from Pesaflow.

    Pesaflow OAuth uses a JSON body with key + secret (confirmed via Postman):
      POST {base_url}/api/oauth/generate/token
      Content-Type: application/json
      Body: {"key": "<consumer_key>", "secret": "<consumer_secret>"}
    """
    print_sep("STEP 1: Get Pesaflow OAuth Token")

    # Check cache first
    cached = load_cached_token()
    if cached:
        return cached

    url = f"{PESAFLOW_BASE_URL}/api/oauth/generate/token"

    # Pesaflow OAuth: POST with JSON body containing key + secret
    oauth_payload = {
        "key": API_KEY,
        "secret": API_SECRET
    }

    print(f"  URL:          {url}")
    print(f"  Method:       POST (JSON body)")
    print(f"  ApiKey:       {API_KEY}")
    print(f"  ApiSecret:    {API_SECRET[:10]}...")
    print(f"  Payload:      {json.dumps(oauth_payload)}")
    print()

    status, body, resp_headers = http_retry(
        "POST", url,
        body=oauth_payload
    )

    print(f"  HTTP Status:  {status}")
    print(f"  Response:     {body[:500]}")

    if status == 0:
        print(f"\n  [FAIL] Could not connect to {PESAFLOW_BASE_URL}")
        print(f"         Error: {body}")
        return None

    if status == 200:
        try:
            data = json.loads(body)
            token = data.get("token") or data.get("access_token")
            expiry = data.get("expiry") or data.get("expires_in") or 3599
            if token:
                print(f"\n  [PASS] Token obtained successfully")
                print(f"         Token:      {token[:50]}...")
                print(f"         Expires in: {expiry}s")
                save_token_to_cache(token, int(expiry))
                return token
            else:
                print(f"\n  [FAIL] 200 OK but no token field. Keys: {list(data.keys())}")
                print(f"         Full response: {json.dumps(data)[:400]}")
                return None
        except json.JSONDecodeError:
            print(f"\n  [FAIL] 200 OK but response is not JSON")
            return None

    # Non-200 — diagnose
    if status in (502, 503):
        print(f"\n  [FAIL] Pesaflow test server is DOWN (HTTP {status})")
        print(f"         nginx at test.pesaflow.com can't reach backend.")
    elif status == 400:
        print(f"\n  [FAIL] HTTP 400 Bad Request")
        try:
            err = json.loads(body)
            print(f"         Pesaflow says: {json.dumps(err)[:300]}")
        except:
            print(f"         Raw: {body[:300]}")
    elif status == 401:
        print(f"\n  [FAIL] HTTP 401 Unauthorized — credentials rejected")
    else:
        print(f"\n  [FAIL] OAuth failed with HTTP {status}")
        print(f"         Body: {body[:300]}")

    return None


def step_2_create_invoice_via_iframe(invoice_ref: str) -> tuple:
    """Create invoice via Pesaflow iframe endpoint - this is the actual invoice creation."""
    print_sep("STEP 2: Create Invoice via Pesaflow iframe endpoint")

    url = f"{PESAFLOW_BASE_URL}/PaymentAPI/iframev2.1.php"
    # Pesaflow expects amount with two decimal places
    amount = "100.00"
    client_id = "TEST-ID-001"
    currency = "KES"
    bill_desc = "Test Overload Fine"
    client_name = "Test User"

    # Hash: apiClientID + amount + serviceID + clientIDNumber + currency +
    #        billRefNumber + billDesc + clientName + secret
    data_string = (f"{API_CLIENT_ID}{amount}{SERVICE_ID}{client_id}"
                   f"{currency}{invoice_ref}{bill_desc}{client_name}{API_SECRET}")
    secure_hash = compute_secure_hash(data_string, API_KEY)

    form_data = {
        "apiClientID": API_CLIENT_ID,
        "serviceID": SERVICE_ID,
        "billDesc": bill_desc,
        "currency": currency,
        "billRefNumber": invoice_ref,
        "clientMSISDN": "254700000000",
        "clientName": client_name,
        "clientIDNumber": client_id,
        "clientEmail": "test@truload-e2e.co.ke",
        "amountExpected": amount,
        "callBackURLOnSuccess": CALLBACK_SUCCESS_URL,
        "callBackURLOnFailure": CALLBACK_FAILURE_URL,
        "callBackURLOnTimeout": CALLBACK_TIMEOUT_URL,
        "notificationURL": NOTIFICATION_URL,
        "secureHash": secure_hash,
        "format": "json",
        "sendSTK": "false"
    }

    print(f"  URL:        {url}")
    print(f"  Invoice Ref: {invoice_ref}")
    print(f"  Amount:      {amount} {currency}")
    print(f"  Hash data:  '{data_string[:80]}...'")
    print(f"  Hash:       {secure_hash}")
    print()

    status, body, _ = http_request("POST", url, form_data=form_data)

    print(f"  HTTP Status: {status}")
    print(f"  Response:    {body[:800]}")

    if status == 200:
        try:
            data = json.loads(body)
            pesaflow_invoice_no = data.get("invoice_number")
            invoice_link = data.get("invoice_link")
            commission = data.get("commission")
            amount_net = data.get("amount_net")
            amount_expected = data.get("amount_expected")

            print(f"\n  [PASS] Invoice created on Pesaflow")
            print(f"         Pesaflow Invoice No: {pesaflow_invoice_no}")
            print(f"         Payment Link:        {invoice_link}")
            print(f"         Amount Net:          {amount_net}")
            print(f"         Commission:          {commission}")
            print(f"         Total Expected:      {amount_expected}")

            return True, invoice_ref, {
                "pesaflow_invoice_number": pesaflow_invoice_no,
                "payment_link": invoice_link,
                "gateway_fee": commission,
                "amount_net": amount_net,
                "total_amount": amount_expected,
                "currency": currency,
                "client_ref": invoice_ref,
            }
        except json.JSONDecodeError:
            print(f"\n  [WARN] Response is not JSON: {body[:200]}")
            return False, invoice_ref, None
    else:
        print(f"\n  [FAIL] Invoice creation failed with HTTP {status}")
        if body:
            try:
                err = json.loads(body)
                print(f"         Error: {json.dumps(err, indent=2)[:500]}")
            except:
                print(f"         Raw: {body[:500]}")
        return False, invoice_ref, None


def step_3_query_payment_status(invoice_ref: str, pesaflow_invoice_no: str = None):
    """Query payment status for the created invoice using Pesaflow invoice number."""
    print_sep("STEP 3: Query Payment Status")

    # Use Pesaflow invoice number if available, otherwise client ref
    ref_no = pesaflow_invoice_no if pesaflow_invoice_no else invoice_ref
    data_string = f"{API_CLIENT_ID}{ref_no}"
    secure_hash = compute_secure_hash(data_string, API_KEY)

    params = urllib.parse.urlencode({
        "api_client_id": API_CLIENT_ID,
        "ref_no": ref_no,
        "secure_hash": secure_hash
    })
    url = f"{PESAFLOW_BASE_URL}/api/invoice/payment/status?{params}"

    print(f"  Invoice Ref:     {invoice_ref}")
    if pesaflow_invoice_no:
        print(f"  Pesaflow Inv No: {pesaflow_invoice_no}")
    print(f"  Hash data:  '{data_string}'")
    print(f"  Hash:       {secure_hash}")
    print(f"  URL:        {url}")
    print()

    status, body, _ = http_request("GET", url)

    print(f"  HTTP Status: {status}")
    print(f"  Response:    {body[:500]}")

    if status == 200:
        print(f"\n  [PASS] Payment status retrieved")
    else:
        print(f"\n  [INFO] Status query returned HTTP {status}")


def save_local_invoice(invoice_ref: str, pesaflow_data: dict = None) -> Path:
    """Save a detailed local invoice JSON with Pesaflow integration details."""
    if pesaflow_data:
        invoice = {
            "client_invoice_ref": invoice_ref,
            "pesaflow_invoice_number": pesaflow_data.get("pesaflow_invoice_number"),
            "payment_link": pesaflow_data.get("payment_link"),
            "amount_net": pesaflow_data.get("amount_net"),
            "gateway_fee": pesaflow_data.get("gateway_fee"),
            "total_amount": pesaflow_data.get("total_amount"),
            "currency": pesaflow_data.get("currency", "KES"),
            "msisdn": "254700000000",
            "name": "Pesaflow API Test User",
            "email": "test@truload-e2e.co.ke",
            "id_number": "TEST-ID-001",
            "status": "pending_payment",
            "created_at": datetime.now(timezone.utc).isoformat(),
            "items": [
                {
                    "desc": "Test Overload Fine (Pesaflow API Test)",
                    "item_ref": invoice_ref,
                    "price": pesaflow_data.get("amount_net", "100.00"),
                    "quantity": "1",
                    "currency": pesaflow_data.get("currency", "KES")
                }
            ]
        }
    else:
        # Fallback: Pesaflow unreachable, save with default values + queue background sync
        invoice = {
            "client_invoice_ref": invoice_ref,
            "pesaflow_invoice_number": None,
            "payment_link": None,
            "amount_net": "100.00",
            "gateway_fee": None,
            "total_amount": "100.00",
            "currency": "KES",
            "msisdn": "254700000000",
            "name": "Pesaflow API Test User",
            "email": "test@truload-e2e.co.ke",
            "id_number": "TEST-ID-001",
            "status": "pending_pesaflow_sync",
            "created_at": datetime.now(timezone.utc).isoformat(),
            "sync_required": True,
            "items": [
                {
                    "desc": "Test Overload Fine (Pesaflow API Test)",
                    "item_ref": invoice_ref,
                    "price": "100.00",
                    "quantity": "1",
                    "currency": "KES"
                }
            ]
        }

    path = LOCAL_INVOICE_DIR / f"pesaflow_local_invoice_{invoice_ref}.json"
    path.write_text(json.dumps(invoice, indent=2))
    print(f"  [LOCAL] Invoice saved: {path}")
    if pesaflow_data:
        print(f"          Pesaflow Invoice: {pesaflow_data.get('pesaflow_invoice_number')}")
        print(f"          Payment Link: {pesaflow_data.get('payment_link')}")
    else:
        print(f"          Status: pending_pesaflow_sync (background queue task will retry)")
    return path


# ─── Main ──────────────────────────────────────────────────────────────────


def main():
    global MAX_RETRIES

    parser = argparse.ArgumentParser(description="Pesaflow API Direct Test")
    parser.add_argument("--retries", type=int, default=1,
                        help="Retry count on 502/503 errors (default: 1)")
    parser.add_argument("--clear-token", action="store_true",
                        help="Clear cached token and request a fresh one")
    args = parser.parse_args()

    MAX_RETRIES = max(1, args.retries)

    if args.clear_token:
        clear_token_cache()

    # Open log file and tee stdout/stderr to both console and markdown log
    log_fh = open(LOG_FILE, "a", encoding="utf-8")
    orig_stdout = sys.stdout
    orig_stderr = sys.stderr
    sys.stdout = Tee(orig_stdout, log_fh)
    sys.stderr = Tee(orig_stderr, log_fh)

    print("=" * 70)
    print("  PESAFLOW API DIRECT TEST")
    print(f"  Base URL:      {PESAFLOW_BASE_URL}")
    print(f"  API Client ID: {API_CLIENT_ID}")
    print(f"  API Key:       {API_KEY}")
    print(f"  API Secret:    {API_SECRET[:10]}...")
    print(f"  Retries:       {MAX_RETRIES}")
    print(f"  Token cache:   {TOKEN_CACHE_FILE}")
    print(f"  Timestamp:     {datetime.now(timezone.utc).isoformat()}")
    print("=" * 70)

    results = {}

    try:
        # Step 1: Get token (from cache or fresh) - not strictly required for iframe but good to test
        token = step_1_get_oauth_token()
        results["oauth"] = "PASS" if token else "FAIL"

        if not token:
            print("\n" + "=" * 70)
            print("  WARNING: OAuth token acquisition failed")
            print("  Continuing with iframe flow (does not require Bearer token)")
            print("=" * 70)

        # Step 2: Create invoice via iframe endpoint (the correct Pesaflow flow)
        invoice_ref = f"TEST-{datetime.now(timezone.utc).strftime('%Y%m%d%H%M%S')}"
        success, client_ref, pesaflow_data = step_2_create_invoice_via_iframe(invoice_ref)
        results["create_invoice"] = "PASS" if success else "FAIL"

        # Step 3: Save local invoice with Pesaflow details (or fallback if unreachable)
        print_sep("STEP 3: Save Local Invoice")
        save_local_invoice(client_ref, pesaflow_data)
        results["save_local_invoice"] = "PASS" if pesaflow_data else "FALLBACK"

        # Step 4: Query payment status (if invoice was created)
        if pesaflow_data and pesaflow_data.get("pesaflow_invoice_number"):
            step_3_query_payment_status(client_ref, pesaflow_data.get("pesaflow_invoice_number"))
            results["payment_status"] = "TESTED"

        print_summary(results)
        return 0 if results.get("create_invoice") == "PASS" else 1
    finally:
        # Restore stdout/stderr and close log file
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
