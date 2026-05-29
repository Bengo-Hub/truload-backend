#!/usr/bin/env python3
"""
eCitizen / Pesaflow -- OAuth Token Generation Test
====================================================
Mirrors ECitizenService.GetAccessTokenAsync exactly:

  1. Check file cache — return immediately if TTL not expired
  2. POST {base_url}/api/oauth/generate/token
     Content-Type: application/json
     Body: {"key": <api_key>, "secret": <api_secret>}
  3. Parse token: "token" field (fallback: "access_token")
  4. Parse expiry: "expiry" field (fallback: "expires_in", default 3599s)
  5. Cache TTL = max(expiry - 60, 60)  [matches backend Math.Max logic]
  6. Write request payload to  oauth_request_<timestamp>.json
     Write response payload to oauth_response_<timestamp>.json

Credentials are loaded from .env.production in the same directory:
    key: <api_key>
    secret: <api_secret>
    api_client_id: <client_id>
    service_id: <service_id>
    live_base_url: <url>

Environment variable overrides (for CI):
    PESAFLOW_BASE_URL    PESAFLOW_API_KEY    PESAFLOW_API_SECRET

Usage:
    python test_oauth_token.py                   # use cached token if valid
    python test_oauth_token.py --clear-cache     # force fresh request
    python test_oauth_token.py --show-postman    # print Postman/curl info and exit
    python test_oauth_token.py --skip-invalid    # skip error-path tests
    python test_oauth_token.py --env test        # force test-env fallback credentials
"""

import argparse
import base64
import json
import os
import sys
import time
import urllib.request
import urllib.error
from datetime import datetime, timezone
from pathlib import Path

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

# ─── Paths ────────────────────────────────────────────────────────────────────
HERE            = Path(__file__).parent
ENV_FILE        = HERE / ".env.production"
TOKEN_CACHE     = HERE / ".pesaflow_token_cache.json"

# ─── Fallback test-environment credentials ────────────────────────────────────
# Used only when --env test is passed or .env.production is missing.
TEST_BASE_URL   = "https://test.pesaflow.com"
TEST_API_KEY    = "hkW0lc/+xu9GA5Di"
TEST_API_SECRET = "tgia2h6QEcwqPmJ1Uxv3V9I7cqf6Ub7X"

# ─── Console colours ─────────────────────────────────────────────────────────
PASS = "\033[32mPASS\033[0m"
FAIL = "\033[31mFAIL\033[0m"
INFO = "\033[36mINFO\033[0m"


# ─── .env.production loader ──────────────────────────────────────────────────

def _load_env_file(path: Path) -> dict[str, str]:
    """
    Parse key: value lines (Pesaflow .env.production format).
    Lines starting with # are ignored. Strips surrounding whitespace.
    """
    result: dict[str, str] = {}
    if not path.exists():
        return result
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        if ":" in line:
            k, _, v = line.partition(":")
            result[k.strip().lower()] = v.strip()
    return result


def _resolve_config(use_test_env: bool = False) -> dict:
    env = {} if use_test_env else _load_env_file(ENV_FILE)
    return {
        "base_url":   os.environ.get("PESAFLOW_BASE_URL",
                      env.get("live_base_url", TEST_BASE_URL)).rstrip("/"),
        "api_key":    os.environ.get("PESAFLOW_API_KEY",
                      env.get("key", TEST_API_KEY)),
        "api_secret": os.environ.get("PESAFLOW_API_SECRET",
                      env.get("secret", TEST_API_SECRET)),
    }


# ─── File-based token cache (mirrors Redis in backend) ───────────────────────

def _cache_ttl(expires_in: int) -> int:
    """Mirrors: Math.Max(expiresIn - 60, 60)"""
    return max(expires_in - 60, 60)


def _load_cached_token(cache_file: Path) -> dict | None:
    if not cache_file.exists():
        return None
    try:
        data = json.loads(cache_file.read_text(encoding="utf-8"))
        if time.time() < data.get("expires_at", 0) - 30:
            return data
    except (json.JSONDecodeError, KeyError):
        pass
    return None


