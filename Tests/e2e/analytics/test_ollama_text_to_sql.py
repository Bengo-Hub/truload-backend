"""
E2E tests for Ollama text-to-SQL pipeline via TruLoad backend.
Tests the /api/v1/analytics/text-to-sql endpoint which:
  1. Takes a natural language question
  2. Sends it to Ollama for SQL generation
  3. Executes the SQL via Superset SQLLab
  4. Returns results

Run: cd Tests/e2e/analytics && python -m pytest test_ollama_text_to_sql.py -v
"""
import pytest
import requests
from conftest import TRULOAD_BASE_URL


ANALYTICS_URL = f"{TRULOAD_BASE_URL}/analytics"
# Longer timeout for Ollama generation (can take 30-120s on llama2 7B)
OLLAMA_TIMEOUT = 200


# ============================================================================
# Helper
# ============================================================================

def ask_question(headers, question: str, timeout: int = OLLAMA_TIMEOUT):
    """Send a text-to-SQL question to TruLoad analytics endpoint."""
    resp = requests.post(
        f"{ANALYTICS_URL}/text-to-sql",
        json={"question": question},
        headers=headers,
        timeout=timeout,
    )
    return resp


# ============================================================================
# Test Scenarios
# ============================================================================

class TestOllamaTextToSQL:
    """Test suite for Ollama-powered text-to-SQL analytics."""

    def test_simple_count_query(self, truload_headers, ollama_available):
        """Test: 'How many weighing transactions are there?'"""
        resp = ask_question(truload_headers, "How many weighing transactions are there?")
        assert resp.status_code == 200, f"Expected 200, got {resp.status_code}: {resp.text}"
        data = resp.json()
        assert data.get("success") is True, f"Expected success=true: {data}"
        assert "sql" in data, f"Expected 'sql' in response: {data}"
        sql = data["sql"].lower()
        assert "select" in sql, f"Expected SELECT in generated SQL: {sql}"

    def test_filtered_query_overloaded(self, truload_headers, ollama_available):
        """Test: 'Show overloaded vehicles from last month'"""
        resp = ask_question(truload_headers, "Show overloaded vehicles from last month")
        assert resp.status_code == 200, f"Status {resp.status_code}: {resp.text}"
        data = resp.json()
        assert data.get("success") is True, f"Expected success: {data}"
        sql = data.get("sql", "").lower()
        assert "select" in sql

    def test_aggregation_revenue(self, truload_headers, ollama_available):
        """Test: 'What is the total revenue collected?'"""
        resp = ask_question(truload_headers, "What is the total revenue collected?")
        assert resp.status_code == 200, f"Status {resp.status_code}: {resp.text}"
        data = resp.json()
        assert data.get("success") is True, f"Expected success: {data}"
        sql = data.get("sql", "").lower()
        assert "select" in sql

    def test_join_query_vehicles_cases(self, truload_headers, ollama_available):
        """Test: 'List vehicles with both cases and invoices'"""
        resp = ask_question(truload_headers, "List vehicles with both cases and invoices")
        assert resp.status_code == 200, f"Status {resp.status_code}: {resp.text}"
        data = resp.json()
        assert data.get("success") is True, f"Expected success: {data}"

    def test_date_range_query(self, truload_headers, ollama_available):
        """Test: 'Weighing transactions in January 2026'"""
        resp = ask_question(truload_headers, "Weighing transactions in January 2026")
        assert resp.status_code == 200, f"Status {resp.status_code}: {resp.text}"
        data = resp.json()
        assert data.get("success") is True, f"Expected success: {data}"
        sql = data.get("sql", "").lower()
        assert "2026" in sql or "january" in sql or "01" in sql

    def test_station_specific_query(self, truload_headers, ollama_available):
        """Test: 'How many cases were created at each station?'"""
        resp = ask_question(truload_headers, "How many cases were created at each station?")
        assert resp.status_code == 200, f"Status {resp.status_code}: {resp.text}"
        data = resp.json()
        assert data.get("success") is True, f"Expected success: {data}"

    def test_edge_empty_question(self, truload_headers, ollama_available):
        """Test: Empty question should return graceful error."""
        resp = ask_question(truload_headers, "")
        # Should be 400 Bad Request or 200 with success=false
        assert resp.status_code in (200, 400), f"Status {resp.status_code}: {resp.text}"
        if resp.status_code == 200:
            data = resp.json()
            assert data.get("success") is False or data.get("error"), \
                f"Expected failure for empty question: {data}"

    def test_edge_sql_injection_attempt(self, truload_headers, ollama_available):
        """Test: SQL injection attempt should be handled safely."""
        malicious = "'; DROP TABLE weighing_transactions; --"
        resp = ask_question(truload_headers, malicious)
        # Should not crash; either error or safely generated SQL
        assert resp.status_code in (200, 400), f"Status {resp.status_code}: {resp.text}"
        if resp.status_code == 200:
            data = resp.json()
            sql = data.get("sql", "").lower()
            # Generated SQL should not contain DROP
            assert "drop" not in sql, f"SQL injection not blocked: {sql}"

    def test_edge_non_sql_question(self, truload_headers, ollama_available):
        """Test: Non-database question should be handled gracefully."""
        resp = ask_question(truload_headers, "What is the meaning of life?")
        # Should still respond (Ollama will try to generate SQL regardless)
        assert resp.status_code in (200, 400), f"Status {resp.status_code}: {resp.text}"
