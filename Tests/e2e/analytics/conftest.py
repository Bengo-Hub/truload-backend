"""
Shared fixtures for TruLoad analytics E2E tests.
Tests against:
  - TruLoad backend (default: http://localhost:4000)
  - Ollama (default: http://localhost:11434)
  - Superset (default: https://superset.codevertexitsolutions.com)
"""
import os
import pytest
import requests

from pathlib import Path
import sys

E2E_ROOT = Path(__file__).resolve().parent.parent
if str(E2E_ROOT) not in sys.path:
    sys.path.append(str(E2E_ROOT))

from test_credentials import LOGIN_EMAIL_DEFAULT, LOGIN_PASSWORD_DEFAULT

# ============================================================================
# Configuration
# ============================================================================

TRULOAD_BASE_URL = os.getenv("TRULOAD_BASE_URL", "http://localhost:4000/api/v1")
OLLAMA_BASE_URL = os.getenv("OLLAMA_BASE_URL", "http://localhost:11434")
SUPERSET_BASE_URL = os.getenv("SUPERSET_BASE_URL", "https://superset.codevertexitsolutions.com")
SUPERSET_USERNAME = os.getenv("SUPERSET_USERNAME", "admin")
SUPERSET_PASSWORD = os.getenv("SUPERSET_PASSWORD", "admin123")

# TruLoad test credentials (from UserSeeder)
TRULOAD_EMAIL = LOGIN_EMAIL_DEFAULT
TRULOAD_PASSWORD = LOGIN_PASSWORD_DEFAULT


# ============================================================================
# Fixtures
# ============================================================================

@pytest.fixture(scope="session")
def truload_token():
    """Authenticate with TruLoad backend and return JWT token."""
    try:
        resp = requests.post(
            f"{TRULOAD_BASE_URL}/auth/login",
            json={"email": TRULOAD_EMAIL, "password": TRULOAD_PASSWORD},
            timeout=15,
        )
        resp.raise_for_status()
        data = resp.json()
        token = data.get("token") or data.get("accessToken")
        assert token, f"No token in response: {data}"
        return token
    except requests.exceptions.ConnectionError:
        pytest.skip("TruLoad backend not reachable")


@pytest.fixture(scope="session")
def truload_headers(truload_token):
    """Auth headers for TruLoad API calls."""
    return {
        "Authorization": f"Bearer {truload_token}",
        "Content-Type": "application/json",
    }


@pytest.fixture(scope="session")
def ollama_available():
    """Check if Ollama is reachable; skip tests if not."""
    try:
        resp = requests.get(f"{OLLAMA_BASE_URL}/api/tags", timeout=5)
        if resp.status_code == 200:
            return True
    except requests.exceptions.ConnectionError:
        pass
    pytest.skip("Ollama not reachable at " + OLLAMA_BASE_URL)


@pytest.fixture(scope="session")
def superset_token():
    """Authenticate with Superset and return access token."""
    try:
        resp = requests.post(
            f"{SUPERSET_BASE_URL}/api/v1/security/login",
            json={
                "username": SUPERSET_USERNAME,
                "password": SUPERSET_PASSWORD,
                "provider": "db",
            },
            timeout=15,
        )
        resp.raise_for_status()
        data = resp.json()
        token = data.get("access_token")
        assert token, f"No access_token in Superset response: {data}"
        return token
    except requests.exceptions.ConnectionError:
        pytest.skip("Superset not reachable at " + SUPERSET_BASE_URL)


@pytest.fixture(scope="session")
def superset_headers(superset_token):
    """Auth headers for Superset API calls."""
    return {
        "Authorization": f"Bearer {superset_token}",
        "Content-Type": "application/json",
    }