def _save_cached_token(cache_file: Path, token: str, expires_in: int) -> None:
    ttl = _cache_ttl(expires_in)
    cache_file.write_text(json.dumps({
        "token":       token,
        "expires_at":  time.time() + ttl,
        "cached_at":   datetime.now(timezone.utc).isoformat(),
        "cache_ttl_s": ttl,
    }, indent=2), encoding="utf-8")


# ─── JSON artifact writers ────────────────────────────────────────────────────

def _ts() -> str:
    return datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")


def _write_request_log(payload: dict) -> Path:
    ts   = _ts()
    path = HERE / f"oauth_request_{ts}.json"
    path.write_text(json.dumps(payload, indent=2, default=str), encoding="utf-8")
    return path


def _write_response_log(payload: dict) -> Path:
    ts   = _ts()
    path = HERE / f"oauth_response_{ts}.json"
    path.write_text(json.dumps(payload, indent=2, default=str), encoding="utf-8")
    return path


# ─── JWT helpers ─────────────────────────────────────────────────────────────

def _validate_jwt_structure(token: str) -> tuple[bool, str]:
    parts = token.split(".")
    if len(parts) != 3:
        return False, f"Expected 3 JWT segments, got {len(parts)}"
    for i, part in enumerate(parts):
        padded = part + "=" * (-len(part) % 4)
        try:
            base64.urlsafe_b64decode(padded)
        except Exception as exc:
            return False, f"Segment {i} not valid base64url: {exc}"
    return True, "3 base64url segments"


def _decode_jwt_payload(token: str) -> dict | None:
    try:
        seg    = token.split(".")[1]
        padded = seg + "=" * (-len(seg) % 4)
        return json.loads(base64.urlsafe_b64decode(padded))
    except Exception:
        return None


# ─── Core HTTP (mirrors ECitizenService.GetAccessTokenAsync) ─────────────────

def _post_token(base_url: str, api_key: str, api_secret: str) -> tuple[dict, dict, dict]:
    """
    POST {base_url}/api/oauth/generate/token
    Body: {"key": api_key, "secret": api_secret}

    Returns (parsed_response, request_log_dict, response_log_dict).
    request_log has the secret masked.
    Raises urllib.error.HTTPError / urllib.error.URLError on failure.
    """
    url  = f"{base_url}/api/oauth/generate/token"
    body = json.dumps({"key": api_key, "secret": api_secret}).encode("utf-8")
    req  = urllib.request.Request(
        url,
        data=body,
        method="POST",
        headers={"Content-Type": "application/json", "Accept": "application/json"},
    )

    request_log = {
        "timestamp":      datetime.now(timezone.utc).isoformat(),
        "endpoint":       url,
        "method":         "POST",
        "headers":        {"Content-Type": "application/json", "Accept": "application/json"},
        "body":           {"key": api_key, "secret": "<MASKED>"},
    }

    with urllib.request.urlopen(req, timeout=30) as resp:
        raw    = resp.read().decode("utf-8")
        parsed = json.loads(raw)
        response_log = {
            "timestamp":       datetime.now(timezone.utc).isoformat(),
            "http_status":     resp.status,
            "headers":         dict(resp.headers),
            "body":            parsed,
        }
        return parsed, request_log, response_log


def _extract_token_and_expiry(response: dict) -> tuple[str | None, int]:
    """
    Mirrors backend:
        token  = root["token"]  ?? root["access_token"] ?? throw
        expiry = root["expiry"] ?? root["expires_in"]   ?? 3599
    """
    token     = response.get("token") or response.get("access_token") or response.get("jwt")
    expire    = response.get("expiry") or response.get("expires_in") or 3599
    return token, int(expire)


# ─── Test helpers ─────────────────────────────────────────────────────────────

