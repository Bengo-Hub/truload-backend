"""Shared TruLoad auth helper with disk-backed JWT caching."""

from __future__ import annotations

import base64
import json
import time
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any


CACHE_FILE = Path(__file__).parent / ".truload_auth_cache.json"
DEFAULT_MIN_TTL_SECONDS = 120


def _decode_jwt_exp(token: str) -> int | None:
    try:
        parts = token.split(".")
        if len(parts) < 2:
            return None
        payload = parts[1]
        payload += "=" * (-len(payload) % 4)
        decoded = base64.urlsafe_b64decode(payload.encode("utf-8"))
        data = json.loads(decoded.decode("utf-8"))
        exp = data.get("exp")
        return int(exp) if exp else None
    except Exception:
        return None


def _load_cache() -> dict[str, Any]:
    if not CACHE_FILE.exists():
        return {}
    try:
        return json.loads(CACHE_FILE.read_text(encoding="utf-8"))
    except Exception:
        return {}


def _save_cache(cache: dict[str, Any]) -> None:
    CACHE_FILE.write_text(json.dumps(cache, indent=2), encoding="utf-8")


def _cache_key(base_url: str, email: str) -> str:
    return f"{base_url.rstrip('/')}|{email.lower()}"


def get_login_data(
    base_url: str,
    email: str,
    password: str,
    force_refresh: bool = False,
    min_ttl_seconds: int = DEFAULT_MIN_TTL_SECONDS,
) -> tuple[dict[str, Any], bool]:
    """
    Returns (login_response_json, from_cache).

    Cache is reused only when token exists and has enough TTL remaining.
    """
    normalized_base = base_url.rstrip("/")
    now = int(time.time())
    key = _cache_key(normalized_base, email)

    cache = _load_cache()
    cached = cache.get(key)
    if not force_refresh and cached:
        token = cached.get("token")
        exp = int(cached.get("exp", 0))
        if token and exp - now > min_ttl_seconds:
            return cached.get("response", {}), True

    login_url = f"{normalized_base}/api/v1/auth/login"
    payload = json.dumps({"email": email, "password": password}).encode("utf-8")
    req = urllib.request.Request(login_url, data=payload, method="POST")
    req.add_header("Content-Type", "application/json")

    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            body = resp.read().decode("utf-8")
            status = resp.status
    except urllib.error.HTTPError as exc:
        body = exc.read().decode("utf-8") if exc.fp else ""
        raise RuntimeError(f"Login failed: {exc.code} {body[:300]}") from exc
    except urllib.error.URLError as exc:
        raise RuntimeError(f"Login failed: network error: {exc.reason}") from exc

    if status != 200:
        raise RuntimeError(f"Login failed: {status} {body[:300]}")

    try:
        data = json.loads(body)
    except json.JSONDecodeError as exc:
        raise RuntimeError(f"Login returned non-JSON response: {body[:200]}") from exc

    token = data.get("accessToken") or data.get("token")
    if not token:
        raise RuntimeError(f"No token in login response: {list(data.keys())}")

    exp = _decode_jwt_exp(token) or (now + 300)
    cache[key] = {"token": token, "exp": exp, "response": data}
    _save_cache(cache)
    return data, False
