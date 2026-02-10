#!/usr/bin/env python3
"""
Pesaflow API Direct Test Script
================================
Tests the Pesaflow (eCitizen) payment API directly using configured credentials
from appsettings.Development.json.

Steps:
  1. Get OAuth Bearer token from Pesaflow (cached to file, reused until expiry)
  2. Create a dummy invoice on Pesaflow
  3. Query payment status for the created invoice
  4. Test Online Checkout endpoint

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

# ─── Pesaflow Test Credentials (from appsettings.Development.json) ──────────
PESAFLOW_BASE_URL = "https://test.pesaflow.com"
API_KEY = "hkW0lc/+xu9GA5Di"       # Consumer Key (HMAC key + Basic auth user)
API_SECRET = "tgia2h6QEcwqPmJ1Uxv3V9I7cqf6Ub7X"  # Consumer Secret (Basic auth pass)
API_CLIENT_ID = "588"                # Service/Account ID

# Webhook/Callback URLs (for dummy invoice)
NOTIFICATION_URL = "http://localhost:4000/api/v1/payments/webhook/ecitizen-pesaflow"
CALLBACK_URL = "http://localhost:4000/api/v1/payments/callback/ecitizen-pesaflow"

# Token cache file (next to this script)
TOKEN_CACHE_FILE = Path(__file__).parent / ".pesaflow_token_cache.json"

MAX_RETRIES = 1

# ─── Utility Functions ─────────────────────────────────────────────────────


def compute_secure_hash(data_string: str, key: str) -> str:
    """Compute Pesaflow secure hash: Base64(hex(HMAC-SHA256(key, data)))"""
    key_bytes = key.encode("utf-8")
    data_bytes = data_string.encode("utf-8")
    hash_bytes = hmac.new(key_bytes, data_bytes, hashlib.sha256).digest()
    hex_hash = hash_bytes.hex()
    return base64.b64encode(hex_hash.encode("utf-8")).decode("utf-8")


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


def step_2_create_dummy_invoice(token: str) -> str | None:
    """Create a dummy invoice on Pesaflow using the Create Invoice API."""
    print_sep("STEP 2: Create Dummy Invoice on Pesaflow")

    url = f"{PESAFLOW_BASE_URL}/api/invoice/create"
    invoice_ref = f"TEST-{datetime.now(timezone.utc).strftime('%Y%m%d%H%M%S')}"

    payload = {
        "account_id": str(API_CLIENT_ID),
        "amount_expected": "100.00",
        "amount_net": "100.00",
        "amount_settled_offline": "0",
        "callback_url": CALLBACK_URL,
        "client_invoice_ref": invoice_ref,
        "commission": "0",
        "currency": "KES",
        "email": "test@truload-e2e.co.ke",
        "format": "json",
        "id_number": "TEST-ID-001",
        "items": [
            {
                "account_id": str(API_CLIENT_ID),
                "desc": "Test Overload Fine (Pesaflow API Test)",
                "item_ref": invoice_ref,
                "price": "100.00",
                "quantity": "1",
                "require_settlement": "true",
                "currency": "KES"
            }
        ],
        "msisdn": "254700000000",
        "name": "Pesaflow API Test User",
        "notification_url": NOTIFICATION_URL
    }

    print(f"  URL:         {url}")
    print(f"  Auth:        Bearer token")
    print(f"  Invoice Ref: {invoice_ref}")
    print(f"  Amount:      100.00 KES")
    print(f"  Payload:\n{json.dumps(payload, indent=4)}")
    print()

    status, body, _ = http_retry(
        "POST", url,
        body=payload,
        headers={"Authorization": f"Bearer {token}"}
    )

    print(f"  HTTP Status: {status}")
    print(f"  Response:    {body[:800]}")

    if status in (200, 201):
        try:
            data = json.loads(body)
            pesaflow_inv_no = data.get("invoice_number")
            if pesaflow_inv_no:
                print(f"\n  [PASS] Invoice created on Pesaflow")
                print(f"         Pesaflow Invoice No: {pesaflow_inv_no}")
                print(f"         Client Ref:          {invoice_ref}")
                return invoice_ref
            else:
                print(f"\n  [WARN] HTTP {status} but no invoice_number in response")
                print(f"         Keys: {list(data.keys())}")
                return invoice_ref
        except json.JSONDecodeError:
            print(f"\n  [WARN] Response is not JSON: {body[:200]}")
            return invoice_ref
    else:
        print(f"\n  [FAIL] Create Invoice failed with HTTP {status}")
        if body:
            try:
                err = json.loads(body)
                print(f"         Error: {json.dumps(err, indent=2)[:500]}")
            except:
                print(f"         Raw: {body[:500]}")
        return None


def step_3_query_payment_status(invoice_ref: str):
    """Query payment status for the created invoice."""
    print_sep("STEP 3: Query Payment Status")

    data_string = f"{API_CLIENT_ID}{invoice_ref}"
    secure_hash = compute_secure_hash(data_string, API_KEY)

    params = urllib.parse.urlencode({
        "api_client_id": API_CLIENT_ID,
        "ref_no": invoice_ref,
        "secure_hash": secure_hash
    })
    url = f"{PESAFLOW_BASE_URL}/api/invoice/payment/status?{params}"

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


def step_4_test_online_checkout(invoice_ref: str):
    """Test Online Checkout (iframe) endpoint."""
    print_sep("STEP 4: Test Online Checkout (iframe)")

    url = f"{PESAFLOW_BASE_URL}/PaymentAPI/iframev2.1.php"
    amount = "100.00"
    client_id = "TEST-ID-001"
    currency = "KES"
    bill_desc = "Test Overload Fine"
    client_name = "Test User"

    # Hash: apiClientID + amount + serviceID + clientIDNumber + currency +
    #        billRefNumber + billDesc + clientName + secret
    data_string = (f"{API_CLIENT_ID}{amount}{API_CLIENT_ID}{client_id}"
                   f"{currency}{invoice_ref}{bill_desc}{client_name}{API_SECRET}")
    secure_hash = compute_secure_hash(data_string, API_KEY)

    form_data = {
        "apiClientID": API_CLIENT_ID,
        "serviceID": API_CLIENT_ID,
        "billDesc": bill_desc,
        "currency": currency,
        "billRefNumber": invoice_ref,
        "clientMSISDN": "254700000000",
        "clientName": client_name,
        "clientIDNumber": client_id,
        "clientEmail": "test@truload-e2e.co.ke",
        "amountExpected": amount,
        "callBackURLONSuccess": CALLBACK_URL,
        "notificationURL": NOTIFICATION_URL,
        "secureHash": secure_hash,
        "format": "json",
        "sendSTK": "false"
    }

    print(f"  URL:        {url}")
    print(f"  Hash data:  '{data_string[:80]}...'")
    print(f"  Hash:       {secure_hash}")
    print()

    status, body, _ = http_request("POST", url, form_data=form_data)

    print(f"  HTTP Status: {status}")
    print(f"  Response:    {body[:800]}")

    if status == 200:
        print(f"\n  [PASS] Checkout endpoint responded")
    else:
        print(f"\n  [INFO] Checkout returned HTTP {status}")


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

    # Step 1: Get token (from cache or fresh)
    token = step_1_get_oauth_token()
    results["oauth"] = "PASS" if token else "FAIL"

    if not token:
        print("\n" + "=" * 70)
        print("  CANNOT PROCEED: OAuth token required for remaining steps")
        print("  Possible causes:")
        print("    - Pesaflow test server down (502)")
        print("    - Test credentials expired/revoked (400 'Invalid token')")
        print("    - Network issue")
        print("  Debug: compare with Postman — same URL, same Basic auth header")
        print("=" * 70)
        print_summary(results)
        return 1

    # Step 2: Create dummy invoice
    invoice_ref = step_2_create_dummy_invoice(token)
    results["create_invoice"] = "PASS" if invoice_ref else "FAIL"

    # Use a fallback ref for steps 3/4 even if step 2 failed
    # (tests status query and checkout independently)
    test_ref = invoice_ref or f"TEST-{datetime.now(timezone.utc).strftime('%Y%m%d%H%M%S')}"

    # Step 3: Query payment status
    step_3_query_payment_status(test_ref)
    results["payment_status"] = "TESTED"

    # Step 4: Test Online Checkout
    step_4_test_online_checkout(test_ref)
    results["online_checkout"] = "TESTED"

    print_summary(results)
    return 0 if results.get("oauth") == "PASS" else 1


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