def _check(label: str, cond: bool, detail: str = "") -> bool:
    status = PASS if cond else FAIL
    suffix = f"  ({detail})" if detail else ""
    print(f"  [{status}] {label}{suffix}")
    return cond


# ─── Test 1: Happy path ───────────────────────────────────────────────────────

def test_token_request(cfg: dict, force_fresh: bool) -> tuple[bool, str | None]:
    print("\n-- Test 1: Token Request (mirrors GetAccessTokenAsync) --")
    all_ok = True

    if not force_fresh:
        cached = _load_cached_token(TOKEN_CACHE)
        if cached:
            ttl_left = int(cached["expires_at"] - time.time())
            print(f"  [{INFO}] Cache hit — valid for {ttl_left}s")
            # Still write request/response logs so every run produces files
            req_log = {
                "timestamp": datetime.now(timezone.utc).isoformat(),
                "source":    "file_cache",
                "cache_file": str(TOKEN_CACHE),
            }
            resp_log = {
                "timestamp":    datetime.now(timezone.utc).isoformat(),
                "source":       "file_cache",
                "token_preview": cached["token"][:40] + "...",
                "expires_in_s": ttl_left,
            }
            rq = _write_request_log(req_log)
            rs = _write_response_log(resp_log)
            print(f"  [{INFO}] Request  log -> {rq.name}")
            print(f"  [{INFO}] Response log -> {rs.name}")
            return True, cached["token"]

    key_preview = cfg["api_key"][:8] + "*" * max(len(cfg["api_key"]) - 8, 0)
    print(f"  POST {cfg['base_url']}/api/oauth/generate/token")
    print(f"  Body: {{\"key\": \"{key_preview}\", \"secret\": \"<MASKED>\"}}")

    try:
        t0 = time.time()
        response, req_log, resp_log = _post_token(
            cfg["base_url"], cfg["api_key"], cfg["api_secret"]
        )
        elapsed_ms = (time.time() - t0) * 1000

        # Write artifacts immediately — even if assertions fail below
        rq_path = _write_request_log(req_log)
        rs_path = _write_response_log(resp_log)
        print(f"  [{INFO}] Request  log -> {rq_path.name}")
        print(f"  [{INFO}] Response log -> {rs_path.name}")

        all_ok &= _check("HTTP 200 OK", True, f"{elapsed_ms:.0f}ms")
        all_ok &= _check("Response is JSON object", isinstance(response, dict))

        token, expires_in = _extract_token_and_expiry(response)
        all_ok &= _check("'token' field present",
                          bool(token), f"keys={list(response.keys())}")

        if not token:
            print(f"  Full response: {json.dumps(response, indent=4)}")
            return False, None

        jwt_ok, jwt_msg = _validate_jwt_structure(token)
        all_ok &= _check("JWT structure (3 base64url segments)", jwt_ok, jwt_msg)

        # Pesaflow JWT carries only an 'id' claim — expiry lives in response body
        claims = _decode_jwt_payload(token)
        if claims:
            resp_log["decoded_jwt_claims"] = claims
            if "exp" in claims:
                exp_in = int(claims["exp"] - time.time())
                all_ok &= _check("JWT 'exp' not expired", exp_in > 0, f"{exp_in}s remaining")
            else:
                print(f"  [{INFO}] JWT claims={list(claims.keys())} "
                      f"-- expiry from response 'expiry' field ({expires_in}s)")

        exp_key = next((k for k in ("expiry", "expires_in") if k in response), None)
        all_ok &= _check(
            "Expiry field present in response",
            exp_key is not None,
            f"response['{exp_key}']={expires_in}s" if exp_key else f"defaulted to {expires_in}s",
        )

        ttl = _cache_ttl(expires_in)
        _save_cached_token(TOKEN_CACHE, token, expires_in)
        resp_log["cache_ttl_applied_s"] = ttl
        # Update the response file with the decoded claims + cache TTL
        rs_path.write_text(json.dumps(resp_log, indent=2, default=str), encoding="utf-8")
        print(f"  [{INFO}] Cached TTL={ttl}s -> {TOKEN_CACHE.name}")

        return all_ok, token

    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")
        req_log = {
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "endpoint":  f"{cfg['base_url']}/api/oauth/generate/token",
            "method":    "POST",
            "headers":   {"Content-Type": "application/json", "Accept": "application/json"},
            "body":      {"key": cfg["api_key"], "secret": "<MASKED>"},
        }
        resp_log = {"timestamp": datetime.now(timezone.utc).isoformat(),
                    "http_status": exc.code, "error_body": body[:500]}
        _write_request_log(req_log)
        _write_response_log(resp_log)
        _check("HTTP 200 OK", False, f"HTTP {exc.code}: {body[:200]}")
        return False, None

    except urllib.error.URLError as exc:
        _check("Network reachable", False, str(exc.reason))
        return False, None

    except json.JSONDecodeError as exc:
        _check("Response is valid JSON", False, str(exc))
        return False, None


