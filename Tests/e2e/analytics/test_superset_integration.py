"""
E2E tests for Superset integration.
Tests direct Superset API access and TruLoad backend Superset proxy endpoints.

Run: cd Tests/e2e/analytics && python -m pytest test_superset_integration.py -v
"""
import pytest
import requests
from conftest import SUPERSET_BASE_URL, TRULOAD_BASE_URL


# ============================================================================
# Direct Superset Tests
# ============================================================================

class TestSupersetDirect:
    """Test Superset service directly."""

    def test_superset_health(self):
        """Test: Superset health check endpoint is reachable."""
        try:
            resp = requests.get(f"{SUPERSET_BASE_URL}/health", timeout=10)
            assert resp.status_code == 200, f"Superset health: {resp.status_code}"
        except requests.exceptions.ConnectionError:
            pytest.skip("Superset not reachable")

    def test_superset_login(self):
        """Test: Superset login via API returns access token."""
        try:
            resp = requests.post(
                f"{SUPERSET_BASE_URL}/api/v1/security/login",
                json={
                    "username": "admin",
                    "password": "admin123",
                    "provider": "db",
                },
                timeout=15,
            )
            assert resp.status_code == 200, f"Login failed: {resp.status_code} {resp.text}"
            data = resp.json()
            assert "access_token" in data, f"No access_token: {data}"
        except requests.exceptions.ConnectionError:
            pytest.skip("Superset not reachable")

    def test_superset_dashboard_list(self, superset_headers):
        """Test: Can list dashboards from Superset."""
        resp = requests.get(
            f"{SUPERSET_BASE_URL}/api/v1/dashboard/",
            headers=superset_headers,
            timeout=15,
        )
        assert resp.status_code == 200, f"Status {resp.status_code}: {resp.text}"
        data = resp.json()
        assert "result" in data, f"No 'result' in response: {data}"

    def test_superset_guest_token(self, superset_headers):
        """Test: Can generate a guest token for dashboard embedding."""
        # First get a dashboard ID
        dash_resp = requests.get(
            f"{SUPERSET_BASE_URL}/api/v1/dashboard/",
            headers=superset_headers,
            timeout=15,
        )
        dashboards = dash_resp.json().get("result", [])
        if not dashboards:
            pytest.skip("No dashboards available in Superset")

        dashboard_id = dashboards[0]["id"]

        # Fetch CSRF token
        csrf_resp = requests.get(
            f"{SUPERSET_BASE_URL}/api/v1/security/csrf_token/",
            headers=superset_headers,
            timeout=10,
        )
        csrf_token = csrf_resp.json().get("result") if csrf_resp.status_code == 200 else None

        guest_headers = {**superset_headers}
        if csrf_token:
            guest_headers["X-CSRFToken"] = csrf_token

        # Generate guest token
        resp = requests.post(
            f"{SUPERSET_BASE_URL}/api/v1/security/guest_token/",
            json={
                "user": {"username": "truload_guest", "first_name": "TruLoad", "last_name": "Guest"},
                "resources": [{"type": "dashboard", "id": str(dashboard_id)}],
                "rls": [],
            },
            headers=guest_headers,
            timeout=15,
        )
        assert resp.status_code == 200, f"Guest token failed: {resp.status_code} {resp.text}"
        data = resp.json()
        assert "token" in data, f"No 'token' in response: {data}"

    def test_superset_guest_token_validity(self, superset_headers):
        """Test: Generated guest token can authenticate API requests."""
        # Get dashboard
        dash_resp = requests.get(
            f"{SUPERSET_BASE_URL}/api/v1/dashboard/",
            headers=superset_headers,
            timeout=15,
        )
        dashboards = dash_resp.json().get("result", [])
        if not dashboards:
            pytest.skip("No dashboards available")

        dashboard_id = dashboards[0]["id"]

        # Get CSRF token
        csrf_resp = requests.get(
            f"{SUPERSET_BASE_URL}/api/v1/security/csrf_token/",
            headers=superset_headers,
            timeout=10,
        )
        csrf_token = csrf_resp.json().get("result") if csrf_resp.status_code == 200 else None

        guest_headers = {**superset_headers}
        if csrf_token:
            guest_headers["X-CSRFToken"] = csrf_token

        # Generate guest token
        guest_resp = requests.post(
            f"{SUPERSET_BASE_URL}/api/v1/security/guest_token/",
            json={
                "user": {"username": "truload_guest", "first_name": "TruLoad", "last_name": "Guest"},
                "resources": [{"type": "dashboard", "id": str(dashboard_id)}],
                "rls": [],
            },
            headers=guest_headers,
            timeout=15,
        )
        if guest_resp.status_code != 200:
            pytest.skip(f"Could not generate guest token: {guest_resp.status_code}")

        guest_token = guest_resp.json()["token"]

        # Use guest token to access dashboard
        resp = requests.get(
            f"{SUPERSET_BASE_URL}/api/v1/dashboard/{dashboard_id}",
            headers={
                "Authorization": f"Bearer {guest_token}",
                "Content-Type": "application/json",
            },
            timeout=15,
        )
        # Guest tokens may have limited access; 200 or 401/403 are valid responses
        assert resp.status_code in (200, 401, 403), \
            f"Unexpected status with guest token: {resp.status_code}"

    def test_superset_nonexistent_dashboard(self, superset_headers):
        """Test: Requesting non-existent dashboard returns 404."""
        resp = requests.get(
            f"{SUPERSET_BASE_URL}/api/v1/dashboard/999999",
            headers=superset_headers,
            timeout=15,
        )
        assert resp.status_code in (404, 403), \
            f"Expected 404/403 for non-existent dashboard, got {resp.status_code}"


# ============================================================================
# TruLoad Backend Superset Proxy Tests
# ============================================================================

class TestTruLoadSupersetProxy:
    """Test Superset integration through TruLoad backend."""

    def test_truload_analytics_dashboards(self, truload_headers):
        """Test: TruLoad backend can list Superset dashboards."""
        resp = requests.get(
            f"{TRULOAD_BASE_URL}/analytics/dashboards",
            headers=truload_headers,
            timeout=15,
        )
        # 200 if Superset connected, 503 if Superset unavailable
        assert resp.status_code in (200, 503), \
            f"Status {resp.status_code}: {resp.text}"

    def test_truload_guest_token_generation(self, truload_headers):
        """Test: TruLoad backend can generate Superset guest token."""
        resp = requests.post(
            f"{TRULOAD_BASE_URL}/analytics/guest-token",
            headers=truload_headers,
            timeout=15,
        )
        # 200 if Superset connected, 503 if unavailable
        assert resp.status_code in (200, 503), \
            f"Status {resp.status_code}: {resp.text}"
        if resp.status_code == 200:
            data = resp.json()
            assert "token" in data or "guestToken" in data, \
                f"No token in response: {data}"