# ─── Test 2: Cache persistence ────────────────────────────────────────────────

def test_token_cache(token: str | None) -> bool:
    print("\n-- Test 2: Token Cache Persistence --")
    if not token:
        print(f"  [{INFO}] Skipped — no token from Test 1")
        return True

    all_ok = True
    cached = _load_cached_token(TOKEN_CACHE)
    all_ok &= _check("Cache file exists", TOKEN_CACHE.exists())
    all_ok &= _check("Cached token matches Test 1 token",
                     cached is not None and cached.get("token") == token,
                     "strings equal")

    # Expired entry must return None (backend: token past TTL triggers new request)
    tmp = HERE / ".expired_test.json"
    tmp.write_text(json.dumps({"token": "dummy", "expires_at": time.time() - 1}),
                   encoding="utf-8")
    all_ok &= _check("Expired cache returns None", _load_cached_token(tmp) is None)
    tmp.unlink(missing_ok=True)

    return all_ok


# ─── Test 3: Wrong secret rejected ────────────────────────────────────────────

def test_invalid_credentials(cfg: dict) -> bool:
    print("\n-- Test 3: Invalid Credentials Rejected --")
    all_ok = True
    try:
        response, req_log, resp_log = _post_token(
            cfg["base_url"], cfg["api_key"], "WRONG_SECRET_VALUE"
        )
        has_error = (
            "error" in response
            or not response.get("token")
            or response.get("status", "").lower() in {"error", "failed", "unauthorized"}
        )
        all_ok &= _check("Wrong secret -> error in 2xx body", has_error,
                          json.dumps(response)[:120])
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")
        all_ok &= _check("Wrong secret -> HTTP 4xx/5xx", exc.code >= 400,
                          f"HTTP {exc.code} — {body[:120]}")
    except urllib.error.URLError as exc:
        print(f"  [{INFO}] Skipped (network): {exc.reason}")
    return all_ok


# ─── Test 4: Missing key field ────────────────────────────────────────────────

def test_missing_key_field(cfg: dict) -> bool:
    print("\n-- Test 4: Missing 'key' Field --")
    all_ok = True
    url  = f"{cfg['base_url']}/api/oauth/generate/token"
    body = json.dumps({"secret": cfg["api_secret"]}).encode("utf-8")
    req  = urllib.request.Request(url, data=body, method="POST",
                                  headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=15) as resp:
            resp_body = json.loads(resp.read().decode("utf-8"))
            has_error = "error" in resp_body or not resp_body.get("token")
            all_ok &= _check("Missing 'key' -> error in body", has_error,
                              str(resp_body)[:120])
    except urllib.error.HTTPError as exc:
        body_str = exc.read().decode("utf-8", errors="replace")
        all_ok &= _check("Missing 'key' -> HTTP error", exc.code >= 400,
                          f"HTTP {exc.code} — {body_str[:100]}")
    except urllib.error.URLError as exc:
        print(f"  [{INFO}] Skipped (network): {exc.reason}")
    return all_ok


# ─── Postman / curl helper ────────────────────────────────────────────────────

def print_postman_info(cfg: dict) -> None:
    sep = "=" * 62
    key_preview = cfg["api_key"]
    print(f"\n{sep}")
    print("  Postman / curl -- eCitizen OAuth Token")
    print(sep)
    print(f"\n  Method  : POST")
    print(f"  URL     : {cfg['base_url']}/api/oauth/generate/token")
    print(f"  Headers : Content-Type: application/json")
    print(f"\n  Body (raw JSON):\n  {{")
    print(f'    "key":    "{key_preview}",')
    print(f'    "secret": "<API_SECRET>"')
    print(f"  }}")
    print(f"\n  curl:")
    print(f"  curl -s -X POST \\")
    print(f"    \"{cfg['base_url']}/api/oauth/generate/token\" \\")
    print(f"    -H \"Content-Type: application/json\" \\")
    print(f"    -d '{{\"key\":\"{key_preview}\",\"secret\":\"<API_SECRET>\"}}'")
    print(f"\n  Expected response:")
    print(f"  {{")
    print(f'    "token":  "<JWT eyJ...>",')
    print(f'    "expiry": 3599')
    print(f"  }}")
    print(f"\n  Notes:")
    print(f"  - Token field: 'token' (fallback: 'access_token')")
    print(f"  - Expiry field: 'expiry' (fallback: 'expires_in', default 3599s)")
    print(f"  - Cache TTL = max(expiry - 60, 60)")
    print(f"  - Backend stores in Redis key 'ecitizen:oauth:token'")
    print(f"  - Pesaflow JWT payload only carries 'id' claim (no 'exp')")
    print(f"{sep}\n")


# ─── Main ─────────────────────────────────────────────────────────────────────

def main() -> int:
    parser = argparse.ArgumentParser(
        description="eCitizen/Pesaflow OAuth token tests — mirrors ECitizenService logic",
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument("--clear-cache",  action="store_true",
                        help="Delete file cache and force a fresh token request")
    parser.add_argument("--show-postman", action="store_true",
                        help="Print Postman/curl payload and exit")
    parser.add_argument("--skip-invalid", action="store_true",
                        help="Skip Tests 3 & 4 (error-path scenarios)")
    parser.add_argument("--env",          choices=["prod", "test"], default="prod",
                        help="'prod' reads .env.production (default); 'test' uses test credentials")
    args = parser.parse_args()

    cfg = _resolve_config(use_test_env=(args.env == "test"))

    if args.show_postman:
        print_postman_info(cfg)
        return 0

    if args.clear_cache and TOKEN_CACHE.exists():
        TOKEN_CACHE.unlink()
        print(f"[INFO] Cache cleared: {TOKEN_CACHE.name}")

    sep = "=" * 62
    env_label = "PRODUCTION" if args.env == "prod" else "TEST"
    print(sep)
    print(f"  eCitizen / Pesaflow -- OAuth Token Tests  [{env_label}]")
    print(f"  Endpoint : {cfg['base_url']}")
    print(f"  Time     : {datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M:%S UTC')}")
    print(sep)

    results: list[bool] = []

    ok1, token = test_token_request(cfg, force_fresh=args.clear_cache)
    results.append(ok1)

    results.append(test_token_cache(token))

    if not args.skip_invalid:
        results.append(test_invalid_credentials(cfg))
        results.append(test_missing_key_field(cfg))

    passed = sum(results)
    total  = len(results)
    print(f"\n{sep}")
    if passed == total:
        print(f"  \033[32mAll {total} test groups passed\033[0m")
    else:
        print(f"  \033[31m{total - passed}/{total} test groups FAILED\033[0m")
    print(sep + "\n")

    return 0 if passed == total else 1


if __name__ == "__main__":
    sys.exit(main())
